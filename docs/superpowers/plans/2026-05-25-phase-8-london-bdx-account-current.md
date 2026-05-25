# Phase 8 London BDX And Account Current Plan

Date: 2026-05-25

## Direction

Phase 8 starts with BRACE/Longleaf monthly carrier reporting. The required monthly run should generate the London premium bordereaux and the Account Current from the same policy transaction dataset, then reconcile the two before the files are treated as complete.

The run includes one main report row per policy transaction where:

`reportingDate = max(transaction effective date, bound/billed/processed date)`

That rule keeps late-processed endorsements in the month they were billed instead of back-posting them into a closed month.

## Required Outputs

- London premium bordereaux workbook.
- Account Current workbook.
- Required London detail tabs:
  - `Auto Veh Info`
  - `IM Unit Info`

The manual helper tabs `Sheet1` and `Sheet2` are not required output. SIMS should fill broker and producer/intermediary fields directly from profile/configured system data.

## Reconciliation Rule

Before finalizing a run, SIMS should compare:

- policy transaction count
- policy transaction keys
- gross premium
- London commission plus brokerage against Account Current gross commission
- London net premium to London against Account Current net due carrier

Mismatches should be visible to the user and should prevent a clean generated status.

## Slice Order

1. Bordereaux profile/run foundation.
2. Profile management API.
3. Shared monthly transaction preview using the effective-or-bound-greater reporting date.
4. London premium BDX XLSX generation with required detail tabs.
5. Account Current XLSX generation from the same dataset.
6. Tandem run and reconciliation gate.
7. Admin/reporting UI for profile selection, preview, validation, generation, history, and downloads.
8. Report-template editor hardening for non-coder changes to tabs, columns, static values, mapped fields, formulas, and validations.
