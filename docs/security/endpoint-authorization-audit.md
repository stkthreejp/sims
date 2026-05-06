# Endpoint Authorization Audit

This matrix is the working map for SIMS API authorization. Runtime authorization policies are registered from `AppPermissions.All`, and seed data uses the same catalog so role permission assignments and API policies stay aligned.

## Current Policy Catalog

| Policy family | Purpose | Seeded default access |
| --- | --- | --- |
| `admin.*` | User, role, and system administration | Admin |
| `underwriting.manage` | Underwriting actions that previously allowed Admin and Underwriter | Admin, Underwriter |
| `accounting.manage` | Accounting read/workflow actions that previously allowed Admin and Underwriter | Admin, Underwriter |
| `accounting.admin` | Accounting mutation/admin actions that previously required Admin | Admin |
| `rating.manage` | Rating workflow actions that previously allowed Admin and Underwriter | Admin, Underwriter |
| `rating.admin` | Rating configuration/admin actions that previously required Admin | Admin |
| `reports.view` | Reports area access | Admin, Underwriter |
| `insureds.*`, `policies.*`, `policies.notes.*`, `policies.attachments.*` | Existing business permissions used by the UI and ready for controller-level follow-up | Varies by role |
| `nav.*` | Frontend navigation visibility only | Varies by role |

## Endpoint Matrix

| Area | Controllers | Current gate | Sensitive operations | Ownership/data-scope status | Follow-up |
| --- | --- | --- | --- | --- | --- |
| Authentication | `AuthController` | Public login/refresh/Microsoft login; authenticated profile/logout endpoints | Token issuance, refresh cookie rotation, logout | User-specific by token; refresh-token family hardening is in place from prior pass | Add tests for reuse detection and inactive external-login users |
| Core parties | `AgentsController`, `CarriersController`, `InsuredsController` | Authenticated user | Party CRUD and lookup data | Not fully audited for record-level scoping | Add permission attributes for create/edit/delete and review cross-entity access |
| Submissions | `SubmissionsController`, submission child controllers | Authenticated user | Submission creation, supplemental data, vehicles, drivers, locations, GL/IM details | Not fully audited for ownership/tenant scoping | Add explicit policy gates and ownership checks around submission IDs |
| Quotes and policies | `QuotesController`, `PoliciesController`, `UWWriteupController`, `NotesController` | Authenticated user with `underwriting.manage` on bind/decline/non-renew/writeup generate | Binding, declining, policy state changes, notes | Not fully audited beyond attachment access | Replace remaining authenticated-only writes with business permissions and entity access checks |
| Attachments | `AttachmentsController` | Authenticated listing/download; `underwriting.manage` for upload/delete | Upload, signed URL creation, delete | Entity and attachment access checks added in prior pass | Consider replacing upload/delete with more granular attachment policies once CSR behavior is finalized |
| Documents and inbox | `DocumentTemplatesController`, `DocumentGenerationController`, `InboundEmailsController` | Authenticated user | Template changes, generated docs, inbound email/document processing | Not fully audited for document/template ownership | Split template admin from generation/read policies |
| Rating | `RatingPlansController`, `RatingPlanVersionsController`, `CarrierRatingAssignmentsController`, `ShadowRatingController` | `rating.admin` for admin/configuration; `rating.manage` for shared rating workflows | Rating plans, versions, assignments, shadow reports | Mostly global configuration, not tenant-scoped | Add tests proving underwriters cannot access admin-only rating mutations |
| Accounting | Billing controllers, `AgentCommissionsController`, `CarrierCommissionsController` | `accounting.manage` for accounting workflows; `accounting.admin` for admin/mutation actions | Invoices, receipts, cash application, disbursements, period close, QBO, commissions | Not fully audited for customer/policy/account scope | Review each mutation for policy/customer/entity checks and idempotency |
| Reports | `ReportsController` | `reports.view` | Operational and financial report data | Not fully audited for row-level/data-domain limits | Add scoped report filters and tests for least-privilege access |
| Admin | `RolesController`, `UsersController`, `Admin/*` controllers | `admin.roles.manage`, `admin.users.manage`, or `admin.system.manage` | Users, roles, task engine, workflow templates, fees, holidays, system events | Global admin functions | Add separate read/manage policies if non-admin support staff need read-only admin views |
| QBO webhook | `QboWebhookController` | No user auth by design | External accounting event ingestion | Provider-origin validation not confirmed in this pass | Verify webhook signature/secret validation and replay protection |

## Notes

- No controller uses `[Authorize(Roles = "...")]` after this pass; role membership is now an implementation detail behind policy claims.
- Frontend permission checks remain display-only. Server-side policies are the enforcement layer.
- This is a foundation pass, not a complete ownership audit. The highest-value next pass is to add entity access checks to submissions, quotes, policies, accounting records, document library items, and inbox documents.
