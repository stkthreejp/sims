# WS5 Part A — Findings & Fix Queue

Running list of findings from the GL one-company setup/test pass (started 2026-07-02).
Protocol: findings accumulate here; fixes ship in **batches** on Jeremiah's call (every
push restarts the test apps), except real blockers which ship immediately.

Statuses: `LOGGED` → `DECIDED` (disposition agreed) → `IN BATCH` → `DEPLOYED` → `VERIFIED`.

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

*(next finding goes here)*
