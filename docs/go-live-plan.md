# SIMS Go-Live Readiness Plan

Last reviewed: 2026-05-23

## Current Readiness Summary

SIMS is not ready to go live yet, but it is in a good hardening position. Backend tests pass, the frontend production build passes, and EF reports no pending model changes. The remaining work is mostly go-live discipline: security and dependency gates, production configuration, endpoint/data-scope closure, deployment smoke testing, and UI link polish.

## Top Findings

- **P0: .NET dependency audit is not clean.** `dotnet list package --vulnerable --include-transitive` reported high-severity advisories for `Microsoft.Bcl.Memory 9.0.0` and `Microsoft.Kiota.Abstractions 1.17.1`.
- **P0: production config does not fail closed enough yet.** JWT is validated, but other required production settings are still placeholder-prone. A real LegiScan key appears in `backend/src/SIMS.API/appsettings.Development.json`, and upload scanning falls back to `NoOpFileScanService` unless ClamAV is explicitly configured.
- **P1: endpoint authorization and ownership scoping still need a closeout pass.** Several areas are not fully audited for record-level access, especially submissions, quotes, policies, documents, accounting, and reports.
- **P1: deployment gates are incomplete.** The GitHub workflow builds and deploys images, but does not yet gate deployment on `dotnet test`, frontend build/lint, dependency audit, or smoke tests.
- **P1: UI readiness has known broken/polish items.** A bound quote action links to `/policies/${q.id}` using the quote id, QuickBooks activity has a `#` placeholder link, and Reports exposes "coming soon" entries.
- **P1: lint is not a real gate yet.** `npm run lint` fails because `eslint` is not installed/configured, despite the package script.

## Phase 0: Baseline Freeze

Purpose: make the current state measurable and stop accidental drift.

Must haves:

- Clean or intentionally documented working tree.
- Backend tests, frontend build, EF drift check, dependency audits, and lint gate all runnable.
- Fix `backend/check-ef-drift.ps1` so it specifies both EF contexts and fails on EF errors.

Gate tests:

- `dotnet test SIMS.sln --no-restore`
- `npm run build`
- `npm run lint`
- EF drift check for `ApplicationDbContext` and `SafetyAnalyticsDbContext`
- npm and NuGet vulnerability audits

## Phase 1: Security And Production Config

Purpose: make production fail loudly when secrets, origins, scanning, auth, or integrations are wrong.

Must haves:

- Rotate/remove committed LegiScan key.
- Resolve high NuGet vulnerabilities.
- Add startup validation for Key Vault-backed production essentials: database, blob storage, QBO, Graph, Gemini/AI where enabled, CORS origins, webhook token, and malware scanning provider.
- Configure Entra production redirect URIs.
- Decide whether automatic `MigrateAsync()` on app startup is acceptable for production, or move migrations to deployment.

Gate tests:

- Auth login, refresh, logout, and Microsoft login integration tests.
- Permission-denied tests for non-admin and non-underwriter roles.
- Upload tests for size, extension, file signature, and malware-scan failure.
- QBO webhook valid/invalid signature tests.
- Dependency audit clean or documented with accepted risk.

## Phase 2: Spine Hardening Closeout

Purpose: finish the Phase 6 control spine so high-risk actions cannot bypass clearance, referrals, authority, checklist, or approval state.

Must haves:

- Close the "Immediate Next Slice" in the Phase 6 matrix.
- Confirm gates on bind, issue, endorsement, renewal, cancellation, non-renewal, reinstatement, rewrite, rating promotion, commission override, and accounting void.
- Ensure blockers are visible in submission, quote, policy, manager queue, and transaction artifact views.
- Triage EF global-query-filter warnings around required relationships.

Gate tests:

- Existing backend tests stay green.
- Add regression tests for every high-risk action blocked by open referrals, authority approvals, or post-bind checklist items.
- Add entity ownership/access tests for submissions, quotes, policies, documents, accounting, and reports.

## Phase 3: Deployment And Ops

Purpose: prove SIMS can run as a production system, not just as local code.

Must haves:

- Staging and production app settings separated.
- App Insights/logging/health checks configured.
- PostgreSQL backups increased and HA decision made.
- Key Vault managed identity access verified.
- QBO sandbox-to-production plan completed.
- Worker behavior checked so scheduled jobs do not double-run unexpectedly.

Gate tests:

- Deploy to staging from `main`.
- Run migration against a restored or production-like database.
- Smoke test auth, database, blob upload/download, document generation, QBO webhook, Graph ingestion, and background workers.
- Backup restore and rollback rehearsal.

## Phase 4: UI Links And Workflow QA

Purpose: remove user-facing rough edges and prove real workflows can be completed.

Must haves:

- Fix bound quote "View Policy" link.
- Remove or complete `#` QuickBooks link behavior.
- Hide, label, or complete coming-soon report/dashboard/activity surfaces.
- Reconcile frontend deployment docs: code uses `/api/v1` proxy behavior, while docs still describe `VITE_API_URL`.

Gate tests:

- Browser route crawl over sidebar links and major dynamic links.
- Role-based UI pass for Admin, Underwriter, CSR, and ReadOnly.
- Manual UAT: submission to quote to bind to policy issue; endorsement; cancellation; post-bind follow-up; accounting void approval; manager queue.

## Phase 5: Business Data Readiness

Purpose: make sure live operations have the right seed/config data.

Must haves:

- Users, roles, and permissions reviewed.
- Programs, carriers, rating assignments, policy number sequences, policy forms, fees, checklists, and underwriting controls validated.
- Legal/compliance tracked sources reviewed.
- QBO accounts and GL mappings reconciled.

Gate tests:

- Quote rating and bind for each active program, carrier, and LOB intended for launch.
- Policy packet/document generation.
- Invoice, receipt, cash application, disbursement, period close, and QBO sync dry run.
- Inbound email to submission workflow.

## Phase 6: Go-Live Rehearsal

Purpose: run the system as if live, then cut over only with evidence.

Must haves:

- No P0/P1 open issues.
- Staging burn-in completed.
- Monitoring, backup, rollback, and support runbook ready.
- Final stakeholder sign-off.

Gate tests:

- Full regression suite.
- Production-like smoke suite after deployment.
- Dependency/security audit clean.
- UAT sign-off across underwriting, manager, admin, and accounting workflows.

## Verification From Review

- Backend tests passed: `174/174`.
- Frontend production build passed.
- EF drift check was clean for `ApplicationDbContext` and `SafetyAnalyticsDbContext`.
- npm production audit found `0` vulnerabilities.
- NuGet audit found high-severity transitive vulnerabilities that must be resolved or accepted before launch.
- Frontend lint is not currently runnable.
