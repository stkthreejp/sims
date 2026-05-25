# Phase 7 Program Setup Closeout

Date: 2026-05-25

## Closeout Position

Phase 7 established Program setup as the top-level umbrella for SIMS configuration while keeping the practical operating path nested:

`Program > Carrier > Line of Business > State > Insured > Policy`

Program, Carrier, Line of Business, and State are setup context. Insured and Policy remain operational records that select or inherit from that setup path.

The important Phase 7 decision is that SIMS should not duplicate every downstream setup area inside Program Configuration. Program Configuration owns participation and availability. Rating, forms, proposal documents, fees, commissions, policy numbers, authority, and reporting remain in their purpose-built setup areas, but now validate or resolve against the Program path when they are scoped to a Program.

## Items Made Up In Phase 7

- Nested Program setup foundation exists for `Program > Carrier > Line of Business > State`, including admin UI and state-copy behavior.
- Program-specific fee scoping exists in the Charges & Fees engine. All-program fee rules remain valid; Program-specific in-house fees can override them when applicable.
- Quote setup uses the selected Program path to constrain participating carriers, LOBs, and states.
- Rating assignments can be scoped by Program and are validated against active Program carrier/LOB setup.
- Policy form packages can be scoped by Program and are validated against active Program carrier/LOB/state setup.
- Proposal document setup can be scoped by Program, Carrier, LOB, and optional State so carrier-specific proposals and state notices have the right attachment point.
- Policy number assignments can be scoped by Program, Carrier, LOB, and optional State. Bind-time assignment prefers the Program-specific assignment and can fall back to the broader assignment.
- Carrier commission setup can be scoped by Program and Carrier/LOB, with Program path validation.
- Agent commission setup can be scoped by Program, Carrier, LOB, and optional State. The quote calculation path can choose the most specific matching rate.
- Non-renewal workflow was split so marking a file for non-renewal can create the work item before the notice is issued.

## Deferred Items Now Documented Forward

- Deactivation dependency checks: SIMS should eventually warn or block when an admin deactivates a Program carrier, LOB, or state that still has active rating, forms, proposal, commission, policy number, or reporting setup.
- Historical setup audit: existing records that predate the Program path should be reviewed with an orphan/incomplete setup report before go-live.
- Payment terms: the Program path reserves this as a Carrier/LOB-level default with optional State override, but Phase 7 did not wire payment term enforcement.
- Authority and appetite defaults: underwriting controls already support Program/Carrier/LOB/State scoping, but broader authority/appetite defaults should continue as a separate hardening track.
- Additional interest rates: current setup remains carrier/LOB/state oriented. Add Program scoping only if Longleaf and a future Program need different rates for the same carrier/LOB/state.
- Proposal generation polish: setup records now have the right scoping, but future document work should continue moving proposal assembly away from hardcoded Inland Marine assumptions.
- Copy-state expansion: Phase 7 copied the state setup foundation. Future copy behavior can include forms, notices, fees, policy number overrides, or other state-level setup after each area has clear copy semantics.
- Bordereaux/reporting assignment: intentionally carried to Phase 8. Bordereaux profiles should sit on top of the Program path rather than create one-off carrier exports.

## Phase 8 Starting Point

Phase 8 can start from this baseline:

1. Bordereaux and carrier reporting should be scoped to Program first, then Carrier, LOB, and State where needed.
2. Premium reporting should be first because policy number, insured state, transaction, premium, tax/fee, and commission values are already connected enough to validate.
3. Tax/fee and commission bordereaux should reuse the Phase 7 fee and commission scoping rather than calculate separate values.
4. Any new report profile must fail loudly when the Program path is incomplete or when required policy/accounting values are missing.
