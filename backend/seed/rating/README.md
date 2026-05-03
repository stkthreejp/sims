# IM Rating Seed Data — Nations Logging Carrier (v1)

Source: `Inland Marine Rater 2.2025 v11 583e 12 - Nations Logging.xlsx`  
Extracted: 2026-05-03  
Program year: 2025 (effective dates from 2026)

---

## Files

| File | Description |
|---|---|
| `equipment_types.csv` | 12 equipment classes (ids 1–12) |
| `territories.csv` | 7 territory ids with modifier and state list |
| `state_territory_map.csv` | State abbreviation → territory id (47 states) |
| `base_rates.csv` | Rate per $100 by equipment type × age band |
| `deductible_factors.csv` | Multiplier by equipment type × deductible tier |
| `eligibility_rules.csv` | All 12 types accepted; same model for both carriers |
| `endorsements.csv` | Policy-level flat charges and TRIA percentage |

---

## Formula (IM_v1)

Per equipment item (confirmed from Premium sheet R300C18):
```
LinePremium = ROUND(
    (StatedAmount / 100)
    × BaseRate[equipment_type_id, age_band]
    × TerritoryMod[territory_id]
    × DeductibleFactor[equipment_type_id, deductible_tier]
    × ScheduleModifier
, 0)
```

Rounding: **nearest whole dollar** (`ROUND(..., 0)` / `MidpointRounding.ToEven`).

Policy totals:
```
ManualPremium  = SUM(LinePremiums)
Endorsements   = sum of selected flat charges (see endorsements.csv)
SubTotal       = ManualPremium + Endorsements
TRIA           = IF(selected, SubTotal × 0.01, 0)
GrandTotal     = SubTotal + TRIA
```

---

## Age Band Lookup

Age = `EffectiveDate.Year - Equipment.Year`.

| Age (years) | Band key |
|---|---|
| 0 | 1-3 |
| 1–3 | 1-3 |
| 4–7 | 4-7 |
| 8–11 | 8-11 |
| ≥ 12 | 12+ |

The Excel formula normalizes age to `MIN(age, 12)` solely as a lookup key — there is no business cap on equipment age. Any equipment 12 years old or older uses the 12+ column. Age 0 ("New") uses the same rates as 1–3 years (identical values in rater).

---

## Deductible Tiers

| Tier key | Meaning |
|---|---|
| `2500` | $2,500 flat |
| `5000` | $5,000 flat |
| `10000` | $10,000 flat |
| `25000` | $25,000 flat |
| `10%ACV` | 10% of ACV |

A factor of `0.00` means that deductible tier is **not available** for that equipment type (Chipper and Tub Grinder cannot use the $2,500 deductible). The engine should reject those combinations before rating.

---

## Policy-Level Endorsements

Confirmed from Premium sheet R304–R310 formulas:

| Code | Label | Charge |
|---|---|---|
| `newly_acquired_equipment` | Newly Acquired Equipment | $500 flat |
| `debris_removal` | Debris Removal | $250 flat |
| `rental_reimbursement` | Rental Reimbursement | $250 flat |
| `towing_storage_recovery` | Towing Storage Recovery | $250 flat |
| `tria` | TRIA | 1% of SubTotal |

TRIA is calculated on the sub-total (IM premium + the four flat endorsements), then added on top.

---

## Settlement Basis

`ACV` (Actual Cash Value) or `RCV` (Replacement Cost Value) is a per-item coverage selection stored on the equipment item. It **does not affect the premium calculation** — it is display/document-only. Co-insurance of 0.9 (ACV) / 0.8 (RCV) is also display-only.

---

## Verification

Verified against filled-out quote (7 items, Territory 1 / MS, 0.70 schedule modifier):

| Item | Type | Age | Band | Ded | Stated Amt | Excel Premium |
|---|---|---|---|---|---|---|
| 1 | Fellerbuncher (4) | 19 | 12+ | 2500 | $35,000 | $1,149 |
| 2 | Skidder (1) | 19 | 12+ | 2500 | $35,000 | $1,127 |
| 3 | Dozer (3) | 20 | 12+ | 2500 | $30,000 | $267 |
| 4 | Loader (2) | 12 | 12+ | 2500 | $100,000 | $892 |
| 5 | Fellerbuncher (4) | 11 | 8-11 | 2500 | $80,000 | $2,074 |
| 6 | Loader (2) | 12 | 12+ | 2500 | $65,000 | $580 |
| 7 | Skidder (1) | 7 | 4-7 | 2500 | $100,000 | $2,541 |

IM Premium total: **$8,630**  
+ Debris Removal: $250  
Sub-total: **$8,880**  
TRIA (NO): $0  
**Grand Total: $8,880**

These 7 items are the first fixtures for `RatingEngineFixtureTests`.
