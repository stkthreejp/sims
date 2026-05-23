# Phase 6 Control Coverage Matrix

This matrix starts Workstream 6A. It records the current high-risk action surface before Phase 6 adds formal clearance, appetite, referral, authority, and approval enforcement.

Status values:

- Present: the control exists today.
- Partial: the action has some protection, but not the full Phase 6 control.
- Missing: the control is not yet enforced.
- Next: planned Phase 6 work.

| Area | Action | Endpoint | Current Permission | Transaction Status Check | Approval / Referral Check | Authority Check | Phase 6 Next Step |
|---|---|---|---|---|---|---|---|
| Quote | Bind quote | `POST /api/v1/quotes/{id}/bind` | Partial: authenticated route, no explicit bind policy on controller | Partial: service validates quote/bind state | Partial: blocked clearance now stops bind | Missing | Add appetite/referral/authority gate before bind. |
| Quote | Commission override | `POST /api/v1/quotes/{id}/commission-override` | Present: `underwriting.manage` | N/A | Present: creates/reuses an authority approval request when needed | Present: requires `underwriting.authority.approve` or approved request | Done for reusable authority gate; refine thresholds in Program Configuration. |
| Quote | Rate quote | `POST /api/v1/quotes/{id}/rate` | Partial: authenticated route | N/A | Missing | Partial: rating eligibility rules exist, authority not formal | Feed rating warnings into appetite/referral records. |
| Quote | Shadow rate | `POST /api/v1/quotes/{id}/shadow-rate` | Present: `underwriting.manage` | N/A | Missing | Missing | Keep advisory; no blocking authority needed unless promoted into production rating. |
| Policy | Issue policy | `POST /api/v1/policies/{id}/issue` | Present: `policies.issue` | Present: service-level issuance checks | Missing | Missing | Block issue for open required referrals or missing authority approvals. |
| Policy | Add endorsement | `POST /api/v1/policies/{id}/endorsements` | Present: `policies.endorse` | Partial: creates transaction | Missing | Missing | Evaluate authority on premium/change summary; create referral when out of authority. |
| Policy | Issue endorsement | `POST /api/v1/policies/{id}/endorsements/{txnId}/issue` | Present: `policies.endorse` | Present: transaction lifecycle/status checks | Missing | Missing | Require resolved authority/referral records before issue. |
| Policy | Create renewal quote | `POST /api/v1/policies/{id}/renew` | Present: `policies.renew` | Partial: policy state checked in service | Missing | Missing | Run clearance/appetite on renewal quote before bind. |
| Policy | Issue cancellation notice | `POST /api/v1/policies/{id}/cancellation-notice` | Present: `policies.cancel` | Present: creates notice transaction | Missing | Missing | Add cancellation authority rules and approval when reason/effective date requires it. |
| Policy | Complete cancellation | `POST /api/v1/policies/{id}/cancellations/{txnId}/complete` | Present: `policies.cancel` | Present: effective date/status checks | Missing | Missing | Require cancellation completion authority before final status change. |
| Policy | Reinstate policy | `POST /api/v1/policies/{id}/reinstate` | Present: `policies.cancel` | Partial: service workflow checks | Missing | Missing | Add reinstatement authority and approval gate. |
| Policy | Start rewrite | `POST /api/v1/policies/{id}/rewrite` | Present: `policies.endorse` | Partial: creates rewrite transaction | Missing | Missing | Add rewrite authority gate and referral record when needed. |
| Policy | Complete rewrite | `POST /api/v1/policies/{id}/rewrites/{txnId}/complete` | Present: `policies.endorse` | Present: transaction workflow checks | Missing | Missing | Require authority/referral resolution before superseding/replacing policy. |
| Policy | Non-renew | `POST /api/v1/policies/{id}/non-renew` | Present: `policies.cancel` | Present: notice transaction workflow | Missing | Missing | Add non-renewal authority and referral visibility. |
| Policy | Complete non-renewal | `POST /api/v1/policies/{id}/non-renewals/{txnId}/complete` | Present: `policies.cancel` | Present: transaction workflow checks | Missing | Missing | Require authority/referral resolution before final non-renewal status. |
| Policy | Void test bind | `POST /api/v1/policies/{id}/void-test-bind` | Present: admin role | Partial: test-bind protections in service | Missing | Partial: admin-only | Keep admin-only; add approval history if used outside test cleanup. |
| Rating | Promote rating plan version | `POST /api/v1/rating-plan-versions/{id}/promote` | Present: `rating.manage` can reach approval gate | Partial: maker/checker and impact-preview checks | Present: creates/reuses authority approval request when promoter lacks rating admin | Present: requires `rating.admin` or approved request | Done for reusable authority gate. |
| Accounting | Void receipt | `POST /api/v1/billing/void/receipts/{id}` | Present: `accounting.manage` | N/A | Present: creates/reuses authority approval request when needed | Present: requires `accounting.admin` or approved request | Done for reusable authority gate. |
| Accounting | Void cash application | `POST /api/v1/billing/void/cash-applications/{id}` | Present: `accounting.manage` | N/A | Present: creates/reuses authority approval request when needed | Present: requires `accounting.admin` or approved request | Done for reusable authority gate. |
| Accounting | Void invoice | `POST /api/v1/billing/void/invoices/{id}` | Present: `accounting.manage` | N/A | Present: creates/reuses authority approval request when needed | Present: requires `accounting.admin` or approved request | Done for reusable authority gate. |
| Accounting | Void disbursement | `POST /api/v1/billing/void/disbursements/{id}` | Present: `accounting.manage` | N/A | Present: creates/reuses authority approval request when needed | Present: requires `accounting.admin` or approved request | Done for reusable authority gate. |
| UW writeup | Submit writeup | `POST /api/v1/quotes/{quoteId}/writeup/submit` | Partial: authenticated route | N/A | Partial: referral flags stored in payload | Missing | Convert referral flags into referral decision records. |
| UW writeup | Approve writeup | `POST /api/v1/quotes/{quoteId}/writeup/approve` | Present: `underwriting.manage` | N/A | Partial: writeup approval only | Missing | Link approval to referral/authority records where applicable. |

## First Implemented Slice

- Added `IUnderwritingClearanceService`.
- Added duplicate open submission warning.
- Added active overlapping policy block.
- Added focused tests for both checks.

## Second Implemented Slice

- Persisted latest submission clearance results in `UnderwritingClearanceResults`.
- Added submission clearance API endpoints:
  - `GET /api/v1/submissions/{id}/clearance`
  - `POST /api/v1/submissions/{id}/clearance/evaluate`
- Added quote bind gate for blocked clearance results.
- Added EF migration `AddUnderwritingClearanceResults`.

## Third Implemented Slice

- Added submission detail clearance panel for users with `underwriting.manage`.
- Added manual clearance evaluation from the submission detail page.
- Added quote bind messaging for blocked clearance results.
- Added frontend clearance API client methods and types.

## Fourth Implemented Slice

- Added `underwriting.clearance.override` permission.
- Added blocked clearance override audit fields.
- Added clearance override API endpoint.
- Kept quote bind blocked unless blocked clearance results are overridden.
- Added clearance override schema and permission migrations.

## Fifth Implemented Slice

- Added clearance override UI action for users with `underwriting.clearance.override`.
- Added inline override reason capture on the submission clearance panel.
- Added overridden status and audit display for blocked clearance results.

## Sixth Implemented Slice

- Added submission appetite result records.
- Added required underwriting referral records from quote writeup referral flags.
- Added quote bind gate for open required underwriting referrals.
- Added focused tests for referral creation and bind blocking.

## Seventh Implemented Slice

- Added referral decision endpoints for approve, decline, and waive.
- Added policy issue gate for unresolved required referrals.
- Added submission appetite/referral UI visibility.

## Eighth Implemented Slice

- Added quote bind warning/disable messaging for open required referrals.
- Added policy issue preview/issue blocking for open required referrals.
- Linked quote and policy blocker messages back to submission referral review.

## Ninth Implemented Slice

- Added admin guideline document records scoped by program, company, line, and state/all states.
- Added proposed/published underwriting control records for appetite rules, referral triggers, authority limits, document checklist items, and appetite notes.
- Added review, approve, reject, publish, and retire API workflow.
- Added audit log records for document creation and every control review/publish/retire action.
- Added admin permissions for managing and publishing underwriting controls.

## Tenth Implemented Slice

- Added Admin > UW Controls page for guideline documents and scoped proposed/published controls.
- Added guideline document creation by program, company, line, and state/all states.
- Added manual proposed-control creation/editing for document checklist items, appetite rules, referral triggers, authority limits, and appetite notes.
- Added approve, reject, publish, and retire UI actions with decision notes.
- Added recent activity visibility from the guideline audit log.

## Eleventh Implemented Slice

- Added AI-agent handoff documentation for creating guideline documents and proposed controls.
- Wired published `DocumentChecklistItem` controls into quote checklist generation for submission, quote, and bind stages.
- Kept issue, post-bind, and renewal document requirements stored but out of the bind checklist until their dedicated enforcement surfaces exist.
- Preserved conservative blocker behavior: published checklist controls only block bind when `isBlocking` is true.

## Twelfth Implemented Slice

- Added persisted enforcement results for published underwriting controls.
- Added bind and issue evaluation checkpoints for published hard blockers, referral triggers, warnings, and non-applicable conditions.
- Added override audit support for blocked enforcement results using the existing clearance override permission.
- Documented the AI contract for unconditional blockers (`conditionJson: null`) and conditional blocker/referral rules using only the approved field/operator/value schema.
- Preserved conservative condition handling: unknown or unsupported measurable fields are flagged as `UnknownField` instead of becoming invented blockers.

## Thirteenth Implemented Slice

- Added quote and policy page visibility for published underwriting control enforcement results.
- Added status display for active blockers, warnings, referrals, unknown fields, and overridden results.
- Added inline blocker override capture for users with `underwriting.clearance.override`.
- Refreshed enforcement results after bind/issue failures caused by published blockers.

## Fourteenth Implemented Slice

- Added stage tracking to quote checklist items so published document checklist controls can be separated by submission, quote, bind, issue, post-bind, and renewal stages.
- Added stage-filtered checklist API reads while preserving the quote page's existing early-stage bind checklist behavior.
- Added policy page surfaces for Issue and PostBind document checklist controls tied to the bound quote.
- Kept checklist completion manual and permissioned through the existing underwriting management path.

## Fifteenth Implemented Slice

- Added first-class Program Configuration records with program code, active status, and notes. Program now represents the umbrella product, such as Longleaf or ShuttleBee.
- Added Admin > Programs for creating, editing, activating, and deactivating program products.
- Linked underwriting guideline documents and proposed controls to optional `ProgramId` while preserving legacy `ProgramName` text.
- Added program selection to Admin > UW Controls guideline setup so AI-imported/manual guideline documents can use the same program identity while company, line, and state stay on the guideline scope.

## Sixteenth Implemented Slice

- Added nullable program assignment to quotes and bound policies.
- Added program selection to submission quote creation without filling or locking company/line, so LOB remains on the quote/policy transaction.
- Updated published underwriting control matching so program-specific controls only apply to quotes/policies assigned to that program.
- Preserved legacy controls without `ProgramId` using the existing company, line, and state/all-states matching path.

## Seventeenth Implemented Slice

- Added an accounting report for invoice totals by program so production and retained commission can be reviewed by umbrella product.
- Grouped invoice totals through policy transaction to policy program, with an Unassigned bucket for older or non-program policies.
- Added Reports navigation for Invoice Totals by Program with gross premium, fees, total billed, commission, agent paid, and net retained.

## Eighteenth Implemented Slice

- Allowed authenticated quote and policy users to read published underwriting control enforcement results when a bind or issue task is blocked.
- Kept blocker override restricted to the existing `underwriting.clearance.override` permission.
- Removed the frontend `underwriting.manage` gate from quote/policy enforcement panels so blocked users can see what must be resolved.

## Nineteenth Implemented Slice

- Blocked policy issue when required Issue-stage document checklist items are incomplete.
- Stopped issue before policy packet assembly/filing so incomplete required documents cannot produce an issued policy.
- Added policy issue error handling that refreshes the document checklist and shows the required document blocker message.
- Left PostBind document items visible but not issue-blocking until a dedicated post-bind action gate exists.

## Twentieth Implemented Slice

- Added a program filter to the Invoice Totals by Program accounting report.
- Passed selected program identity from Reports UI to the report API so users can drill into one umbrella product at a time.
- Preserved the all-program view as the default and kept unassigned totals visible there.

## Twenty-First Implemented Slice

- Added a shared post-bind activity gate for required PostBind checklist items.
- Blocked endorsements, endorsement issue, renewals, cancellation notices/direct cancellations, rewrites, non-renewals, reinstatements, and transaction completions while required PostBind items remain incomplete.
- Returned the blocking PostBind item names in the error message so users know what must be completed before continuing policy activity.

## Twenty-Second Implemented Slice

- Added clearer blocked-action explanations to bind, issue, and policy activity controls.
- Showed named required checklist blockers directly on disabled bind/issue/action buttons instead of only after the user clicks.
- Added a policy activity warning banner when required PostBind items are still incomplete.

## Twenty-Third Implemented Slice

- Added a lightweight Reports > Operations > Post-Bind Follow-Up queue for active policies with incomplete required PostBind checklist items.
- Included policy, insured, program, carrier, line, state, bind/issue age, and missing item names so signed documents and quote subjectivity follow-up can be worked from one place.
- Backed the queue with `GET /api/v1/reports/operations/post-bind-follow-up` under the existing report permission surface.

## Twenty-Fourth Implemented Slice

- Added computed owner, due date, days-until-due, and SLA status to the Post-Bind Follow-Up queue.
- Assigned follow-up ownership to Assistant UW when present, otherwise Underwriter, without creating separate task records yet.
- Added queue filters for owner, SLA status, due window, and search so signed documents and post-bind subjectivities can be worked by team member and urgency.

## Twenty-Fifth Implemented Slice

- Added the reusable authority approval request spine for Phase 6 closeout.
- Added `authority_approval_requests` so quotes, policy transactions, rating versions, and accounting actions can share the same pending/approved/declined approval flow.
- Added `underwriting.authority.approve` as the first authority approval permission and seeded it to Admin.
- Added an authority approval service that allows users with the required permission, reuses pending requests, and treats approved matching requests as authority to proceed.

## Twenty-Sixth Implemented Slice

- Enforced authority approval on commission overrides, rating plan promotion, and sensitive accounting voids.
- Returned a shared `AUTHORITY_APPROVAL_REQUIRED` response when a user can start the action but lacks the elevated authority to complete it.
- Let rating managers reach the promotion approval gate while still requiring `rating.admin` or an approved authority request before promotion mutates rating data.
- Let approved accounting void requests satisfy the existing prior-period admin guard so approvals are operational, not just audit notes.

## Twenty-Seventh Implemented Slice

- Added a unified Reports > Operations > Manager Queue for open referrals, pending authority approvals, and post-bind follow-up.
- Backed the queue with `GET /api/v1/reports/operations/manager-queue` using existing referral, authority approval, and checklist data instead of creating a second task system.
- Added queue counts, SLA status, owner, work type, detail, and action links back to the underlying submission, quote, policy, or rating version.
- Preserved the dedicated Post-Bind Follow-Up report for deeper signed-document and subjectivity filtering.

## Immediate Next Slice

1. Close Phase 6 with an updated matrix, focused regression tests, and backlog notes for Phase 7.
