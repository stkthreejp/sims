# WS5 Part A — Findings & Fix Queue

Running list of findings from the GL one-company setup/test pass (started 2026-07-02).
Protocol: findings accumulate here; fixes ship in **batches** on Jeremiah's call (every
push restarts the test apps), except real blockers which ship immediately.

Statuses: `LOGGED` → `DECIDED` (disposition agreed) → `IN BATCH` → `DEPLOYED` → `VERIFIED`.

## Batch 1 (shipped 2026-07-02)

- **F1 DEPLOYED (backend enforcement)** — program Add/Update-LOB rejects a LOB the carrier
  doesn't declare (`CARRIER_LOB_NOT_SUPPORTED`). **Frontend dropdown-filter deferred**: the
  program-config page lacks the carrier's capability list, so the dropdown still lists all
  LOBs and the backend rejects an unsupported pick with a clear toast. Filtering the dropdown
  needs carrier-capability data plumbed into that page — small follow-up.
- **F2 DEPLOYED (interim)** — YOA on the London export derived from each transaction's
  effective year; `yearOfAccount` dropped as a manual static. Full Binder = post-UAT.
- **F6 DEPLOYED** — AI-rate amount fields gated by charge method (frontend shows only the
  applicable field; backend nulls the rest).
- **F7 DEPLOYED** — BDX readiness = Tabs + statics; required-columns/mapping dropped.
- **F8 — no prod change needed** (`requireReconciliation` was test-fixture only; UI already
  sends `{}`). Reconcile stays optional. Checklist T14 marked optional.
- **F11 DEPLOYED** — Intermediaries admin stacked top/bottom (list capped-scroll on top).

Deferred: full Binder (F2 end-state), F9b (agent doc attachments), F10 (CreatePayable → WS11).

## Batch 2 (shipped 2026-07-03)

- **F9a DEPLOYED** — agent compliance rework: E&O limit + insurance-company fields, Broker
  Agreement continuous flag, and **multiple state licenses** (collection). See F9 below.

---

## F1 — Program Add-LOB ignores the carrier's declared lines of business

**Status: DECIDED (Option A) — queued for fix batch**

Found during Phase 1. The "Add line of business" picker under a program carrier offers
every active LOB; the carrier's own LOB capability list (`CarrierLineOfBusiness`, set on
the carrier create/edit form) is enforced nowhere — it's display-only today.

**Decision:** make the capability list real. Carrier record declares what paper the
carrier writes; program setup deploys a subset of it.

**Fix scope (small):**
- Backend: `ProgramConfigurationService.AddLineOfBusinessAsync` (and Update) — reject
  LOBs not in the carrier's `CarrierLineOfBusiness` list (`CARRIER_LOB_NOT_SUPPORTED`).
- Frontend: filter the Add-LOB dropdown (ProgramConfigurationAdminPage:357) to the
  selected carrier's declared LOBs; empty-state hint "Add lines of business on the
  carrier record first."
- Resulting setup funnel: declare LOBs on carrier → deploy subset on program →
  downstream config already constrained by the program spine.

## F2 — Program setup carries carrier detail; should be spine-only

**Status: DECIDED in principle — scope below to confirm; larger item, likely its own batch**

Found during Phase 1. The program-carrier-LOB dialog asks for **payment terms and the
five London/BDX fields** (UMR, section, class of business, risk code, insurance type)
— detail that belongs with the carrier, not in the program spine
(ProgramConfigurationAdminPage:368–375).

**Decision (mental model):** *program setup is just the spine* — which carriers, which
LOBs, which states, active/effective when. *Carrier setup is where all the detail
lives* — commissions, rating assignments, AI rates, BDX profiles (all already there),
and the **London binder data moves into the BDX profile** (decided 2026-07-02: UMR is
carrier/binder-specific and belongs with the profile that already carries
`umr`/`coverholderPin`/`yearOfAccount` statics).

Today's duplication this kills: the London export resolves UMR as
`ProgramCarrierLineOfBusiness.LondonUmr ?? profile static umr` (PCL wins —
BordereauxService.cs:830), while section/class-of-business/risk-code/insurance-type
come only from the PCL row. Two sources of truth for UMR; profile becomes the single one.

**Rescoped plan:**
- **BDX profile** becomes the sole home for London reporting config:
  - Profile-level statics (existing): `umr`, `coverholderPin`, `yearOfAccount`,
    `coverholderName`.
  - **New per-LOB section map** in the profile config (one binder → many sections):
    section number, class of business, risk code, insurance type keyed by LOB — a
    multi-LOB profile (combined BDX: GL + Auto + IM tabs) needs these per LOB.
- Export reads section/class/risk from the profile's per-LOB map; UMR from profile
  statics only (drop the PCL fallback). `missingLondonLobSetupRows` validation moves
  from PCL completeness to profile completeness — joining the existing profile
  setup-status panel (coherent: one place answers "is this BDX ready?").
- Drop the five `London*` columns from `ProgramCarrierLineOfBusiness` (migration;
  backfill any entered values into the matching profile first).
- ProgramConfigurationAdminPage LOB dialog trims to spine fields: LOB, active,
  effective/expiration dates.
- **Leftover — payment terms** (`PaymentTermsDays`, billing detail not BDX): move its
  editing to the carrier page (alongside commissions/billing); data can stay on PCL.
  Sub-decision open: keep per-program-LOB or make it a carrier-level default.
- Checklist Phase 1/7 updated to match the new homes.

**REFRAME (2026-07-02, from testing) — model a Binder, don't just move fields.**
The London binder data isn't static profile metadata; it's **binding-authority-period**
data that changes at renewal. Proposed richer model:
- A **Binder** (binding authority) entity per program/carrier/LOB with **effective +
  expiration dates**, **UMR**, section/class/risk/insurance-type, commission terms,
  and a **renewal** action that opens the next period (new UMR/commission possible).
- **Year of Account (YOA) is derived**, not keyed: YOA = the binder period whose
  effective range covers the policy's effective date (typically that period's
  inception year). Kills the manual `yearOfAccount` static (F7 relevance).
- The BDX profile references the binder (or resolves the active binder period at run
  time); UMR/commission/YOA on the export come from the binder period matching each
  transaction's effective date.
- Ties to existing `CarrierCommission` effective-dating — a binder renewal that changes
  commission should line up with a new commission effective row.
- **Status: DECIDED (2026-07-02) — interim now, full binder post-UAT.**
  - **Interim (queued for batch):** derive **YOA from the transaction/policy effective
    year** in the BDX export (drop the hand-keyed `yearOfAccount` static and remove it
    from the F7 readiness statics); keep UMR/section/class/risk on the profile (or PCL)
    as-is so WS5 setup proceeds. No new schema.
  - **Post-UAT (tracked in GO-LIVE plan):** full **Binder** entity — eff/exp dates,
    UMR, commission terms, section/class/risk, **renewal** opening the next period; BDX
    + YOA resolve from the binder period covering each transaction's effective date;
    renewal-driven commission changes align with `CarrierCommission` effective rows.

## F6 — Additional-interest rate: amount fields not gated by charge method

**Status: DECIDED — queued for fix batch (small)**

Found during Phase 2. The AI-rate form (CarrierDetailPage) shows both Per-Interest and
Blanket amount inputs regardless of the selected charge method, and the backend
(`CarrierAdditionalInterestRatesController.Apply`) stores whatever is sent — so you can
save a PerInterest rule with a stray blanket amount (and vice versa). Backend `Validate`
only checks the *required* amount is present, not that the *other* is absent.

**Fix:**
- Frontend: show only the amount relevant to the chosen charge method (PerInterest →
  per-interest amount; BlanketFlat → blanket amount; NoCharge/Included → neither;
  min/max only for the two charging methods).
- Backend `Apply`: null the non-applicable amount(s) by charge method so stored data
  can't carry a contradictory value.

## F7 — BDX profile can never reach "Ready for Export" (required columns/mapping have no UI)

**Status: DECIDED (Option A) — queued; borderline-blocker (Phase 7 checklist item)**

Found during Phase 7 ("how do you even satisfy the required columns?"). The readiness
computation counts four groups: Required Tabs, **Required Columns** (3 fixed London
columns), Static Values, and **Mapping Rules** (commissionBasis). But the setup panel
(and the admin form) only edit **Tabs** and **Static Values** — there is no control for
Required Columns or Mapping Rules, and both create dialogs send `[]`/`{}`. So those
items are permanently "Missing" and `IsReadyForExport` can never be true via the UI.
(Runs still generate — readiness is advisory, not a hard gate — but the Phase 7
checklist item "0 missing items" is unsatisfiable.)

**Decision (Option A):** the 3 required columns are structural London-template columns
the exporter always writes — they shouldn't be a manual checklist item. Drop Required
Columns from the readiness computation (or auto-satisfy). For **Mapping Rules /
commissionBasis**: CONFIRM whether it's actually consumed by the export
(commissionPlusBrokerage vs commission). If consumed → expose it as a small dropdown in
the setup panel; if vestigial → drop it too. Net: readiness = Tabs (auto/LOB-driven) +
real Static Values (UMR, coverholder PIN, YOA/coverholderName-with-default), which is
achievable and meaningful.

---

*(next finding goes here)*

## F3 — BLOCKER: can't assign rating plan to a program set up after the rate version's inception

**Status: FIXED — shipped immediately (blocker)**

Found during Phase 2 (adding GL_v2 to Lloyd's of London – Dale). Creating a
program-scoped rating assignment failed with "Selected carrier and line of business
are not active for this program" even though the program, carrier, GL and states were
all configured.

**Root cause:** `CarrierRatingAssignmentService.ResolveProgramCarrierLobPathAsync`
required the program path to be active *as of the rating version's effective date*
(GL_v2 v1 = 2026-01-01). Any program line with a later effective date (e.g., binder
inception 2026-08-01) could never pass — backwards logic.

**Fix:** the version's effective range and the program path's effective range must
**overlap** (a version live since January is valid for a program line starting in
August); disjoint ranges still reject. Regression tests added for both directions
(`CarrierRatingAssignmentProgramScopeTests`).

## F4 — BLOCKER: BDX profile creation always fails ("Required tabs must be a non-empty JSON array")

**Status: FIXED — shipped immediately (blocker)**

Found during Phase 7. Both create surfaces (Admin → Bordereaux profiles and the
carrier-page dialog) send `requiredTabsJson: '[]'` with no tab input at create time —
the tab picker lives in the *post-create* setup panel — while the backend rejected
empty arrays. Chicken-and-egg: no profile could ever be created from the UI.

**Fix:**
- Backend: `RequiredTabsJson`/`RequiredColumnsJson` now accept an empty (but valid)
  JSON array at create/update — completeness stays policed by the setup-status panel
  (`IsReadyForExport` + "Missing" items), which is the designed progressive flow.
  Malformed/non-array input still rejects.
- `IncludedTransactionTypesJson` stays strict (empty would filter every premium
  preview to zero rows); the carrier-page dialog now defaults to the same full
  transaction-type set the admin page already used (shared `DEFAULT_BDX_TXN_TYPES`).
- Regression tests: create with empty tabs/columns succeeds + flags Missing; create
  with empty transaction types still rejects.

## F5 — BLOCKER: rating assignment still fails after F3 ("failed to assign rating plan", 500)

**Status: FIXED — shipped immediately (blocker; F3 follow-through)**

Same action as F3, new failure mode: generic toast (no backend message) because the
API 500'd. Container logs showed `DbUpdateException → Npgsql P0001` from the
**database trigger** `validate_carrier_rating_assignment_program_scope` — a second
enforcement layer (migration `AddCarrierRatingAssignmentProgramScopeRefs`) still
enforcing the old point-in-time rule ("path active as of the version's effective
date"). F3 fixed the C# resolver only, so the service approved and the DB vetoed.

**Fix:** migration `FixRatingAssignmentTriggerVersionOverlap` replaces the trigger
function with the same range-overlap semantics as the service; Down restores the
original. Reviewed the sibling program-scope triggers (commissions, fees, forms,
numbering, proposals, brokerage, SL): they validate against their row's own
user-chosen effective date, not a global version date, so they don't share this trap.

**Lesson recorded:** the "program SOT" constraints are enforced twice — service layer
AND plpgsql trigger. Any semantic change to one must change both.

---

## F8 — Reconciliation is optional (not part of SMM's London cycle); drop the decorative flag

**Status: DECIDED — no functional fix needed; minor cleanup queued**

Confirmed during Phase 7. SMM generates BOTH the BDX and Account Current from the same
frozen snapshot (same SOT → they tie out by construction) and sends both to London,
which bills from them. So there's no internal reconciliation to perform.

Verified nothing forces it: `ReconciliationStatus` starts NotRun → Generated and only
changes on an explicit Reconcile call; the default profile's
`ValidationRulesJson {"requireReconciliation":true}` is **never read/enforced** anywhere
in the backend. So the step is already skippable — no code change needed to proceed.

**Cleanup (queued, minor):**
- Remove `requireReconciliation:true` from the default profile `ValidationRulesJson`
  (misleading — implies enforcement that doesn't exist).
- WS5 checklist: mark T14 reconcile sub-steps optional/N-A for the London flow.
- Keep the Reconcile feature itself (still useful for reconciling against actual cash
  collected or a carrier's returned statement on other binders).

## F9 — Agent compliance docs: missing fields, single-per-type, and no attachments

**Status: F9a DEPLOYED (Wave 2, 2026-07-03) — F9b attachments post-UAT.**
- **F9a (DEPLOYED):** E&O certificate gained **limit** + **insurance-company name** fields;
  **State License reworked to a collection** — `AgentComplianceDoc` StateLicense rows are now
  multiple (one per state, keyed by row id) via `POST/PUT/DELETE /agents/{id}/compliance/licenses`,
  with duplicate-state and required-state guards; EOCertificate & BrokerAgreement stay singletons
  on the existing `PUT/DELETE /compliance/{docType}` (which now reject StateLicense). Broker
  Agreement gained a **Continuous** (evergreen) flag that hides the executed date when set.
  Quote-readiness = EO present & not-expired + Broker present + ≥1 license & none expired.
  Migration `AddAgentComplianceEoAndContinuousFields` adds `eo_limit`, `eo_carrier_name`,
  `is_continuous`. Detail-page compliance card reworked into EO + Broker cards + a multi-row
  State Licenses section. (Note: the new E&O limit/carrier fields are captured but **not**
  gated as required — kept informational to avoid blocking quote-readiness; revisit if UAT wants
  them hard-required.)
- **F9b (post-UAT):** required document uploads (dec page / license copy / signed agreement)
  auto-filed to the agent Documents section.

Found during Agent setup. `AgentComplianceDoc` supports 3 types (EOCertificate,
StateLicense, BrokerAgreement) but is **upserted one-per-type**
(`PUT /agents/{id}/compliance/{docType}`) and carries only ExpirationDate / LicenseState
/ ExecutedDate / Notes — **no file attachment**. Agents already have a general
**Documents** section (`entityType="Agent"`), so "auto-file" has a target.

Requested changes:
- **E&O**: add **limit** (decimal) + **insurance company name**; require both; require a
  **dec-page attachment** that is auto-filed to Agent Documents.
- **State License**: support **multiple** (currently singleton-per-type — the API path is
  keyed by docType); each with state + expiration + a required **license copy** auto-filed.
- **Broker Agreement**: add a **Continuous** flag that grays out the expiration date;
  require a **signed-agreement upload** auto-filed.
- **Auto-file** = on upload, create an Agent Document (existing DocumentsSection) so the
  file appears in the agent's documents automatically.

Scope: real MGA compliance gate (unlicensed/no-E&O agent shouldn't place business), but
the multiple-license rework + attachment/auto-file plumbing is a moderate build. Decide:
data fields + multiple licenses now / full incl. attachments now / all post-UAT.

## F10 — Intermediary `CreatePayable` is stored but not wired to create a payable

**Status: LOGGED — confirm intent; behavior not implemented (ties to WS11)**

Confirmed the intended semantics (matches Jeremiah): `CreatePayable` ON = SMM issues a
**direct payable to the broker** for their brokerage (hence the required payee); OFF =
net settlement — SMM remits premium-less-SMM-commission to the intermediary and they
take their brokerage out of that flow (no separate payable).

**Caveat:** `CreatePayable` is only ever **written** (IntermediaryService) — nothing
reads it to actually create an AP payable. The brokerage amount is always **netted**
into the London BDX "net due carrier" figures regardless of the flag. So today the
checkbox records intent only; the direct-payable path isn't implemented. Ties to WS11
(financial integrity / disbursements). No action needed for the netted London flow;
implement the direct-payable branch when WS11 disbursements are built.

---

## F11 — Intermediaries admin: prefer top/bottom layout over left/right

**Status: DECIDED — queued for batch (small, frontend-only)**

Found during Intermediary setup. `IntermediariesAdminPage` uses a two-column
`grid grid-cols-[320px_1fr]` (list left, details right, line ~358). Change to a stacked
**list-on-top / details-below** layout (`flex flex-col` / `grid-cols-1`). If the
full-width vertical list reads too tall, make the top list compact (horizontal card
row or a slim table) — confirm during implementation. No API/schema change.

---

*(next finding goes here)*
