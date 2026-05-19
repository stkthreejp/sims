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

## Immediate Next Slice

1. Add override permission and override audit fields for blocked clearance.
2. Add submission detail UI panel for clearance status and evaluation.
3. Add bind-block messaging in the quote UI.
4. Start appetite result records after clearance UI is visible.
