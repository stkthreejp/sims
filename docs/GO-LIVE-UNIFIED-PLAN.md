# SIMS — Unified Go-Live Plan (Internal UAT / Staging → Live Business)

> **Owner:** Jeremiah O'Donovan · **Created:** 2026-06-08 · **Reaudited & restructured:** 2026-06-10 · **Targets:** (1) Internal UAT on staging; (2) **Live business** — real submissions, binds, issuance, premium accounting, carrier reporting, and regulatory compliance.
>
> **What this is:** One plan that reconciles the ~40 plan/spec documents in `docs/` against the actual state of the repo, and sequences the remaining work. The 2026-06-10 reaudit (four parallel code-level reviews: workstream verification, MGA operating-cycle gap analysis, security closeout, frontend/ops readiness) found the codebase substantially ahead of the 6-08 plan on UI/security/bordereaux/reports — and behind it on **premium-accounting correctness**, which is now the critical path. Findings are in §8.
>
> **How to use it:** Work top-to-bottom by workstream. P0 blocks the stated gate; P1 before that gate is exited; P2 during/after. Two gates now exist: **Gate A (internal UAT)** and **Gate B (live business)** — see §2.

---

## 1. Where SIMS actually is (verified against code, 2026-06-10)

This is a genuinely built MGA platform. The 6-08 plan's frontier items have moved fast; the reaudit verified each claim against code rather than commit messages.

**Complete and verified (do not re-plan):**

- **Program configuration + Program-SOT contract** — nested Program > Carrier > LOB > State with canonical FKs enforced across fees, bordereaux profiles, surplus lines, form packages, proposal configs, policy numbers, commissions, rating assignments. Plus: **program orphan audit endpoint** (`GET /admin/program-configurations/orphan-audit`) and **AL non-bindable clearance block** (`UnderwritingClearanceService.cs:74-82`).
- **Underwriting control layer** — clearance, referrals, published controls, stage-aware checklists, post-bind gate, authority-approval spine, manager queue.
- **Rating engine** — `IM_v1` + `GL_v1`, versioning, bind-locked snapshots, Excel parity harness, **shadow-rate mode live with per-LOB toggles** (`RatingSettings`, `AdminShadowRatingPage`), schedule bounds configurable per plan version, minimum premiums seeded.
- **Policy lifecycle** — endorse / cancel / reinstate / rewrite / non-renew / renew endpoints; cancellation notice flow with effective-date math and legal-requirement snapshots.
- **Policy issuance documents** — `PolicyAssemblyService` end-to-end: state-scoped form packages, Mandatory/Conditional triggers, PDF fill/merge, `IssuedPolicyPacket` with SHA-256 + version/transaction linkage. Generic across LOBs (not IM-only as previously believed).
- **Bordereaux pipeline** — profiles admin, premium preview, snapshot runs, validation engine, CSV/XLSX export, **London BDX + Account Current XLSX (UMR / coverholder PIN / Auto-Veh / IM-Unit tabs)**, reconciliation gate, Bordereaux Workbench UI. Far more complete than the 6-08 plan implied. (Two defects: §WS7.)
- **Production reports** — renewals-upcoming, bound-by-period, hit-ratio-by-carrier: backend + frontend fully wired (UI-LINK-003 resolved).
- **Claims backend (WS10)** — Claim/ClaimImportBatch entities, CSV import with dedupe + policy matching, loss-run endpoint, ClaimsController CRUD/import/loss-run. (No frontend; security + valuation defects: §WS10.)
- **Accounting core** — invoice → receipt → cash application → disbursement chain, double-entry ledger, trust account, distribution sweeps, trust reconciliation + SL-tax aging + payable/AR aging reports, atomic invoice/receipt numbering via DB sequence, live QBO integration.
- **Security closeout** — C1 (UW writeup scope), H1 (FallbackPolicy), H2 (void tier), M1 (checklist scope), L2 (role remnants) **all verified fixed in code**. No `[Authorize(Roles=…)]` anywhere. Auth hardening: refresh-token-reuse revocation and inactive-user blocks exist and are correct.
- **UI closeout** — UI-LINK-001/002/003/005 fixed and verified; sidebar-vs-route parity fixed; real 404 page; Login redesigned on the design system; zero "coming soon" / `href="#"` / TODO in rendered UI.
- **CI/deploy** — `deploy.yml` gates on backend tests, `tsc`, lint, frontend build; Docker → ACR → App Service for both apps; health checks (`/health/live`, `/health/ready`) wired. Startup config validation fails closed (incl. malware-scanner provider and prod QBO-sandbox guard).

**The new frontier (what the reaudit surfaced):** the system can *sell and issue* policies but cannot yet *unwind or correctly account for* them — return premium does not exist anywhere, SL tax fails open, dec-page fees and invoice fees are never reconciled, and bordereaux has no once-and-only-once reporting guarantee. Those four are the P0 spine of Gate B (§3b). Secondarily: four frontend API clients are silently broken (billing/commissions UIs non-functional), the claims module has no UI and a High-severity scoping hole, and renewals are a dead-end workflow (visible, not actionable).

---

## 2. Definition of ready — two gates

### Gate A — Internal UAT (staff testing on staging)

1. ~~Working tree committed; build/test/typecheck/build green; EF drift clean.~~ ✅ (CI green 2026-06-10)
2. ~~Config fails closed in staging.~~ ✅ code-side; **operational items remain** (Azure app settings, Entra redirect URIs, firewall — §WS1).
3. ~~Backend authorization passes ownership-scope audit on core surfaces.~~ ✅ C1/H1/H2/M1 fixed; **except ClaimsService scoping (new High — §WS10)**.
4. ~~Broken-link/placeholder list zero; High-priority pages match the UI guide.~~ ✅ except dashboard Tasks card body (cosmetic).
5. One full program configured end-to-end and bindable. ❌ **Lloyd's carriers/programs not seeded** (§WS5) — open.
6. Production visibility for the launch program. ◐ launch trio live; **written-premium, pipeline, and UW-workload views promoted to day-one (§5.9)** — open in WS9.
7. Loss-run capability for the launch program. ❌ backend yes / frontend none / valuation defects (§WS10).
8. Deployed to Azure test env with health checks; backup/rollback rehearsal. ◐ deployed + healthy; **App Insights not wired; no post-deploy smoke test; rehearsal not done**.
9. CI gates green; burn-in with no open P0/P1. ◐ gates green; burn-in pending.
10. **The four broken frontend API clients fixed** (§WS4-R) — disbursements, cash distribution, agent & carrier commission tabs currently 401 on every call.

### Gate B — Live business (new, from the 2026-06-10 MGA operating-cycle audit)

Everything in Gate A, plus:

1. **Return premium exists**: midterm/flat cancellation and negative endorsement produce a credit invoice, carrier-payable reduction, SL-tax reversal, agent-commission chargeback, balanced ledger entries, and a negative-premium row on the next BDX. (§WS11)
2. **SL tax fails closed**: a filing-state bind that produces no SL tax + stamping lines is blocked, not silently tax-free. State validated against the program's eligible list at bind. (§WS12)
3. **Single source of premium truth**: dec page, ledger, and BDX cannot disagree — quote `TaxesAndFees` reconciled to (or replaced by) the fee engine at bind. (§WS11)
4. **Once-and-only-once carrier reporting**: every premium transaction appears on exactly one submitted bordereau; late-arriving items carry forward; runs have a "submitted" closure state. (§WS7)
5. **Cancellation notices are compliant**: statutory minimum days enforced from the in-system legal chart; proof-of-mailing captured; additional-interest/lienholder copies generated. (§WS13)
6. **Claims are scoped and correct**: access scope threaded through ClaimsService; loss runs use real valuation snapshots; imports cannot regress newer valuations. (§WS10)
7. **Staff can operate daily workflows in the UI**: claims list/import/loss-run, start-renewal action, bordereaux CSV download + mark-submitted. (§WS4-R, WS10, WS7)
8. Producer licensing tracked at least manually with a documented SOP; binder-vs-direct-issue decision implemented. (§WS13)

---

## 3. Workstreams — status after reaudit

### WS0 — Stabilize the working tree ✅ DONE
Tree committed, CI green (build + 453 tests + tsc + lint + build), EF drift clean as of 2026-06-10.

### WS1 — Go-live hardening / config fail-closed — ✅ code-side / ✅ operational

Code complete: startup validator covers JWT/DB/Blob/QBO×4/webhook/Graph/origins/malware-provider/QBO-sandbox-in-prod; secrets out of source; design-time factory throws; zero vulnerable NuGet packages; `MigrateAsync()` on boot (fine single-instance).

Operational punch list — **all done (2026-06-11)**:
- [x] Rotate the LegiScan API key; stored in Key Vault as `LegiScan--ApiKey`.
- [x] Azure app settings on `sims-api-test`: `Uploads__MalwareScanning__Provider` (`NoOp`), `GraphApi__ClientSecret` (rotated), `Qbo__WebhookVerifierToken`, `AllowedOrigins__0`.
- [x] Entra staging redirect URIs added (SPA platform, both short and long hostnames).
- [ ] Remove dev IP from Postgres firewall (do when VNet/private endpoint is set up).

### WS2 — Authorization & data-scope — ✅ P0s closed / open P1-P2 backlog

Verified fixed: C1, H1, H2, M1, L2 (file-level verification 2026-06-10).

- [ ] **(P1) ClaimsService scoping** — moved to WS10 item 1; it is the only High finding in current code.
- [ ] (P1) Remove class-level `[AllowAnonymous]` on `AuthController` — it neutralizes the per-action `[Authorize]` on `logout`/`me`/`me/password` (anonymous calls 500 instead of 401; future endpoints on that controller ship anonymous by default). Put `[AllowAnonymous]` on login/microsoft/refresh only.
- [ ] (P1) **M3 idempotency keys** on accounting create/apply (`ReceiptsController`, `CashApplicationController`, `DisbursementsController`) — duplicate postings under live volume are expensive to unwind now that voids need admin authority. `Idempotency-Key` header + key→result table.
- [ ] (P2) Entity-scope on the 11 submission child controllers (drivers carry DOB/license PII) and decide/document the shared-inbox stance for `InboundEmailsController`. Mitigated today only if every active user has `CanAccessAllBusinessData`. **Role model decided (§5.8 — tiered): underwriters manage insureds; agents/carriers admin-managed — apply when scoping.**
- [ ] (P2) Security regression tests: underwriter-cannot-reach-`rating.admin`, refresh-token-reuse, inactive-external-user (runtime logic exists and is correct; tests absent). Delete the dead `IsInRole("Admin")` in `VoidController:28`.

### WS3 — Open bug fixes — ✅ DONE (remaining items absorbed into WS11)
Email paging ✅, atomic numbering ✅ (DB sequence), NOT_FOUND→404 ✅, paging/sort validation ✅. The "cancellation accounting rules" open decision is **promoted from decision to build** — it is WS11.

### WS4 — UI alignment — ✅ DONE, plus **WS4-R: frontend repair (new, P0 for Gate A)**

Closeout verified: links fixed, guards aligned, 404 real, Login redesigned, no placeholders. Update `docs/ui-broken-links-tracker.md` statuses (rows still say "Open").

**WS4-R — ✅ DONE (2026-06-10):**
- [x] **(P0)** Four API clients (`agentCommissions`, `carrierCommissions`, `disbursements`, `cashDistribution`) rewritten on the shared `apiClient` (in-memory token + 401-refresh); Agent/Carrier detail pages now surface commission-load errors instead of rendering an empty list.
- [x] **(P1)** Bordereaux CSV download fetches as an authenticated blob (`downloadBordereauxRunCsv`) instead of a plain anchor.
- [x] **(P1)** Bordereaux Workbench route now requires `accounting.admin`; the Workbench and QB Sync Health entries in the Reports sidebar are permission-filtered.
- [x] **(P1)** Dashboard Tasks card renders open/overdue counts and the next four tasks from the user's queue.
- [x] **(P2)** Dead `ComingSoon`/`soon` code removed from ReportsPage; dead `'activity'` Tab member removed from InsuredDetailPage.

### WS5 — Program / carrier setup — ❌ the open Gate-A blocker

Machinery done (hierarchy, AL block, orphan audit). **Data missing: no carriers/programs are seeded** — Lloyd's IM (Beazley AFB 623/2623) and Lloyd's GL (DALE 1729) per the launch decision, with eligible states, limits, territories, rating assignments, fees, SL setup, policy-number sequences, form packages, commissions, QBO/GL mappings.

- [ ] Configure both launch programs end-to-end (state lists/limits per the SMM Underwriter context).
- [ ] Run the orphan audit; resolve every finding.
- [ ] **(new, from §8.2) Per launch state, an SL "tax assertion" check**: a test bind in each filing state must produce SL tax + stamping lines and include the state's mandatory SL disclosure form. This is the WS12 fail-closed behavior exercised as a setup-verification step.
- [ ] **(new, §5.4) Per launch state, diligent-effort research**: determine which launch states mandate pre-bind diligent effort/affidavits vs filing-time, and set `DiligentSearchRequired`/`AffidavitRequired` accordingly — feeds the WS12 bind blocker.
- [ ] Historical-versioning gap: defer, documented.

### WS6 — Rating — ✅ shadow-mode infrastructure done / cutover pending

- [ ] **Shadow-rate cutover**: accumulate shadow-vs-actual deltas on real-shaped UAT quotes; flip per-LOB toggles to authoritative when deltas are explained. (The comparison data + admin UI exist.)
- [ ] Confirm renewal rate-version + endorsement rating policy defaults.
- [ ] Second LOB rater — blocked on actuarial workbook handoff (post-UAT unless APD enters scope).

### WS7 — Bordereaux — ✅ pipeline done / ❌ reporting-integrity gaps (P0 for Gate B)

- [ ] **(P0) Once-and-only-once ledger**: rows are currently selected purely by ReportingDate-within-period — an invoice that lands late (ReportingDate in an already-submitted period) is **never reported on any run**. Stamp `BordereauxRunId`/reported-period on inclusion at run *submission*; next preview = "ReportingDate ≤ periodEnd AND not yet reported"; add an "N unreported prior-period items" validation row. Test: late invoice appears in the following run; no transaction ever appears on two submitted runs.
- [ ] **(P0, lands with WS11)** Return-premium rows flow onto the BDX automatically once credit invoices exist (preview already joins Invoice→PolicyTransaction).
- [ ] (P1) **Mark-submitted closure state** in the Workbench (status fields exist; no UI action) — also the hook for the once-only stamping.
- [ ] (P2) Carrier settlement netting: Account Current ↔ disbursement linkage (today payables are per-invoice due +30; London settlement is a manual match). Acceptable manual at launch.

### WS8 — Deploy, burn-in, UAT — ◐

- [ ] **App Insights** (zero references in code today) — or at minimum Azure log streaming before testers arrive; container logs are currently the only triage surface.
- [ ] deploy.yml: add post-deploy smoke (`curl /health/ready` loop — a bad container currently ships silently), `dotnet build backend` in the ci job (API-only compile errors currently surface late, in the docker build), dependency audit step.
- [ ] Backup/rollback rehearsal; QBO sandbox→production decision; KeyVault managed identity; worker double-run check.
- [ ] Full manual UAT script (submission → quote → bind → issue on launch program; endorsement; cancellation **with return premium once WS11 lands**; void approval; manager queue; bordereaux month-end; claims import + loss run).
- [ ] Burn-in, sign-off.

### WS9 — Production reporting — ✅ launch trio done / **three more promoted to P1 pre-UAT (§5.9)**

- [ ] **(P1)** Written premium by program/carrier/LOB/state and period.
- [ ] **(P1)** Submission pipeline funnel (received→quoted→bound→declined, conversion %, by program/agent).
- [ ] **(P1)** UW workload (open submissions/quotes/tasks per underwriter).

### WS10 — Claims & loss runs — ✅ DONE except data loads (2026-06-11)

- [x] **(P0)** `UserAccessScope` threaded through all ClaimsService reads and writes; claims scope via linked Policy `ForAccessScope`; **unlinked claims (PolicyId null) require full business-data access (fail closed)**; loss-run requests for foreign policies/insureds return ACCESS_DENIED; `UpdatedById` stamped; imported claims' financials are feed-owned (manual edits touch descriptive fields only).
- [x] **(P1)** Batch-keyed valuation history: `ClaimValuation` snapshot per (claim, valuation date); loss runs value each claim from the latest snapshot ≤ asOfDate; older import files upsert their snapshot but cannot regress current values; expense column mapped correctly (Paid = loss paid, Reserved = loss O/S, Expense = ALAE paid + O/S).
- [x] **(P1)** Import hardening: 20k row cap, batched existing-claim lookup (was N+1), rows colliding with manual claims are skipped with an error-summary entry.
- [x] **(P1)** Frontend: claims list with filters + totals, CSV import (Unified_Claims_Import layout) with batch history, sidebar/route guarded by `claims.view`/`claims.manage`; **Loss Run download buttons on Insured and Policy detail pages**.
- [x] **(P1)** Loss-run CSV export endpoint (`GET /claims/loss-run/csv`) with summary header + claim detail.
- [x] EF migration `AddClaimsAndValuations` (claims tables were previously entities-only — no migration existed); 9 new service tests covering scoping, valuation regression, snapshots, collisions, and the expense mapping.
- [ ] **Operational**: import the launch program's current claims (Sedgwick) + first historical load; grant `claims.view`/`claims.manage` to the right roles in Role Permissions.

---

## 3b. New workstreams for live business (from the 2026-06-10 MGA audit)

### WS11 — Return premium & financial integrity (P0, the critical path to Gate B)

Today: negative endorsements are hard-blocked (`RETURN_PREMIUM_ENDORSEMENT_ACCOUNTING_REQUIRED`), `CompleteCancellationAsync` books **zero accounting**, credit invoices skip payables (`if (GrossPremium > 0)`), the fee engine's `MinimumAmount` would flip a negative tax to a *positive minimum* on a credit, and `CancelAsync` accepts an arbitrary `PremiumChange` with no pro-rata validation. A midterm cancellation would over-remit SL tax, over-owe carriers on paper, and misreport written premium to Lloyd's.

Build (one coherent unit — ruleset decided, see §5.1):
- [ ] **Earned-premium calculator** — methods: pro-rata / short-rate / flat. Short-rate config per program (table **or** penalty-% of unearned). MEP support as an earned floor, configured at Program > Carrier (all LOBs) with per-LOB override — capability only, no MEP values at launch. Flat charges fully earned at issuance, excluded from return calcs. Flat cancellation = 100% unwind of premium, tax, stamping, fees, commission.
- [ ] **Method selection UX + governance**: cancellation/endorsement transactions get a method picker defaulted by reason (insured request → short rate; company-initiated/non-pay → pro rata); changing the default routes through the existing **authority-approval queue** before the transaction completes; chosen method + approver recorded on the transaction.
- [ ] **Credit-invoice path** in `InvoicingService`: negative `GrossPremium`; carrier-payable *reduction* (or receivable-from-carrier); **proportional agent-commission chargeback** ledger lines; **proportional SL tax/stamping reversal** that nets against the next filing while still appearing as a (negative) payable line — routed to the **filing vendor** payee normally, to the **state** payee when late (uses the existing `SurplusLinesStateSetup` filing-payee config).
- [ ] **Fee-engine negative-base guards**: `MinimumAmount` and `Stratified` must handle credits correctly; flat-earned charges must not re-enter the credit calc. **Fees are fully earned on midterm cancellation (§5.2)** — the credit invoice reverses premium + SL tax/stamping only; fees return **only** on flat cancellation's full unwind.
- [ ] Wire `CompleteCancellationAsync` and `IssueEndorsementAsync` (negative path) to the credit-invoice flow; `PremiumChange` is computed by the calculator, not user-keyed.
- [ ] **Premium single-source-of-truth**: at bind, reconcile or replace user-keyed `quote.TaxesAndFees`/`TotalPremium` with fee-engine output so dec page = ledger = BDX. (Today a quote keyed `TaxesAndFees=0` binds with a $0-tax dec page and a taxed invoice.)
- [ ] Regression tests: 50%-term pro-rata cancel → −50% premium invoice, negative SL tax line, **fees untouched (fully earned)**, payable reduced, ledger balanced, negative BDX row; short-rate (table and penalty-% variants) with approval-gated override; MEP floor honored when configured; flat endorsement charge survives later cancellation un-returned; flat cancel → 100% unwind including fees; quote-vs-invoice parity assertion.

### WS12 — Surplus-lines compliance (P0 fail-closed + P1 filing surface)

- [ ] **(P0) Fail-closed SL tax**: validate state at bind (non-empty + in program eligible list — today `State ?? ""` silently produces a tax-free invoice); after fee calc, assert filing-state binds produced lines for the configured `SurplusLinesTaxFeeDefinitionId`/`StampingFeeDefinitionId` (those FKs exist and are currently decorative). **`SlHomeState` field on submission (decided §5.3): defaults to insured's state, UW-overridable; this field — not the raw insured state — drives tax calc and filings.**
- [ ] (P1) **SL document merge**: `StampingWording`/`RequiredNoticeText` are stored but consumed nowhere; `BuildPolicyData` has zero `SurplusLines.*` merge fields (broker name/license, tax amounts). Interim workaround: static per-state mandatory forms (supported today) — verify per launch state in WS5.
- [ ] (P1) **Diligent-effort enforcement (decided §5.4 — per-state bind blocker)**: wire `DiligentSearchRequired`/`AffidavitRequired` (config exists, never read at bind) as a bind blocker in states that mandate pre-bind diligent effort, filing-checklist item elsewhere. Per-state research happens during WS5 setup.
- [ ] (P1) **SL filing report**: per-state period detail (policy, premium, tax, stamping) for SLAS/SLTX-style filings + filing calendar/tasks. `FilingFrequency`/`FilingDueDayOfMonth`/`FilingPaymentTermsDays`/`CreateFilingPayable` are dead config today; payables are hardcoded due `InvoiceDate+30`.

### WS13 — Issuance & notice compliance (P1)

- [ ] **Cancellation notices**: enforce `NoticeRequirementDays >=` the statutory minimum already in-system (`LegalRequirementSection` via `GetCancellationGuidanceAsync`) instead of accepting any integer; add proof-of-mailing fields/evidence to `PolicyCancellationDetail` (hang on existing `ComplianceEvidence`); generate notice copies per `SubmissionAdditionalInterest` (lienholders/loss payees — data already loaded for assembly). The direct-bill/notices memo defers *mailing automation* post-launch — that's fine; *capturing* compliance evidence is not deferrable for UW-initiated cancellations.
- [ ] **Endorsement documents**: `IssueEndorsementAsync` posts accounting but produces no paper; `GenerateForPolicyTransactionAsync` is only called for cancel/non-renew notices today.
- [ ] **Binder/certificate at bind (decided §5.5)**: auto-generate a binder at bind via the existing document-generation pipeline (`DocumentType.Binder` exists, nothing generates one); full packet issues after checklist completion. Binder shows coverage, carrier/syndicate, effective dates, SL disclosures where required.
- [ ] **Mandatory-form server guard**: issuance requires only "≥1 included form" — add a server-side check that `Mandatory` package forms cannot be deselected.
- [ ] **Producer licensing — hard block (decided §5.6, promoted to Gate B build)**: per-state producer license + E&O model with expiration dates on Agent (today: name + one free-text license number); bind blocks when the producing agent's license for the risk state is missing/expired, surfaced through the existing clearance/control layer. Admin UI for license entry; expiration report.

### WS14 — Post-launch backlog additions (P2, scheduled but not gating)

Premium-finance-company workflows (PFC entity, NOC intake, return-premium assignment — trucking E&S is heavily financed; schedule right behind WS11). Renewal automation worker (report exists; auto-task at X days pre-expiry). FNOL intake/TPA referral. Treaty/reinsurance model before Brace onboarding. Retention/legal-hold policy (document an SOP now; soft-deletes can currently remove records). Agent commission statements + automated commission disbursements. Installments + late notices (see direct-bill memo).

---

## 4. Reconciling overlaps & conflicts
(unchanged from 2026-06-08 — see git history for the original text; key calls: "Phase 6" = UW Control Layer; AI triage post-UAT; founding docx plans historical; one rating backlog in WS6.)

---

## 5. Open decisions needed from Jeremiah

Resolved: launch scope (Lloyd's IM Beazley + Lloyd's GL DALE; AL non-bindable) ✅; BDX day one (yes, GL) ✅.

**New, from the MGA audit — these gate WS11–WS13 builds:**

1. ~~**Earned-premium basis**~~ **✅ DECIDED (2026-06-10):**
   - **Methods:** Pro-rata (default), Short-rate, and Flat — selectable per cancellation/endorsement transaction. Flat covers endorsement charges that are never prorated regardless of when in the term they occur.
   - **Short-rate calculation:** configurable per program — either a short-rate table or a penalty-% of unearned; program setup chooses.
   - **Reason-driven defaults:** insured's request → short rate; company-initiated / non-pay / UW reasons → pro rata. Changing the method away from the default requires **override + authority approval** (route through the existing authority-approval queue before the transaction completes).
   - **MEP:** not used today, but build the capability — configured in **Program > Carrier setup (applies to all LOBs) with optional per-LOB override**. Calculator enforces it as an earned floor when set.
   - **Commission chargeback:** proportional — agent returns commission on returned premium at the rate paid.
   - **SL tax/stamping on returns:** reverse proportionally on the credit invoice; insured refund includes it; SMM nets the credit against the next filing — **and the net transaction must still appear in the payable**. Tax payables route to the **filing vendor** in the normal case, payable **directly to the state when late**.
   - **Flat endorsement charges:** fully earned at issuance — never returned on later cancellation.
   - **Flat cancellation (inception):** full unwind — 100% of premium, tax, stamping, and fees returned; full commission chargeback; policy recorded as flat-cancelled.
2. ~~**Fee earned semantics**~~ **✅ DECIDED (2026-06-10):** policy/broker fees are **fully earned** on midterm cancellation — no fee reversal on the credit invoice. The single exception is **flat cancellation** (inception), where the full unwind per §5.1 returns fees too.
3. ~~**NRRA home state**~~ **✅ DECIDED (2026-06-10):** explicit `SlHomeState` field on the submission, **defaulting to the insured's state with UW override** for principal-place-of-business cases. The field (not the raw insured state) drives tax calc and filings.
4. ~~**Diligent effort**~~ **✅ DECIDED (2026-06-10):** **per-state bind blocker** — during WS5 setup, research each launch state; where the state mandates pre-bind diligent effort, bind blocks until affidavit/declination info is recorded; other states get a filing-time checklist item.
5. ~~**Binder vs direct-to-issue**~~ **✅ DECIDED (2026-06-10):** **binder at bind, policy follows** — the system auto-generates a binder/certificate at bind as evidence of coverage; the full packet issues after checklist completion. WS13 binder item is now a definite build.
6. ~~**Producer licensing**~~ **✅ DECIDED (2026-06-10):** **hard block at bind** — build the per-state producer license/E&O model with expirations now; bind blocks when the producing agent's license for the risk state is missing or expired. Promoted from post-UAT to a Gate B build (WS13).
7. ~~**Claims valuation cadence**~~ **✅ DECIDED (2026-06-10):** **mixed cadence** — some feeds monthly, some historical feeds weekly or daily. Valuation history must be **batch-keyed by valuation date** (each import batch is a snapshot), supporting arbitrary cadence; no fixed monthly snapshot table.
8. ~~**Party/workflow role model**~~ **✅ DECIDED (2026-06-10):** **tiered** — underwriters create/edit insureds in the normal workflow; agents and carriers are admin-managed setup data. Unblocks WS2 P2 permission scoping.
9. ~~**Day-one report set**~~ **✅ DECIDED (2026-06-10):** all three additional views are must-have for UAT day one — **written premium by program/carrier/LOB/state, submission pipeline funnel, and UW workload**. WS9 fast-follow items promoted to P1 pre-UAT.

**All §5 decisions are now resolved.** WS11–WS13 are fully unblocked.

---

## 6. Where agents can simplify this
(unchanged in substance from 2026-06-08; the 2026-06-10 reaudit itself followed this model — four parallel read-only reviewers. Continue using: security-auth reviewer per release, frontend reviewer for the WS4-R fixes, insurance-workflow reviewer to re-verify WS11/WS12 after build, browser route crawl per role before UAT.)

---

## 7. Sequenced path

**To Gate A (internal UAT):**
1. **WS4-R** broken API clients (small, restores billing/commission UIs) + **WS10 ClaimsService scoping** (small, closes the High).
2. **WS5** seed both launch programs end-to-end; orphan audit clean; SL tax-assertion check per state.
3. **WS1 operational** items + **WS8** App Insights/smoke-test/rehearsal.
4. UAT script + burn-in. *(WS6 shadow data accumulates during UAT.)*

**To Gate B (live business):**
5. **WS11** return premium & financial integrity ← *the critical path; start the §5.1 decisions now.*
6. **WS12** SL fail-closed (small, do with WS11) + filing report.
7. **WS7** BDX once-only + mark-submitted (before the first real DALE submission).
8. **WS13** notice compliance + endorsement docs + binder decision.
9. **WS10** claims valuation fixes + claims/loss-run UI; first Sedgwick import.

Gate B = §2 Gate-B checklist fully satisfied with no open P0/P1.

---

## 8. Live audit findings

### 8.0 Reaudit summary (2026-06-10, four parallel code-level reviews)

| Review | Headline |
|---|---|
| Workstream verification | WS7/WS9 further along than planned; WS5 has **no seeded carrier data**; WS10 backend-only; App Insights absent; no installments/late-notices/agent-statements. |
| MGA operating cycle | **4 P0s**: return premium nonexistent; SL tax fails open; quote-vs-invoice fee divergence; BDX once-only gap. 9 P1s incl. notice compliance, SL filing surface, endorsement/binder docs, claims valuation, producer licensing. |
| Security closeout | C1/H1/H2/M1/L2 all verified fixed. **New High: ClaimsService unscoped.** M3 idempotency + child-controller scoping remain. AuthController class-level `[AllowAnonymous]` (Low). |
| Frontend/ops | WS4 verified closed. **4 API clients silently broken (401 everything)**; bordereaux CSV anchor 401s; renewal workflow is a dead-end (no Start Renewal caller); claims UI absent; no post-deploy smoke. |

Full detail lives in the workstream sections above (§3, §3b), which supersede the 6-08 tables below where they conflict.

### 8.1 Security / authorization (2026-06-08 baseline — closeout state now in WS2)

Historical reference: C1 (UW writeup), H1 (fallback policy), H2 (void tier), M1 (checklist scope), M2 (least-privilege view perms), M3 (idempotency), L1 (doc-gen ordering), L2 (role remnants). C1/H1/H2/M1/L2 verified fixed 2026-06-10; M3 open (WS2); M2/L1 unchanged (P2).

### 8.2 Route + links crawl (2026-06-08 — now closed)

UI-LINK-001/002/003/005, UI-DOC-001, 404 page, sidebar parity, `/tasks` guard: all resolved and verified 2026-06-10 (UI-LINK-004 partial — empty card body). Update `docs/ui-broken-links-tracker.md` to match. New frontend findings are in **WS4-R**.

---

## Appendix A — Source-plan crosswalk
(unchanged from 2026-06-08, plus:)

| Workstream | Primary source |
|---|---|
| WS11 return premium | 2026-06-10 MGA audit (§8.0); `InvoicingService.cs`, `PolicyService.cs:898-1106`, `FeeCalculationService.cs` |
| WS12 SL compliance | 2026-06-10 MGA audit; `SurplusLinesStateSetup.cs`, `QuoteService.cs:476` |
| WS13 issuance/notices | 2026-06-10 MGA audit; `DIRECT-BILL-AND-NOTICES-ARCHITECTURE.md` (mailing automation stays post-launch) |
| WS4-R frontend repair | 2026-06-10 frontend audit; the four `*.api.ts` raw-fetch clients |

## Appendix B — Out of scope for UAT (post-launch backlog)

Unchanged: shared job/outbox framework, full AI extraction/scoring/triage, FMCSA 2–7, compliance-doc remaining build, issuance automation beyond pilot, program historical-interval versioning, **direct bill + ePay + dunning + mailing automation** (see architecture memo). Newly scheduled (WS14): PFC workflows, renewal worker, FNOL, treaty model (pre-Brace), retention SOP, agent statements, installments.
