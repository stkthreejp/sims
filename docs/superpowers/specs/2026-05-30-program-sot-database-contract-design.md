# Program SOT Database Contract Design

## Purpose

SIMS should treat Program setup as the database-backed source of truth for valid MGA setup paths, while avoiding the tedious pattern where every downstream setting must be duplicated state by state.

The canonical Program tree remains:

`Program > Carrier > Line of Business > State`

Downstream setup rows should reference the appropriate level of that tree instead of storing loose combinations of `ProgramConfigurationId`, `CarrierId`, `LineOfBusiness`, and `StateCode` as their authority.

## Design Goals

- Enforce Program setup at the database level where a row is Program-scoped.
- Allow broad defaults, such as one policy number assignment for all states under a Program/Carrier/LOB.
- Allow state overrides where business rules vary by state, such as taxes, fees, forms, and filing setup.
- Preserve historical truth forever for quoted, bound, issued, invoiced, exported, or filed business.
- Avoid forcing all setup areas to the state level when the business rule only needs Program/Carrier/LOB.
- Migrate incrementally, starting with the setup surfaces that affect money, filings, and reporting.

## Current Problem

Many SIMS setup and transaction tables store the same dimensions independently:

- `ProgramConfigurationId`
- `CarrierId`
- `LineOfBusiness`
- `StateCode` or `State`

Recent fixes added service-level validation in some places, such as quote creation and surplus lines setup. That is useful, but it still depends on every service remembering to validate the path.

The long-term issue is that the database does not know that a specific Program/Carrier/LOB/State combination must exist under Program setup. Future imports, new services, direct data repairs, or incomplete admin screens can still create drift.

## Canonical Setup Levels

The existing Program hierarchy should become the canonical scope model:

1. `ProgramConfiguration`
   - Program-level default.
   - Example: a general in-house fee for all Longleaf business.

2. `ProgramCarrier`
   - Program + Carrier default.
   - Example: carrier-level payment behavior if it applies to all LOBs.

3. `ProgramCarrierLineOfBusiness`
   - Program + Carrier + LOB default.
   - Example: one policy-number assignment for Longleaf / BRACE / GL across all states.

4. `ProgramCarrierLobState`
   - Program + Carrier + LOB + State override.
   - Example: Texas surplus-lines tax or a state-specific form package.

State remains required in the Program tree so SIMS knows which states are authorized for each Program/Carrier/LOB. "All States" should not be represented by a fake state. It is represented by referencing the broader `ProgramCarrierLineOfBusiness` level.

## Scope Rule

Downstream setup rows choose the least-specific valid scope that matches the business rule:

- Global fallback: no Program scope.
- Program default: reference `ProgramConfiguration`.
- Program/Carrier default: reference `ProgramCarrier`.
- Program/Carrier/LOB default for all states: reference `ProgramCarrierLineOfBusiness`.
- State-specific override: reference `ProgramCarrierLobState`.

A row must not store an arbitrary Program/Carrier/LOB/State combination as its authority. If the row is Program-scoped, it must point to one canonical Program setup level.

## Inheritance Rule

When resolving setup for a quote, policy, invoice, document, filing, or report, SIMS should apply the most specific matching setup:

1. State-specific row wins.
2. Program/Carrier/LOB row applies when no state-specific row exists.
3. Program/Carrier row applies when no LOB row exists.
4. Program row applies when no carrier row exists.
5. Global fallback applies only when no Program-scoped row exists.

Each setup area can restrict which scope levels it supports. For example, taxes may require state-specific rows, while policy numbers can support LOB-level defaults.

## Setup Area Scope Matrix

Initial target behavior:

| Setup area | Minimum useful scope | State override | Notes |
| --- | --- | --- | --- |
| Policy numbers | Program/Carrier/LOB | Optional | Supports all-state numbering plus state-specific exceptions. |
| Fees and in-house charges | Program or Program/Carrier/LOB | Optional | Program-level fees are useful; regulatory taxes are usually state-specific. |
| Surplus lines filing setup | Program/Carrier/LOB/State | Required | Filing rules are state-specific. |
| Policy form packages | Program/Carrier/LOB | Optional | All-state defaults are allowed, with state-specific required forms overriding. |
| Proposal document configs | Program/Carrier/LOB | Optional | State notices attach at state level. |
| Bordereaux profiles | Program/Carrier/LOB | Optional | State-specific profiles only when reporting differs by state. |
| Carrier commissions | Program/Carrier/LOB | Optional | Existing fallback behavior remains useful. |
| Agent commissions | Program/Carrier/LOB | Optional | Agent-specific rows may remain separate but should still use canonical Program path when scoped. |
| Rating assignments | Program/Carrier/LOB | Optional later | Current rating assignment is LOB-level; state-specific rating can be added later if needed. |
| Intermediary/brokerage setup | Program/Carrier/LOB | Optional | State-specific only when brokerage/reporting differs. |

## Historical Immutability

Anything already quoted, bound, issued, invoiced, exported, or filed must keep the Program setup version/path it used at that time.

Rules:

- Program setup identity fields are immutable once referenced by downstream setup or transaction records.
- If a Program path changes, SIMS expires or deactivates the old path and creates a new path.
- Quotes and policies store the resolved Program path used at quote/bind time.
- Invoices, bordereaux rows, forms, taxes, fees, commissions, and document packets use the setup snapshot/version from the business event, not the current Program setup.
- Historical records keep display snapshots where needed, such as Program code/name, carrier name, LOB, state, and effective dates.

This means a policy bound under an old Longleaf / BRACE / GL / TX path keeps that path forever, even if the Program setup changes later.

## Database Contract

For each Program-scoped setup table, add canonical scope references instead of relying on loose dimensions.

The exact columns can vary by table, but the contract is:

- A global fallback row has no Program scope reference.
- A Program-scoped row references exactly one canonical setup level.
- A state-specific row references `ProgramCarrierLobStateId`.
- An all-state LOB row references `ProgramCarrierLineOfBusinessId`.
- A carrier-level row references `ProgramCarrierId`.
- A Program-level row references `ProgramConfigurationId`.

Tables should include check constraints to prevent contradictory scope references, such as both `ProgramCarrierLineOfBusinessId` and `ProgramCarrierLobStateId` on the same row unless the table has a clear reason and consistency validation.

Existing loose columns can remain temporarily for compatibility, display, search, or migration, but they should not be treated as the authority after the canonical FK is added.

## Effective Dating

The database FK proves that the setup path exists. Service validation still needs to prove that the path is active for the relevant effective date.

Examples:

- A new fee rule effective on `2026-07-01` can reference only a Program path active on that date.
- A quote effective on `2027-01-01` resolves against active Program paths for that quote effective date.
- An expired path can remain referenced by historical rows, but cannot be selected for new setup or new business after expiration.

## Delete And Edit Protection

Program setup paths should not be hard-deleted once used.

Rules:

- If a ProgramCarrier, ProgramCarrierLineOfBusiness, or ProgramCarrierLobState is referenced, the admin UI should show it as locked for identity edits.
- Allowed changes on referenced paths are limited to non-identity metadata, notes, and expiration/deactivation.
- Identity changes require creating a new path or using an explicit copy/migration workflow.
- Delete should become deactivate/expire for referenced paths.

Identity fields include:

- Program Carrier: Program and Carrier.
- Program Carrier LOB: parent ProgramCarrier and LineOfBusiness.
- Program Carrier LOB State: parent LOB and StateCode.

## UI Model

Admin setup screens should use scoped selectors:

1. Select Program.
2. Select Carrier from active carriers under the Program.
3. Select LOB from active LOBs under that Program Carrier.
4. Select either "All States" or a specific active State, depending on the setup area.

The UI should make inherited setup visible:

- Show when a state is using an all-state default.
- Show when a state has an override.
- Show which Program setup path a row is attached to.
- Disable invalid combinations before save.

Backend validation remains mandatory because direct API calls must also be rejected.

## Migration Strategy

This should be phased.

### Phase 0: Data Audit

Add read-only diagnostics for existing setup rows:

- Rows with Program/Carrier/LOB/State combinations not present in Program setup.
- Rows that use Program scope but no matching active path for their effective date.
- Duplicate fallback rows that would conflict with tighter constraints.
- Program setup paths that would become locked because downstream rows already reference them.

No data changes should happen in Phase 0.

### Phase 1: Highest-Risk Setup Rows

Add canonical scope references and validation for:

- Fees and in-house charges.
- Bordereaux profiles.
- Surplus lines setup.

These affect money, taxes, filings, and reporting, so they should move first.

### Phase 2: Forms, Documents, Policy Numbers

Add canonical scope references and resolution for:

- Policy form packages.
- Proposal document configurations.
- Policy number assignments.

Policy numbers should support Program/Carrier/LOB all-state defaults with optional state overrides.

### Phase 3: Commissions, Rating, Intermediaries

Add canonical scope references and resolution for:

- Carrier commissions.
- Agent commissions.
- Rating assignments.
- Intermediary/brokerage setup.

### Phase 4: Transaction Snapshots

Store resolved Program scope on operational records:

- Quotes.
- Policies.
- Policy transactions.
- Invoices.
- Bordereaux run rows/snapshots.

The goal is historical stability, not just current setup validation.

### Phase 5: Deprecate Loose Scope Authority

After all major consumers use canonical Program path references:

- Stop accepting loose Program/Carrier/LOB/State combinations in APIs where a canonical scope is required.
- Keep legacy columns only as denormalized display/search data if useful.
- Add cleanup migrations and stronger check constraints.

## Out Of Scope For The First Implementation Pass

- A full generic `ProgramScope` table.
- Rewriting every setup table in one migration.
- Backfilling every historical policy/invoice/export in one pass.
- Removing legacy columns immediately.
- Changing business decisions such as whether missing commission setup should block bind.

Those are later steps after the DB-level contract is proven on the highest-risk setup surfaces.

## First Implementation Recommendation

Start with Fees because the finding is confirmed by both backend and frontend reviewers, and fees affect invoices, taxes, filing payables, and bordereaux totals.

The first coded slice should:

1. Add canonical Program scope references to fee rule versions where needed.
2. Backfill existing fee rules from current Program/Carrier/LOB/State values when a unique matching path exists.
3. Add backend validation so invalid Program-scoped fee rules fail before save.
4. Update Fees admin cascading selectors.
5. Keep existing global/all-program fee behavior working.
6. Add tests for all-state defaults and state-specific overrides.

Then repeat the pattern for Bordereaux profiles and Surplus Lines.

## Verification Expectations

Each implementation phase should include:

- Regression tests for invalid Program scope paths.
- Regression tests for all-state/default behavior.
- Regression tests for state-specific override behavior.
- Tests for inactive and expired Program paths.
- Tests that referenced Program setup identity cannot be changed in place.
- Backend build and full application test suite.
- Frontend typecheck and production build when UI changes are included.

For migrations:

- Include read-only preflight queries before enforcing constraints.
- Fail with clear diagnostics if existing data cannot be backfilled safely.
- Avoid destructive cleanup unless explicitly approved.

