# SIMS Rating Engine — Implementation Plan

**Status:** Draft for team review
**Owner:** Jeremiah O'Donovan
**Last updated:** 2026-05-03

---

## 1. Goal

Replace the per-program Excel raters (today: `Inland Marine Rater 2.2025 v11.xlsx` and equivalents) with a SIMS-native rating engine that produces premium for every quote SMM issues. Factors must be maintainable by an admin user through the UI — updating rates must never require a code deployment.

## 2. Scope

### Lines of business (active)
SMM writes four LOBs and does not bind package policies. Each carrier × LOB pair has its own rater.

| LOB | Enum value | Notes |
|---|---|---|
| General Liability | `GeneralLiability = 1` | Already in enum |
| Inland Marine | `InlandMarine = 10` | **Newly added** |
| Auto Liability | `AutoLiability = 11` | **Newly added** (split from CommercialAuto) |
| Auto Physical Damage | `AutoPhysicalDamage = 12` | **Newly added** (split from CommercialAuto) |

Deprecated LOBs (`Property`, `CommercialAuto`, `BusinessOwners`, `WorkersCompensation`, `ProfessionalLiability`, `Umbrella`, `Cyber`, `ExcessLiability`) remain in the enum for historical record integrity but are not selectable for new quotes. Frontend pickers use the new `ACTIVE_LOBS` constant; `ALL_LOBS` is reserved for displaying historical data.

### Carriers and programs
Each (`CarrierId`, `LineOfBusiness`) tuple has at most one **rating plan**. The first plan delivered will be Inland Marine for the carrier currently writing equipment business through SMM. Adding a new carrier or LOB is a configuration task, not a code task.

### Out of scope (explicit)
- Package / multi-LOB policies.
- Reinsurance treaty math (handled in actuarial / SharePoint workflow, not SIMS).
- Loss-rated or experience-rated workers comp (we don't write WC).
- Schedule rating filings — we apply schedule modifiers within filed bounds; we do not draft filings in SIMS.

---

## 3. Architecture overview

```
┌──────────────────────────────────────────────────────────────────────────┐
│                       SIMS.API  (REST controllers)                       │
│   POST /api/quotes/{id}/rate     POST /api/admin/rating/preview-impact   │
│   GET  /api/admin/rating/plans   POST /api/admin/rating/plans/{id}/...   │
└──────────────┬─────────────────────────────────────┬─────────────────────┘
               │                                     │
┌──────────────▼─────────────────────┐  ┌────────────▼────────────────────┐
│      RatingEngineService           │  │   RatingPlanAdminService        │
│  - LookupFactors(planVersion, in)  │  │  - CRUD draft factor tables     │
│  - ApplyFormula(factors)           │  │  - Promote draft → active       │
│  - PersistSnapshot(quoteId)        │  │  - Run impact preview on book   │
└──────────────┬─────────────────────┘  └────────────┬────────────────────┘
               │                                     │
               └────────────┬────────────────────────┘
                            ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                         SIMS.Infrastructure                              │
│   RatingPlan / RatingPlanVersion / FactorTable / FactorRow / etc.        │
│   QuoteRatingSnapshot (line-by-line, immutable once bound)               │
└──────────────────────────────────────────────────────────────────────────┘
```

### Key design choices

1. **Plan + Version + Factor split.** A *plan* is the contract (carrier, LOB, formula key, expected inputs). A *version* is a dated, frozen snapshot of all factor tables for that plan. Quotes are rated against the version that was active on `Quote.EffectiveDate`.
2. **Formula by key, not by SQL/string interpreter.** Each plan declares a `FormulaKey` (e.g. `"IM_v1"`). The engine dispatches to a registered C# implementation. We do **not** ship a free-form formula evaluator — too risky, too easy to mis-rate.
3. **Factor tables are generic key-value-by-dimension.** Every factor row has up to N dimension values + a numeric factor. This lets us model 1-D lookups (Territory), 2-D matrices (Base Rate by class × age), and 3-D matrices (Deductible by class × deductible amount) using the same schema.
4. **Snapshot, don't recompute.** When a quote is bound, the rating result and every factor used is written to `QuoteRatingSnapshot` and never modified. Re-rating produces a new version row; the old one is preserved.

---

## 4. Domain model

### 4.1 New entities (in `SIMS.Domain.Entities.Rating`)

```csharp
RatingPlan
  Id                     Guid
  CarrierId              Guid          // FK
  LineOfBusiness         PolicyLineOfBusiness
  Name                   string        // "IM Logging Equipment — Carrier X"
  FormulaKey             string        // dispatch key, e.g. "IM_v1"
  Status                 PlanStatus    // Draft | Active | Retired
  // unique index: (CarrierId, LineOfBusiness) where Status = Active

RatingPlanVersion
  Id                     Guid
  RatingPlanId           Guid          // FK
  VersionNumber          int           // monotonic per plan
  EffectiveDate          DateOnly
  ExpirationDate         DateOnly?     // null = open-ended
  Status                 VersionStatus // Draft | Active | Retired
  PromotedAt             DateTime?
  PromotedById           Guid?
  Notes                  string?       // changelog note

FactorTable
  Id                     Guid
  RatingPlanVersionId    Guid          // FK
  Code                   string        // "BaseRate", "DeductibleFactor", "TerritoryMod"
  DimensionNames         string[]      // ["EquipmentTypeId", "AgeBand"]
  ValueSemantics         FactorKind    // Multiplier | RatePer100 | FlatAmount

FactorRow
  Id                     Guid
  FactorTableId          Guid          // FK
  DimensionValues        jsonb         // {"EquipmentTypeId": 1, "AgeBand": "1-3"}
  Factor                 decimal(18,6)
  // gin index on DimensionValues

EligibilityRule
  Id                     Guid
  RatingPlanVersionId    Guid          // FK
  Code                   string        // "AcceptedClass", "ValuationBasis"
  Expression             jsonb         // structured rule, not free text
  // examples: {"if": "ClassId in [1..12]", "then": "ACCEPTED"}

QuoteRatingSnapshot
  Id                     Guid
  QuoteId                Guid          // FK; one snapshot per (Quote, RatingRun)
  RatingPlanVersionId    Guid          // FK; pinned at rate time
  RatedAt                DateTime
  RatedById              Guid
  ManualPremium          decimal       // before fees/taxes
  ScheduleModifier       decimal       // 1.00 = no mod, 0.85 = 15% credit
  ScheduleModifierReason string?
  TotalPremium           decimal       // ManualPremium × ScheduleModifier
  IsBoundSnapshot        bool          // true once Quote is bound; immutable after

QuoteRatingLine
  Id                     Guid
  QuoteRatingSnapshotId  Guid
  ExposureRef            string        // "SubmissionEquipment:abc123"
  Inputs                 jsonb         // copy of inputs used
  FactorsApplied         jsonb         // {BaseRate:1.14, AgeFactor:1.71, ...}
  LinePremium            decimal
```

### 4.2 Touch points to existing entities

- **`Quote`** — no new columns, but `PremiumAmount` is now derived from the latest non-bound `QuoteRatingSnapshot`. At bind, the snapshot is locked (`IsBoundSnapshot = true`).
- **`SubmissionEquipment`** — no new columns. `Year`, `Value`, optional `EquipmentTypeId` (FK to lookup) and `TerritoryCode` (FK to lookup) are the rating inputs.
- **New lookup tables** seeded once: `EquipmentType`, `Territory`. These are the *dimension members* the factor tables reference.

### 4.3 Versioning behavior

- Activating a new version automatically sets `ExpirationDate = newVersion.EffectiveDate - 1 day` on the previously active version.
- The engine's lookup is `WHERE PlanId = ? AND Status = Active AND EffectiveDate <= quoteEffectiveDate AND (ExpirationDate IS NULL OR ExpirationDate >= quoteEffectiveDate)`.
- Retiring a version is reversible until any quote has been bound against it.

---

## 5. Calculation engine

### 5.1 Service contract

```csharp
public interface IRatingEngine
{
    Task<RateQuoteResult> RateAsync(Guid quoteId, CancellationToken ct);
    Task<RateQuoteResult> PreviewAsync(RatingRequest request, CancellationToken ct);
}

public record RatingRequest(
    Guid CarrierId,
    PolicyLineOfBusiness Lob,
    DateOnly EffectiveDate,
    IReadOnlyList<RatingExposure> Exposures,
    decimal ScheduleModifier = 1.0m,
    string? ScheduleModifierReason = null);
```

### 5.2 Inland Marine formula (`IM_v1`)

For each equipment item:
```
LinePremium = (StatedAmount / 100)
            × BaseRate[EquipmentTypeId, AgeBand]
            × TerritoryMod[TerritoryCode]
            × DeductibleFactor[EquipmentTypeId, DeductibleTier]
            × (any flat coverage modifiers)
```

Quote-level:
```
ManualPremium    = round(Σ LinePremium, 2)
TotalPremium     = round(ManualPremium × ScheduleModifier, 2)
                   subject to MinimumPremium[Plan]
```

Rounding: `MidpointRounding.ToEven` per Excel default; verified against fixtures.

`AgeBand` derived from `Quote.EffectiveDate.Year - Equipment.Year`, mapped via a lookup (`1-3`, `4-7`, `8-11`, `12+`).

### 5.3 GL / AL / APD formulas

Out of scope for V1 deliverable but the same engine runs them — they each register a `FormulaKey` (`GL_v1`, `AL_v1`, `APD_v1`) and declare which factor tables they consume. Domain model does not change.

### 5.4 Schedule rating / IRPM

- Per-plan filed bounds stored in `RatingPlanVersion.ScheduleMin`/`ScheduleMax` (e.g. 0.75–1.25).
- Authority limits per role: Underwriter ±15%, Senior UW ±25%, Admin ±filed cap. Enforced server-side in the engine, not the UI.
- Reason text required on any modifier ≠ 1.00. Stored on the snapshot.
- IRPM credits/debits visible on quote summary and document templates.

### 5.5 Eligibility rules

Class Lookup–style flags (`ACCEPTED/REJECTED`, `ACV/RCV`, `BEAZLEY/BRACE`) are not multipliers. They run as a **pre-rating gate**:
- A `REJECTED` class blocks the engine from producing a premium and surfaces a referral message.
- `ACV` vs `RCV` and similar selectors are inputs to the formula (a coverage choice), captured on the quote.

Rules are versioned with the rating plan version so historical eligibility decisions remain reproducible.

---

## 6. API surface

### Quote-side
| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/quotes/{id}/rate` | Run engine, write snapshot, update `Quote.PremiumAmount`. Idempotent until bind. |
| `GET` | `/api/quotes/{id}/rating-detail` | Retrieve latest snapshot + line breakdown for display / docs. |
| `POST` | `/api/quotes/{id}/schedule-modifier` | Apply IRPM with reason; re-rates. |

Bind flow already exists in `QuoteService`; it gains one step: `snapshot.IsBoundSnapshot = true`. Endorsements that change exposures call `/rate` against the **bound version** (not the current active version) and produce a new snapshot tagged as the endorsement.

### Admin-side
| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/admin/rating/plans` | List plans (carrier × LOB) with active version. |
| `POST` | `/api/admin/rating/plans` | Create a new plan. |
| `POST` | `/api/admin/rating/plans/{id}/versions` | Open a draft version (copies from current active). |
| `PUT` | `/api/admin/rating/versions/{id}/factors/{tableCode}` | Bulk-replace a factor table. |
| `POST` | `/api/admin/rating/versions/{id}/import-csv` | Paste/upload one or more factor tables as CSV. |
| `POST` | `/api/admin/rating/versions/{id}/preview-impact` | Run the impact preview (see §8). |
| `POST` | `/api/admin/rating/versions/{id}/promote` | Activate a draft (maker/checker rules apply). |
| `POST` | `/api/admin/rating/versions/{id}/retire` | Retire a version (only if no bound quotes reference it). |

Authorization: a new permission `RatingAdmin` (separate from generic `Admin`) is required for any `/api/admin/rating/*` endpoint.

---

## 7. Admin UI

A new top-level Admin section: **Rating**.

### 7.1 Plans index — `/admin/rating`
Table of `Carrier × LOB` cells. Each cell shows: active version number, effective date, last edit, and a status badge. Empty cells show a **"Create plan"** action. Clicking a cell opens the plan detail.

### 7.2 Plan detail — `/admin/rating/plans/:id`
- **Versions panel**: timeline of all versions (draft, active, retired). One-click to clone the active version into a new draft.
- **Active version inspector**: read-only view of every factor table with full search/filter.
- **Audit log**: who promoted what, when, with the changelog note.

### 7.3 Draft version editor — `/admin/rating/versions/:id`
- One tab per factor table (Base Rate, Deductible, Territory, Eligibility Rules).
- **Two editing modes per tab:**
  - **Grid edit** — inline edit of individual rows; useful for one-off corrections.
  - **Paste/import** — paste a block from Excel or upload a CSV; the UI shows a row-level diff (added / changed / removed) before save.
- **Validation**:
  - All required dimensions present.
  - Numeric factors within sanity bounds (0 < f < 100).
  - No dangling rows referencing missing dimension members.
- **Header**: effective date picker, changelog note (required), save / discard buttons.

### 7.4 Impact preview — modal launched from draft editor
**This is the rate-change preview the team specifically asked for.**

When the admin clicks **"Preview impact"**:

1. The server pulls every in-force quote/policy where `EffectiveDate >= today` (configurable lookback for already-bound business is also offered).
2. For each, the engine runs once against the current active version and once against the draft version, using the snapshot inputs.
3. The UI shows:
   - **Summary card**: total premium delta in $ and %; number of quotes that move up vs down; max single-quote delta.
   - **Distribution histogram**: bucket the per-quote premium deltas (e.g. -20%, -10%, 0%, +10%, +20%, +20%+) so an outlier-heavy change is visually obvious.
   - **Table**: top 25 movers (by absolute %), with quote number, insured, current premium, new premium, delta. Click-through to the quote.
   - **By dimension**: average delta per equipment type, per territory, per deductible tier. Quickly answers "did the territory 4 change have the effect I expected?"
4. Preview results are cached on the draft version so they don't have to be re-run on every visit; running again is a single button.
5. Promote is gated: if the impact preview hasn't been run against this draft, the promote button is disabled with a tooltip.

### 7.5 Promote (maker / checker)
- The admin who edited the draft cannot promote it (enforced server-side).
- Promote dialog requires the second admin to type the plan name and the changelog note.
- On promote: previous active version's `ExpirationDate` is set to `newVersion.EffectiveDate - 1 day`; the draft becomes `Active`.

---

## 8. Tax, fees, commission

The rating engine produces **manual premium and total premium (manual × IRPM)** only. The existing `FeeCalculationService` and commission stamping in `QuoteService` continue to handle:
- Surplus lines tax, stamping fees, state surcharges (`Quote.TaxesAndFees`).
- Carrier / SMM / agent commission split.
- Filing-state logic (`IsFilingState`).

The plan does not modify those services. The contract is: rating engine sets `Quote.PremiumAmount`; the existing pipeline computes everything downstream.

---

## 9. Excel parity test harness

A first-class artifact, not a one-time check.

- `tests/SIMS.Application.Tests/Rating/Fixtures/` — checked-in fixture pairs:
  ```
  im_skidder_100k_terr4_2500ded.json     ← inputs
  im_skidder_100k_terr4_2500ded.expected ← Excel-produced premium
  ```
- `RatingEngineFixtureTests` — runs every fixture on every CI build. A failure blocks merge.
- New fixtures are required when:
  - A new formula key is introduced.
  - An existing formula's behavior is intentionally changed (the fixture is updated *and* the changelog note explains why).
- For paranoia: a `/api/admin/rating/run-fixture/{name}` endpoint that runs a fixture against the live database (validates seed data, not just code).

A separate **shadow-rate** mode (off by default, controlled by feature flag) re-runs every quote through both the engine and the legacy spreadsheet during the cutover period and logs deltas for review. Used for the first 30 days of production use.

---

## 10. Excel data extraction (one-time seed)

The factor tables in the rater workbooks have merged headers, sentinel rows, and matrix layouts that defeat naive parsers. The agreed approach:

1. **Manual extraction**: each factor table is exported to a clean CSV by hand (or with a tightly-scoped Python script per workbook). CSVs are checked into `backend/seed/rating/` keyed by carrier + LOB + plan version.
2. **Seed via migration**: an EF Core migration reads the CSVs and inserts them as the v1 active version of each plan.
3. **CSVs are the source of truth for v1**, not the Excel files. After cutover, all changes happen in the Admin UI.
4. The Excel workbooks are archived in SharePoint with a pointer noted on the `RatingPlanVersion.Notes`.

---

## 11. Phasing and acceptance criteria

### Phase 0 — Cleanup (1 day)
- ☐ Add `InlandMarine`, `AutoLiability`, `AutoPhysicalDamage` to `PolicyLineOfBusiness` (**done**).
- ☐ Update frontend pickers to use `ACTIVE_LOBS` (in progress — type/constant added; component updates pending).
- ☐ **Decision required:** how to treat existing data with deprecated LOB values. Options: (a) leave alone and never display, (b) re-classify each record manually, (c) migrate all `CommercialAuto` records to AL+APD pairs. Recommend (a) since deprecated business is small and historical.
- ☐ Update `GeminiExtractionService` LOB list and prompts to emit `AutoLiability` / `AutoPhysicalDamage` instead of `CommercialAuto`.
- **Done when:** new submissions only show 4 LOBs in pickers; AI extraction produces only the 4 active LOBs; deprecated values still render correctly on historical records.

### Phase 1 — Domain + versioning + IM seed (3–4 days)
- ☐ All entities from §4 created with EF configurations and migration.
- ☐ `EquipmentType`, `Territory` lookup tables seeded.
- ☐ Inland Marine v1 plan + factor tables seeded from CSV.
- **Done when:** the database holds a complete IM v1 plan and every factor row in the Excel rater is reproduced exactly.

### Phase 2 — Engine + parity harness (3–4 days)
- ☐ `RatingEngine` service implements `IM_v1`.
- ☐ `QuoteRatingSnapshot` write path on `/api/quotes/{id}/rate`.
- ☐ Fixture harness with at least 20 fixtures spanning all equipment types, age bands, deductible tiers, and territories. 100% match against Excel.
- **Done when:** every fixture passes; running an Inland Marine quote in the API produces the exact same premium as the Excel rater.

### Phase 3 — Wire into Quote / Submission flow (2 days)
- ☐ Frontend quote screen: when LOB = Inland Marine, premium fields are populated from the engine and not user-editable.
- ☐ Eligibility rules block rating when triggered, with a referral message.
- ☐ Schedule modifier UI with role-based bounds.
- ☐ Bind locks the snapshot.
- **Done when:** a new IM quote can be created end-to-end without touching the spreadsheet.

### Phase 4 — Admin UI (4–5 days)
- ☐ Plans index, plan detail, draft editor (grid + paste-import).
- ☐ Impact preview (§7.4).
- ☐ Maker / checker promote flow.
- ☐ `RatingAdmin` permission added; only that role sees the menu.
- **Done when:** Jeremiah can change a Territory factor, run impact preview, promote with a second approver, and a fresh quote uses the new factor — no developer involvement.

### Phase 5 — Shadow-rate cutover (1 week soak)
- ☐ Feature flag enables shadow rating against the spreadsheet.
- ☐ Daily report of any discrepancy.
- ☐ Sign-off when 5 consecutive days show 0 discrepancies.

### Phase 6 — Add second LOB (effort scales with formula complexity)
- ☐ Pick GL or AL next based on premium volume.
- ☐ Repeat Phase 1–3 for that plan only. Phase 4 (UI) is reused.

---

## 12. Open questions for the team

1. **Deprecated LOB cleanup** (Phase 0): keep, re-classify, or migrate? See §11.
2. **Schedule rating authority** — what are the actual filed bounds and per-role authority limits today? Need them to seed.
3. **Minimum premium** — is there a minimum per program, per LOB, or both?
4. **Endorsement rating policy** — when an item is added mid-term, do we use the bound version's factors (consistency) or the currently-active version (current pricing)? My recommendation: bound version, always.
5. **Who can be a `RatingAdmin`?** Recommend Jeremiah + one other, separate from the generic `Admin` role so a routine admin doesn't accidentally edit factors.
6. **Renewal rating** — does a renewal use the rates active at the renewal effective date (typical) or at the original bind date? Typical answer: renewal effective date.
7. **Carrier-specific raters in the same LOB** — confirmed the model is one rating plan per (Carrier, LOB)? If two carriers ever share a rater, we'd model it as two plans pointing at the same factor tables, but that's a future-us problem.

---

## 13. Risks

| Risk | Mitigation |
|---|---|
| Engine produces wrong premium → rating error / E&O exposure | Excel parity harness in CI; shadow-rate cutover with discrepancy report. |
| Admin edits cause silent rate change | Maker/checker promote; required impact preview; changelog note required; permission gated; full audit log. |
| Historical re-rate not reproducible | Versioning + snapshotting; bound snapshot is immutable. |
| Excel parsing during seed is wrong | Manual CSV extraction with row-level review; CSVs checked into git as source of truth. |
| One-off carrier/LOB needed urgently | Plan supports new (Carrier, LOB) by config alone unless it needs a new formula key; new formula key is ~1 day of work. |

---

## 14. Out-of-scope (future)

- Rating worksheet PDF generated from the snapshot (nice-to-have for broker explanations).
- Multi-currency.
- Real-time rate-change notifications to bound policies (regulatory implications).
- Self-service broker rating portal.
