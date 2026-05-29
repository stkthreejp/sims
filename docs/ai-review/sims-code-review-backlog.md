# SIMS Code Review Backlog

Generated from a read-only multi-agent audit on 2026-05-27.

Last updated on 2026-05-29 during P1 mixed-payee disbursement remediation.

## Audit Checkpoints

### 2026-05-28 targeted auth/security re-audit

- Scope: commits `5315244`, `93fbf25`, and `4f0cc2a`, plus nearby access-control surfaces affected by the hardening work.
- Result: original quote/submission, party CRUD, party attachment, legal source, user list/detail, and frontend permission-gate fixes still look directionally sound from static review and targeted tests.
- Follow-up: legacy policy attachment routes and quote/policy note routes were remediated on 2026-05-28 with backend action policies, policy-to-bound-quote attachment resolution, and focused controller tests.
- Audit artifacts: scan notes were generated under ignored workspace diagnostics at `temp/codex-security-scans/SIMS/4f0cc2a_20260528-063707/`.

### 2026-05-28 P1 ledger reversal remediation

- Scope: `ApplicationDbContext` ledger immutability guard and `LedgerService.ReverseTransactionGroupAsync`.
- Result: ledger rows remain immutable except for the specific posted-to-voided metadata transition required to link original rows to reversal rows.
- Verification: focused ledger service regression tests cover successful reversal rows plus continued rejection of amount mutation.

### 2026-05-28 P1 financial posting atomicity remediation

- Scope: invoice bind/posting, receipt create/posting, and cash application/distribution instruction workflows.
- Result: each workflow now opens a database transaction when it owns the unit of work and reuses any existing outer transaction when called from a larger bind/issue flow.
- Verification: SQLite-backed rollback regression tests simulate ledger/distribution failures and assert no partial invoice, receipt, application, ledger, payable, or status changes remain.

### 2026-05-29 P1 endorsement invoicing remediation

- Scope: `PolicyService.IssueEndorsementAsync` and endorsement invoice creation.
- Result: endorsement issue now treats invoice creation failure as a blocking failure inside the issue transaction; return-premium endorsements are blocked before issue until valid return-premium accounting is implemented.
- Verification: regression tests cover invoice-failure rollback and return-premium rejection without issuing or changing policy premium.

### 2026-05-29 P1 mixed-payee disbursement remediation

- Scope: `DisbursementService.CreateDisbursementAsync`.
- Result: draft disbursement creation now rejects selected payables whose entity payee, carrier, or fallback payee name do not resolve to the same payee identity.
- Verification: focused disbursement service regressions cover mixed carrier payees, mixed entity payees, and same-carrier success.

## P0 Immediate Security / Secret Response

### Hardcoded production-looking PostgreSQL credential

- Status: Remediated on 2026-05-27. The literal connection string was removed from the EF design-time factory, and the database password was rotated by the operator.
- Evidence: `backend/src/SIMS.Infrastructure/Data/DesignTimeDbContextFactory.cs` `DesignTimeDbContextFactory`.
- Risk: repo access may expose a live database host, username, and password.
- Impact: possible database compromise if the credential is still valid.
- Fix: rotate the database password immediately, remove the literal connection string, read from environment/user-secrets, and remove `Trust Server Certificate=true` unless intentionally required.
- Verification: old credential fails; secret scan passes; EF design-time commands work with environment-based configuration.

## P1 Security / Authorization

### Quote and submission write workflows are authenticated-only

- Status: Partially remediated on 2026-05-27. Quote/submission write routes now require explicit policies, and quote creation checks the referenced submission against the caller's business-data access scope.
- Evidence: `backend/src/SIMS.API/Controllers/QuotesController.cs`, `backend/src/SIMS.API/Controllers/SubmissionsController.cs`, `backend/src/SIMS.Application/Services/QuoteService.cs` `CreateAsync`.
- Risk: low-privilege authenticated users can create/update/delete/bind quotes and submissions. Quote creation loads `SubmissionId` without access-scope validation, then grants quote access through `CreatedById`.
- Impact: unauthorized quote creation, policy binding, policy-number use, and invoice creation.
- Fix: add server-side policies for quote/submission create/edit/delete/bind and require access-scope checks on referenced `SubmissionId`.
- Verification: role tests with a read-only user assert `403` for write/bind routes and cross-submission quote creation.

### Core party CRUD is authenticated-only

- Status: Partially remediated on 2026-05-27. Insured create/update/delete now require insured permissions; agent/carrier mutation routes now require system-admin permission.
- Evidence: `backend/src/SIMS.API/Controllers/InsuredsController.cs`, `backend/src/SIMS.API/Controllers/AgentsController.cs`, `backend/src/SIMS.API/Controllers/CarriersController.cs`.
- Risk: any authenticated user can create/update/delete insureds, agents, carriers, locations, and contacts.
- Impact: unauthorized tampering with data used by underwriting, documents, submissions, and downstream workflows.
- Fix: apply granular policies for insured, agent, and carrier management, or explicitly map these routes to existing admin/underwriting policies.
- Verification: role matrix tests for read-only, CSR, underwriter, and admin.

### Party attachment downloads are globally accessible to authenticated users

- Status: Remediated on 2026-05-27. Attachment list/download-url endpoints require `policies.view`, and party attachment access now requires elevated access or a scoped submission/quote relationship to the carrier, agent, or insured.
- Evidence: `backend/src/SIMS.API/Controllers/AttachmentsController.cs`; `backend/src/SIMS.Application/Services/AttachmentService.cs` `CanAccessEntityAsync`.
- Risk: for agent/carrier/insured attachments, access checks only confirm entity existence.
- Impact: sensitive uploaded documents can be exposed through signed blob URLs.
- Fix: require explicit view/download permissions and object-level rules for party attachments.
- Verification: non-elevated users receive `403` for another party's attachment list and download-url endpoints.

### Legal requirements admin surface allows compliance tampering and SSRF

- Status: Remediated on 2026-05-27. Legal source mutation, scan, import, simulate, approve, and reject routes require system-admin permission; OpenLaws base URLs now require the official allowlisted host, HTTPS, no custom ports, and no localhost/private/link-local literal addresses.
- Evidence: `backend/src/SIMS.API/Controllers/LegalRequirementsController.cs`; `backend/src/SIMS.API/Services/OpenLawsClient.cs`.
- Risk: any authenticated user can create/update tracked legal sources with arbitrary absolute URLs, trigger scans, and approve/reject results.
- Impact: regulatory source-of-truth tampering and server-side requests to attacker-selected URLs with a bearer API key.
- Fix: restrict the controller to admin/compliance policies, allowlist OpenLaws hosts, block private/link-local addresses, and separate scan approval permission.
- Verification: tests reject `127.0.0.1`, `169.254.169.254`, and non-admin source/approval mutations.

### User list/details expose staff metadata to all authenticated users

- Status: Remediated on 2026-05-27 for full list/detail endpoints. `GET /api/v1/users` and `GET /api/v1/users/{id}` now require `admin.users.view`.
- Evidence: `backend/src/SIMS.API/Controllers/UsersController.cs`; `backend/src/SIMS.Application/DTOs/Users/UserDto.cs`.
- Risk: `GET /api/v1/users` and `GET /api/v1/users/{id}` only require authentication while returning emails, status, password-change flag, and roles.
- Impact: non-admin users can enumerate staff/user metadata.
- Fix: require `AdminUsersManage` for full user admin DTOs, or add a limited lookup endpoint for assignment dropdowns.
- Verification: non-admin token gets `403` for full user list/detail.

### Frontend routes are authenticated but not permission-protected

- Status: Partially remediated on 2026-05-27. Permission route guards were added for admin, billing, reports, users, core policy/insured/submission, document, compliance, agent, and carrier pages; browser-level role checks still need automation.
- Evidence: `frontend/src/App.tsx` `ProtectedRoute`.
- Risk: sidebar hides links, but direct URLs to admin, billing, reports, users, and document routes remain reachable by any authenticated user.
- Impact: confusing 403s at best; possible data/action exposure where backend checks are incomplete.
- Fix: add route-level permission wrappers aligned with `usePermissions` and sidebar gates.
- Verification: low-privilege role cannot directly open `/billing/invoices` or `/admin/role-permissions`.

### Legacy policy attachment routes bypass action-level attachment permissions

- Status: Remediated on 2026-05-28. Legacy policy attachment routes now require backend action policies, resolve policy ids to bound quote ids, and reject download/delete requests for attachments outside the requested policy context.
- Evidence: `backend/src/SIMS.API/Controllers/PoliciesController.cs` legacy attachment methods; `backend/src/SIMS.Application/Services/AttachmentService.cs` `GetDownloadUrlAsync` and `DeleteAsync`.
- Risk: legacy `/api/v1/policies/{id}/attachments...` endpoints rely on class-level authentication and object-scope checks, while the hardened attachment controller requires explicit view/upload/delete policies.
- Impact: an authenticated user with object access can list, upload, download, or delete attachments without the action-level attachment permission expected by the newer route family.
- Fix: add explicit policies to the legacy policy attachment routes, normalize policy ids to bound quote ids, and ensure download/delete belong to the requested policy context.
- Verification: controller policy tests cover policy attachment list/upload/download/delete; focused controller tests assert policy attachment routes use bound quote ids and do not download/delete attachments outside the requested policy.

### Quote and policy note routes bypass action-level note permissions

- Status: Remediated on 2026-05-28. Quote and policy note routes now require backend read/create/edit/delete note policies aligned with the existing permission model.
- Evidence: `backend/src/SIMS.API/Controllers/NotesController.cs`; `backend/src/SIMS.API/Controllers/PoliciesController.cs` note methods; `backend/src/SIMS.Application/Services/NoteService.cs`.
- Risk: note routes rely on class-level authentication and quote object-scope checks, but do not require `policies.view`, `policies.notes.create`, `policies.notes.edit`, or `policies.notes.delete`.
- Impact: an authenticated user with object access can read, create, edit, pin, or delete notes despite lacking the matching note action permission.
- Fix: add `policies.view` to note reads, `policies.notes.create` to creates, `policies.notes.edit` to updates/pin toggles, and `policies.notes.delete` to deletes across quote and policy note routes.
- Verification: controller policy tests cover `NotesController` and policy note methods; role tests assert object-scoped users without note action permissions receive `403`.

## P1 Financial / Data Integrity

### Ledger reversals are blocked by ledger immutability guard

- Status: Remediated on 2026-05-28. The ledger immutability guard now allows only the required void metadata update and continues to reject other ledger mutations/deletes.
- Evidence: `backend/src/SIMS.Infrastructure/Data/ApplicationDbContext.cs` `UpdateTimestamps`; `backend/src/SIMS.Application/Services/LedgerService.cs` `ReverseTransactionGroupAsync`.
- Risk: reversal/void workflows mark original ledger rows modified, but the DbContext throws for modified `LedgerTransaction` rows.
- Impact: invoice, receipt, and disbursement voids may fail.
- Fix: either allow the specific void metadata update, or keep ledger rows fully immutable and store reversal state in a separate link/status table.
- Verification: integration test posts a ledger group, calls `ReverseTransactionGroupAsync`, and asserts reversal rows plus void metadata.

### Financial posting is not atomic

- Status: Remediated on 2026-05-28. Invoice binding, receipt creation, and cash application now wrap ledger posting and related parent/payable/status updates in one database transaction when no outer transaction exists.
- Evidence: `backend/src/SIMS.Application/Services/InvoicingService.cs`, `ReceiptsService.cs`, `CashApplicationService.cs`.
- Risk: invoice/receipt/cash application rows, ledger rows, payables, and statuses are saved in separate steps without one explicit transaction.
- Impact: failures can leave partial financial data, such as posted ledger rows without matching invoice/payable/application state.
- Fix: wrap each financial unit of work in one EF transaction covering ledger posting and all parent/payable/status updates.
- Verification: force an exception after ledger posting and assert no partial invoice/receipt/application/ledger rows remain.

### Issued endorsements can end up without a valid invoice

- Status: Remediated on 2026-05-29. Positive-premium endorsement issue and invoice creation now share one transaction and propagate invoice failures; return-premium endorsements fail before issue instead of creating invalid negative ledger entries.
- Evidence: `backend/src/SIMS.Application/Services/PolicyService.cs` `IssueEndorsementAsync`; `backend/src/SIMS.Application/Services/LedgerService.cs`.
- Risk: endorsement status, policy premium, and policy version are updated before invoicing, and invoice creation result is not checked.
- Impact: an endorsement may be issued without a valid invoice or ledger result, especially for return-premium endorsements.
- Fix: wrap endorsement issue and invoice creation in one transaction and model return-premium accounting with valid reversal-style ledger rows.
- Verification: issue an endorsement with `PremiumChange = -1000`; assert either a valid return-premium invoice/ledger result or no issued endorsement/premium change.

### Disbursements can combine payables for different payees

- Status: Remediated on 2026-05-29. Draft disbursement creation now validates one shared payee identity before creating disbursement rows.
- Evidence: `backend/src/SIMS.Application/Services/DisbursementService.cs` draft disbursement creation.
- Risk: selected payables are not verified to share the same payable identity.
- Impact: one payment can be labeled for Carrier A while clearing Carrier B or fee-entity payables.
- Fix: require all selected payables to share the same `PayeeId` or carrier/payee identity; reject mixed-payee drafts.
- Verification: two open payables for different carriers cannot be included in one disbursement.

### Nullable unique indexes do not enforce fallback uniqueness

- Evidence: `CarrierCommissionConfiguration.cs`, `AgentCommissionConfiguration.cs`, `BordereauxProfileConfiguration.cs`.
- Risk: PostgreSQL treats `NULL` values as distinct in unique indexes.
- Impact: duplicate fallback commission/profile rows can exist, making rate/profile selection nondeterministic.
- Fix: use `NULLS NOT DISTINCT`, expression indexes with `COALESCE`, or partial unique indexes for fallback/specific scopes.
- Verification: inserting duplicate fallback rows with null scope columns fails.

### Soft-delete filters are missing for some soft-deletable entities

- Evidence: `backend/src/SIMS.Infrastructure/Data/ApplicationDbContext.cs`; rating, proposal document, and quote writeup configurations map `IsDeleted`.
- Risk: deleted rating plans/snapshots, UW writeups, and proposal document configs can leak into normal queries.
- Impact: stale rating/document/writeup records can be selected unless every service remembers manual predicates.
- Fix: add query filters for every `BaseEntity`, preferably through a generic model-builder pass.
- Verification: metadata test asserts every `BaseEntity` entity type has a query filter.

### Policy number year/sequence can use stale quote effective date

- Evidence: `backend/src/SIMS.Application/Services/QuoteService.cs` bind flow; `PolicyNumberService.cs`.
- Risk: policy number generation occurs before applying the bind effective date.
- Impact: annual resets and `{YYYY}` / `{YY}` tokens can use the old quote year.
- Fix: pass bind effective date into policy-number generation or update quote dates before generating within the transaction.
- Verification: quote effective `2026-12-31`, bind effective `2027-01-01`, annual reset format uses `2027`.

### London bordereaux export can disagree with invoice/account-current commission

- Evidence: `backend/src/SIMS.Application/Services/BordereauxService.cs`.
- Risk: account-current rows use invoice-stamped commission, while London rows re-resolve current commission setup.
- Impact: commission overrides or later setup changes can make exported bordereaux disagree with posted invoice/reconciliation totals.
- Fix: export invoice-stamped commission amount/rate or persist the exact run snapshot value.
- Verification: bind with commission override, generate preview and London export, assert same commission and net due carrier.

### Quote create/update/bind accepts invalid money and date ranges

- Evidence: `backend/src/SIMS.Application/DTOs/Quotes/QuoteDto.cs`; `backend/src/SIMS.Application/Services/QuoteService.cs`.
- Risk: negative premium/fees and invalid effective/expiration date ranges can persist.
- Impact: bad quote data can flow into policies, invoices, and reports.
- Fix: validate amounts and dates in application service before mutation.
- Verification: negative amounts, default dates, and expiration-before-effective return failure and leave no created rows.

## P1 Operations

### QBO failures can be marked done and stop retrying

- Evidence: `backend/src/SIMS.Infrastructure/Services/QboJournalDriver.cs`; `backend/src/SIMS.Infrastructure/Workers/QboSyncRetryWorker.cs`.
- Risk: export failures are swallowed and retry worker can mark pending sync `Done`.
- Impact: failed exports silently stop retrying.
- Fix: return an explicit export result, throw after marking failed, or have the retry worker inspect rollup status before marking done.
- Verification: simulated QBO failure leaves pending sync `Retrying` or `Failed`, never `Done`.

### QBO retry rows are not atomically claimed

- Evidence: `backend/src/SIMS.Infrastructure/Workers/QboSyncRetryWorker.cs`.
- Risk: multiple deployed API instances can select the same retry row and post duplicate QBO journals.
- Impact: duplicate accounting entries.
- Fix: claim rows with database-side atomic update/lease, `FOR UPDATE SKIP LOCKED`, or row-version concurrency; use idempotency/request IDs.
- Verification: two worker instances against one due retry row produce one QBO post.

### Email ingestion only processes first unread page

- Evidence: `backend/src/SIMS.Infrastructure/Services/EmailIngestionService.cs`.
- Risk: ingestion fetches first 50 unread Graph messages and does not follow paging.
- Impact: unread messages beyond the first page can starve, especially if mark-read fails on already-ingested messages.
- Fix: follow `@odata.nextLink`, or use durable checkpoints and retry/dead-letter behavior.
- Verification: mock 60 unread messages; message 51+ are eventually ingested.

### FMCSA scheduled jobs suppress retries after failed service results

- Evidence: `backend/src/SIMS.Infrastructure/Workers/FmcsaScheduledJobsWorker.cs`.
- Risk: daily/monthly run markers are set even when service returns failure.
- Impact: transient failures wait until the next day/month instead of retrying; multi-instance behavior is inconsistent because state is in memory.
- Fix: set last-run markers only after success and consider a durable job-run table.
- Verification: forced failure retries on the next poll.

## P2 Frontend / Workflow

### Role Permissions page can crash from conditional hook order

- Status: Remediated on 2026-05-27. Draft initialization now uses `useEffect`, and permission grouping is computed before any early return.
- Evidence: `frontend/src/pages/admin/RolePermissionsPage.tsx`.
- Risk: `useMemo` is called after an early return, so hook count changes between loading and loaded renders.
- Impact: cold refresh can crash with React hook-order error.
- Fix: move all hooks above early returns; use `useEffect` for draft initialization.
- Verification: cold refresh `/admin/role-permissions` without hook error.

### Quote bind UI uses create-policy permission instead of bind permission

- Status: Remediated on 2026-05-27. Quote bind availability now checks `policies.bind`.
- Evidence: `frontend/src/pages/quotes/QuoteDetailPage.tsx`; `usePermissions` exposes `canBindPolicies`.
- Risk: bind UI checks `canCreatePolicies`.
- Impact: a user allowed to bind but not create is blocked; a user allowed to create but not bind sees a failing action.
- Fix: use `canBindPolicies` for bind availability and messaging.
- Verification: test roles with `policies.bind` only and `policies.create` only.

### Add quote is always rendered on submission detail

- Status: Remediated on 2026-05-27. Submission detail Add Quote entry points and the quote form are gated by `policies.create`.
- Evidence: `frontend/src/pages/submissions/SubmissionDetailPage.tsx`.
- Risk: add-quote entry point is not permission-gated.
- Impact: read-only users can attempt quote creation and enter unauthorized workflow.
- Fix: gate with the backend-aligned quote-create permission.
- Verification: read-only submission viewer does not see or cannot invoke add quote.

### Empty task queues show sample production-looking tasks

- Evidence: `frontend/src/pages/tasks/TaskQueuePage.tsx`.
- Risk: empty API result falls back to `SAMPLE_TASKS`.
- Impact: users see fake overdue/blocked work and incorrect metrics.
- Fix: remove runtime sample fallback and render a true empty state.
- Verification: mock empty `/tasks/my-queue`; no sample rows or overdue metrics appear.

## P2 API / Platform Quality

### `NOT_FOUND` service results often return `400 Bad Request`

- Evidence: `backend/src/SIMS.API/Controllers/QuotesController.cs`, `SubmissionsController.cs`; `QuoteService.cs`.
- Risk: controllers collapse service failures into bad request.
- Impact: clients cannot distinguish missing records from validation errors.
- Fix: centralize `Result` to HTTP mapping; map `NOT_FOUND` to `404` and conflict-like codes to `409`.
- Verification: missing quote update and missing submission delete return `404`.

### Paging accepts invalid values and `SortBy` is not honored

- Evidence: `backend/src/SIMS.Application/Common/QueryParameters.cs`; `QuoteService.cs`; `SubmissionService.cs`.
- Risk: invalid page/pageSize can cause bad query behavior; public sort contract is misleading.
- Impact: malformed query strings can trigger 500s or confusing pages; UI sorting may appear broken.
- Fix: enforce page/pageSize bounds and either implement whitelisted `SortBy` or remove it.
- Verification: invalid paging returns controlled failure or normalized values; supported sort changes ordering.

### User updates ignore Identity operation failures

- Evidence: `backend/src/SIMS.Application/Services/UserService.cs`.
- Risk: `UpdateAsync`, `RemoveFromRolesAsync`, and `AddToRolesAsync` results are not checked.
- Impact: admins can see successful saves after failed or partial role updates.
- Fix: check every `IdentityResult` and wrap user/role changes in a transaction where possible.
- Verification: simulated Identity failure returns error and leaves roles unchanged.

### Invoice and receipt numbers race under concurrency

- Evidence: `backend/src/SIMS.Application/Services/InvoicingService.cs`; `ReceiptsService.cs`.
- Risk: numbers use `Count + 1`.
- Impact: concurrent creates can duplicate numbers or fail with unique-index errors.
- Fix: use a database sequence/counter table with serializable transaction and retry.
- Verification: parallel invoice/receipt creation produces distinct numbers.

### No readiness/liveness health endpoints for dependencies

- Evidence: `backend/src/SIMS.API/Program.cs`.
- Risk: deployment can report process running while DB, blob storage, Graph, QBO, FMCSA, LegiScan, geocoding, or AI providers are broken.
- Impact: production incidents are harder to detect and route.
- Fix: add `/health/live` and `/health/ready`, separating process health from dependency readiness.
- Verification: bad dependency credential fails readiness while liveness remains up.

## P2 Targeted Test Gaps

- `IntermediaryServiceTests.DeleteBrokerageSetupAsync_HidesDeletedSetupFromDetailsAndCounts`: deleted brokerage setup must not appear in details/counts or BDX mapping.
- `BordereauxServiceTests.CreatePremiumRunSnapshotAsync_RecordsClearValidationWhenAllSetupsMatch`: validation summary should be clear when London and surplus-lines setup exists.
- `InvoicingProgramScopeTests.BindAsync_CreatesEntityFeePayableForConfiguredPayee`: entity fee routing should create a payable for configured payee.
- `PolicyTransactionLifecycleServiceTests.TransitionAsync_StampsStatusSpecificAuditFields`: issued/completed transitions stamp the right metadata only.
- `IntermediariesControllerTests.IntermediariesController_RequiresAdminSystemManagePolicy`: intermediary setup API remains admin-only.

## Open Business-Rule Questions

### Cancellation accounting

- Evidence: `backend/src/SIMS.Application/Services/PolicyService.cs`; `backend/src/SIMS.Application/DTOs/Policies/PolicyDtos.cs`.
- Question: should midterm cancellation completion require calculated return premium, fee/tax reversal, commission chargeback, invoice, payable, and ledger entries?
- Current behavior risk: cancellation completion can change policy status while recording `PremiumChange = 0m` and no accounting step.

### Party and workflow role model

- Question: what roles should manage insureds, agents, carriers, quotes, submissions, legal sources, and attachments?
- Current behavior risk: many backend routes are authenticated-only while the frontend has partial navigation/UI gating.

## Recommended Fix Order

1. Done: rotate and remove the hardcoded database credential.
2. Done: close residual policy attachment and quote/policy note action-permission gaps.
3. Done: fix ledger reversal behavior and financial posting atomicity.
4. Continue P1 financial/data integrity: fix policy-number bind date and London bordereaux commission source.
5. Fix QBO retry failure/idempotency behavior.
6. Add soft-delete filters and nullable fallback uniqueness enforcement.
7. Add quote money/date validation and user IdentityResult handling.
8. Fix frontend hook crash, bind permission, add-quote gating, and sample task fallback.
9. Add targeted regression tests listed above.
10. Add readiness/liveness health checks and tighten scheduled job retry behavior.
