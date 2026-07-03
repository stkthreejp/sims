# WS5 — Launch Program Setup & Test Checklist

Goal: clear WS5 (the open Gate-A blocker) by configuring the launch programs
end-to-end and verifying every subsystem they touch. Written 2026-07-02, after
GL_v2 went live. Test on the Azure test env (`sims-frontend-test` regional URL).

**How to use (staged — decided 2026-07-02):** do **Part A only** for now (Lloyd's
GL — DALE 1729/Brace): Phases 0–9 top to bottom, then lifecycle tests T1–T18, then
the per-state matrix. Fix what Part A surfaces before configuring anything else —
most defects will be in shared machinery (rating, SL, numbering, forms, BDX,
accounting), so setting up a second program against a dirty pipeline just doubles
the rework. **Part B (Lloyd's IM — Beazley AFB 623/2623) is deferred** until Part A
is clean. Anything in §Known-gaps is *expected* to fail — record it, don't chase it.

## What "WS5 cleared" means (plan §WS5 → this checklist)

| Plan item | Covered by |
|---|---|
| Configure both launch programs end-to-end | Part A (GL) **now**; Part B (IM) deferred until Part A is clean |
| Orphan audit clean | Phase 1 (re-run after Part B) |
| Per launch state: SL "tax assertion" check | T7 + per-state matrix |
| Per launch state: diligent-effort research → flags | Phase 4 + per-state matrix |
| Historical-versioning gap | Already deferred/documented — nothing to do |

**Gate-A item 5 reads "one full program configured end-to-end and bindable" — so
finishing Part A alone flips the Gate-A blocker.** WS5 as a workstream fully closes
after the deferred Part B pass (§Sign-off below).

---

# Part A — Lloyd's GL (DALE 1729 / Brace)

---

## Phase 0 — Pre-flight (verify the deploy state)

- [ ] Admin → Rating: **GL_v2** plan shows **Active**, version 1, formula `GL_v2`; **GL_v1 shows Retired**.
- [ ] Admin → Rating plan detail: factor tables present — `GL_CLASS` (14), `GL_LOSS_COST_334` (186), `GL_LOSS_COST_336` (76), `GL_ILF` (30), `GL_PARAMS` (2), `GL_LL_ENDORSEMENT` (8).
- [ ] Admin → Database Status: no pending migrations; healthy.

## Phase 1 — Program hierarchy (Program Configuration admin)

- [ ] Create/verify Program (e.g., Longleaf) and Carrier (Lloyd's GL — DALE 1729 / Brace).
- [ ] ProgramCarrier link active with effective date.
- [ ] GL line of business active under the carrier, with the **London fields** filled (UMR, section number, class of business `FORESTRY GENERAL LIABILITY`, risk code, insurance type `DIRECT`) — the London BDX consumes these; missing = validation warnings on runs.
- [ ] Add each launch **state** under GL, active with effective date. (Rater supports: AL AR FL GA LA MS OK SC TN TX PA MD VA NC KY.)
- [ ] Non-launch state is *not* selectable downstream (quote on a non-configured state should be blocked: INVALID_PROGRAM_SETUP_PATH behavior).
- [ ] **Orphan audit** (Program Configuration admin): run it; resolve every finding.

## Phase 2 — Rating wiring

- [ ] Carrier detail → rating assignments: **GL → GL_v2 v1** assigned (program-scoped if applicable). AdminRatingPage carrier count increments.
- [ ] Carrier additional-interest rates: the global GL rules exist — AI individual **$50** (PerInterest), AI blanket **$250** (BlanketFlat), WOS individual **$50**, WOS blanket **$250**, PNC **$250**. (Seeded carrier-agnostic; add carrier-specific overrides only if rates differ.)

## Phase 3 — Money setup

- [ ] Carrier commission (rate + SMM retention, effective-dated). This drives the invoice-stamped commission and BDX "Gross commission".
- [ ] Producing Agent with primary location + license number; agent commission configured.
- [ ] Intermediary (London broker) + program/carrier/LOB setup with brokerage rate, if placing through one (feeds BDX brokerage columns).

## Phase 4 — Surplus lines & fees (per launch state)

- [ ] Surplus Lines admin: `SurplusLinesStateSetup` per state — filing broker name/license/address, license holder type, filing-required flag, **payee** config.
- [ ] Fees admin: SL **tax** and **stamping** fee definitions per state, linked on the SL setup.
- [ ] Diligent-effort research per state → set `DiligentSearchRequired` / `AffidavitRequired` accordingly (config only — enforcement is WS12, not yet wired).

## Phase 5 — Policy numbering

- [ ] Sequence created (format, next number, term-suffix format); **preview** shows expected numbers.
- [ ] Assignment scoped to program/carrier/GL(/state) points at the sequence.

## Phase 6 — Forms & documents

- [ ] Policy form package for program/carrier/GL/state(s), including each state's **mandatory SL disclosure form** as a static form (the merge-field path is a known gap — see §Known-gaps).
- [ ] Proposal template (non-email kind) exists; test-render against a policy succeeds.

## Phase 7 — Bordereaux profile

- [ ] Admin → Bordereaux profiles: Premium/monthly/XLSX profile for the program/carrier (GL or all-LOB per how you'll report), `RequiresAccountCurrent` on.
- [ ] Static values set: **UMR** (+ coverholder PIN/name default) → Setup status = **Ready for Export** (0 missing items). *(Post-batch F2/F7: Year of Account is now auto-derived from each transaction's effective year; required-columns/mapping no longer gate readiness — readiness = required tabs + UMR.)*

## Phase 8 — Accounting

- [ ] Chart of accounts / journal mapping present. Xero optional now: unbound config just leaves `pending_journal_syncs` rows in Pending (worker retries; not an error).

## Phase 9 — Access

- [ ] Role-permission spot check: a non-admin (Underwriter/CSR) sees the right nav; cannot reach admin pages; `claims.view`/`claims.manage` granted to intended roles.

---

## Lifecycle tests (the real proof)

### T1 — Intake & clearance
- [ ] Create insured (**State drives GL rating** — pick a launch state), then submission (GL).
- [ ] Duplicate-submission clearance triggers on a same-insured resubmit; bind-blocking overlap behaves.

### T2 — GL data entry
- [ ] Coverages: Each Occurrence (300K/500K/1M) → General Aggregate auto = 2×occ, PA&I auto = occ; PCO agg (1M/2M); Med (5/10/15/25K); DPRTYU; TRIA checkbox; **AI/WOS individual counts**; **Logging & Lumbering limit** dropdown. Save persists and reloads.
- [ ] Classifications: all 14 class codes selectable (incl. new **45819 Lumberyards**, **61212 LRO**); description auto-fills; exposure entry; CRUD works.

### T3 — Rating golden values (must match to the dollar)
Quote on the configured program/carrier, rate, and compare **quote grand total**:

| # | Insured state | Classifications (exposure) | Occ/PCO | Extras | Expected total |
|---|---|---|---|---|---|
| 1 | TX | 97111 ($500,000 payroll) | 1M/2M | none | **$8,177** |
| 2 | AL | 94007 ($200,000 payroll) | 1M/2M | TRIA + AI count 1 | **$10,309** (10,009 + 250 TRIA + 50 AI) |
| 3 | MS | 97111 ($300k) + 94007 ($150k) + 58873 ($1,000,000 sales) | 500K/1M | sched mod **0.90** (w/ reason) + blanket AI + PNC | **$10,734** (10,234 + 250 + 250) |
| 4 | AL | 97111 ($50,000 payroll) | 300K/1M | L&L limit $100K | **$892** (642 + 250 L&L min) |
| 5 | GA | 97111 ($500,000 payroll) | 1M/2M | L&L limit $1M | **$8,586.63** (7,806 + 780.63 = 10% of 97111 prem) |

- [ ] Line breakdown shows per-class lines with state, co-rates (LC×1.65), ILFs in factors; `GL-TRIA`, `GL-END-LL`, and `ADDINT-*` lines where applicable.
- [ ] Blanket AI/WOS/PNC are set in the **Additional Interests** section (not GL coverages) and each adds a flat $250.

### T4 — Rating guardrails
- [ ] No insured state → clear MISSING_FIELD error, not a $0 rate.
- [ ] Classification without exposure → MISSING_FIELD naming the row.
- [ ] **"(a)" refer-to-company**: 49451 (Vacant Land) in TX, or 91581 anywhere except AL → LOOKUP_FAIL "refer to company", never $0.
- [ ] Schedule modifier ≠ 1.00 without a reason → REASON_REQUIRED; value outside 0.80–1.20 clamps to bounds.

### T5 — Count-at-quote vs named records
- [ ] AI count 3 → $150 line. Then add **2 named** AI records (GL) → re-rate → line becomes **$100** (records supersede count; no double charge).
- [ ] Note: nothing currently forces named records to cover the count before issuance — known fast-follow.

### T6 — Proposal
- [ ] Select policy forms on the quote; proposal generates with the selected forms; email/send flow files a communication.

### T7 — Bind
- [ ] Bind succeeds; **policy number** matches the sequence format (term suffix correct).
- [ ] Invoice created and **Posted**; per-state assertion: invoice shows **SL tax + stamping fee lines** for the filing state (this is the WS12 manual guard — if lines are missing, record it per state).
- [ ] Premium parity: quote total = invoice premium = dec page (record any drift — WS11 single-source-of-truth item).
- [ ] Authority approval / referral queue triggers if program rules require (e.g., high limits); approve → bind completes.

### T8 — Issue
- [ ] Issuance readiness gates: open required referrals block preview/issue; resolves cleanly.
- [ ] Preview → draft PDF; Issue → final packet filed with the selected forms incl. the state SL disclosure form; dec page fields correct.

### T9 — Accounting artifacts
- [ ] Ledger transaction balanced; carrier payable, agent commission, receivable rows as expected; Xero sync row Pending/Synced (per config).

### T10 — Endorsement (positive)
- [ ] Midterm endorsement with premium increase: rates, invoices, completes; shows on policy transaction artifacts.

### T11 — Negative endorsement *(expected block)*
- [ ] Returns `RETURN_PREMIUM_ENDORSEMENT_ACCOUNTING_REQUIRED` — correct until WS11 lands.

### T12 — Cancellation notice
- [ ] Notice flow creates pending transaction + notice document without cancelling; completing a cancellation books **zero accounting** (known WS11 gap — verify the policy status flips, record the accounting gap).

### T13 — Renewal
- [ ] Renewal quote creates from the policy; rates on the **current** GL_v2 version.

### T14 — Bordereaux month-end
- [ ] Workbench: premium preview shows the bound transaction in its **invoice-date** month with correct gross/commission/net-due-carrier.
- [ ] Create run snapshot → run #1; validation summary **clear** (no missing London-LOB or SL-setup warnings if Phases 1/4 done).
- [ ] Generate export package → London BDX + Account Current XLSX download; UMR/PIN/statics present; commission from invoice stamp.
- [ ] *(Optional — F8)* Reconcile is **not required** for the London flow (BDX + Account Current derive from the same snapshot; London bills from what's sent). Exercise only if checking against external/actual-cash figures: matching totals → **Matched**; off-by-$1 → **Mismatch**.
- [ ] Second run creates run #2 without touching run #1 (frozen snapshots).

### T15 — Reports
- [ ] Written premium by program/carrier/LOB/state shows the bind; pipeline funnel counts the submission→bound; UW workload reflects the queue.

### T16 — Dashboard
- [ ] Bound-premium hero and task cards update.

### T17 — Loss runs
- [ ] Loss Run download works from Insured and Policy detail (empty result is fine pre-data-load).

### T18 — Logs
- [ ] No unexpected API errors in container logs during the above (`az webapp log tail`).

---

## Per-state matrix (repeat for each launch state)

| State | Rates correct (spot-check 97111 LC vs matrix) | SL tax+stamping lines on bind | Mandatory SL form in packet | Diligent-effort flags set |
|---|---|---|---|---|
| MS | ☐ | ☐ | ☐ | ☐ |
| AL | ☐ | ☐ | ☐ | ☐ |
| GA | ☐ | ☐ | ☐ | ☐ |
| … | ☐ | ☐ | ☐ | ☐ |

# Part B — Lloyd's IM (Beazley AFB 623/2623) — **DEFERRED** until Part A is clean

**Do not start this until Part A findings are fixed and its golden values pass.**
Kept here (not deleted) because a few code paths are IM-only and will never be
exercised by GL testing — the equipment schedule, the IM endorsement charges
(including the discrepancy flagged below), the deductible-eligibility guard, and
the IM Unit Info BDX tab.

The IM rater (`IM_v1`, data-driven) has been seeded since May — equipment types,
territories, base-rate/deductible factor tables, eligibility rules, and a Beazley
carrier assignment (pattern-matched). Part B is mostly *verification* plus the
program-hierarchy/financial/forms setup that no migration covers.

## Setup deltas (repeat Phases 1–9 for Beazley/IM)

- [ ] Phase 1: Program hierarchy — Beazley carrier, **IM LOB** active + launch states; London fields for the IM binder (its own UMR/section/class of business).
- [ ] Phase 2: Carrier rating assignment **IM → IM_v1 v1** exists (seeded — verify in AdminRatingPage/carrier detail rather than create).
- [ ] Phase 2b: Equipment types (12) and territories (7, with state→territory map) present.
- [ ] Phases 3–8: commissions, SL setup + fees per state, policy-number sequence + assignment for IM, form package + proposal template, BDX profile (IM Unit Info tab matters), accounting mapping — same drill as Part A.
- [ ] Re-run the **orphan audit** after Part B setup; resolve findings.

## IM lifecycle tests

- [ ] **IM golden value** (from `backend/seed/rating/README.md`, verified vs the IM workbook): MS insured, schedule mod **0.70** (w/ reason), deductible $2,500 on all items, **Debris Removal only** (uncheck Rental/Towing/Newly), 7 items: Fellerbuncher '07 $35K · Skidder '07 $35K · Dozer '06 $30K · Loader '14 $100K · Fellerbuncher '15 $80K · Loader '14 $65K · Skidder '19 $100K (ages vs a 2026 effective date: 19/19/20/12/11/12/7) → IM premium **$8,630**, + $250 debris = **$8,880**.
- [ ] ⚠️ **Endorsement-charge discrepancy to adjudicate**: the engine charges Rental Reimbursement **$500**, Towing/Storage/Recovery **$175**, Newly Acquired **$0**; the IM workbook/seed README says **$250 / $250 / $500**. Verify against the current IM rater and correct whichever is wrong before UAT sign-off.
- [ ] Ineligible deductible guard: Chipper or Tub Grinder with $2,500 deductible must refuse to rate (factor 0.00 = unavailable).
- [ ] TRIA (1% of subtotal for IM) calculates when selected.
- [ ] AI charges on an IM quote: named AI/loss-payee records charge per the global engine (count-at-quote fields are GL-only — IM uses named records/blanket).
- [ ] Repeat T6–T9 (proposal → bind incl. SL tax/stamping lines → issue → accounting) on the IM program.
- [ ] BDX: IM policy's equipment schedule appears on the **IM Unit Info** tab of the London export (serials, values).

## Sign-off

- [ ] **Part A done** (all boxes ticked; findings recorded against §Known-gaps or filed and fixed): flip §2 **Gate-A item 5 to ✅** in `GO-LIVE-UNIFIED-PLAN.md` and annotate §WS5 "GL configured end-to-end; IM deferred" with the date. **The Gate-A blocker is cleared at this point.**
- [ ] **Part B done** (run after Part A is clean): tick the remaining §WS5 checkboxes — WS5 fully closed.

---

## Known gaps — expected findings, don't chase

| Area | Expected behavior today | Tracked |
|---|---|---|
| Return premium / cancellation accounting | Negative endorsements hard-blocked; cancellation books no accounting | WS11 |
| SL tax fail-closed | Missing state config silently produces a tax-free invoice — the T7 per-state assertion is the manual guard | WS12 |
| SL document merge fields | `StampingWording`/`RequiredNoticeText` not consumed; use static per-state forms | WS12/P1 |
| AI/WOS issuance reconciliation | Named records not enforced to cover quoted counts at issue | fast-follow |
| Diligent-effort enforcement | Flags stored, never read at bind | WS12 |
| BDX once-and-only-once ledger | Late invoice (ReportingDate in an already-submitted period) never reports; no mark-submitted UI action | WS7 P0 |
| Quote TaxesAndFees parity | User-keyed quote tax fields can disagree with fee-engine invoice | WS11 |
