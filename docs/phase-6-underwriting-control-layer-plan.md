# Phase 6: Underwriting Control Layer Plan

## Goal

Add auditable underwriting controls for clearance, appetite, referrals, authority, and approvals without creating a second workflow beside the policy transaction spine completed in Phase 5.

## Closeout Status

Phase 6 is complete as an operational underwriting-control baseline. SIMS now has auditable clearance checks, referral records, published underwriting controls, document checklist blockers, program-scoped guideline configuration, post-bind activity gates, authority approval requests, high-risk authority enforcement, and manager queue visibility.

The original Phase 6 plan also described a broader deterministic authority rule engine for every bind, issue, cancellation, reinstatement, rewrite, and endorsement edge case. That should not keep Phase 6 open. The reusable control records, published-rule model, approval spine, and enforcement points now exist; the remaining work is rule-depth and reporting expansion that belongs in Program Configuration / Phase 7 hardening.

### Complete In Phase 6

- Clearance evaluation and override audit for duplicate/open-account risks.
- Bind blocking for unresolved blocked clearance results.
- Structured underwriting referral records from UW writeup referral flags.
- Bind and issue blocking for open required referrals.
- Admin guideline/rule setup by Program > Carrier > LOB > State/all states.
- AI handoff contract for proposed controls using documented measurable fields.
- Published underwriting controls and deterministic enforcement results.
- Override audit for published blockers.
- Stage-aware document checklist controls for submission, quote, bind, issue, post-bind, and renewal stages.
- Issue blocking for incomplete required Issue-stage documents.
- Post-bind activity blocking for required PostBind items before endorsements, cancellation, reinstatement, rewrite, renewal, and transaction completion work.
- Program configuration as the umbrella product identity, with quote/policy program assignment and accounting reporting by program.
- Authority approval request spine for quotes, policy transactions, rating versions, and accounting actions.
- Server-side authority gates for commission overrides, rating plan promotion, and accounting voids.
- Operations queues for post-bind follow-up and the unified manager queue.

### Deferred To Phase 7 / Hardening

- Full deterministic authority thresholds for quote bind, policy issue, endorsement issue, cancellation completion, reinstatement, rewrite, and non-renewal.
- Rule-version input snapshots for every appetite/authority outcome beyond the published-control enforcement record already stored today.
- Transaction artifact panels that show all related referrals, authority approvals, and tasks inline for every transaction type.
- Approval turnaround, authority override, decline reason, and clearance override reporting beyond the operational manager queue.
- Conservative historical backfill from old UW writeups where the original decision can be proven.

Disposition: the main 5.17 improvement roadmap now assigns these items to Phase 7 Program Configuration planned changes and acceptance criteria. Broader dashboards that go beyond these operational control reports remain Phase 9.

### Final Call

Do not add another Phase 6 implementation slice only to satisfy the broad wording of the original plan. The foundation is usable and auditable; remaining items are better handled as Phase 7 program-configuration depth, reporting, and transaction-artifact polish.

## Assumptions

- Phase 5 is complete enough that policy transactions, status history, transaction artifacts, cancellation/non-renewal/reinstatement/rewrite detail records, and transaction approvals exist.
- Phase 6 should reuse `PolicyTransaction`, `PolicyTransactionApproval`, UW writeups, quote checklists, task workflows, rating eligibility rules, FMCSA data, loss history, and permissions.
- Program Configuration foundation now exists in Phase 6 as the umbrella product identity, so new guideline/rule setup should prefer `ProgramId` while still matching through carrier/LOB/state on the guideline, rule, quote, and policy scopes.
- AI underwriting work remains advisory until deterministic clearance, referral, authority, and approval records exist.

## Scope

Phase 6 includes:

- Submission and quote clearance checks.
- Appetite rule results.
- Referral decision records.
- Authority rules and server-side enforcement.
- Reusable approval request workflow, extending the transaction approval foundation from Phase 5.
- Underwriting control snapshots on submissions, quotes, and policy transactions.
- UI surfaces for underwriters and managers to review, approve, decline, or escalate.
- Tests for blocked bind/issue/cancellation/reinstatement paths.

Phase 6 excludes:

- Full submission-level program assignment independent of quote selection.
- Bordereaux.
- Claims.
- Broker self-service clearance.
- AI-generated final underwriting decisions.
- Replacing the existing UW writeup with a new system.

## Core Decisions

1. Do not create free-text-only underwriting controls. Every control outcome needs a structured record with reason, severity, status, owner, timestamps, and source context.
2. Use `PolicyTransactionApproval` where the approval is tied to a policy transaction. Add a broader approval/request shape only where the target can be a submission, quote, rating plan version, accounting action, or future program setup.
3. Authority must be enforced server-side before bind, issue, cancellation completion, reinstatement completion, sensitive accounting voids, rating promotion, and commission override actions.
4. Appetite and authority rules should snapshot the rule version used. Rule ownership should move under Program Configuration without losing older carrier/LOB/state history.
5. Referral flags in UW writeups should become referral records, not remain isolated checkboxes.
6. Approval, referral, and authority outcomes should be visible in transaction artifacts when tied to a transaction.

## Workstream 6A: Post-Phase-5 Alignment

Confirm the current lifecycle foundation before adding controls:

- Inventory every endpoint that can bind, issue, endorse, cancel, complete cancellation, non-renew, reinstate, rewrite, void, promote rating, override commission, or perform sensitive accounting action.
- Mark whether each endpoint currently has a permission check, transaction status check, approval check, and authority check.
- Document how existing `PolicyTransactionApproval` is used and whether it should be extended or wrapped by a generic approval service.
- Identify which Phase 5 transaction statuses map to underwriting control states: `InReview`, `Referred`, `Approved`, `Declined`, `Withdrawn`, and `Voided`.

Acceptance:

- Phase 6 starts from an endpoint/control matrix, not assumptions.
- Existing transaction approval behavior is preserved.
- No new control table duplicates an existing Phase 5 artifact without a reason.

Current matrix: `docs/phase-6-control-coverage-matrix.md`.

## Workstream 6B: Clearance

Add a submission/quote clearance result that can be rerun and audited.

Clearance checks:

- Duplicate insured/account by normalized name, FEIN where available, address, and DOT number where available.
- Duplicate open submission for the same insured, producer, LOB, and effective date window.
- Active policy overlap by insured, LOB, state, carrier, and policy term.
- Existing quote/bind conflict for the same risk and effective date.
- Prior declined, cancelled, non-renewed, or voided policy transaction indicators.

Data to store:

- Target type: submission or quote.
- Target id.
- Status: clear, warning, blocked, referred, overridden.
- Match type and matched record id.
- Match explanation.
- Reviewer and reviewed timestamp.
- Override reason and override user when allowed.
- Snapshot JSON of compared fields.

Acceptance:

- A submission or quote can show whether it is clear, warned, blocked, referred, or overridden.
- Bind is blocked for unresolved blocked clearance results.
- Clearance overrides are permissioned and audited.

## Workstream 6C: Appetite

Add versioned appetite rule evaluation while keeping the rule model simple and aligned to Program Configuration where available.

Initial rule dimensions:

- Carrier.
- LOB.
- State.
- Effective date.
- Operation type/class.
- TIV, premium, loss ratio, largest item, driver count, vehicle count, FMCSA safety indicators, and restricted classes.

Rule result values:

- Pass.
- Warn.
- Refer.
- Decline.

Data to store:

- Rule code and label.
- Rule version.
- Result.
- Severity.
- Source fields used.
- Explanation.
- Evaluated by service/user.
- Evaluated at.

Acceptance:

- Submission and quote pages can show appetite results.
- Quote bind is blocked for unresolved decline results.
- Refer results create or update referral records.
- Appetite result history survives later rule edits.

## Workstream 6D: Referral Records

Convert existing referral flags into shared referral decision records.

Referral fields:

- Target type: submission, quote, or policy transaction.
- Target id.
- Reason code.
- Reason label.
- Source: UW writeup, appetite rule, authority rule, AI advisory, manual.
- Severity: info, warning, referral required, decline recommended.
- Owner.
- Status: open, in review, approved, declined, waived, withdrawn.
- Due date.
- Resolution notes.
- Decision by and decision timestamp.

Integration points:

- UW writeup referral checkboxes create or update referral records on submit.
- Appetite `Refer` and authority `Refer` outcomes create referral records.
- Policy transaction status moves to `Referred` when a required referral is open.
- Approved or waived referral can move the transaction back to `Approved` or `InReview`, depending on context.
- Task engine creates follow-up tasks for assigned referral owners.

Acceptance:

- Referral history is visible from the submission, quote, and transaction artifact views.
- Referral records can be approved, declined, waived, or withdrawn.
- Bind/issue paths cannot bypass open required referrals.

## Workstream 6E: Authority Rules

Add deterministic authority evaluation and enforcement.

Initial authority categories:

- Schedule credits/debits and rate reductions.
- Premium thresholds.
- Loss ratio thresholds.
- Class and operation restrictions.
- TIV and single-item thresholds.
- Driver, vehicle, and FMCSA thresholds.
- Cancellation notice/completion authority.
- Reinstatement authority.
- Rewrite authority.
- Rating plan promotion authority.
- Commission override authority.
- Sensitive accounting void authority.

Data to store:

- Rule code and version.
- Required permission or role.
- Required approval type.
- Current user authority result.
- Rule input snapshot.
- Evaluated at.

Enforcement points:

- Quote bind.
- Policy issue.
- Endorsement issue.
- Cancellation notice issue and completion.
- Reinstatement completion.
- Rewrite completion.
- Rating plan version promotion.
- Commission override.
- Accounting void actions.

Acceptance:

- Authority is enforced in backend services, not only in the UI.
- Failed authority checks return clear reasons and next action.
- Authority approvals attach to the affected submission, quote, policy transaction, rating version, or accounting action.

## Workstream 6F: Approval Workflow

Extend the Phase 5 approval foundation into a reusable service.

Approval behavior:

- Create approval request from referral or authority rule.
- Assign approver by permission/role.
- Optional due date and task creation.
- Approver can approve, decline, or request more information.
- Decision records who decided, when, and why.
- Approval completion re-evaluates the blocked action before allowing it.

Approval targets:

- Policy transaction: reuse `PolicyTransactionApproval`.
- Quote or submission: add equivalent target support if needed.
- Rating plan version, commission override, and accounting void: use the same approval service contract even if storage differs by target type.

Acceptance:

- Existing transaction approvals remain visible in transaction artifacts.
- New approval requests are not hardcoded to a single workflow.
- Approval decisions are permissioned and audited.

## Workstream 6G: UI and Work Queues

Add control visibility without burying users in admin screens.

UI surfaces:

- Submission detail: clearance and appetite panel.
- Quote workspace/writeup: referral and authority panel.
- Policy transaction artifacts: referrals, authority results, approvals, and related tasks.
- Task queue: referral and approval tasks with target links.
- Admin/configuration: early rule management for appetite and authority.

Acceptance:

- Underwriters can see why a risk is clear, referred, declined, or blocked.
- Managers can see pending approvals and referrals in one place.
- Users do not need to inspect raw notes to understand underwriting control state.

## Workstream 6H: Audit, Reporting, and Migration

Add enough history and reporting to make controls trustworthy.

Migration/backfill:

- Existing approved UW writeups can be backfilled as approved underwriting decisions where practical.
- Existing referral fields should remain readable, then be translated into referral records when writeups are submitted or resubmitted.
- Existing `PolicyTransactionApproval` rows remain valid.

Operational reporting:

- Open referrals by owner and age.
- Declines by appetite reason.
- Authority overrides by user and type.
- Approval turnaround time.
- Clearance blocks and overrides.

Acceptance:

- Phase 6 records support audit review without searching notes manually.
- Existing records remain readable.
- Backfill is conservative and does not invent decisions that were not recorded.

## Workstream 6I: Testing

Add focused tests around the controls that can block business actions.

Backend tests:

- Duplicate active policy overlap blocks clearance unless overridden.
- Appetite decline blocks bind.
- Appetite refer creates referral record.
- Open required referral blocks bind/issue.
- User without authority cannot issue an out-of-authority endorsement.
- Approval allows the action only after re-evaluation.
- Cancellation/reinstatement authority is enforced server-side.
- Approval/referral history appears in transaction artifacts.

Frontend tests or manual verification:

- Clearance/appetite/referral panels render expected states.
- Bind/issue buttons show actionable block reasons.
- Approval decision UI updates pending queues and transaction artifacts.

Acceptance:

- High-risk control paths have tests before enforcement changes go live.
- Manual QA covers the main underwriting and manager workflow.

## Phase 6 Acceptance Criteria

- A risk can be cleared, referred, approved, declined, overridden, or escalated with full history.
- Bind, issue, cancellation, reinstatement, rating promotion, commission override, and sensitive accounting paths enforce authority server-side.
- Referral decisions are structured records, not only UW writeup flags or notes.
- Existing Phase 5 transaction artifacts show related approvals, referrals, authority checks, tasks, and documents where applicable.
- Appetite and authority decisions preserve the rule version and input snapshot used.
- The design can move more enforcement and reporting under Program Configuration without rewriting Phase 6 history.
