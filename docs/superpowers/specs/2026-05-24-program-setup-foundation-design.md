# Program Setup Foundation Design

## Purpose

Phase 7 starts by turning Program setup into the source of truth for how SIMS organizes an MGA product. Program remains the umbrella, but the practical setup path is nested:

`Program > Carrier > Line of Business > State`

Quotes and policies consume that setup path. Operational records then continue through:

`Program > Carrier > Line of Business > State > Insured > Policy`

Insured and Policy are not setup levels. They are business records that should point back to, or snapshot values from, the setup path that produced them.

## Current System Fit

SIMS already has Program Configuration admin, but the current program record is mostly an umbrella record with name, code, active status, notes, and underwriting guideline/control relationships.

SIMS also already has several setup areas that use carrier, line of business, and state dimensions:

- Underwriting controls and guideline documents already support Program plus carrier, line of business, and state scoping.
- Policy form packages already resolve by carrier, line of business, and state.
- Policy number assignment already exists outside Program and currently resolves by carrier, writing company, line of business, and state priority.
- Fees already exist in Charges & Fees with versioned rules scoped by carrier, company, producer, line of business, state, city, license type, and effective date.
- Proposal generation exists, but Inland Marine proposal generation is currently hardcoded rather than driven from Program setup.

The Phase 7 foundation should connect these areas through the new setup path rather than duplicate them inside Program setup.

## Foundation Data Model

Add nested setup records under Program Configuration:

1. Program Carrier
   - Belongs to one Program.
   - References one Carrier.
   - Stores active status, effective date, optional expiration date, and notes.
   - Represents that this carrier participates in the program.

2. Program Carrier Line of Business
   - Belongs to one Program Carrier.
   - Stores line of business, active status, effective date, optional expiration date, and notes.
   - This is the primary future setup level for most operational defaults.

3. Program Carrier LOB State
   - Belongs to one Program Carrier Line of Business.
   - Stores state code, active status, effective date, optional expiration date, and notes.
   - This is the future state-specific override/detail level.

Program Configuration itself must not regain direct Carrier, Line of Business, or State columns. Those dimensions belong in nested child setup records.

## Defaults And Future Hooks

The first implementation slice should create the nested foundation and make room for downstream setup without wiring every downstream workflow immediately.

Carrier + LOB is the main operational setup level. Future assignments/defaults should attach here first:

- policy number assignment default
- commission defaults
- payment terms
- proposal template default
- rating setup/default
- in-house fee defaults when not state-specific
- authority/appetite defaults

State is the detail and override level. Future assignments/defaults should attach here when state rules differ:

- policy form package assignment
- state-required notices
- proposal attachments/notices
- state-specific fee overrides
- state-specific policy number override
- state-specific underwriting or filing rules

The UI can show these future areas as intentionally deferred only if they are not editable in the first slice. The data model should avoid unused assignment columns unless an assignment is being actively wired.

## Fees And Taxes

Fees and taxes should stay in the existing Charges & Fees engine. Program setup should not duplicate fee calculation rules.

The fee engine must become Program-aware because SIMS can charge in-house fees that vary by program. For example, Longleaf and a future Shuttlebee program may charge different agency/MGA fees even when regulatory taxes and stamping fees are shared.

Fee rule versions should support optional Program scoping:

- `ProgramId = null`: applies across all programs, subject to the existing carrier, LOB, state, license type, and effective-date matching.
- `ProgramId = Longleaf`: applies only to Longleaf.
- Future programs can define their own program-specific in-house fee rules without changing Longleaf.

Fee calculation context should receive Program along with carrier, line of business, state, license type, effective date, and premium. Program-specific fee rules should win over all-program defaults for the same fee definition when other specificity is otherwise comparable.

Regulatory surplus lines taxes and stamping fees can remain broad all-program rules unless a real program-specific exception exists.

## Policy Number Assignment

Policy number assignment should be treated as at least a Carrier + LOB level setup concern, with optional State override.

The first slice should document and shape the foundation so a Program Carrier LOB can later point to a default policy number assignment. A Program Carrier LOB State can later override that assignment where state-specific numbering is required.

Existing policy number assignment logic should not be duplicated in Program setup. The later wiring step should either link the existing assignment records to the setup path or resolve existing assignments using the selected Program > Carrier > LOB > State context.

## Documents And Proposals

Forms and proposal documents have different needs:

- Policy forms already resolve by carrier, line of business, and state.
- Proposal generation should become setup-driven so different carriers under the same Program can use different proposal templates.
- State-required proposal notices should attach at the state level.

The first slice should not rewrite proposal generation. It should reserve the correct attachment points:

- Program Carrier LOB: default proposal template for that carrier/LOB.
- Program Carrier LOB State: state-required proposal notices or attachments.

## Copy State Setup

The Program setup UI should support copying one state setup to another under the same Program Carrier LOB. This allows SIMS to copy the common setup and then make small state-specific changes.

For the foundation slice, copy behavior should copy only fields owned by the new Program Carrier LOB State setup record. Future copy behavior can include forms, notices, fees, or policy number overrides after those assignments are wired.

## Admin Experience

The existing Program Configuration admin page should become the entry point for the nested setup.

The first slice should let an admin:

- create and edit Programs as today
- add carriers under a Program
- add lines of business under a Program Carrier
- add states under a Program Carrier LOB
- activate/deactivate each setup level
- set effective and optional expiration dates
- add notes
- copy one state setup to another state under the same Program Carrier LOB

The UI should make the nesting clear without turning the page into a large unrelated settings area. The editable foundation should remain focused on participation and availability.

## Resolution Rules

When downstream workflows are wired, SIMS should resolve setup in this order:

1. Select Program.
2. Select participating Carrier within the Program.
3. Select active Line of Business under that Program Carrier.
4. Select active State under that Program Carrier LOB.
5. Use the selected setup path to resolve rating, forms, policy numbers, fees, commissions, payment terms, documents, authority, appetite, and bordereaux references.
6. Snapshot material values on quote, policy, policy version, or transaction records where history must remain stable.

## Out Of Scope For First Slice

The first slice should not fully wire every downstream workflow. These items are intentionally carried forward:

- rating assignment resolution through Program setup
- policy form package assignment through Program setup
- proposal generation by carrier/LOB/state setup
- state proposal notice attachment
- commission default calculation
- payment term enforcement
- policy number assignment selection
- bordereaux/reporting assignment
- authority/appetite enforcement from the new setup records

The first slice should make these future wiring points explicit and avoid building temporary one-off logic that would conflict with them.

## Verification

The implementation plan should include tests for:

- creating the nested setup path
- preventing duplicate carrier, LOB, and state rows within the same parent
- retrieving a Program with nested carrier/LOB/state setup
- copying a state setup under the same Program Carrier LOB
- fee rule resolution preferring Program-specific rules over all-program defaults
- leaving existing all-program fee rules valid when Program is not specified

Frontend verification should include type checking and a manual check of the Program admin page.
