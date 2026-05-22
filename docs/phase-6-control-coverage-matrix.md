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
| Quote | Commission override | `POST /api/v1/quotes/{id}/commission-override` | Present: `underwriting.manage` | N/A | Missing | Missing | Require authority or approval for out-of-standard commission changes. |
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
| Rating | Promote rating plan version | `POST /api/v1/rating-plan-versions/{id}/promote` | Present: rating admin controller policy | Partial: maker/checker and impact-preview checks | Missing | Partial: maker/checker only | Convert promotion gate into reusable approval/authority record. |
| Accounting | Void receipt | `POST /api/v1/billing/void/receipts/{id}` | Present: `accounting.manage` | N/A | Missing | Partial: prior-day requires admin | Add sensitive accounting authority and approval record. |
| Accounting | Void cash application | `POST /api/v1/billing/void/cash-applications/{id}` | Present: `accounting.manage` | N/A | Missing | Partial: prior-day requires admin | Add sensitive accounting authority and approval record. |
| Accounting | Void invoice | `POST /api/v1/billing/void/invoices/{id}` | Present: `accounting.manage` | N/A | Missing | Missing | Add sensitive accounting authority and approval record. |
| Accounting | Void disbursement | `POST /api/v1/billing/void/disbursements/{id}` | Present: `accounting.manage` | N/A | Missing | Partial: prior-day requires admin | Add sensitive accounting authority and approval record. |
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

## Immediate Next Slice

1. Decide whether non-admin bind/issue users should see published enforcement results without `underwriting.manage`.
2. Add enforcement behavior for required issue/post-bind document checklist blockers if the business wants those to stop issue automatically.
3. Add program filters to accounting/reporting pages if users need to drill into one program at a time.
