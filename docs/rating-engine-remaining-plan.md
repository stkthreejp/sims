# SIMS Rating Engine — Remaining Plan

**Status:** Draft for team review
**Owner:** Jeremiah O'Donovan
**Last updated:** 2026-05-24
**Related:** [rating-engine-plan.md](./rating-engine-plan.md) (original roadmap)

This document picks up where the original plan left off and lays out **everything remaining** in detail — every endpoint, every UI page, every validation, every gotcha — through to the end.

---

## 0. Status snapshot (where we are today)

| Phase | Status | Commit |
|---|---|---|
| 0 — LOB cleanup (IM/AL/APD active enum values) | ✅ done | `a8822b7` |
| 1 — Domain + versioning + IM seed (incl. Beazley assignment) | ✅ done | `034834e` |
| 2 — Engine (`IM_v1`) | ✅ done | `034834e` |
| 2 — Excel parity test harness | ✅ done — implemented in `backend/tests/SIMS.Application.Rating.Tests` with IM v1 fixtures |
| 3A — Submission Equipment editor (rating fields + IM lookups) | ✅ done | `f7eb0de` |
| 3B — Quote Rating panel + snapshot retrieval | ✅ done | `f7eb0de` |
| 3C — Bind flow integration (snapshot lock) | ✅ done | `f2a1742` |
| **Phase 4 — Plan & Carrier Admin** | ⏳ next | — |
| Phase 5 — Shadow rate cutover | ❌ pending | — |
| Phase 6 — Second LOB rater | ❌ pending | — |
| Phase 7 — Polish (parity harness, role bounds, etc.) | ⚠️ partially done — parity harness/admin safety gates are in place; role bounds, worksheet PDF, renewal, and endorsement policies remain | — |

---

## Phase 4 — Plan & Carrier Admin

This is the largest remaining phase. It merges:
- The "Carrier Setup Admin UI" from your plan (Phase 4 in your original) — small, concrete.
- The "Rating Admin" from your plan (Phase 5) — read-only, promote-only.
- The "Admin UI for factor maintenance" from my plan — full editor + impact preview + maker/checker.

I split it into **5 sub-phases** so we can ship incrementally and stop at any line.

### 4A — Carrier Rating Assignment UI ✅ ship-first slice

**Why first:** Without an assignment row, the engine returns `NO_RATING_PLAN`. Today only Beazley is wired (via seed). Underwriters can't onboard a new carrier without a code change.

**Backend**

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/v1/carrier-rating-assignments` | List all assignments. Joins carrier name, LOB label, plan name, version number/effective date. Optional `?carrierId={guid}` filter. |
| `GET` | `/api/v1/rating-plan-versions?lob={lob}` | List all `Active` versions for an LOB, with plan name + carrier-friendly label. Used to populate the version picker. |
| `POST` | `/api/v1/carrier-rating-assignments` | Create. Body: `{ carrierId, lineOfBusiness, ratingPlanVersionId }`. Validates: carrier exists, LOB is in `ACTIVE_LOBS`, plan version exists and matches the LOB, no existing assignment for `(carrierId, lineOfBusiness)`. |
| `PUT` | `/api/v1/carrier-rating-assignments/{id}` | Update — only `ratingPlanVersionId` changes. Same validation as create. |
| `DELETE` | `/api/v1/carrier-rating-assignments/{id}` | Soft-delete (BaseEntity already has IsDeleted). Returns 409 if any **bound** quote references this version (data-integrity guard). |

**DTOs (new file `CarrierRatingAssignmentDto.cs`)**
```csharp
public class CarrierRatingAssignmentDto {
    public Guid Id; Guid CarrierId; string CarrierName;
    public PolicyLineOfBusiness LineOfBusiness; string LineOfBusinessLabel;
    public Guid RatingPlanVersionId; string PlanName;
    public int VersionNumber; DateOnly EffectiveDate;
}
public class CarrierRatingAssignmentCreateDto { Guid CarrierId; PolicyLineOfBusiness LineOfBusiness; Guid RatingPlanVersionId; }
public class CarrierRatingAssignmentUpdateDto { Guid RatingPlanVersionId; }
public class RatingPlanVersionPickerDto { Guid Id; string PlanName; int VersionNumber; DateOnly EffectiveDate; PolicyLineOfBusiness Lob; }
```

**Authorization:** `[Authorize(Roles = "Admin,Underwriter")]`

**Frontend — `CarrierDetailPage.tsx`**
Add a new section between Commissions and Documents: **"Rating Plans"**.
- Table columns: `Line of Business | Plan | Version | Effective Date | Actions (edit / remove)`.
- "Assign Rating Plan" button → modal with 2 dropdowns (LOB filtered to `ACTIVE_LOBS`, then plan version filtered to that LOB).
- Edit click → modal pre-filled, only the version dropdown is enabled.
- Remove click → confirm dialog → DELETE.
- Empty state: "No rating plans assigned. Quotes for this carrier won't rate until a plan is assigned."

**New API client functions** in `frontend/src/api/rating.api.ts` (new file).

**Done when:** I can take a non-Beazley carrier in the UI, assign IM v1 to it, then create a quote against that carrier and rate successfully.

---

### 4B — Admin Rating Plans Index

**Why next:** Gives the team a place to see what plans exist before we let them edit anything.

**Backend**

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/v1/rating-plans` | All plans, with their currently-active version summary. |

**DTO**
```csharp
public class RatingPlanListItemDto {
    public Guid Id; PolicyLineOfBusiness Lob; string LobLabel;
    public string Name; string FormulaKey; PlanStatus Status;
    public int? ActiveVersionNumber; DateOnly? ActiveEffectiveDate; Guid? ActiveVersionId;
    public int VersionCount;
    public int AssignedCarrierCount;
}
```

**Authorization:** `[Authorize(Roles = "Admin")]` — reuse existing `Admin` role rather than creating `RatingAdmin` until 4E. (We'll harden this in 4D.)

**Frontend — new page `frontend/src/pages/admin/AdminRatingPage.tsx` at `/admin/rating`**
- Add a sidebar nav entry under "Admin" (gated by `roles.includes('Admin')`).
- Card grid grouped by LOB. Each card shows: plan name, formula key, active version number + effective date, "View" link to plan detail.
- Empty state per LOB: "No rating plan for {LOB}. Create one via a seed migration."

**Done when:** Admin can navigate to `/admin/rating` and see "IM v1 — Inland Marine, Active v1, Effective 2026-01-01, 1 carrier assigned."

---

### 4C — Plan Detail + Version Timeline + Factor Viewer + Promote

**Why next:** Once admins can see plans, they need to inspect versions and promote drafts. This is read-only-plus-promote — still no factor editing.

**Backend**

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/v1/rating-plans/{id}` | Plan + all versions + summary of who's assigned. |
| `GET` | `/api/v1/rating-plan-versions/{id}` | Version metadata (effective dates, status, schedule bounds, min premium, notes, promoted-by). |
| `GET` | `/api/v1/rating-plan-versions/{id}/factors` | All factor tables for this version, with rows. Returned grouped by table code, with dimension keys + factors. Read-only. |
| `GET` | `/api/v1/rating-plan-versions/{id}/eligibility-rules` | All eligibility rules for this version, joined with equipment-type names. |
| `POST` | `/api/v1/rating-plan-versions/{id}/promote` | Activate a Draft version. Side effects: prior Active version's `ExpirationDate = newVersion.EffectiveDate.AddDays(-1)`; new version gets `Status = Active`, `PromotedAt = now`, `PromotedById = currentUser`. Returns 409 if not Draft, or if EffectiveDate is in the past. |
| `POST` | `/api/v1/rating-plan-versions/{id}/retire` | Force a version to Retired. Returns 409 if any **bound** quote references it. |

**Validations on promote:**
- Version is `Draft`.
- Version has at least one factor table with at least one row (basic sanity).
- `EffectiveDate >= today`. (We don't promote into the past.)
- No other version of the same plan has `Status = Active` AND a later `EffectiveDate` than this one (no time-travel conflicts).

**Frontend — new pages**

**`AdminRatingPlanDetailPage.tsx`** at `/admin/rating/plans/:planId`
- Header: Plan name, LOB, formula key, status badge.
- **Versions timeline**: vertical list of versions, newest first.
  - Each version card: `v{N}` badge, status (Draft/Active/Retired), effective range (e.g. "2026-01-01 onward" or "2025-01-01 to 2025-12-31"), notes, promoted-by-and-when (if active), assigned carriers count, "View" / "Promote" / "Retire" buttons.
- **Carriers tab** (sub-section below versions): all `CarrierRatingAssignment` rows pointing at any version of this plan. Each row links to that carrier's detail page.

**`AdminRatingPlanVersionPage.tsx`** at `/admin/rating/versions/:versionId`
- Header: plan name, version number, status, effective range.
- **Tabs:**
  1. **Schedule & Limits** — read-only display of `ScheduleMin`, `ScheduleMax`, `MinimumPremium`, `Notes`.
  2. **Factor Tables** — one collapsible per table code (`BASE_RATE`, `DEDUCTIBLE_FACTOR`, `TERRITORY_MOD`, etc.). Renders as a sortable table or as a matrix when there are exactly 2 dimensions. Search/filter input above each table.
  3. **Eligibility Rules** — list of `{equipment_type, accepted}` rows.
  4. **Audit** — promoted-at, promoted-by, retired-at, retired-by, notes log.
- **Promote button** (top-right) — only visible when status = Draft. Confirms with a dialog: "Promote v{N} to Active for {plan_name}? This will retire v{prior} effective {newEffective - 1}."

**Done when:** Admin can click into a plan, see all versions, drill into any version's factor tables, and (when a Draft exists) promote it to Active with proper retirement of the prior Active.

---

### 4D — Maker/Checker Promote Enforcement

**Why next:** Once factor editing exists in 4E, the same admin who edits a draft must not be able to promote it. We add the policy now so it's already in place when the editor lands.

**Data model changes (small migration):**
- `RatingPlanVersion.CreatedById` (Guid, nullable on existing rows) — who created/edited the draft.
- `RatingPlanVersion.LastEditedById` (Guid, nullable) — touched on every factor table mutation.

**Backend changes:**
- The `/promote` endpoint validates: `currentUserId != version.CreatedById` AND `currentUserId != version.LastEditedById`. Returns 403 with code `MAKER_CHECKER` and a message naming the user who edited.
- (Open question for team) Allow override by users with a future `RatingAdminElevated` permission? Recommend **no** — keep maker/checker absolute.

**Frontend:**
- Promote button shows a tooltip "You edited this draft — a different admin must promote." when the policy would block.

**Done when:** I can edit a draft, then attempt to promote it as the same user, and the API rejects with `MAKER_CHECKER`.

---

### 4E — Draft Factor Editor + Excel Paste/Import + Impact Preview ⚠️ biggest piece

**Why last:** Highest-leverage feature ("update factors without code") but also biggest risk surface — a wrong-keyed factor changes premium for every new quote. Maker/checker (4D) is the safety net.

**Backend**

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/v1/rating-plans/{planId}/versions` | Create a new Draft version, optionally cloning all factor tables from a source version. Body: `{ effectiveDate, sourceVersionId?, notes? }`. |
| `PUT` | `/api/v1/rating-plan-versions/{id}` | Update a Draft's metadata (effective date, notes, schedule bounds, minimum premium). 409 if not Draft. |
| `PUT` | `/api/v1/rating-plan-versions/{id}/factors/{tableCode}` | Bulk-replace one factor table's rows. Body: `{ rows: [{ dimensionValues: {...}, factor: number }, ...] }`. Server validates: dimension names match the table's `DimensionNames`, factors are positive numbers, dimension values are non-empty strings, no duplicate dimension-key combinations. |
| `POST` | `/api/v1/rating-plan-versions/{id}/import-csv` | Multi-table CSV upload (one CSV per factor table; matched by table code). Same validation as PUT factors. Returns a per-table diff report. |
| `POST` | `/api/v1/rating-plan-versions/{id}/preview-impact` | Run the engine against every in-force quote (and optionally bound policies in renewal window) using both the current Active version and this Draft. Body: `{ scope: "QuotesOnly" \| "QuotesAndRenewals", lookbackDays: int }`. Returns: summary, distribution buckets, top movers. |

**Impact preview DTO**
```csharp
public class RatingImpactPreviewDto {
    public int QuotesEvaluated; int QuotesChanged;
    public decimal TotalPremiumDelta; decimal TotalPremiumDeltaPercent;
    public List<DistributionBucketDto> Distribution;   // ranges like [-20%..-10%, -10%..0%, ...]
    public List<TopMoverDto> TopMovers;                // top 25 by abs %
    public List<DimensionImpactDto> ByDimension;       // avg delta per equipment_type, per territory
    public DateTime ComputedAt; Guid ComputedById; string ComputedByName;
    // Result is cached on the version so re-opening the page doesn't re-run.
}
```

**New table:** `rating_plan_version_impact_previews` — one row per (versionId, scope, lookbackDays, computedAt). Stored as denormalized JSON to avoid schema churn as the preview shape evolves.

**Frontend — extend `AdminRatingPlanVersionPage.tsx`**

Add a "Edit" mode on a Draft version (button at top-right; only visible when status = Draft).

**Edit mode UI per factor table:**
- Toggle: **Grid edit** (default) | **Paste import**.
- **Grid edit**: react-table-style editable grid. Each row's cells are dimension values (read-only or selectable) + factor (editable number). Add row / remove row buttons. Inline validation (factor > 0, dimension values non-empty). "Discard changes" button clears local state.
- **Paste import**:
  - Textarea for pasting tab- or comma-separated values from Excel.
  - Parser detects header row (must match `DimensionNames + ["factor"]`).
  - **Diff view** before save: green for added rows, yellow for changed factors (with old → new), red for removed rows. User confirms before save.

**Impact preview button** (top of edit mode):
- Disabled if there are unsaved factor changes.
- Click → modal: "Run impact preview against current quotes?" with scope selector. Loading state shows progress (it might take a few seconds).
- Result cached on the version; reopening the page shows the cached result with a "Recompute" button.

**Promote gate:** the Promote button is hidden until impact preview has been run on the current factor state. Tooltip explains why.

**Permissions:**
- Add a new permission `Permissions.RatingAdmin` (separate from generic `Admin`). Seed it on the Admin role for the migration.
- `[Authorize(Roles = "Admin")]` on all 4E endpoints; further authorization via the permission inside the controller.

**Done when:**
1. I can clone Active v1 to Draft v2.
2. Edit one factor in the Territory table (e.g. Territory 4 modifier from 1.10 to 1.15).
3. Run impact preview — see histogram showing all quotes with Territory 4 equipment shifted up ~4.5%.
4. Promote v2. Quotes from this point forward use 1.15.
5. Existing bound quotes are unchanged (their snapshot is locked).

---

## Phase 5 — Shadow Rate Cutover

**Goal:** Run the engine in production alongside the spreadsheet for a sign-off period before fully replacing it.

**Approach:**
1. **Feature flag** `Rating.ShadowMode` (env var or config) — when on, the engine still rates but the result is NOT stamped on the quote. Instead, the engine writes its result to a `shadow_rating_results` table, and the underwriter still types the spreadsheet number into `Quote.PremiumAmount`.
2. **Dual-rate endpoint** `POST /api/v1/quotes/{id}/shadow-rate` — only runs in ShadowMode; same logic as `/rate` but writes to the shadow table.
3. **Daily report job** — runs at 23:00 each day via a scheduled task, compares every shadow result to the actual `Quote.PremiumAmount` for quotes created that day, emails the team a CSV of any deltas > 0.5%.
4. **Dashboard** at `/admin/rating/shadow` — table of last 30 days of shadow results with the delta column highlighted for outliers.
5. **Sign-off**: 5 consecutive days of zero deltas (or all deltas explained) flips `Rating.ShadowMode = false` and the engine becomes the source of truth.

**Cutover checklist:**
- [ ] Confirm CarrierRatingAssignment exists for every carrier currently quoting IM.
- [ ] Confirm every active equipment item on open quotes has `EquipmentTypeId` and `Value`.
- [ ] Run impact preview against the entire open book — no surprises.
- [ ] Communicate the change date to UWs.
- [ ] Flip the flag.
- [ ] Keep shadow mode running for 30 days post-cutover for safety.

**Done when:** spreadsheet is officially deprecated, archived to SharePoint with a pointer noted in the rating plan version's `Notes`.

---

## Phase 6 — Second LOB Rater

**Recommendation:** Build **Auto Liability** next (high premium volume per quote, well-understood by the team), then APD (smaller, simpler), then GL (most complex due to class-code rating and exposure-based premium).

For each LOB:

1. **Workbook intake** — get the live Excel rater workbook from the actuarial team.
2. **Manual CSV extraction** of every factor table → `backend/seed/rating/{lob}/v1/`.
3. **Domain check** — does the formula need new dimensions/concepts the IM model doesn't have? Examples:
   - AL: needs vehicle inputs (radius, GVW, garaging ZIP/territory) instead of equipment.
   - APD: similar to IM but on vehicles, with stated value or ACV.
   - GL: class codes, payroll/sales exposure, rating-base unit (per $100 of payroll, etc.).
4. **New formula key** (e.g. `AL_v1`, `APD_v1`, `GL_v1`) registered with the engine. Each formula is a separate C# implementation that consumes the same factor table infrastructure but applies its own math.
5. **Seed migration** for the v1 plan + factor tables + eligibility rules.
6. **Parity test fixtures** (Phase 7) added for the new LOB.
7. **Frontend submission editor** — for AL/APD this means the existing `SubmissionVehicle` editor needs the same rating-input pass we did for equipment (territory, radius confirmation, etc.). GL needs a new classification-rating editor.
8. **Quote rating panel** — already generic; just needs the per-formula presentation (column names, etc.) to be configurable per LOB.

**Estimated effort per LOB:** APD ≈ 2 days; AL ≈ 3 days; GL ≈ 5 days (class-code complexity).

---

## Phase 7 — Polish

These are small but real items that the engine isn't truly "done" without.

### 7A — Excel parity test harness *(was Phase 2 leftover)*
**Status:** Done in `backend/tests/SIMS.Application.Rating.Tests` with 24 IM v1 fixture folders. Keep adding fixture coverage for each new LOB.

- New project `tests/SIMS.Application.Rating.Tests/`.
- Folder `Fixtures/{lob}/{name}/` containing `inputs.json` + `expected.json`.
- xUnit test `RatingFixturesTests` that runs every fixture against an in-memory engine + seeded factor tables.
- 20+ fixtures for IM v1 (one per equipment type × age band × deductible tier sample).
- CI gate: any fixture failure blocks merge.

### 7B — Per-role schedule modifier authority
**Status:** Still open. The main 5.17 roadmap now assigns this to Phase 7A, with the option to enforce through the Phase 7 authority matrix.

- Three permissions: `RatingMod15` (UW: ±15%), `RatingMod25` (Senior UW: ±25%), `RatingModFull` (Admin: full filed range).
- Engine validates the modifier against the user's effective cap (computed from their roles' permissions), not just the plan bounds.
- UI shows the user their effective range above the modifier input.

### 7C — Rating worksheet PDF
**Status:** Still open. Keep this tied to immutable rating snapshots and the document-artifact backlog if it is not delivered in Phase 7A.

- New endpoint `GET /api/v1/quotes/{id}/rating-worksheet.pdf` — generates a PDF showing the snapshot's per-line breakdown, factors, modifier, total. Useful for explaining premium to brokers.
- Reuses the existing `DocumentGenerationService` infrastructure.

### 7D — Renewal rating logic
**Status:** Still open. The default recommendation is renewal-effective-date rates until SMM confirms otherwise.

- When a quote is created as a renewal (linked to a prior bound quote), confirm the policy: do we use the rates active at the renewal effective date (typical) or the bound version of the prior policy (rare)?
- Default to **renewal effective date** rates.
- UI flag on the renewal screen: "Using rates effective {date}, version v{N}".

### 7E — Endorsement rating logic
**Status:** Still open. The default recommendation is bound-policy-version rates plus pro-rata calculation for mid-term changes.

- Mid-term equipment add: a re-rate against the **bound** version of the policy, producing a new snapshot tagged as endorsement-only.
- New `QuoteRatingSnapshot` field: `IsEndorsement bool`, `EndorsementOf Guid?` pointing at the prior snapshot.
- Pro-rata calculation against remaining policy term.

---

## Open questions for the team (still)

1. **Maker/checker absolute, or admin override?** Recommend absolute.
2. **Filed schedule rating bounds** — what are SMM's actual filings? Need real numbers for `ScheduleMin`/`ScheduleMax` per plan, not the placeholder 0.5–1.5.
3. **Minimum premium per program** — is there a floor? IM v1 currently has none seeded.
4. **Renewal rate-version policy** — recommend renewal-effective-date rates; confirm.
5. **Endorsement rate-version policy** — recommend bound-version rates; confirm.
6. **Cancellation calculation** — pro-rata or short-rate? Out of rating engine scope but adjacent.
7. **AL/APD/GL workbook intake** — when is the actuarial team ready to hand them off?

---

## Suggested execution order

1. **Now:** 4A (carrier assignment UI) — unblocks every non-Beazley carrier.
2. **Next:** 4B + 4C (plans index, version detail, factor viewer, promote) — gives admins visibility + the safe "promote a draft someone else built" path.
3. **Already covered:** 7A (parity harness), 4D (maker/checker), and 4E safety gates/factor edit/impact preview now exist in the codebase.
4. **Then:** Phase 5 (shadow cutover).
5. **Then:** Phase 6 (second LOB — APD recommended first, easiest shape).
6. **Then:** 7B–7E in any order, unless per-role schedule modifier authority is pulled into the main Phase 7 authority matrix first.

---

## Risks (updated)

| Risk | Mitigation |
|---|---|
| Admin edits a factor wrong → mis-rate | Maker/checker (4D) + parity harness (7A) + impact preview (4E) + shadow mode (5). |
| Team relies on engine but it differs from spreadsheet | Shadow cutover (5) catches deltas before retiring spreadsheet. |
| New LOB formula has shape the model can't express | Each formula is a C# class with full flexibility; only the factor tables share schema. |
| Renewal at prior rates by accident | Default to current rates + UI badge showing which version is being used. |
| Carrier added without rating assignment | Engine already returns `NO_RATING_PLAN` cleanly. UI in 4A makes it a 30-second fix instead of a code deploy. |

---

## End-state acceptance

The rating engine is "done done" when:
- ✅ All four active LOBs (GL, IM, AL, APD) have a v1 plan in the system.
- ✅ Every carrier writing those LOBs has an active assignment.
- ✅ Every new quote rates through the engine without spreadsheet involvement.
- ✅ An admin can edit a factor, run impact preview, get maker-checker promote, and the change is live for new quotes within 5 minutes — no developer touched the codebase.
- ✅ Bound policies' rating snapshots are immutable and reproducible 1+ year later.
- ✅ Excel parity harness runs in CI on every commit.
- ✅ Shadow mode is off; spreadsheet is archived.
