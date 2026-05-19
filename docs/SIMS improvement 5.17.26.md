# SIMS Improvement Roadmap 5.17.26

## Purpose

This roadmap turns the SIMS gap assessment into an ordered improvement plan for building a durable MGA policy administration system.

The goal is not to rebuild SIMS from scratch. SIMS already has meaningful foundations for submissions, quotes, policies, rating, documents, accounting, compliance, communications, workflows, reports, and integrations. The goal is to strengthen the shared spine those modules attach to, then expand carefully into the missing MGA capabilities.

The roadmap is ordered to reduce rework:

1. Stabilize the shared insurance lifecycle.
2. Attach existing modules to that lifecycle.
3. Add missing underwriting and program controls.
4. Add carrier reporting and production analytics.
5. Add claims visibility after the policy/admin core is stable.
6. Add platform reliability and scale protections throughout.

## Guiding Principles

- Keep SIMS as a modular ASP.NET Core monolith for now. Do not move to microservices prematurely.
- Reuse the existing clean architecture: API, Application, Infrastructure, Domain.
- Treat `PolicyTransaction` as the future system backbone.
- Prefer explicit workflow states over hidden side effects.
- Keep rating, documents, accounting, compliance, and communications traceable to the transaction that caused them.
- Add configuration only where the business needs ongoing change without developer involvement.
- Add tests before changing high-risk lifecycle behavior.
- Future-proof through indexing, paging, async jobs, idempotency, observability, and load-aware workflows before business volume forces emergency fixes.

## Current SIMS Assets To Preserve

### Existing Strengths

- Core entities: `Insured`, `Agent`, `Carrier`, `Submission`, `Quote`, `Policy`, `PolicyTransaction`.
- Submission exposure data: drivers, vehicles, equipment, locations, GL classifications, IM coverages, GL coverages, prior carriers, loss years, loss claims, additional interests.
- Quote and bind flow: quote creation, rating snapshot lock, policy creation, policy number assignment, new business transaction, invoice creation.
- Policy operations: issue, endorsement, cancellation, renewal quote, non-renewal.
- Rating engine: rating plans, versions, factor tables, eligibility rules, carrier rating assignments, rating snapshots, impact previews, shadow rating, fixture tests.
- Documents: templates, policy forms, package configuration, quote form selections, proposal generation, policy assembly, Azure Blob storage.
- Communications: inbound email ingestion, outbound communications, Graph integration.
- Accounting: invoices, receipts, cash application, cash distribution, disbursements, payables, payee statements, ledger, QBO, CSV journal export, fee rules, commission rates, trust reconciliation.
- Compliance: compliance documents, reviews, evidence, attestations, legal requirements, cancellation/non-renewal guidance, audit logs.
- Workflow/task engine: task types, task instances, workflow templates, escalation rules, system events.
- Integrations: Azure AD, Microsoft Graph, QBO, Azure Blob, Gemini extraction, Syncfusion, Google geocoding, FMCSA/Socrata, LegiScan.
- Background workers: email ingestion, task notifications, task escalation, QBO retry, shadow rating report, FMCSA scheduled jobs.
- Tests: rating fixture tests, policy number tests, due date formula tests, compliance tests, file scan tests, business access tests.

### Existing Constraints

- `PolicyTransaction` is present but too thin.
- Many modules attach to quote or policy instead of the lifecycle transaction.
- Some workflows update final state immediately instead of moving through pending/review/effective states.
- Claims are only underwriting loss history, not true claims administration.
- Bordereaux/carrier reporting is not first-class.
- Reporting is accounting-heavy and production-light.
- Background jobs are service-specific and not unified under an observable job/outbox framework.

## Target End State

SIMS should become a transaction-centered MGA policy admin system.

The operating model should look like this:

1. An account/insured submits risk data.
2. SIMS clears the risk, checks appetite, and routes referrals.
3. A quote is created and rated.
4. A quote is bound into a policy.
5. Every policy action becomes a `PolicyTransaction`.
6. Every transaction can own or reference rating snapshots, documents, notices, communications, accounting entries, compliance evidence, tasks, approvals, and audit events.
7. Reports, bordereaux, dashboards, and downstream integrations read from that transaction-centered history.

## Roadmap Overview

### Phase 0: Safety Baseline and Inventory

Purpose: protect existing behavior before changing the foundation.

### Phase 1: Shared Lifecycle Vocabulary

Purpose: define consistent statuses and transaction types across submissions, quotes, policies, and policy transactions.

### Phase 2: Policy Transaction Spine

Purpose: make `PolicyTransaction` the backbone of the policy lifecycle.

### Phase 3: Policy Versions and Snapshots

Purpose: preserve before/after policy state for endorsements, renewals, cancellations, reinstatements, rewrites, documents, and audits.

### Phase 4: Transaction-Aware Rating, Documents, Accounting, Communications, and Compliance

Purpose: attach existing modules to transactions without rebuilding them.

### Phase 5: Full Lifecycle Workflows

Purpose: implement skeletons and then functional workflows for endorsement, cancellation, reinstatement, renewal, non-renewal, rewrite, and audit.

### Phase 6: Underwriting Control Layer

Purpose: add clearance, appetite, authority, referral, and approval as auditable shared controls.

### Phase 7: Program Configuration

Purpose: create the program-level source of truth for carrier, LOB, rating, forms, fees, commissions, authority, appetite, documents, and bordereaux setup.

### Phase 7A: Rating Model Deepening

Purpose: keep rating formulas in tested code while making rating inputs, factor tables, eligibility rules, fees, endorsements, versioning, and snapshots more configurable and auditable.

### Phase 8: Bordereaux and Carrier Reporting

Purpose: support MGA carrier obligations with configurable exports and reconciliation.

### Phase 9: Production Reporting and Operational Dashboards

Purpose: add reporting for renewals, expirations, hit ratio, bound premium, underwriter workload, carrier/program performance, and operational health.

### Phase 10: Claims Visibility

Purpose: add policy-linked claims visibility after the policy/admin spine is stable.

### Phase 11: Shared Job, Outbox, and Scale Readiness

Purpose: keep SIMS stable as business volume grows.

These phases should be executed roughly in order. Some infrastructure and scale work should happen in parallel whenever a phase touches a high-volume path.

## Phase 0: Safety Baseline and Inventory

### Why This Comes First

SIMS already has working modules. Before changing the policy lifecycle foundation, create tests and inventory around the behavior we must preserve.

### Existing Pieces To Reuse

- Existing backend test projects.
- Rating fixture test style.
- Business access tests.
- Policy number tests.
- Current docs in `docs/`.
- Current service boundaries.

### Deliverables

1. Lifecycle inventory
   - Document current submission statuses.
   - Document current quote statuses.
   - Document current policy statuses.
   - Document current transaction types and statuses.
   - Document every endpoint that changes policy, quote, accounting, documents, or communications.

2. Regression tests for current high-risk flows
   - Quote bind creates policy.
   - Quote bind creates new business transaction.
   - Quote bind locks latest rating snapshot.
   - Quote bind creates invoice.
   - Issue policy requires ready forms.
   - Endorsement can be created and issued.
   - Cancellation records legal/compliance snapshot.
   - Non-renewal updates policy status.
   - Renewal creates renewal quote.
   - Test bind voiding still protects non-test records.

3. Data integrity checks
   - No bound quote missing a policy.
   - No policy missing a bound quote.
   - No invoice linked to missing policy transaction.
   - No policy number duplicates.
   - No active policy with impossible term dates.

4. Performance baseline
   - Capture current page load behavior for dashboard, submissions, quotes, policies, billing pages, reports.
   - Identify endpoints returning too much data.
   - Identify queries lacking indexes for common filters.

### Acceptance Criteria

- Existing core workflows are covered by tests before structural changes begin.
- Known lifecycle data inconsistencies are documented.
- Slow or high-risk endpoints are listed.

## Phase 1: Shared Lifecycle Vocabulary

### Why This Comes Next

The current statuses overlap and are too thin. A clean vocabulary prevents future workflows from inventing incompatible states.

### Existing Pieces To Reuse

- `SubmissionStatus`
- `QuoteStatus`
- `PolicyStatus`
- `TransactionType`
- `PolicyTransactionStatus`
- workflow events and task engine
- permissions in `AppPermissions`

### Planned Changes

1. Define canonical status meanings.

   Submission statuses should describe intake and underwriting progress:
   - New
   - InProgress
   - Referred
   - Quoted
   - Bound
   - Declined
   - Withdrawn

   Quote statuses should describe quote readiness and decision:
   - Draft
   - Submitted
   - Referred
   - Quoted
   - Accepted
   - Bound
   - Declined
   - Expired
   - Withdrawn
   - Voided

   Policy statuses should describe the current policy state:
   - PendingIssue
   - Active
   - PendingCancellation
   - Cancelled
   - PendingReinstatement
   - Reinstated
   - RenewalPending
   - Renewed
   - NonRenewed
   - Expired
   - Rewritten
   - Voided

   Policy transaction statuses should describe transaction workflow:
   - Draft
   - Submitted
   - InReview
   - Referred
   - Approved
   - Quoted
   - Bound
   - NoticePending
   - NoticeSent
   - PendingEffectiveDate
   - Issued
   - Completed
   - Declined
   - Withdrawn
   - Voided

2. Expand transaction types:
   - NewBusiness
   - Endorsement
   - Cancellation
   - Reinstatement
   - Renewal
   - NonRenewal
   - Rewrite
   - Audit

3. Define allowed transitions.
   - Make illegal transitions fail loudly.
   - Keep transition logic server-side.
   - Record transition history.

4. Align workflow events.
   - Add or normalize events such as `policy.transaction.created`, `policy.transaction.submitted`, `policy.transaction.approved`, `policy.transaction.issued`, `policy.transaction.completed`.

### Acceptance Criteria

- Status definitions are documented.
- Every status has a clear owner and meaning.
- New statuses are not added casually in later modules.
- The UI can display lifecycle state consistently across submissions, quotes, policies, and transactions.

## Phase 2: Policy Transaction Spine

### Why This Is The Core Phase

The current system already has `PolicyTransaction`, and accounting already references `PolicyTransactionId`. This is the right foundation. It needs to become rich enough to own lifecycle actions.

### Existing Pieces To Reuse

- `PolicyTransaction`
- `Policy`
- `Quote`
- `Invoice.PolicyTransactionId`
- `PolicyService`
- `QuoteService.BindAsync`
- policy detail transaction timeline UI
- permissions for issue, endorse, renew, cancel

### Planned Changes

1. Expand `PolicyTransaction`.

   Add fields conceptually equivalent to:
   - requested date
   - effective date
   - expiration date if relevant
   - transaction type
   - transaction status
   - reason code
   - reason text
   - requested by
   - reviewed by
   - approved by
   - issued by
   - completed by
   - requested at
   - reviewed at
   - approved at
   - issued at
   - completed at
   - prior policy version id
   - resulting policy version id
   - source quote id
   - renewal quote id
   - premium before
   - premium delta
   - premium after
   - taxes and fees delta
   - commission delta
   - billing mode snapshot
   - external reference
   - void/reversal references

2. Add transaction detail tables only where needed.

   Keep common fields on `PolicyTransaction`. Use detail tables for type-specific data:
   - `PolicyEndorsementDetail`
   - `PolicyCancellationDetail`
   - `PolicyReinstatementDetail`
   - `PolicyRenewalDetail`
   - `PolicyNonRenewalDetail`
   - `PolicyRewriteDetail`
   - `PolicyAuditDetail`

3. Add transaction transition history.

   Add a history/audit table for:
   - old status
   - new status
   - changed by
   - changed at
   - reason/comment
   - system event name

4. Update existing flows to create transaction records consistently.

   Existing flows to normalize:
   - bind/new business
   - issue policy
   - endorsement
   - cancellation
   - renewal quote
   - non-renewal
   - future reinstatement
   - future rewrite

5. Keep backward compatibility.

   Existing policies and transactions should still render.
   Existing `Pending` maps to an appropriate new pending status.
   Existing `Issued` maps to `Issued` or `Completed` depending on transaction type.

### Acceptance Criteria

- Every policy lifecycle action has a transaction.
- Transaction timeline is the primary policy history.
- Accounting, documents, tasks, approvals, compliance checks, and rating can attach to transactions.
- Old policy records remain readable.

## Phase 3: Policy Versions and Snapshots

### Why This Matters

Without policy versions, endorsements and cancellations mutate current policy values without preserving a complete before/after state. That becomes risky as business volume increases and audits become more common.

### Existing Pieces To Reuse

- Current `Policy`
- Current `Quote`
- `QuoteRatingSnapshot`
- policy document generation data paths
- existing bound quote link

### Planned Changes

1. Add policy version model.

   A policy version should capture:
   - policy id
   - version number
   - created by transaction id
   - prior version id
   - effective date
   - expiration date
   - status
   - premium amount
   - taxes and fees
   - total premium
   - limits/deductibles summary
   - serialized coverage state
   - serialized exposure state summary
   - rating snapshot reference
   - created by
   - created at

2. Add policy state snapshot.

   Keep it simple at first. The first version can snapshot:
   - policy header
   - quote financials
   - coverage fields
   - exposure summaries
   - rating result id
   - form selection snapshot id if needed later

3. Create initial version at bind.

   When a quote is bound:
   - create policy
   - create NewBusiness transaction
   - create policy version 1
   - link transaction to resulting version
   - link invoice to transaction

4. Create new version on issued endorsement.

   For manual endorsements at first:
   - prior version is current version
   - resulting version applies premium delta
   - document any changed fields in transaction detail

5. Create terminal versions for cancellation and non-renewal.

   Cancellation should produce a version/status representing cancelled state.
   Non-renewal should produce transaction history, even if no premium changes.

### Acceptance Criteria

- You can answer "what did the policy look like before and after this transaction?"
- Endorsements do not erase prior state.
- Document generation can eventually target a specific policy version.
- Accounting can be reconciled to the version/transaction that caused it.

## Phase 4: Transaction-Aware Rating, Documents, Accounting, Communications, and Compliance

### Why This Comes After Versions

SIMS already has these modules. The improvement is to attach them to the right lifecycle object.

### Existing Pieces To Reuse

- `QuoteRatingSnapshot`
- `QuoteRatingLine`
- document templates
- policy forms
- quote policy form selections
- policy assembly
- outbound communications
- invoices
- ledger transactions
- legal requirement snapshots
- quote checklist
- task engine

### Planned Changes

1. Rating
   - Add optional `PolicyTransactionId` to rating snapshots or create transaction rating snapshots.
   - Preserve quote rating behavior for pre-bind workflows.
   - Allow endorsement/renewal/audit rating to produce transaction-specific snapshots.
   - Use bound version rates for endorsements unless business rule says otherwise.
   - Use renewal effective date rates for renewals unless business rule says otherwise.

2. Documents
   - Add optional transaction linkage to generated documents/attachments.
   - Generate policy packet against policy version.
   - Generate endorsement packet against endorsement transaction.
   - Generate cancellation/non-renewal notices against notice transaction.

3. Communications
   - Add optional `PolicyTransactionId` to outbound communications.
   - Track communication purpose: proposal, binder, policy issue, endorsement, cancellation notice, non-renewal notice, renewal invitation, internal referral, carrier reporting.
   - Preserve Graph message id and web link.

4. Accounting
   - Keep `Invoice.PolicyTransactionId`.
   - Make accounting workflows transaction-aware by default.
   - Add return premium invoice/credit workflows for cancellation and endorsements.
   - Ensure ledger entries include enough memo/source context for transaction reconciliation.

5. Compliance
   - Attach legal requirement snapshots to transaction.
   - Add transaction-level compliance checklist results.
   - Add proof-of-notice artifacts to transaction.

6. Tasks and approvals
   - Allow tasks to point to `PolicyTransaction`, not only broad entity types.
   - Add approval records for sensitive transaction transitions.

### Acceptance Criteria

- A policy detail screen can show a transaction and all related rating, docs, notices, emails, invoices, tasks, approvals, and compliance records.
- Existing quote and policy screens still work.
- Transaction artifacts are not scattered in unrelated tabs without traceability.

## Phase 5: Full Lifecycle Workflows

### Why This Comes Before New Big Modules

An MGA policy admin system lives or dies by lifecycle handling. Build the skeleton for all transaction types before adding more advanced features.

### Existing Pieces To Reuse

- `PolicyService`
- `PoliciesController`
- `PolicyDetailPage`
- `PolicyTransaction`
- legal guidance endpoints
- policy forms and documents
- invoice and fee engine
- permissions

### Workstream 5A: New Business

Improve current bind flow:

- Create policy transaction first or as part of the same transaction scope.
- Create policy version 1.
- Lock rating snapshot.
- Assign policy number.
- Create invoice.
- Seed issuance packet.
- Fire workflow event.
- Record transition history.

Acceptance:
- New business bind remains atomic.
- If invoice creation fails, policy bind rolls back.
- Bound policy has version 1 and transaction history.

### Workstream 5B: Endorsements

Improve current manual endorsement flow:

- Create endorsement transaction in Draft.
- Allow premium-bearing or non-premium endorsement.
- Attach changed coverage/exposure summary.
- Rate transaction when native rating supports it.
- Create endorsement document packet.
- Create invoice/credit memo.
- Issue endorsement.
- Create resulting policy version.

Acceptance:
- Endorsement has before/after version references.
- Premium delta and accounting are traceable.
- Endorsement documents attach to the endorsement transaction.

### Workstream 5C: Cancellation

Move from immediate cancellation to notice-driven workflow:

- Create cancellation transaction.
- Create cancellation detail record.
- Select reason and cancellation method.
- Select reason code from cancellation reason library.
- Capture required reason-specific fields from bracketed placeholders.
- Capture notice mailing date.
- Capture cancellation day notice requirement.
- Capture mailing days.
- Calculate cancellation effective date from notice mailing date plus notice requirement days plus mailing days.
- Select cancellation notice template.
- Pull legal guidance.
- Generate compliance checklist.
- Generate cancellation notice.
- Store proof of notice.
- Set status to NoticeSent or PendingEffectiveDate.
- Complete cancellation on effective date.
- Create return premium accounting.
- Create cancelled policy version.

Acceptance:
- Cancellation is not final until the workflow reaches completion.
- Notice date, effective date, proof, legal snapshot, and return premium are all traceable.
- User can see the selected reason and calculated cancellation date before the notice is issued.
- Generated cancellation notice is attached to the cancellation transaction.

### Workstream 5D: Reinstatement

Add reinstatement skeleton and workflow:

- Create reinstatement transaction from cancelled policy.
- Create reinstatement detail record.
- Capture reinstatement reason.
- Capture payment or approval requirement.
- Generate reinstatement notice/document if needed.
- Restore policy to Active or Reinstated status.
- Create reinstated policy version.
- Record accounting if premium/cash changes.

Acceptance:
- Cancelled policies can be reinstated through a controlled transaction.
- Reinstatement does not erase cancellation history.

### Workstream 5E: Renewal

Upgrade current renewal quote creation:

- Create renewal transaction before or alongside renewal quote.
- Create renewal detail record.
- Generate renewal quote from prior policy version.
- Re-rate using renewal effective date rules.
- Generate renewal invitation/proposal.
- Bind renewal into new policy term.
- Link prior policy, renewal transaction, renewal quote, and new policy.
- Mark prior policy Renewed when renewal binds.

Acceptance:
- Renewal workflow is traceable from expiring policy to renewal quote to renewed policy.
- Renewal status appears in reports and dashboard.

### Workstream 5F: Non-Renewal

Upgrade current non-renewal:

- Create non-renewal transaction.
- Create non-renewal detail record.
- Pull legal guidance.
- Generate non-renewal notice.
- Store proof of notice.
- Track non-renewal effective date.
- Mark policy NonRenewed.

Acceptance:
- Non-renewal creates a transaction.
- Notice and compliance history are preserved.

### Workstream 5G: Rewrite

Add rewrite skeleton:

- Create rewrite transaction.
- Create rewrite detail record.
- Link original policy and replacement quote/policy.
- Record rewrite reason.
- Handle cancellation or supersession of original policy.
- Preserve accounting traceability.

Acceptance:
- Rewrite can be tracked without ad hoc notes.

### Workstream 5H: Audit

Add audit transaction skeleton:

- Capture audit basis.
- Create audit detail record.
- Capture audited exposure values.
- Calculate additional/return premium.
- Generate audit invoice/credit.
- Create audit transaction history.

Acceptance:
- Audits are not hidden as manual endorsements.

## Phase 6: Underwriting Control Layer

### Why This Comes After Lifecycle Spine

Clearance, appetite, referral, and authority checks need a stable place to attach. They should operate on submissions, quotes, and transactions.

### Existing Pieces To Reuse

- submissions
- UW writeups
- referral flag fields
- task engine
- quote checklist
- rating eligibility rules
- FMCSA safety data
- loss history
- permissions

### Planned Changes

1. Post-Phase-5 alignment
   - Inventory every bind, issue, endorsement, cancellation, reinstatement, rewrite, void, rating promotion, commission override, and sensitive accounting endpoint.
   - Mark whether each endpoint has permission, transaction status, approval, referral, and authority checks.
   - Reuse the Phase 5 transaction artifact model and `PolicyTransactionApproval` instead of creating parallel approval history.

2. Clearance
   - Add submission clearance result.
   - Check duplicate insured/account.
   - Check duplicate submission.
   - Check active policy overlap.
   - Check existing quote/bind conflict.
   - Track clearance status and reviewer.
   - Store matched record, match explanation, compared-field snapshot, override reason, override user, and override timestamp.

3. Appetite
   - Add configurable appetite rules by program/LOB/state.
   - Capture rule result: pass, warn, refer, decline.
   - Store rule version used.
   - Surface results in submission and quote workflows.
   - Create referral records for refer outcomes.
   - Block bind for unresolved decline outcomes.

4. Referral
   - Convert referral flags into referral decision records.
   - Track reason, severity, owner, status, due date, resolution.
   - Link referral to quote or policy transaction.
   - Support submission-level referrals before a quote exists.
   - Preserve source: UW writeup, appetite rule, authority rule, AI advisory, or manual.

5. Authority
   - Add authority rules for:
     - schedule credits/debits
     - premium thresholds
     - loss ratio thresholds
     - class restrictions
     - TIV thresholds
     - driver/vehicle/FMCSA thresholds
     - cancellation/reinstatement authority
   - Enforce server-side.
   - Add authority checks for rating plan promotion, commission overrides, voids, and sensitive accounting actions.
   - Store rule version and input snapshot used for each decision.

6. Approval
   - Add reusable approval request records.
   - Use for rating plan promotion, commission overrides, high-risk referrals, cancellations, voids, and sensitive accounting actions.
   - Extend or wrap existing `PolicyTransactionApproval` so transaction approvals continue to appear in transaction artifacts.
   - Create task engine follow-up for approval owners.

7. UI and work queues
   - Show clearance and appetite on submissions.
   - Show referrals and authority results on quotes and UW writeups.
   - Show referrals, approvals, authority results, and related tasks in policy transaction artifacts.
   - Add manager views for pending referrals and approvals.

8. Audit, migration, and reporting
   - Backfill conservatively from existing approved UW writeups and referral fields where the historical decision is clear.
   - Keep existing writeups and approvals readable.
   - Add reports for open referrals, authority overrides, approval turnaround time, decline reasons, and clearance overrides.

### Acceptance Criteria

- A risk can be cleared, referred, approved, declined, or escalated with full history.
- Authority decisions are not just free-text writeup fields.
- Underwriting controls can be reused across LOBs.
- Bind, issue, cancellation, reinstatement, rating promotion, commission override, and sensitive accounting paths cannot bypass unresolved required controls.
- Appetite and authority outcomes preserve the rule version and input snapshot used.
- The design can move under Program Configuration in Phase 7 without rewriting Phase 6 history.

Detailed execution plan: `docs/phase-6-underwriting-control-layer-plan.md`.
Current control coverage matrix: `docs/phase-6-control-coverage-matrix.md`.

## Phase 7: Program Configuration

### Why This Is Needed

Carrier + LOB is not enough. MGA work is program-driven. Program should connect carrier, forms, rating, fees, commissions, underwriting appetite, authority, documents, and reporting.

### Existing Pieces To Reuse

- `Carrier`
- `CarrierLineOfBusiness`
- `CarrierCommission`
- `AgentCommission`
- `CarrierRatingAssignment`
- rating plans and versions
- policy forms and packages
- fee rules
- policy number assignments
- legal requirements

### Planned Changes

1. Add `Program`.

   Core fields:
   - program name
   - carrier id
   - writing company id if needed
   - line of business
   - states
   - status
   - effective dates
   - billing mode
   - authority summary
   - default policy number assignment

2. Link program to setup modules.
   - Rating assignment.
   - Policy forms/package.
   - Application forms/package.
   - Fees.
   - Commissions.
   - Authority rules.
   - Appetite rules.
   - Bordereaux profile.

3. Update quote creation to select program.

   Quote should know:
   - carrier
   - program
   - LOB
   - effective date
   - rating assignment
   - forms package
   - fees/commission setup

4. Keep migration gentle.

   Existing quotes/policies can have nullable program initially.
   New business should require program once setup is complete.

### Acceptance Criteria

- New quote setup is program-based.
- Forms/rating/fees/commissions can be derived from program.
- Existing records remain readable.

## Phase 7A: Rating Model Deepening

### Why This Comes After Program Configuration

SIMS already has a rating engine foundation: rating plans, versions, factor tables, eligibility rules, carrier assignments, rating snapshots, impact previews, shadow rating, and fixture tests. The next improvement should not be a free-form JSON formula engine. That would add a fragile mini-programming language before the business needs it.

Instead, keep the actual premium formulas in C# and deepen the configurable, versioned rating data around them. This gives SIMS most of the operational benefit of a rating engine while preserving auditability, testability, and predictable premium calculations.

### Existing Pieces To Reuse

- `RatingPlan`
- `RatingPlanVersion`
- `FactorTable`
- `FactorRow`
- `EligibilityRule`
- `CarrierRatingAssignment`
- `QuoteRatingSnapshot`
- `QuoteRatingLine`
- rating fixture tests
- shadow rating results
- impact preview framework
- quote rating panel
- carrier rating assignment UI

### Planned Changes

1. Inventory hardcoded and configurable rating values.

   Document where each rating value currently lives:
   - base rates
   - deductible factors
   - territory modifiers
   - schedule modifier min/max
   - minimum premium
   - endorsement fees
   - TRIA percentage
   - additional interest charges
   - eligibility rules
   - carrier/program/LOB assignments

2. Strengthen rating plan versions as the source of truth.

   Each rating version should own:
   - effective date
   - expiration date
   - status
   - schedule min/max
   - minimum premium
   - factor tables
   - eligibility rules
   - endorsement and fee tables
   - notes/change reason
   - created by, edited by, promoted by
   - impact preview status

3. Move endorsements and rating fees into versioned tables.

   Add a configurable charge table per rating version with:
   - code
   - label
   - charge type: flat, percent, per unit, calculated
   - amount
   - default selected
   - required flag
   - calculation base: manual premium, subtotal, or grand total
   - LOB/program applicability

   The calculation logic should stay in code, but the changeable charge values should come from the active rating version.

4. Expand structured eligibility rules.

   Keep eligibility rules typed and explicit instead of free-form expressions at first:
   - equipment type accepted/rejected
   - deductible allowed by equipment type
   - state allowed/rejected
   - minimum and maximum equipment value
   - required coverage fields
   - carrier/program restrictions

   Ineligible combinations should fail before rating with readable messages.

5. Improve rating snapshots and explanations.

   Snapshots should preserve:
   - rating plan version id
   - manual premium
   - final premium
   - schedule modifier and reason
   - selected endorsements
   - fees/charges applied
   - line-level inputs
   - factors used per line
   - eligibility warnings or errors
   - rated by and rated at

6. Strengthen version testing and impact preview.

   Before activation, a draft version should be testable against:
   - fixture quotes
   - open quotes
   - renewal candidates
   - selected historical examples

   The preview should show premium movement, top movers, outliers, and validation errors.

7. Improve rating admin UI after the backend model is solid.

   Useful admin screens:
   - rating plans list
   - version detail
   - factor table editor/import
   - eligibility editor
   - endorsement/fee editor
   - carrier/program assignment page
   - impact preview page
   - promote/retire actions

### Acceptance Criteria

- Routine rate, factor, fee, endorsement, minimum premium, and eligibility changes can be made through versioned rating data rather than code.
- Rating formulas remain in tested C# implementations.
- Every rated quote can explain which version, factors, fees, endorsements, and eligibility decisions produced the premium.
- A draft rating version can be previewed before activation.
- Bound quote snapshots remain immutable and reproducible.

## Phase 8: Bordereaux and Carrier Reporting

### Why This Comes After Program Configuration

Bordereaux requirements are carrier/program-specific. Building this before Program would force brittle custom exports.

### Existing Pieces To Reuse

- policies
- policy transactions
- invoices
- ledger
- payables
- rating snapshots
- documents
- carrier setup
- report service patterns
- CSV journal export patterns

### Planned Changes

1. Add bordereaux profile.

   Profile fields:
   - carrier/program
   - report type: premium, tax, commission, claims later
   - frequency
   - output format
   - required columns
   - mapping rules
   - date basis
   - transaction types included
   - validation rules

2. Add bordereaux run.

   Run fields:
   - profile id
   - period start/end
   - status
   - generated by
   - generated at
   - file location
   - validation result summary

3. Add export generation.

   Start with CSV/XLSX.
   Include premium bordereaux first.
   Add tax/fee and commission outputs after premium works.

4. Add validation.

   Flag:
   - missing policy numbers
   - missing insured state
   - missing transaction type
   - missing premium/tax/commission values
   - unissued policy packets
   - accounting not posted
   - transaction outside period

5. Add reconciliation.

   Compare exported rows with carrier statements.
   Track accepted, rejected, corrected, resubmitted.

### Acceptance Criteria

- SIMS can generate at least one carrier/program premium bordereaux from policy transaction data.
- Exported files are stored and traceable.
- Validation prevents obvious bad reports.

## Phase 9: Production Reporting and Operational Dashboards

### Why This Comes After Lifecycle and Bordereaux

Good reports need reliable lifecycle data. Once transactions and programs are consistent, production reports become much easier.

### Existing Pieces To Reuse

- reports page
- dashboard
- report service
- submissions, quotes, policies, transactions
- accounting reports
- task engine
- FMCSA data

### Planned Reports

1. Renewals upcoming
   - expiring policies by date range
   - renewal status
   - assigned underwriter
   - premium expiring
   - days until expiration

2. Expiring policies
   - active policies nearing expiration
   - no renewal started
   - non-renewal candidate flag

3. Bound premium by period
   - by month
   - by carrier
   - by program
   - by LOB
   - by producer
   - by underwriter

4. Submission aging
   - open submissions by age
   - stalled stage
   - assigned underwriter
   - missing quote/rating/forms

5. Hit ratio
   - submission to quote
   - quote to bind
   - by carrier/program/producer/underwriter

6. Underwriter workload
   - open submissions
   - open referrals
   - renewals due
   - pending transactions
   - overdue tasks

7. Carrier/program performance
   - premium
   - count
   - average premium
   - endorsement volume
   - cancellation rate
   - renewal retention

8. Operational health
   - failed jobs
   - email ingestion delay
   - QBO sync failures
   - document generation failures
   - bordereaux validation failures

### Acceptance Criteria

- Reports use paged/filterable server-side queries.
- Reports do not load entire books into frontend memory.
- Reports have permission checks.
- Reports can handle materially larger policy counts.

## Phase 10: Claims Visibility

### Why This Comes Later

Claims are important, but SIMS first needs a stable policy and transaction foundation. Current loss history already supports underwriting review, so true claims admin can wait.

### Existing Pieces To Reuse

- submission loss history
- claim-like loss rows
- attachments
- policy documents
- admin jobs placeholder
- proposal claims instructions
- reporting framework

### Planned Changes

1. Add policy-linked claims.

   Core claim fields:
   - claim number
   - policy id
   - policy transaction id if relevant
   - date of loss
   - report date
   - status
   - coverage type
   - loss description
   - claimant
   - adjuster/TPA
   - paid
   - reserve
   - expense
   - incurred

2. Add FNOL intake.

   Start simple:
   - manual entry
   - document upload
   - email attachment link
   - policy lookup

3. Add TPA import later.

   Add import jobs for:
   - claim list
   - reserves
   - payments
   - status changes

4. Add loss run generation.

   Use policy claims and submission loss history.
   Generate PDF/Excel.
   Attach to insured/account/policy.

### Acceptance Criteria

- SIMS can store claims against policies.
- Claims can be used in underwriting and reporting.
- TPA integration can be added without changing the core claim model.

## Phase 11: Shared Job, Outbox, and Scale Readiness

### Why This Is Critical

As business volume grows, synchronous workflows will become fragile. Document generation, email sends, external sync, reporting exports, bordereaux, rating impact previews, and data imports should not block user workflows or crash the app when they get slow.

### Existing Pieces To Reuse

- existing background workers
- QBO retry worker
- pending QBO sync pattern
- Azure Blob storage
- Graph message ids
- shadow rating report worker
- FMCSA scheduled worker
- admin jobs page

### Planned Changes

1. Add shared job table.

   Job fields:
   - job type
   - entity type
   - entity id
   - policy transaction id if relevant
   - status
   - priority
   - run after
   - attempt count
   - max attempts
   - locked by
   - locked until
   - payload JSON
   - result JSON
   - error message
   - created by
   - created at
   - started at
   - completed at

2. Add outbox table.

   Outbox fields:
   - event name
   - aggregate type
   - aggregate id
   - payload JSON
   - status
   - attempts
   - next retry
   - processed at

3. Standardize async workflows.

   Move these toward jobs:
   - document generation
   - policy packet assembly
   - outbound email
   - bordereaux generation
   - QBO sync
   - rating impact preview
   - shadow rating reports
   - FMCSA imports
   - LegiScan scans
   - claims/TPA imports later

4. Add admin job monitor.

   Show:
   - running jobs
   - failed jobs
   - retrying jobs
   - dead-lettered jobs
   - last successful run by job type
   - average duration
   - replay button for safe jobs

5. Add idempotency.

   Critical flows must be safe to retry:
   - bind quote
   - generate policy packet
   - send notice
   - create invoice
   - export bordereaux
   - sync QBO

### Acceptance Criteria

- Long-running work does not block main user workflows.
- Failed background work is visible and retryable.
- Retried jobs do not create duplicate policy packets, emails, invoices, or exports.

## Scalability and Crash-Prevention Roadmap

This track should run alongside all phases. These are the safeguards that keep SIMS stable as submissions, policies, documents, emails, accounting rows, and reports grow.

### Database Scaling

1. Index high-volume queries.

   Priority indexes:
   - submissions by status, created date, underwriter, insured, agent
   - quotes by status, submission, effective date, carrier, LOB
   - policies by status, expiration date, carrier, LOB, insured
   - policy transactions by policy, type, status, effective date
   - invoices by status, policy transaction, invoice date
   - ledger transactions by account, effective date, posting status
   - attachments by entity type/entity id
   - outbound communications by status/entity/sent date
   - jobs by status/run after/priority

2. Use server-side paging everywhere.

   Avoid loading the entire book into dashboard, reports, or admin screens.

3. Avoid unbounded includes.

   Replace large eager loads with targeted projections for list pages.

4. Add query timeouts and cancellation tokens.

   Long reports should not tie up API threads indefinitely.

5. Archive or partition later.

   Candidate tables:
   - ledger transactions
   - job history
   - inbound emails
   - outbound communications
   - audit logs
   - generated document records

### API and Application Scaling

1. Keep endpoints idempotent where business users may retry.

   Especially:
   - bind
   - issue
   - cancellation completion
   - reinstatement
   - QBO sync
   - document generation
   - email send

2. Use optimistic concurrency on high-risk records.

   Candidates:
   - policy
   - policy transaction
   - rating plan version
   - invoice
   - fee rule version
   - compliance document

3. Make expensive work asynchronous.

   Avoid request-time blocking for:
   - PDF assembly
   - large report export
   - bordereaux generation
   - rating impact preview
   - external API pulls

4. Add clear failure boundaries.

   If document generation fails, the policy transaction should remain in a recoverable state.
   If email sending fails, the communication should show failed and retryable.
   If accounting sync fails, ledger should remain posted and QBO sync should retry.

### Frontend Scaling

1. Do not fetch more rows than needed.

   Dashboard and report pages should use summary endpoints, not large list endpoints.

2. Add filters before volume hurts usability.

   Critical filters:
   - status
   - date range
   - underwriter
   - carrier
   - program
   - LOB
   - producer

3. Use background status indicators.

   For long jobs, show queued/running/failed/completed rather than freezing the UI.

4. Keep detail pages sectional.

   Load heavy sections on demand:
   - documents
   - communications
   - accounting
   - transaction history
   - claims
   - audit trail

### Document and File Scaling

1. Keep files in Blob storage.

   Continue not storing large files in PostgreSQL.

2. Store document metadata in the database.

   Metadata should support:
   - entity type/id
   - policy transaction id
   - document type
   - generated by
   - generated at
   - template/version
   - blob path

3. Avoid regenerating documents unnecessarily.

   Store generated packets.
   Regenerate only when source data or selected forms change.

4. Add virus scan and file size protections.

   File scanning exists conceptually. Keep it enforced for all uploads.

### Background Job Scaling

1. Use lease-based job locking.

   Prevent two workers from processing the same job.

2. Use retries with backoff.

   Follow the QBO retry pattern, but generalize it.

3. Add dead-letter handling.

   Failed jobs should stop retrying after max attempts and require review.

4. Track duration.

   Slow jobs should become visible before users report the app as broken.

### Observability and Operations

1. Add structured logging around lifecycle transitions.

   Log:
   - transaction id
   - policy id
   - quote id
   - user id
   - event name
   - old status
   - new status

2. Add health checks.

   Include:
   - database
   - blob storage
   - Graph
   - QBO
   - job queue depth
   - failed job count

3. Add operational alerts.

   Alert on:
   - repeated job failures
   - QBO sync backlog
   - email ingestion stalled
   - document generation failures
   - database connection errors
   - high API error rate

4. Add backup and restore discipline.

   Before production:
   - increase PostgreSQL backup retention
   - test restore
   - document recovery steps
   - confirm Blob storage retention/versioning

## Recommended First Work Package

The first actual implementation plan should focus only on the shared lifecycle spine.

### Work Package 1: Lifecycle Spine Hardening

Scope:

- Add regression tests for current quote bind, policy issue, endorsement, cancellation, renewal quote, and non-renewal behavior.
- Expand policy transaction statuses and types.
- Add transaction transition history.
- Add policy version/snapshot model.
- Create initial policy version at bind.
- Ensure new business transaction links to policy version.
- Update policy detail response/UI to display transaction state without breaking existing data.

Do not include in this first work package:

- Program configuration.
- Bordereaux.
- Claims.
- Full notice automation.
- New frontend redesign.
- Microservices.
- Message broker.

### Why This First

This work gives every later improvement a stable anchor. Without it, bordereaux, claims, notices, accounting, compliance, and reporting will each invent their own version of "what happened to the policy," and SIMS will become harder to maintain as volume grows.

## Recommended Second Work Package

### Work Package 2: Transaction-Aware Artifacts

Scope:

- Add transaction linkage to generated documents/attachments.
- Add transaction linkage to outbound communications.
- Add transaction linkage to rating snapshots or transaction rating snapshots.
- Add transaction-level compliance checklist/snapshot storage.
- Add transaction-aware task support.
- Update policy detail UI to show transaction artifacts.

## Recommended Third Work Package

### Work Package 3: Full Lifecycle Workflow Completion

Scope:

- Add required policy transaction detail tables.
- Convert cancellation to notice-driven transaction lifecycle.
- Add manual cancellation notice issue flow with reason code, reason fields, notice days, mailing days, calculated cancellation date, and template selection.
- Convert non-renewal to transaction lifecycle.
- Add reinstatement workflow.
- Complete renewal traceability when renewal binds.
- Add rewrite skeleton.
- Add audit skeleton.
- Generate and store notice documents.
- Track proof of notice.
- Add return premium accounting hooks.
- Fix DOCX table import enough for notice templates.

Detailed execution plan: `docs/phase-5-lifecycle-workflows-plan.md`.

## Recommended Fourth Work Package

### Work Package 4: Underwriting Clearance, Appetite, Referral, and Authority

Scope:

- Inventory control coverage across lifecycle, rating, commission, void, and accounting endpoints after Phase 5.
- Add clearance checks.
- Add appetite rules.
- Convert referral flags into referral decision records.
- Extend reusable approval records from the Phase 5 transaction approval foundation.
- Enforce authority server-side.
- Add referral and approval work queues.
- Add audit/reporting for open referrals, overrides, decline reasons, and approval turnaround.

Detailed execution plan: `docs/phase-6-underwriting-control-layer-plan.md`.

## Recommended Fifth Work Package

### Work Package 5: Program Configuration

Scope:

- Add program entity.
- Link program to carrier, LOB, rating, forms, fees, commissions, authority, appetite, and policy number setup.
- Require program on new quotes after migration.

## Recommended Fifth-A Work Package

### Work Package 5A: Rating Model Deepening

Scope:

- Inventory hardcoded versus configurable rating values.
- Move version-sensitive endorsements, fees, TRIA percentages, minimum premiums, and schedule bounds into rating plan version data where they are not already there.
- Expand structured eligibility rules without introducing a free-form formula language.
- Improve rating snapshots so they preserve factors, charges, selected endorsements, warnings, and calculation explanations.
- Strengthen draft-version impact preview and promotion gates.
- Keep premium formulas in C#.

Do not include in this work package:

- A generic JSON formula interpreter.
- Broker self-service rating.
- Replacing all existing rating formulas.
- New LOB rating formulas unless needed to validate the model.

## Recommended Sixth Work Package

### Work Package 6: Bordereaux and Carrier Reporting

Scope:

- Add bordereaux profiles.
- Add bordereaux runs.
- Generate premium bordereaux.
- Add validation.
- Store export files.
- Add carrier reconciliation workflow.

## Recommended Seventh Work Package

### Work Package 7: Production Reports and Dashboards

Scope:

- Renewals upcoming.
- Expiring policies.
- Bound premium by period.
- Submission aging.
- Hit ratio.
- Underwriter workload.
- Carrier/program/producer performance.
- Operational health.

## Recommended Eighth Work Package

### Work Package 8: Shared Job and Outbox Framework

Scope:

- Add shared job table.
- Add outbox table.
- Add worker framework.
- Add admin job monitor.
- Move long-running document, email, report, bordereaux, and integration work toward jobs.

This can start earlier if document generation, email sending, or reporting begins to affect user experience.

## Recommended Ninth Work Package

### Work Package 9: Claims Visibility

Scope:

- Add policy-linked claims.
- Add FNOL intake.
- Add reserve/payment tracking.
- Add TPA import framework.
- Add loss run generation.

## Final Recommendation

Start with Work Package 1. It is the least flashy work, but it is the foundation that makes everything else safer. Once the transaction spine and policy versioning are in place, SIMS can grow into bordereaux, claims, stronger underwriting controls, and production analytics without creating disconnected modules.

The most important future-proofing decision is to make every significant business action traceable, retryable where appropriate, and recoverable when a downstream system fails. That is what will keep SIMS from becoming brittle as business volume increases.
