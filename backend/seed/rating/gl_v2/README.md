# GL_v2 Rating Seed Data — Longleaf/Brace GL (multi-state)

Source: `SMM_GL_Rater_Finalv11.xlsx` (LONDON GL folder)
Extracted: 2026-07-01 · Seeded by migration `20260701235109_GL_v2_RatingSeed`
Formula: `GlV2Formula` (SIMS.Application/Rating) — pure, reads all rate data from factor tables.

**The database is the source of truth for live rates.** The Excel workbook was the
extraction basis and remains useful only as an actuarial workspace for *structural*
changes (new formula = new `GL_v3` + fixtures). Ordinary rate changes never need it.

---

## Files

| File | Description |
|---|---|
| `gl_rates.json` | Full extraction: params (LCM 1.65, TRIA 2.5%, endorsement charges), 14 classes, ILF table, loss-cost matrices 334/336 (15 states), L&L endorsement min/pct |
| `gen_gl_v2_migration.py` | Generator that wrote the seed migration from `gl_rates.json`. Verified byte-identical to the shipped migration. |

## Factor tables seeded (per plan version)

| Code | Dimensions | Contents |
|---|---|---|
| `GL_CLASS` | class_code | description, premium basis, has_pco, P/O + P/CO ILF tier, divisor |
| `GL_LOSS_COST_334` | class_code, state | ISO Prem/Ops loss costs (missing row = "(a)" refer-to-company) |
| `GL_LOSS_COST_336` | class_code, state | ISO Prod/Comp-Ops loss costs (P/CO classes only) |
| `GL_ILF` | limit, tier | Increased-limits factors (PO_T1–T3, PCO_TA–TC) |
| `GL_PARAMS` | key | LCM, TRIA_RATE |
| `GL_LL_ENDORSEMENT` | limit, kind | Logging & Lumbering min premium + pct by limit |

AI/WOS/PNC charges are **not** here — they live in `carrier_additional_interest_rates`
(global additional-interest engine, already UI-editable).

## Rate-change runbook (interim, until the post-UAT rate workbench)

1. Edit `gl_rates.json` with the new values (or re-extract from a new workbook).
2. Scaffold a new empty migration:
   `dotnet ef migrations add GL_v2_Rates_v<N> --project src/SIMS.Infrastructure --startup-project src/SIMS.API --context ApplicationDbContext`
3. Copy `gen_gl_v2_migration.py`, point it at the new migration file, and adapt the SQL:
   insert a **new** `rating_plan_versions` row (`version_number` = next), attach factor
   tables to that version id, repoint `carrier_rating_assignments` to it.
   **Never mutate a version that has rated/bound quotes.**
4. `dotnet build` + `dotnet test`, commit to main, push — the migration runs on API startup.

Notes:
- Test fixtures (`backend/tests/SIMS.Application.Rating.Tests/Fixtures/GL_v2/rate_data.json`)
  pin the **formula logic** against the frozen v11 snapshot — live rate changes do **not**
  require fixture updates.
- Column casing is mixed per-table: rating tables are snake_case, but
  `carrier_additional_interest_rates` uses quoted PascalCase. Check
  `ApplicationDbContextModelSnapshot.cs` before writing raw SQL.

## Post-UAT plan (decided 2026-07-02)

Versioned **rate workbench** in SIMS: clone Active → Draft, grid/CSV edit factor rows,
diff vs prior version + shadow-rate recent quotes, promote with effective date
(`RatingPlanVersion` already carries Draft/Active/Retired, EffectiveDate,
PromotedAt/By, CreatedBy/LastEditedBy for exactly this workflow).
