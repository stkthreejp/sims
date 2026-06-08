# Direct Bill + Electronic Payments + Notices/Reminders — Architecture Memo

> **Owner:** Jeremiah O'Donovan · **Created:** 2026-06-08 · **Status:** Exploratory / post-launch scope. **Not** part of the internal-UAT go-live plan — this is a separate initiative triggered by the opportunity for one program to move to direct bill.
>
> **Scope:** (1) adding a direct-bill billing mode to SIMS, (2) accepting electronic payments (we've worked with ePayPolicy before), (3) late-payment reminders + cancellation-notice mailing, and (4) how it all integrates with QuickBooks and SIMS. Includes a developer-side question checklist to send ePayPolicy and a mail-vendor checklist.

---

## 1. Current state in SIMS (what the code actually does today)

Grounded in a read of the accounting subsystem and the notice/scheduling infrastructure:

**Billing is hardcoded agency-bill, but a billing-mode seam already exists.**
- The money flow assumes the agent remits gross premium to SMM, SMM nets its commission, then sweeps the remainder to the carrier. The `Invoice` entity has **no bill-to party**; AR is booked *net of agent commission* (`LedgerService.PostInvoiceAsync` comments "AR is net of agent commission… SMM expects to receive TotalAmount minus what agent keeps"); the carrier payable is gross-minus-commission; and `CashApplicationService` auto-deducts broker commission when cash is applied.
- **However**, a `BillingMode` field already exists on `ProgramCarrierLineOfBusiness`, and `BillingModeSnapshot` on `PolicyTransaction` — so billing mode is already **program/carrier/LOB-scoped** at the schema level. The catch: **no billing logic reads these fields yet**; the invoicing/cash code is unconditionally agency-bill regardless of the value.

**Trust/fiduciary accounting is already modeled well.** The Chart of Accounts seed separates `1000 Cash — Operating` from `1100 Cash — Trust (Fiduciary)`, with per-state surplus-lines tax payables and a `CashDistribution`/`CashMovementInstruction` engine that sweeps net premium out of trust to carriers. This matches SMM holding premium in trust via TS Management LLC.

**QuickBooks is one-way, journal-entries-only.** SIMS pushes period-rolled GL journal entries to QBO (`QboJournalDriver`); it does **not** create QBO Invoice/Payment objects. The webhook only *detects* QBO-side changes (marks a rollup "Divergent"). So payments reach QBO today only as GL journal lines, not as QBO Payments.

**No electronic payment intake exists.** Receipts are 100% manual key-in (date/amount/payer-name/reference). Grep for any gateway/Stripe/card/ACH-intake code returns nothing. This is greenfield.

**Notice/reminder infrastructure is partially there:**
- A background-worker framework already runs 6 workers (`TaskNotificationWorker`, `TaskEscalationWorker`, `ShadowRateDailyReportWorker`, `EmailIngestionWorker`, `QboSyncRetryWorker`, `FmcsaScheduledJobsWorker`) — a scheduled dunning/notice worker fits this pattern directly.
- An `OutboundCommunication` subsystem exists (entity + service + attachments + `IOutboundEmailSenderService`) — so **email** notices can already be sent and recorded.
- A `CancellationReasonLibrary` and an `AddCancellationComplianceSnapshot` migration exist, and `docs/commercial-cancellation-law-tracking-chart.md` already tracks **state-by-state notice periods and "Proof of Notice" rules for all of SMM's states** (AL, AR, FL, GA, LA, MD, MS, NC, OK, PA, SC, TN, TX, VA).
- **Missing:** (a) a dunning/reminder engine that watches invoice due/past-due dates and fires reminders, and (b) **physical mail with proof-of-mailing** — email alone will not satisfy most states' proof-of-notice requirements for cancellation.

**Verdict:** This is a tractable extension, not a rewrite. The program-scoped billing-mode field and the trust accounting are already in place. The genuinely net-new pieces are: an insured bill-to on the invoice, electronic payment intake, a dunning engine, and physical-mail delivery.

---

## 2. Direct-bill module — concrete change list

Branch on the existing `BillingModeSnapshot` so one program can be direct bill while the rest stay agency bill.

1. **Add a bill-to / insured party to `Invoice`** (new FK + migration). Direct-bill invoices must name and reach the insured. *(New)*
2. **Branch billing logic on `BillingModeSnapshot`:**
   - `InvoicingService.BindAsync` + `LedgerService.PostInvoiceAsync`: for direct bill, book AR at the **full** premium (not netted by agent commission), and record agent commission as a **payable owed out to the agent** rather than withheld from a remittance.
   - `CashApplicationService`: for direct bill, do **not** auto-deduct broker commission from the receipt.
3. **Decide fund routing** (see §6 compliance): direct-bill insured funds are still fiduciary premium → they should land in `1100 Trust`, then sweep to the carrier as today. Confirm the `ReceiptsService`/`CashDistributionService` paths honor this for the direct-bill case.
4. **Agent commission disbursement:** direct bill means SMM pays the agent's commission *out* (a `Disbursement`/`Payable` to the agent) instead of the agent self-deducting. Reuse the existing payee/disbursement machinery.
5. **Electronic payment intake service** (§3) + new `Receipt` fields for gateway transaction id / payment method / settlement reference.
6. *(Optional)* Extend QBO sync to push real Invoice/Payment objects if you want AR aging visible inside QBO (today only GL JEs flow).

---

## 3. Electronic payments — approach

Use **ePayPolicy** (we have prior history with them). It supports both agency and direct bill, card + ACH, and offers **both** a hosted payment page and an **API/embeddable** option so the insured can pay inside SIMS without leaving the app. Architecture:

```
Insured  →  ePayPolicy (hosted page or embedded widget in SIMS)
                 │  payment captured (card or ACH)
                 ▼
         ePayPolicy webhook  →  SIMS PaymentIntakeService (NEW)
                 │  creates a Receipt (existing flow)
                 ▼
         Funds settle into SMM premium TRUST account
                 │
                 ▼
         Receipt → GL journal entry → QBO via existing period rollup
```

Two practical notes:
- **ACH-first.** Card interchange (~3%) is painful on large E&S premiums; ACH is the workhorse. ePayPolicy supports passing card convenience fees to the payer where state-permitted.
- **SIMS stays the system of record.** The vendor only captures funds; SIMS owns the invoice, receipt, trust accounting, and QBO sync.

---

## 4. QuickBooks integration path

Don't rely on a vendor↔QBO direct connector (ePayPolicy's native integrations are insurance AMS systems like AMS360/Applied Epic/MGA Systems IMS, not QuickBooks). Instead: **vendor → webhook → SIMS Receipt → QBO via the existing journal-entry rollup**, exactly like manual receipts today. This keeps SIMS authoritative and requires no new QBO surface. Building true QBO Invoice/Payment sync is a separate, optional enhancement only needed if you want AR aging inside QBO itself.

---

## 5. Late-payment reminders + cancellation-notice mailing

This is the piece that makes direct bill operationally real — under agency bill the agent chases the insured; under direct bill SMM does.

**Dunning / reminder engine (new worker).** Add a scheduled worker (mirroring `TaskNotificationWorker`) that scans open direct-bill invoices and fires a configurable reminder ladder, e.g. *due-soon → due → 5/10/15 days past due → intent-to-cancel → cancellation notice*. Each step writes an `OutboundCommunication` record (already supported) and, where required, triggers a physical mailing (below). Make the ladder program-scoped so direct-bill programs use it and agency-bill programs don't.

**Cancellation notice flow — tie into what already exists.** SIMS already has the `CancellationReasonLibrary`, the compliance snapshot, and the state cancellation-law chart that tracks notice periods and "Proof of Notice." The notice flow should compute the cancellation effective date as **notice mailing date + state notice days + mailing days**, pulling the state-specific notice period from the cancellation-law data, and must capture **proof of mailing** (see vendors below). Mortgagee/lienholder and state-authority notice copies are already flagged per-state in the chart and should be addressed in the same mailing.

**Why physical mail is required.** For most of SMM's states, a cancellation notice needs documented **proof of mailing** (USPS Form 3665 Certificate of Mailing) or **Certified Mail** to be legally effective — courts have repeatedly held that *proof of mailing*, not proof of receipt, is what controls. Email alone won't satisfy this. So the notice engine needs a print-and-mail vendor that returns a certificate of mailing / certified-mail tracking that SIMS stores against the policy transaction as compliance evidence.

### Mail-vendor options (print-and-mail APIs)

| Vendor | Best for | Notes |
|---|---|---|
| **Lob** | Developer-first general print & mail | Mature, well-documented API; automates Certified Mail end-to-end (printing, postage, certified tracking). Strong fit if we want a clean REST API and SIMS owns the letter templates. |
| **PostGrid** | Insurance/compliance-leaning print & mail | API parity with Lob; First-Class + Certified + Certificate of Mailing; compliance toggles (HIPAA, BAA); publishes insurance-notice content. |
| **Simple Certified Mail / Send Certified Mail** | Insurance-native certified mail + USPS 3665 | Specialists in insurance compliance mailings; API for Certified Mail cover sheets, electronic postage, Proof of Acceptance/Delivery, and Form 3665 Certificate of Mailing. Strongest on the *proof* side specifically. |

Recommendation: shortlist **Lob or PostGrid** for the letter production + mailing (SIMS holds templates, calls the API, stores the returned certificate/tracking as evidence). If proof-of-mailing rigor is the dominant concern, **Simple Certified Mail** is the insurance-specialist option. All three are API-driven and integrate with SIMS the same way ePayPolicy does — SIMS calls them and stores the result; there's no QBO dependency.

Email remains the channel for *reminders* (cheap, fast, already supported); physical mail is reserved for *legally-operative notices* (intent-to-cancel, cancellation, nonrenewal).

---

## 6. Open compliance / business decisions

1. **Trust vs. operating routing** for direct-bill insured funds (recommend trust → it's still fiduciary premium).
2. **Vendor settlement account** — ePayPolicy must deposit into the **premium trust account**, not operating, to avoid commingling.
3. **State remittance deadlines** still apply (often 30 days from *collection*; some states 15 days) — direct collection doesn't change the carrier-remittance clock.
4. **Proof-of-mailing standard per program** — Certificate of Mailing (3665) vs. Certified Mail w/ return receipt; confirm against the cancellation-law chart and carrier binder requirements.
5. **Which program(s)** move to direct bill, and whether agent commission is paid on collection or on a cycle.
6. **Refund/return-premium path** for electronic payments (card/ACH reversal vs. check disbursement).

---

## 7. Suggested phasing

1. **Wire the billing-mode branch** + add insured bill-to on `Invoice` (makes one program direct-bill-capable in the ledger).
2. **ePayPolicy integration** — embed payment, build `PaymentIntakeService` (webhook → Receipt), settle to trust, reconcile to QBO via existing rollup.
3. **Dunning engine** — scheduled worker + program-scoped reminder ladder over email (`OutboundCommunication`).
4. **Cancellation-notice mailing** — print-mail vendor integration with proof-of-mailing capture, wired to the cancellation-law data + effective-date math.
5. *(Optional)* True QBO Invoice/Payment sync for in-QBO AR aging.

---

## Appendix A — ePayPolicy: developer/integration questions to ask

*(Copy-paste and send.)*

**API & embedding**
1. Do you offer a fully embeddable payment component (card + ACH) that renders inside our own web app (React), or is integration limited to a hosted/redirect payment page? Any iframe/SDK/Elements-style option?
2. What does the API surface cover — create payment, tokenize/save a payer's bank/card for recurring, refunds/voids, payment status lookup? Is there full REST API documentation and a sandbox?
3. Authentication model for the API (API keys, OAuth)? Per-account or per-environment keys? IP allowlisting?

**Webhooks / reconciliation**
4. Do you send webhooks on payment events (authorized, settled, failed, refunded, ACH return/NSF)? What's the payload schema, and how do you sign/verify them (HMAC signature, like our QBO webhook)?
5. How is settlement reported — do we get a settlement/batch ID and the per-transaction fee on the webhook so we can reconcile net deposits to our trust account in SIMS?
6. What's the typical settlement timing for ACH vs. card, and how are ACH returns/chargebacks surfaced back to us programmatically (so we can reverse the Receipt in SIMS)?

**Money movement / trust compliance**
7. Can funds settle directly into our **premium trust account** (fiduciary), and can different programs settle to different bank accounts?
8. Do you support a convenience/surcharge fee passed to the payer for card, configurable by state where permitted? How is that fee represented in the API/settlement data?
9. For ACH, what are per-transaction and daily limits, and how do you handle large E&S premiums (e.g., $100k+)?

**Data model fit**
10. What identifiers can we pass through and get back on a payment (our invoice #, policy #, insured id) so payments map cleanly to a SIMS invoice/receipt?
11. Do you support partial payments / installments against a single invoice, and overpayment handling?
12. Saved-payer / recurring billing: can we store a payer profile and initiate future ACH pulls on our schedule (for installment direct bill), and what authorization capture do you provide for NACHA compliance?

**Integration logistics**
13. Is there a QuickBooks Online integration, or do you expect accounting reconciliation to happen in the partner system (SIMS)? *(We plan to reconcile in SIMS and push GL to QBO ourselves.)*
14. PCI scope: with the embedded component, what PCI SAQ level are we responsible for? Do card details ever touch our servers?
15. Sandbox/test credentials, rate limits, and go-live/certification steps?
16. SLA, support model, and any per-transaction or monthly platform pricing relevant to the integration design?

## Appendix B — Mail vendor (Lob / PostGrid / Simple Certified Mail) questions

1. API for First-Class, Certified, and **Certificate of Mailing (USPS Form 3665)** — which do you support, and do you return a retrievable proof artifact (PDF + tracking) we can store as compliance evidence?
2. Webhook/callback on mail events (accepted by USPS, in-transit, delivered, return-to-sender)? Payload + signing?
3. Do we supply a print-ready PDF (SIMS generates the notice) or do you template it? Address verification / NCOA included?
4. Certified Mail return-receipt (electronic vs. green card) and how the signed receipt is returned to us via API.
5. Batch/bulk send for a day's notice run; per-piece pricing for First-Class vs. Certificate of Mailing vs. Certified.
6. Data handling / retention and any insurance-compliance posture; sandbox + go-live steps.

---

## Sources

- ePayPolicy — [Agency vs Direct Bill](https://epaypolicy.com/blog/agency-bill-or-direct-bill/), [API / embed into your site](https://help.epaypolicy.com/can-i-build-epaypolicy-s-platform-into-my-site-so-that-customers-do-not-go-to-external-site-for-payment-processing-), [Integrations](https://epaypolicy.com/integrations/)
- [One Inc](https://www.oneinc.com/) · [Input 1](https://www.input1.com/)
- Mail vendors — [Lob: Certified Mail](https://help.lob.com/print-and-mail/building-a-mail-strategy/mailing-classes-and-postage/certified-mail-or-registered-mail), [PostGrid: Certificate vs Certified Mail](https://www.postgrid.com/certificate-or-proof-of-mailing-vs-certified-mail/), [Simple Certified Mail API](https://www.simplecertifiedmail.com/api/), [Digital 3665 Certificate of Mailing for Insurance Notices](https://www.eoshost.com/blog/digital-3665-certificate-of-mailing-for-insurance-notices/)
- Compliance — [Proof of Mailing Sufficient to Effect Cancellation](https://www.insurance-advocate.com/2018/02/26/proof-of-mailing-sufficient-to-effect-cancellation/), [Premium Trust Account Requirements by State](https://brokerageaudit.com/blog/premium-trust-account-requirements-by-state-a-practical-guide-for-agencies)
- QBO — [QuickBooks Online Payment API](https://developer.intuit.com/app/developer/qbo/docs/api/accounting/all-entities/payment)
