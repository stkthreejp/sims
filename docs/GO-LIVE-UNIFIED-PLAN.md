# SIMS — Unified Go-Live Plan (Internal UAT / Staging Target)

> **Owner:** Jeremiah O'Donovan · **Created:** 2026-06-08 · **Target:** Internal UAT on staging — SMM staff running real-shaped data through auth + core workflows + at least one program end-to-end.
>
> **What this is:** One plan that reconciles the ~40 plan/spec documents in `docs/` against the actual state of the repo, and sequences the remaining work to reach live testing. It supersedes the scattered phase plans as the *coordination* layer — the individual plans remain the detailed execution references (see crosswalk in Appendix A).
>
> **How to use it:** Work top-to-bottom by workstream. Each item is a checkbox. P0 blocks launch; P1 should be done before UAT; P2 can run during/after UAT. "→ source" points to the detailed plan.

---

## 1. Where SIMS actually is today

This is a genuinely built MGA platform, not a scaffold. The build substantially exceeds what several of the older plan docs imply, so a lot of "planned" work is in fact done.

**Already complete (do not re-plan as net-new):**

- **Program configuration + Program-SOT DB contract.** Nested `Program > Carrier > LOB > State` setup with canonical foreign keys and enforcement across fees, bordereaux profiles, surplus lines, form packages, proposal configs, policy numbers, carrier & agent commissions, rating assignments, and intermediary/brokerage. This is the most heavily completed area.
- **Underwriting control layer (Phase 6).** Clearance, referrals, published controls, stage-aware document checklists, post-bind gate, reusable authority-approval spine, manager queue. Closed as an operational baseline.
- **Rating engine core.** `IM_v1` engine, versioning, bind-locked snapshots, Excel parity harness (24 IM fixtures), admin factor edit + impact preview + maker/checker.
- **Policy lifecycle transactions.** Endorse / cancel / reinstate / rewrite / non-renew / renew endpoints all exist.
- **Accounting + live QuickBooks Online integration**, compliance document register, FMCSA safety backend, LegiScan bill tracking.
- **Security baseline.** No secrets in tracked source; `.env` and `appsettings.*` gitignored; MSAL/Azure AD frontend + JWT backend; the P0 hardcoded DB credential was already fixed and rotated (2026-05-27). 74 of 75 controllers carry `[Authorize]` (the one exception, the QBO webhook, uses HMAC verification by design).
- Backend tests: ~70 service-test files across two projects; 174/174 passing at last go-live review.

**The current frontier:** Program-scope enforcement rollout (large, mostly *uncommitted* — see the immediate risk below), go-live hardening, UI alignment, and bordereaux runs.

**Immediate risk to resolve first:** the working tree has ~283 uncommitted changes (mostly the program-scope enforcement migrations and configs), and a stale `.git/index.lock` was observed. Source on disk differs substantially from `main`. **Nothing else in this plan is safe to reason about until that work is committed, built, and tested.** See WS0.

---

## 2. Definition of "ready for live testing"

For the internal UAT/staging target, SIMS is ready when **all** of the following hold:

1. Working tree committed; `dotnet build` + `dotnet test` + `npx tsc --noEmit` + `npm run build` all green; EF drift clean.
2. Config **fails closed** in staging — every required secret/setting validated at startup, no committed keys, malware scanning real (or explicitly accepted).
3. Backend authorization passes an **ownership/entity-scope** audit on the core workflow surfaces (submissions, quotes, policies, accounting, documents, inbox).
4. The **broken-link/placeholder list is zero**, and the High-priority UI pages (including Login) match the SIMS UI guide.
5. At least **one full program is configured end-to-end** (program > carrier > LOB > state, rating assignment, fees, policy-number sequence, forms, checklists, UW controls, QBO/GL mapping) and a submission can go submission → quote → bind → issue against it.
6. App + frontend are **deployed to the Azure test environment** (`sims-api-test` / `sims-frontend-test`) with App Insights, health checks, and a backup/rollback rehearsal done.
7. CI gates on build/test/lint/audit; staging burn-in shows no open P0/P1.

---

## 3. Workstreams

### WS0 — Stabilize the working tree (P0, do first)

- [ ] Clear the `.git/index.lock` only after confirming no git process is running. → repo
- [ ] Review the ~283 uncommitted changes in logical groups (migrations, configurations, domain, controllers, services, seed CSVs, frontend, `deploy.yml`). Commit the program-scope enforcement work to `main` per the solo-dev workflow. → `AGENTS.md`, `CLAUDE.md`
- [ ] Decide what to do with untracked `.agents/`, `plugins/`, `SIMS-UI-Guide/` (commit, gitignore, or relocate). The UI guide is referenced by the UI alignment work, so it should be tracked.
- [ ] Run full build + test + type-check + frontend build; confirm EF migration drift is clean (`check-ef-drift.ps1`). → `docs/go-live-plan.md`

### WS1 — Go-live hardening / config fail-closed (P0)

- [ ] **Rotate and remove committed secrets.** A real LegiScan key sits in `appsettings.Development.json` — rotate it and remove it. Rotate `GraphApi:ClientSecret`. → `go-live-plan.md`, `deployment.md`
- [ ] **Startup validation (fail closed).** Extend `StartupConfigurationValidator` so staging/prod refuse to boot without: DB, Blob, QBO, Graph, AI/Doc-AI, CORS `AllowedOrigins`, webhook token, and a configured malware scanner. → `go-live-plan.md`
- [ ] **Malware scanning.** Either configure ClamAV or make the fallback to `NoOpFileScanService` an explicit, logged, environment-gated decision (not a silent default). → `go-live-plan.md`
- [ ] **NuGet dependency audit.** Resolve or formally accept the high-severity transitive advisories in `Microsoft.Bcl.Memory 9.0.0` and `Microsoft.Kiota.Abstractions 1.17.1`. → `go-live-plan.md`
- [ ] **Migration strategy.** Decide auto-`MigrateAsync()` on boot vs deploy-time migrations for staging/prod, and document it. → `go-live-plan.md`, `deployment.md`
- [ ] Remove the `Host=localhost;…;Password=postgres` design-time fallback in `SafetyAnalyticsDesignTimeDbContextFactory.cs` for parity with the hardened primary factory. → security catalog
- [ ] Configure Entra production/staging redirect URIs; remove dev IP from the Postgres firewall. → `deployment.md`

### WS2 — Authorization & data-scope closeout (P1)

The role/permission layer exists; the remaining work is **ownership/entity-scope** checks — making sure an authenticated user can only reach *their own* records. **A code-level audit on 2026-06-08 (§8.1) found the model is per-user ownership scope (`UserAccessScope`), not cross-tenant — so the residual risk is specific endpoints that bypass `ForAccessScope`.** Three of those are now confirmed (C1/H1/H2) and promoted below.

- [ ] **(P0 — C1) Fix UW Writeup broken object-level authorization.** `UWWriteupController` Get/Save/Submit are `[Authorize]`-only and `UWWriteupService` loads by `QuoteId` with no scope filter — any authenticated user can read/overwrite any quote's underwriting writeup by enumerating IDs. Thread `UserAccessScope` through `IUWWriteupService` and gate on parent-quote access. → §8.1
- [ ] **(P0 — H1) Add a fail-closed fallback authorization policy.** `Program.cs` registers per-permission policies but no `FallbackPolicy`; any unannotated controller is public by default. Add `RequireAuthenticatedUser()` fallback and explicitly `[AllowAnonymous]` the login/refresh/microsoft + webhook endpoints. → §8.1
- [ ] **(P1 — H2) Align the disbursement-void permission tier.** `VoidController` is gated `accounting.manage` while the parallel `DisbursementsController` void requires `accounting.admin` — a `manage`-tier user can void via the other route. Raise `VoidController` to `accounting.admin`. → §8.1
- [ ] Add entity-level ownership/scope checks to **submissions and all 11 submission child controllers, quotes, policies, accounting records, document-library items, and inbox documents** (the audit's named "highest-value next pass"). The §8.1 detail confirms reads on submissions/quotes/policies *are* correctly scoped via `CurrentAccess`; the remaining gaps are `QuoteChecklistController.GetForQuote` (M1) and the items above. → `docs/security/endpoint-authorization-audit.md`, §8.1
- [ ] Finish granular CRUD policies on core parties (insureds/agents/carriers) and review cross-entity access. → security catalog
- [ ] Add tests proving underwriters cannot reach `rating.admin` mutations; add refresh-token-reuse and inactive-external-user tests; confirm QBO webhook replay protection. → endpoint audit
- [ ] Decide the **party/workflow role model** (who manages insureds/agents/carriers/quotes/submissions/legal sources/attachments) — this is a business decision that unblocks the scoping work above. → **Open decision, §5**

### WS3 — Open bug fixes (P1/P2)

Financial/data-integrity bugs are already resolved. Remaining open items from the code-review backlog:

- [ ] **Email ingestion paging** — only the first 50 unread messages are processed; 51+ can starve. Add paging. (P1-Ops) → `ai-review/sims-code-review-backlog.md`
- [ ] **Invoice/receipt number concurrency** — `Count + 1` races under load; use a sequence/atomic allocation. (P2)
- [ ] **`NOT_FOUND` → returns 400 instead of 404** on quotes/submissions. (P2)
- [ ] **Paging validation + `SortBy` not honored** — invalid page/pageSize accepted; sort contract misleading, can 500. (P2)
- [ ] Decide **cancellation accounting rules** — should midterm cancellation completion require return premium, fee/tax reversal, commission chargeback, invoice, payable, ledger entries? Currently can complete with `PremiumChange = 0` and no accounting. → **Open decision, §5**

### WS4 — UI alignment (P1)

Two parts: dead/placeholder wiring (must be zero), and visual consistency with the SIMS UI guide. **The 2026-06-08 static route crawl (§8.2) confirmed all 6 tracked items are still open with exact file:line, found UI-DOC-001 is wider than recorded (three docs, one hardcoding `localhost:5000`), and surfaced three new issues — an unguarded `/tasks` route, a sidebar-vs-route guard mismatch, and no real 404 page.** Exact locations and fixes are in §8.2.

Broken links / placeholders — **all 6 currently open** (→ `docs/ui-broken-links-tracker.md`, `docs/superpowers/plans/2026-05-25-phase-4-ui-links-workflow-qa.md`):

- [ ] **UI-LINK-001** — bound-quote "View Policy" links to `/policies/${quote.id}` (quote id, wrong). Add `boundPolicyId` to the quote DTO from `Policy.BoundQuoteId`; only render when present.
- [ ] **UI-LINK-002** — QuickBooks journal link is `'#'`. Remove the anchor unless a real external URL is exposed.
- [ ] **UI-LINK-003** — Reports `renewals-upcoming`, `bound-by-period`, `hit-ratio-by-carrier` show "coming soon" but are clickable. Hide/disable or implement.
- [ ] **UI-LINK-004** — Dashboard Tasks card says "coming soon" though `/tasks` exists; `All →` has no handler. Wire to `/tasks`.
- [ ] **UI-LINK-005** — Insured detail Activity tab "coming soon." Hide/disable or wire real activity.
- [ ] **UI-DOC-001** — stale `VITE_API_URL` in **three** docs: `deployment.md` (L60, L96), `frontend.md` (L120, which also hardcodes `localhost:5000`), and `infrastructure.md` (L97). Client truth is relative `/api/v1` (`api/client.ts:5`). Rewrite all three.
- [ ] **(NEW) Guard the `/tasks` route** — `App.tsx:235` is the only top-level route with no `withPermission` wrapper; any authenticated user reaches the Task Queue. Wrap it (or confirm tasks are intentionally universal). → §8.2
- [ ] **(NEW) Fix sidebar-vs-route guard mismatch** — sidebar hides Billing/Reports/Task-admin behind a compound `nav.* && <action>` check (`usePermissions.ts:60-64`) but the routes check only the action half (`App.tsx:237-241,259-268`); a user with the action permission but not the `nav.*` flag can reach the page by URL. Make route guards match the sidebar. → §8.2
- [ ] **(NEW) Add a real 404 page** — `App.tsx:272` catch-all silently redirects every unknown URL to `/dashboard`, masking genuine broken links (including UI-LINK-001's bad id). Add a dedicated NotFound route. → §8.2

Visual consistency (High-priority pages still "needs tweaking" per `docs/ui-design-audit-plan.md`):

- [ ] **Login page** — never updated; first thing a tester sees. Highest priority.
- [ ] Quote detail, quote writeup, policy detail, agent detail, carrier detail, quote rating panel, auto-safety panel, admin rating-version page, fees admin, legal-requirements page.
- [ ] All billing pages (≈8/9) need the design-system pass.
- [ ] Run the **browser route crawl** across Admin / Underwriter / CSR / ReadOnly roles (the crawl table in the tracker is still empty) to catch any 404s, console errors, or role-leak routes before UAT.

### WS5 — Program / carrier alignment (P1 — the business-data gate)

This is the gap most likely to bite at live testing. The platform's program-setup machinery is built, but the **actual SMM programs are not all configured**, and the system currently has only **Beazley** seeded as a rating carrier and **Longleaf** as the live program. SMM runs six live program×carrier×LOB combinations plus one paused line. The unified plan must get the launch programs configured correctly, with the right limits/territories/rating bases, and AL explicitly **non-bindable**.

Reference for every value below: the SMM Underwriter program context (limits, territories, rating bases, referral triggers).

- [ ] **Decide launch scope** — which program(s) must be live for UAT day one. Recommend piloting **one** end-to-end first (Inland Marine via Beazley is the most rating-complete) and adding the others in sequence. → **Open decision, §5**
- [ ] Configure each launch **Program > Carrier > LOB > State** with correct eligible-state lists:
  - **Lloyd's IM — Beazley (AFB 623/2623):** states AL, AR, FL, GA, LA, MS, NC, OK, SC, TN, TX, VA; per-item $500k / per-loss $1.5M; ACV; 7 territory bands; min ded $1k.
  - **Lloyd's GL — DALE Syndicate 1729:** $1M occ / $2M agg; ISO loss-cost × LCM; CA pre-bind notice rules.
  - **Lloyd's APD — Longleaf® via HWS Specialty:** states AL, AR, GA, LA, MD, MS, PA, TX, VA; $150k TIV/unit (ACV); referral at ≥$150k single unit.
  - **Brace / Longleaf® GL:** states AL, AR, FL, GA, MS, NC, OK, SC, TN, TX; LCM 1.65; 12 eligible ISO classes.
  - **Brace / Longleaf® APD:** Stated Amount basis; up to $250k TIV/unit; flat rate table.
  - **Brace / Longleaf® IM:** 2 territory bands; min ded $2,500; per-loss $1M.
- [ ] **Auto Liability (AL): configure as inactive / non-bindable.** SMM lost the treaty Feb 2025; no binding authority. Ensure the system cannot quote or bind AL (block at clearance/authority layer), and flag AL submissions as pending. → SMM context §5
- [ ] Seed/verify per-program **rating assignments** beyond Beazley (the Carrier Rating Assignment UI, Phase 4A, unblocks non-Beazley carriers). → `rating-engine-remaining-plan.md`
- [ ] Verify per-program **fees, surplus-lines setup, policy-number sequences, form packages, proposal configs, carrier & agent commissions, QBO/GL mappings**. → `phase-7-program-setup-closeout.md`
- [ ] Run the **orphan / incomplete Program-setup audit report** (a documented pre-go-live Phase 7 item) to catch any program path with missing children. → `phase-7-program-setup-closeout.md`
- [ ] Resolve the known **historical-versioning gap**: Program setup has path-only unique constraints and cannot yet represent multiple historical intervals for a program path. Decide if this matters for UAT (likely defer, but document). → `specs/2026-05-30-program-sot-database-contract-design.md`

### WS6 — Rating engine readiness (P1/P2)

- [ ] **Shadow-rate cutover** — run the new engine in shadow mode against the Excel raters before it becomes authoritative; this is the safe path and gates trusting the engine in UAT. → `rating-engine-remaining-plan.md` (Phase 5)
- [ ] Set real **schedule-rating bounds** (currently placeholder 0.5–1.5) and **minimum premiums per program** (none seeded for IM; SMM context lists per-program minimums). → `rating-engine-remaining-plan.md`
- [ ] Confirm **renewal rate-version policy** (default: renewal-effective-date rates) and **endorsement rating policy** (default: bound version + pro-rata). → `rating-engine-remaining-plan.md` 7D/7E
- [ ] Second LOB rater (recommend APD next) — **blocked on the actuarial workbook handoff** (AL/APD/GL). Treat as post-UAT unless APD is in launch scope. → **Open decision / dependency, §5**
- [ ] (P2) Per-role schedule-modifier authority; rating worksheet PDF from snapshot. → `rating-engine-remaining-plan.md` 7B/7C

### WS7 — Bordereaux (conditional P1)

Bordereaux is a go-live blocker **only for launch carriers that require day-one reporting.** BRACE/Longleaf reports monthly (15 days after interval), so if Brace/Longleaf is in launch scope, this is required. Profiles are already Program-SOT complete; the run/validation/export/reconciliation pipeline is the open work.

- [ ] Premium-row preview (effective-or-bound date basis) + validation engine (missing policy#/state/txn-type/premium/tax/commission, unissued packet, unposted accounting, out-of-period). → `phase-8-bordereaux-carrier-reporting.md`
- [ ] CSV + XLSX export with signed download URLs; run history; paging for large periods. → `phase-8` slice 8.10
- [ ] **BRACE/Longleaf London premium BDX + Account Current from one dataset**, with the reconciliation gate (txn count/keys, gross premium, commission+brokerage vs AC gross, net vs net due carrier) and the required `Auto Veh Info` / `IM Unit Info` detail tabs. → `phase-8-london-bdx-account-current.md`
- [ ] Replace any coming-soon placeholder in Reports/Admin with the real surface (no placeholders allowed at go-live). → `phase-8`
- [ ] Report-template editor hardening so non-coders can change tabs/columns/static values/mapped fields/formulas. → `phase-8-london-bdx-account-current.md`

### WS8 — Deploy, burn-in, rehearsal, UAT (P1)

- [ ] **Deploy API + frontend to the Azure test environment.** Both are currently local-only; DB/KeyVault/Blob already on Azure. → `deployment.md`, `AGENTS.md`
- [ ] CI gates: make `deploy.yml` gate on `dotnet test`, `npx tsc --noEmit`, `npm run build`, `npm run lint`, dependency audit, and a smoke test. → `go-live-plan.md`
- [ ] **Fix the lint gate** — `npm run lint` currently fails (eslint not installed/configured). Make it a real, passing gate. → `go-live-plan.md`
- [ ] App Insights + health-check wiring in staging; QBO sandbox → production decision; Postgres backup retention → 35 days; HA decision; KeyVault managed identity; worker double-run check. → `deployment.md`, `go-live-plan.md`
- [ ] **Backup/rollback rehearsal** on staging. → `go-live-plan.md`
- [ ] **Full manual UAT script:** submission → quote → bind → issue (on the launch program), endorsement, cancellation, post-bind follow-up, accounting void approval, manager queue. → `phase-4-ui-links-workflow-qa.md`
- [ ] Stakeholder sign-off; staging burn-in with no open P0/P1. → `go-live-plan.md`

---

## 4. Reconciling overlaps & conflicts in the existing plans

These ambiguities exist across the doc set and are resolved here so they don't cause double-work:

- **"Phase 6" is overloaded** — it means both the roadmap's UW Control Layer *and* the AI plan's triage queue. In this plan, "Phase 6" = UW Control Layer (done). The AI triage queue is post-UAT.
- **Phase 6 control matrix vs closeout** — the matrix lists deterministic authority *thresholds* as "Missing" on most bind/issue/policy actions while the closeout declares Phase 6 done. **Decision:** authority thresholds on bind/issue/cancellation-complete are **post-UAT** for internal testing (the approval *spine* exists; thresholds are a tuning layer). Revisit before any external pilot.
- **Rating worksheet PDF, renewal/endorsement rate policy, per-role schedule authority** appear in both `rating-engine-remaining-plan.md` (7B–7E) and roadmap Phase 7A — **one backlog**, tracked in WS6.
- **AI plan exists in three forms** — the multi-model `ai-underwriting-plan.md` is authoritative; the Claude-only `SIMS_AI_Plan.docx` and the `SIMS_AI_Implementation_Guide.docx` are historical. Full AI (Doc AI extraction + scoring) is **out of scope for UAT** (Gemini/Doc-AI key not configured; feature is advisory-only and inactive). The guideline→control handoff that *is* live stays.
- **Founding docx plans** (`SMMIMS Plan 4.11.26`, `SMM_PolicyAdmin_*`) describe the April MVP (Neon DB, NetRate, IMS naming) and are **superseded** — historical only.
- **`VITE_API_URL`** stale guidance appears in four places; single fix (UI-DOC-001, WS4).

---

## 5. Open decisions needed from Jeremiah

These are business calls that gate the technical work; resolve early.

1. **Launch program scope** — which program(s) go live for UAT? (Recommend IM/Beazley first, then sequence.) Drives WS5, WS6, WS7.
2. **Bordereaux day-one?** — does the launch program require bordereaux at go-live? If Brace/Longleaf is in scope, yes → WS7 becomes P1.
3. **Party/workflow role model** — who can manage which entities? Unblocks WS2 scoping.
4. **Cancellation accounting rules** — must midterm cancellation completion post full accounting? → WS3.
5. **Actuarial workbook handoff** — when do AL/APD/GL rating workbooks arrive? Gates WS6 second-LOB rater.
6. **Authority thresholds** — confirm deferral of deterministic bind/issue thresholds to post-UAT.

---

## 6. Where agents can simplify this

This project already has an agent-based review harness (`docs/ai-review/runbook.md`) and a solo-dev Codex workflow (`AGENTS.md`). Lean on agents for the high-fan-out, well-bounded work:

- **Authorization audit (WS2)** — the `sims_security_auth_reviewer` agent is purpose-built: read-only, cites file/line, gives attack paths. Run it across the named controller surfaces to produce the ownership-scope gap list, then fix from its findings. Highest-leverage agent use in this plan.
- **UI route crawl (WS4)** — a browser agent (Claude in Chrome) can crawl every route per role, capturing 404s, console errors, dead buttons, and role-leak routes far faster than manual clicking. Feeds the empty "Route Crawl Findings" table directly.
- **Visual consistency sweep (WS4)** — the `sims_frontend_ui_reviewer` agent can diff each High-priority page against `SIMS-UI-Guide/tokens.css` and flag off-token colors/spacing in bulk, so the human pass is just confirmation.
- **Program-setup completeness (WS5)** — an agent can generate the orphan/incomplete Program-setup audit report and cross-check configured limits/territories against the SMM Underwriter context file, flagging mismatches per program×carrier×LOB.
- **Bug triage (WS3)** — the `sims_qa_test_coverage_reviewer` and `sims_data_ef_reviewer` agents can confirm which backlog items are truly open vs already fixed in the uncommitted tree, and propose tests.
- **Parallel reviewers + lead synthesizer** — run the 7 specialist reviewers read-only, then `sims_review_lead` to dedup/severity-rank into one punch list before each UAT cycle.

Keep agents **read-only for audits**; apply fixes deliberately and commit to `main` per the AGENTS.md protocol.

---

## 7. Sequenced path to live testing

A pragmatic ordering (dependencies, not calendar):

1. **WS0** — commit and stabilize the tree, green build/test. *(blocks everything)*
2. **WS1** — config fail-closed + secret rotation. *(launch blocker)*
3. **WS5 (one program)** + **WS6 shadow-rate** — get a single program rating correctly end-to-end. Run the security-auth agent (**WS2**) in parallel.
4. **WS4** — broken links to zero, Login + High-priority pages aligned, route crawl. **WS3** — open bugs.
5. **WS7** — only if the launch program needs day-one bordereaux.
6. **WS8** — deploy to Azure test, CI/lint gates, burn-in, full UAT script, sign-off.

Gate to "live testing" = §2 checklist fully satisfied with no open P0/P1.

---

## 8. Live audit findings (audited 2026-06-08)

These are the concrete results of the code-level security audit and the static route/links crawl. They feed WS2 and WS4. (Servers were not running, so the route crawl is a static code crawl — more complete than a click-through for catching wiring/guard issues.)

### 8.1 Security / authorization (code-level)

**Threat-model context (important):** SIMS is a **single-tenant, internal-staff** app. There is *no* agent/tenant claim on the JWT (`AuthService.GenerateAccessToken`) and `User` has no `AgentId`. So classic cross-agent IDOR does not apply. The real object model is **per-user ownership scope** via `UserAccessScope` / `BusinessDataAccess.ForAccessScope`: a user without `underwriting.manage` or `admin.system.manage` sees only records where they are `CreatedById`/`UnderwriterId`/`AssistantUWId`; users with `underwriting.manage` see all business data **by design**. The residual risk is therefore endpoints that **bypass `ForAccessScope`**, exposing data to lower-privilege authenticated users.

| ID | Sev | Finding | Location | Fix |
|---|---|---|---|---|
| **C1** | Critical | UW Writeup broken object-level authorization. Get/Save/Submit are `[Authorize]`-only; service loads by `QuoteId` with no scope filter (`SaveAsync` doesn't even take `userId`). Any authenticated user can read/overwrite any quote's underwriting writeup, conditions, and referral content by enumerating quote IDs. | `UWWriteupController.cs:18-39`; `UWWriteupService.cs:29,47,98` | Thread `UserAccessScope` into `IUWWriteupService`; verify parent-quote access (reuse `QuoteService.GetByIdAsync(quoteId, access)`) before load; `SaveAsync` must take/use `userId`. |
| **H1** | High | No fallback authorization policy → fail-open default. Per-permission policies are registered but no `FallbackPolicy`/`DefaultPolicy`; any controller/action a dev forgets to annotate is publicly reachable. | `Program.cs:59-66` | Add `FallbackPolicy = RequireAuthenticatedUser()`, then explicitly `[AllowAnonymous]` the public auth endpoints (login/microsoft/refresh) and the QBO webhook. |
| **H2** | High | Disbursement-void permission tier gap. `VoidController` is class-gated `accounting.manage`; the parallel `DisbursementsController` void requires `accounting.admin`. A `manage`-tier user can void disbursements (and receipts/cash-apps/invoices) via the Void route. | `VoidController.cs:15,63`; cf. `DisbursementsController.cs:57-59` | Raise `VoidController` to `accounting.admin`. (An authority-approval gate already compensates partially.) |
| **M1** | Medium | Quote checklist read not scope-checked — any authenticated user can read UW checklist/gating info for any quote. | `QuoteChecklistController.cs:25-30` | Pass `CurrentAccess` into `GetForQuoteAsync` (or front with the scoped quote lookup). |
| **M2** | Medium | Submissions/Quotes/Policies list+read are base `[Authorize]` (no `*.view` permission). **Not a true IDOR** — all correctly pass `CurrentAccess`. Flagged for least-privilege only. | `SubmissionsController.cs:38-47`, `QuotesController.cs:41-50`, `PoliciesController.cs:38-62` | Optionally add `policies.view`/`submissions.view` for defense-in-depth. |
| **M3** | Medium | No idempotency keys on accounting create/apply — a retried/double-clicked `CreateReceipt`/`Apply` can create duplicate ledger entries (double-void is already guarded). | `ReceiptsController.cs:30`, `CashApplicationController.cs:23`, `DisbursementsController.cs:39-50` | Add idempotency-key header + dedup table on create/apply. |
| **L1** | Low | DocumentGeneration merges entity PII before the object-level access check (final download IS access-controlled, so no leak — just early work). | `DocumentGenerationService.cs:30-100` | Call `CanAccessEntityAsync` as step 0, before building the data dictionary. |
| **L2** | Low | `[Authorize(Roles="Admin")]` still used despite the audit doc claiming none remain — bypasses the permission catalog. | `PoliciesController.cs:102-104`, `VoidController.cs:28` | Replace with a permission policy; update the audit doc. |

**Confirmed already correctly scoped (the "done" column):** `AttachmentService` object-level checks (the 2026-05-27 fix — solid: download/delete/list/upload across Submission/Policy/Carrier/Agent/Insured, returns 403); `PoliciesController`/`QuotesController` consistently pass `CurrentAccess`; all submission child controllers bind child→parent (`v.SubmissionId == submissionId`); `NotesController` fully scoped + per-action gated; Users/Insureds/Agents/Carriers method-level gated with typed DTOs (no mass-assignment); accounting reads `accounting.manage` / mutations `accounting.admin` (except H2); QBO webhook unauthenticated-by-design but HMAC-verified and rejects an unconfigured token; no `[AllowAnonymous]` anywhere; auth endpoints rate-limited.

**Top 3 before go-live: C1, H1, H2.**

### 8.2 Route + links crawl (static)

All six tracked broken-link items remain **open** with verified locations:

| ID | Still open? | Exact location | Fix |
|---|---|---|---|
| UI-LINK-001 | Yes | `SubmissionDetailPage.tsx:1260` builds `/policies/${q.id}` from the **quote** id; root cause `quote.types.ts:57-73` (`QuoteListItem` lacks `boundPolicyId`). | Add `boundPolicyId` to `QuoteListItem` + backend DTO; render link only when present. |
| UI-LINK-002 | Yes | `billing/ActivityPage.tsx:123` (`qbDeepLink = '#'`), rendered `:236-239`. | Remove the anchor unless a real `externalJournalUrl` is returned. |
| UI-LINK-003 | Yes | `reports/ReportsPage.tsx:1282-1284` (def), `:1347-1369` (clickable), `:1245` (msg). | `disabled`/`aria-disabled`, no select, until backed by data. |
| UI-LINK-004 | Yes | `dashboard/DashboardPage.tsx:516` (`All →` has no `onClick`), `:519` (stub text); `/tasks` exists. | `onClick={() => navigate('/tasks')}`; replace stub. |
| UI-LINK-005 | Yes | `insureds/InsuredDetailPage.tsx:230` (tab), `:606` (stub text). | Hide/disable tab until wired. |
| UI-DOC-001 | Yes (wider) | Client truth `api/client.ts:5` (`/api/v1`); stale docs `deployment.md:60,96`, `frontend.md:120` (+hardcoded `localhost:5000`), `infrastructure.md:97`. | Rewrite all three to the same-origin proxy. |

**New issues not in the tracker:**

1. **`/tasks` route unguarded** — `App.tsx:235` is the only top-level route without `withPermission`. Any authenticated user reaches the Task Queue. Wrap it, or confirm it's intentionally universal.
2. **Sidebar-vs-route guard mismatch** — sidebar uses compound `nav.* && <action>` (`usePermissions.ts:60-64`); routes check only the action half (`App.tsx:237-241,259-268`). A user with the action permission but not the `nav.*` flag can reach Billing/Reports/Task-admin by URL. Align the route guards.
3. **No real 404 page** — `App.tsx:272` catch-all redirects all unknown URLs to `/dashboard`, masking genuine broken links. Add a dedicated NotFound route.

**Route-guard coverage (good news):** all 60 page components have routes (no orphans/stubbed route targets); every `<Link>`/`navigate()` target except UI-LINK-001 resolves to a defined route; no other `href="#"`, empty `onClick`, or hardcoded `localhost` exists in `src`. Admin routes are correctly gated (`admin.system.manage`, `rating.admin`, etc.); `/dashboard` open is acceptable (it's the fallback landing).

---

## Appendix A — Source-plan crosswalk

| Workstream | Primary source plan(s) |
|---|---|
| WS0 stabilize tree | `AGENTS.md`, `CLAUDE.md`, `go-live-plan.md` |
| WS1 hardening/config | `go-live-plan.md`, `deployment.md`, `infrastructure.md` |
| WS2 authorization | `security/endpoint-authorization-audit.md`, `ai-review/sims-code-review-backlog.md` |
| WS3 bugs | `ai-review/sims-code-review-backlog.md` |
| WS4 UI alignment | `ui-broken-links-tracker.md`, `ui-design-audit-plan.md`, `superpowers/plans/2026-05-25-phase-4-ui-links-workflow-qa.md`, `SIMS-UI-Guide/` |
| WS5 program/carrier | `superpowers/plans/2026-05-25-phase-7-program-setup-closeout.md`, `superpowers/specs/2026-05-30-program-sot-database-contract-design.md`, SMM Underwriter context |
| WS6 rating | `rating-engine-plan.md`, `rating-engine-remaining-plan.md` |
| WS7 bordereaux | `superpowers/plans/2026-05-24-phase-8-bordereaux-carrier-reporting.md`, `superpowers/plans/2026-05-25-phase-8-london-bdx-account-current.md` |
| WS8 deploy/UAT | `go-live-plan.md`, `deployment.md`, `phase-4-ui-links-workflow-qa.md` |
| Reconciled / historical | `SIMS improvement 5.17.26.md`, `phase-6-*`, `ai-underwriting-plan.md`, `SIMS_AI_Plan.docx`, `SMM_PolicyAdmin_*`, `SMMIMS Plan 4.11.26.docx` |

## Appendix B — Out of scope for UAT (post-launch backlog)

Production reporting/dashboards (Phase 9), claims visibility (Phase 10), shared job/outbox/observability framework (Phase 11), full AI extraction + risk scoring + triage queue (AI plan Phases 1–7), FMCSA phases 2–7, compliance-doc module remaining build, document issuance automation beyond the IM pilot, and Program historical-interval versioning.
