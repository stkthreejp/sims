# SIMS — Unified Go-Live Plan (Internal UAT / Staging → Live Business)

> **Owner:** Jeremiah O'Donovan · **Created:** 2026-06-08 · **Reaudited & restructured:** 2026-06-10 · **Merged with the 13-lane setup audit:** 2026-07-04 · **Targets:** (1) Internal UAT on staging; (2) **Live business** — real submissions, binds, issuance, premium accounting, carrier reporting, and regulatory compliance.
>
> **What this is:** the single coordination doc. The 2026-07-04 edition reconciles the 6-10 plan against actual code (23 stale claims corrected — Gate A was much closer than recorded) and merges the setup audit: 13 parallel review lanes + 18-claim adversarial verification (16 CONFIRMED / 2 PARTIAL / 0 refuted). **Finding-level detail lives in `docs/SETUP-AUDIT-2026-07-04.md`** (IDs like A1.2/B4/W7 below refer to it). The WS5 execution checklist is `docs/WS5-COMPANY-SETUP-TEST-CHECKLIST.md`; the running WS5 fix queue is `docs/WS5-FINDINGS.md`.
>
> **How to use it:** Work top-to-bottom by workstream; §7 is the sequence. P0 blocks the stated gate; P1 before that gate is exited; P2 during/after. Gates: **Gate A (internal UAT)**, **Gate B (live business)** — §2.

---

## 1. Where SIMS actually is (verified against code, 2026-07-04)

**Complete and verified (do not re-plan):**

- **Program configuration + Program-SOT contract** — nested Program > Carrier > LOB > State with canonical FKs enforced across fees, bordereaux profiles, surplus lines, form packages, proposal configs, policy numbers, commissions, rating assignments. Orphan-audit endpoint **now has a UI button** (Batch 0). Trigger violations now surface as **409s with the rule message** (P0001/23505/23503 exception mapper, Batch 0) instead of generic 500s.
- **Underwriting control layer** — clearance, referrals, published controls, stage-aware checklists, post-bind gate, authority-approval spine, manager queue. *(Caveat: Bind-stage checklist blockers and writeup Required conditions are NOT enforced at bind — WS13a.)*
- **Rating engine** — **IM_v1 + GL_v2** (GL_v1 retired 2026-07-01; GL cut over directly to authoritative with golden-value verification, not via shadow toggles). AI/WOS/PNC via the global engine with count-at-quote. Versioning, bind-locked snapshots, per-LOB shadow toggles *(shadow rater is hard-coded to IM — GL/AL/APD toggles are non-functional; WS6)*. **Corrections queued (WS5-R Batch 4): IM territory modifiers are seeded but never applied (~47–91% underrating), IM endorsement charges contradict the workbook, minimum premiums are NOT seeded and GL_v2 ignores the field, parity fixtures are formula-derived rather than workbook-derived.**
- **Policy lifecycle** — endorse / cancel / reinstate / rewrite / non-renew / renew endpoints; cancellation notice flow with effective-date math and legal-requirement snapshots. *(Reinstatement books zero accounting — joins WS11 scope. Endorsement issuance and start-renewal have no UI callers — WS5-R Batch 3.)*
- **Policy issuance documents** — `PolicyAssemblyService` end-to-end: state-scoped form packages, Mandatory/Conditional triggers, PDF fill/merge, `IssuedPolicyPacket` with SHA-256 + version/transaction linkage.
- **Bordereaux pipeline** — profiles admin (readiness = required tabs + UMR statics; YoA auto-derived from transaction effective year; reconciliation optional for the London flow per F8), premium preview, frozen snapshot runs, validation engine, CSV/XLSX export, London BDX + Account Current, Workbench UI. (Integrity gaps: §WS7.)
- **Production reports** — all six live: renewals-upcoming, bound-by-period, hit-ratio-by-carrier, **written premium, submission pipeline funnel, UW workload** (bdd20ac).
- **Claims (WS10)** — backend **and frontend** done: scoped reads/writes, batch-keyed valuations, import hardening, claims list/import UI, loss-run downloads + CSV endpoint. Only operational data loads remain.
- **Accounting core** — invoice → receipt → cash application → disbursement chain, double-entry ledger, trust account, distribution sweeps, reconciliation + aging reports, atomic numbering, **live Xero integration** (QBO replaced, 46fa34f). *(Xero gaps: journal export can double-post on resync; pending_journal_syncs retry queue is dead code; GL account map has no admin UI — WS5-R/WS8.)*
- **Security closeout** — C1/H1/H2/M1/L2 **and ClaimsService scoping** fixed and verified. AuthController `[AllowAnonymous]` scoped; **M3 idempotency server half** live (client header not yet sent — WS2). Refresh-token-reuse revocation and inactive-user blocks correct.
- **UI closeout** — WS4 + WS4-R fully closed (four API clients, BDX CSV auth download, Tasks card, sidebar parity, 404, Login).
- **CI/deploy** — deploy.yml gates on backend build+tests, tsc, lint, frontend build, dependency audit, **and post-deploy smoke tests against the real regional hostnames**. App Insights SDK wired (2a76e86) — **connection string not yet configured, telemetry silently no-ops (WS8)**. Startup validator: JWT/DB/Blob/**Xero×3**/Graph secret/origins/malware provider. *(The old QBO-sandbox prod guard was deleted in the swap and needs a Xero replacement — WS8.)*

**The frontier (2026-07-04):** two fronts. (1) **Financial correctness for live business** — return premium + reinstatement, SL fail-closed, BDX once-only, notices (WS11–WS13, unchanged P0 spine of Gate B). (2) **Setup integrity** — the audit's fail-open family (commissions→0%, policy numbers→legacy fallback, fee CalcType→$0), lifecycle-integrity family (soft-delete/unique-index 500s, unguarded deactivation/deletion), dead admin knobs, and seed defects — now owned by **WS5-R** and sequenced against the Part A checklist.

---

## 2. Definition of ready — two gates

### Gate A — Internal UAT (staff testing on staging)

1. ~~Working tree committed; CI green; EF drift clean.~~ ✅ (~509 backend tests + vuln audit + post-deploy smoke)
2. ~~Config fails closed in staging; operational items.~~ ✅ (punch list done 2026-06-11; Postgres firewall deferred to VNet)
3. ~~Backend authorization passes ownership-scope audit.~~ ✅ (incl. ClaimsService scoping)
4. ~~Broken-link/placeholder list zero; High-priority pages match the UI guide.~~ ✅
5. One full program configured end-to-end and bindable. ❌ **open — WS5 Part A (GL/DALE) in progress; Batch-0 setup blockers cleared 2026-07-04**
6. ~~Production visibility for the launch program.~~ ✅ (six reports live)
7. Loss-run capability. ◐ code done; **operational**: Sedgwick import + role grants
8. Deployed with health checks; backup/rollback rehearsal. ◐ deployed + healthy + smoke-tested; **open: rehearsal, App Insights connection string**
9. CI gates green; burn-in with no open P0/P1. ◐ gates green; burn-in pending
10. ~~Four broken frontend API clients fixed.~~ ✅

**Also before UAT exit (not gating UAT start): WS5-R Batches 1–3** — fail-closed money/identity, lifecycle integrity, UI wiring (§WS5-R).

### Gate B — Live business

Everything in Gate A, plus:

1. **Return premium exists** — midterm/flat cancellation and negative endorsement produce credit invoice, payable reduction, SL-tax reversal, commission chargeback, balanced ledger, negative BDX row. **Scope extended 2026-07-04: reinstatement books the accounting mirror (re-charge premium/tax/commission, RN row on BDX)** — today it books zero. (§WS11)
2. **SL tax fails closed** — filing-state bind with no SL tax + stamping lines is blocked; `SlHomeState` drives calc; **`FilingRequired` config wired to `quote.IsFilingState`**. (§WS12)
3. **Single source of premium truth** — dec page = ledger = BDX. (§WS11)
4. **Once-and-only-once carrier reporting** — BDX run stamping + mark-submitted closure. (§WS7)
5. **Cancellation notices compliant** — statutory minimums enforced from the in-system chart (**Kentucky chart data + state-normalization fix required first**); proof-of-mailing; additional-interest copies. (§WS13)
6. **Claims scoped and correct.** ✅ code-side; operational loads remain. (§WS10)
7. **Staff can operate daily workflows in the UI** — claims ✅, BDX CSV ✅; **open: mark-submitted (WS7), endorsement-issue + start-renewal buttons (WS5-R Batch 3)**.
8. **Producer licensing** — model built (F9a ✅); **open: bind-time hard block for the risk state + expiration report**. (§WS13)
9. **NEW — Binding-authority capacity**: written premium tracked against binder aggregates; at minimum a monitored utilization report + SOP before first live bind, bind-time guard by end of Gate B. (§WS15)
10. **NEW — Sanctions screening**: documented OFAC screening SOP with evidence captured at bind/payee-creation (build automation post-UAT). (§WS13a)
11. **NEW — Subjectivities enforced at bind**: Bind-stage blocker checklist items actually block; open subjectivities render on the binder. (§WS13a)
12. **NEW — Quote validity**: Declined/stale quotes cannot bind; effective-date changes at bind force re-rate; validity window per program. (§WS5-R Batch 1 / Q10)

---

## 3. Workstreams

### WS0 — Stabilize the working tree ✅ DONE
CI green (~509 backend tests, tsc, lint, build, vuln audit, post-deploy smoke), EF drift clean.

### WS1 — Config hardening — ✅ round 1 done / round 2 folded into WS8 ops
Round 1 complete (validator fails closed on JWT/DB/Blob/Xero×3/Graph/origins/malware provider; secrets out of source; punch list done 2026-06-11 except Postgres firewall→VNet). **Stale-claim corrections:** validator no longer checks QBO anything; `Qbo__WebhookVerifierToken` app setting is dead — remove; `XeroSettings.WebhookKey` is bound but no webhook endpoint exists — drop or build. Round-2 items (Xero prod-org guard, malware whitelist, mailbox validation, AllowedHosts, Key Vault logging, API-key log hygiene) are listed under **WS8 ops** so there is one ops punch list.

### WS2 — Authorization & data-scope — ✅ P0/P1 closed / P2 backlog extended by audit
Done: C1, H1, H2, M1, L2, ClaimsService scoping, AuthController `[AllowAnonymous]` scoping, M3 idempotency **server half** (filter + store + `[Idempotent]` on the three billing POSTs).
- [ ] **(P1) DocumentTemplatesController write guard** (S1 ✓) — POST/PUT/DELETE have no permission policy; any authenticated user (incl. ReadOnly) can alter/delete the templates behind issued documents. Add admin policy or a document-library-manage permission.
- [ ] (P1) **Client idempotency headers** (A3.4 ✓) — no frontend client sends `Idempotency-Key`, so M3 is inert end-to-end (and the axios 401-refresh interceptor silently replays POSTs). Generate a UUID per mutation in receipts/cash-application/disbursements clients.
- [ ] (P2) **Guard parity**: carrier AI-rates writable at `UnderwritingManage` via the carrier-scoped controller vs `AdminSystemManage` on the admin surface (S2); `CreateContactLog` unguarded (S3).
- [ ] (P2) **M2 least-privilege tier** (S4): PN sequences / form packages / proposal configs gated at broad `UnderwritingManage` — any UW can reset `NextNumber`. Decide the admin tier and align (also fixes the `/admin/policy-forms`+`/admin/policy-numbers` route-guard inconsistency, UI15).
- [ ] (P2) Entity-scope on the 11 submission child controllers; inbound-email stance. (Role model decided §5.8.)
- [ ] (P2) Security regression tests (Q4 — confirmed still absent): refresh-token-reuse, inactive-user-on-refresh, underwriter-role-seed assertions. Delete the dead `IsInRole("Admin")` in `VoidController:28` **and replace the load-bearing one in `ActivityController:17` with a permission policy** (PD8).
- [ ] (P2) `admin.roles.view` is grantable but never enforced (H12) — wire a read-only roles page or delete the permission.

### WS3 — Open bug fixes — ✅ DONE
Residual NOT_FOUND→400 convention drift across setup controllers (B9) moved to WS5-R Batch 2.

### WS4 / WS4-R — UI alignment & frontend repair — ✅ DONE
- [ ] (P2, 5-min doc task) `docs/ui-broken-links-tracker.md` rows still say "Open" for six fixed items (PD9) — flip to Fixed or deprecate the tracker.

### WS5 — Program / carrier setup — ❌ the open Gate-A blocker (Part A in progress)
Staged GL-first (decided 2026-07-02): **Part A = Lloyd's GL (DALE 1729/Brace)** per the checklist; Part B (Beazley IM) deferred until Part A is clean. Gate-A item 5 flips on Part A alone.
- **2026-07-04: Batch-0 blockers cleared** (commit 20cfba6): payee create path (Phase 4 was unexecutable), orphan-audit UI (Phase 1), BDX profile phantom enums (Phase 7), P0001→409 mapper, staging email sink (T6 protection; `Email__RedirectAllTo` set on sims-api-test), program-config options endpoint for non-admin pickers.
- [ ] Configure the GL program end-to-end (Phases 0–9), run lifecycle tests T1–T18, per-state matrix.
- [ ] Run the orphan audit; resolve every finding. *(Audit currently checks hierarchy emptiness only — completeness expansion is WS5-R Batch 2 A2.6.)*
- [ ] Per launch state: SL tax-assertion check (T7) and diligent-effort research → flags.
- [ ] **Checklist corrections** (audit-confirmed counterfactuals): Phase 8/T9 — with Xero unbound, a rollup export marks **Failed** ("Xero is not configured"); no `pending_journal_syncs` rows appear (retry queue is dead code — WS8). Part B Phase 2 — the Beazley IM rating assignment seed was a conditional no-op on a fresh DB; **create it if absent**.
- [ ] Part B (IM) after Part A is clean **and WS5-R Batch 4 IM corrections land** (territory, endorsement charges, AI rates, deductible guard — otherwise the golden values cannot pass).
- Findings log: `docs/WS5-FINDINGS.md` (F1–F11 shipped in batches b6e7d3f, 2db0a04, d65e282, cd23a70, 0612229).

### WS5-R — Setup hardening & repair (NEW 2026-07-04; owns the audit fix batches)
Full finding detail: `docs/SETUP-AUDIT-2026-07-04.md`. Engineering rules adopted: (i) dual enforcement — change C# and trigger together; (ii) money/identity lookups fail closed; (iii) no dead admin knobs — wire or hide; (iv) filtered unique indexes wherever soft-delete exists.

**Batch 0 — ✅ DONE 2026-07-04 (20cfba6):** payee CRUD; P0001/23505/23503→409 middleware; orphan-audit UI; BDX enum fix + query-key fix; email sink (+ app setting); program-config options endpoint.

**Batch 1 — fail-open money & identity (P1, before bind tests T7–T9):**
- [ ] Commission fail-closed (A1.1, decided Q2): validate LOB on unscoped paths; `COMMISSION_SCHEDULE_MISSING` block at bind with explicit 0%-rate rows as the rare-case path; `ACTIVE_LOBS` in the agent picker; complete label maps.
- [ ] Policy-number fail-closed (A1.2): block program-scoped binds with no assignment; drop/validate `WritingCompanyId`; guard sequence delete; `NextNumber ≥ max(usage)+1`.
- [ ] Fee-engine traps (A1.3): whitelist CalcType/FeeCategory (+CHECK constraints); validate FKs; hide-or-implement PercentOfNet ✓, LicenseType/City scope, OnlyAppliesToIssuanceState, AppliesToFlatCancellations (Q5 pending); normalize taxability states; fix the taxability-wipe + `ledgerAccountId:0` UI paths (UI8).
- [ ] Bind guards (A1.4 ✓): status whitelist (Declined quotes currently bind via API); `RERATE_REQUIRED` on effective-date change; re-validate program path at bind; quote LOB ∈ submission LOBs (closes the AL non-bindable bypass); quote-validity window (Q10).
- [ ] **Require program on quotes** (A1.5 ✓, decided Q1): ProgramId mandatory at create + bind; remove the "No program" option (null-program policies can never reach a bordereau).

**Batch 2 — setup lifecycle integrity (P1):**
- [ ] Filtered unique indexes + duplicate checks (A2.1 ✓: rating assignments, carriers, PN sequences) + per-module create-delete-recreate tests.
- [ ] Carrier edit/delete guards (A2.2): `CARRIER_LOB_IN_USE`; delete blocked by program/policy/commission references. Same reference-count pattern for agents, sequences, form templates (D8).
- [ ] Rating assignment guards (A2.3): `Status==Active` on create/update; retire 409 when referenced; update-path overlap re-resolution (companion to F3/F5).
- [ ] Program deactivation semantics (A2.4, Q3 pending): C# pre-checks with clean errors; fee triggers honor `DisabledDate`; today a program with any program-scoped fee rule can never be deactivated.
- [ ] As-of-today→overlap semantics for BDX profiles, policy packages, PN assignments (A2.5 ✓ partial — proposal docs already correct), C# + triggers together.
- [ ] Orphan-audit completeness expansion (A2.6): per-active-path checks (rating resolvable, commission ≥1, PN assignment, ≥1 package w/ Mandatory form, SL setup when filing, BDX profile) — turns the checklist into a machine gate.
- [ ] Referential/validation sweep (A2.7): B7–B14, W13, W15 (NOT_FOUND mapper, AI-rate validation, state-code whitelist, `Enum.IsDefined`, role-permission silent drop, package/template scope checks, form-trigger path validation).

**Batch 3 — UI wiring & consistency (P1/P2, before the lifecycle tests they serve):**
- [ ] Endorsement **Issue** button (A3.1 ✓ — T10/T11 unpassable without it); Start-Renewal button + `RenewalBehavior` numbering (A3.2 — T13); Xero GL-account-map admin (A3.3 — T9).
- [ ] Picker/form fixes (A3.5): Add-LOB carrier-capability filter (deferred F1 half — data already on the page); copy-state source list; PN admin LOB∩carrier + inactive markers + delete confirms; UW-controls scope through the program spine; shared `US_STATES` constant (8 copies, insured pages omit DC); form-trigger boolean fields (conditional forms can never fire today); manual-invoice LOB select; dead template tags; `AutoLiabilityNonBindable` label; UW picker active-user filter; program-switch edit-state reset; commission error toasts.
- [ ] Policy-expiry sweep + endorsement date guard (M7/H9 — S, cheap, stops Active-forever policies during UAT).

**Batch 4 — rating & seed corrections (P1; A4.5 during Part A, the rest before Part B):**
- [ ] **IM territory modifiers** (A4.1 ✓ — engine ignores them entirely; ~47–91% underrating; fix formula + call sites, regenerate fixtures from the workbook incl. whole-dollar rounding).
- [ ] IM endorsement charges → workbook values $250/$250/$500 (A4.2, adjudicated) + fix the `Premium>0` no-line filter; move to a versioned factor table.
- [ ] IM AI-rate seed decision + rows (A4.3, Q8); deductible factor 0.00 → reject as ineligible (A4.4).
- [ ] **Kentucky legal chart** (A4.5 ✓): add KY to both Oden seeds **and** the `NormalizeState` map (seed rows alone are insufficient).
- [ ] Minimum-premium decision (A4.7, plan §1 claim was false): confirm workbook intent; wire GL_v2 or hide the field.
- [ ] COA per-state accounts for MD/KY tax + MS/NC/PA stamping (A4.8); GL expansion-state confirmation — **NC outstanding** (Q9 ◐); housekeeping (A4.9: Beazley-assignment checklist step, no in-place reseeds of Active versions, PolicyNumber index filter casing + fresh-DB CI step, PN `FOR UPDATE` concurrency, BDX profile unique-index/cascade migration).

**Batch T — pinning tests (P2, alongside the batches):** trigger-overlap migration test (Q1), PN sequence validation + preview-vs-bind parity (Q2), Update-LOB branch of F1 (Q3), London YoA assertion (Q5). *(Security trio lives in WS2.)*

### WS6 — Rating — ✅ GL_v2 authoritative / IM corrections pending / cutover rescoped
- GL_v2 went authoritative 2026-07-01 with golden values; **the shadow-cutover item now applies to IM only**. GL/AL/APD shadow toggles are non-functional (shadow rater hard-coded to IM_v1) — disable them in UI/API or make the shadow service dispatch by formula (T8); delete the dead `Rating:ShadowMode` appsettings key.
- IM_v1 correctness items: **WS5-R Batch 4** (territory, endorsement charges, deductible, AI rates, fixtures).
- [ ] Confirm renewal rate-version + endorsement rating policy defaults.
- [ ] Second LOB rater — blocked on actuarial workbook handoff (post-UAT).
- [ ] Rate workbench (post-UAT, decided 2026-07-02); interim rate changes via the `backend/seed/rating/gl_v2` runbook — **new versions only, never mutate a version with rated/bound quotes** (the GL_v2 seed's in-place reseed of v1 must not recur — D14).

### WS7 — Bordereaux — ✅ pipeline done / ❌ reporting-integrity gaps (P0 for Gate B)
Ground-truth annotations (2026-07-04): profile readiness = required tabs + UMR statics; YoA auto-derived from transaction effective year (F2 interim); reconciliation optional for the London flow (F8); profile creation no longer requires tabs/columns up front (F4).
- [ ] **(P0) Once-and-only-once ledger** — stamp `BordereauxRunId` at submission; next preview = "≤ periodEnd AND not yet reported"; unreported-prior-items validation row. Test: late invoice appears next run; nothing appears on two submitted runs.
- [ ] **(P0, lands with WS11)** Return-premium (and reinstatement) rows flow onto the BDX automatically.
- [ ] (P1) Mark-submitted closure state in the Workbench (the stamping hook).
- [ ] (P2) `requireReconciliation` flag is decorative — remove from default profile (F8 cleanup). BDX profile unique-index embeds mutable `IsActive` + runs cascade-delete from profiles — migration (D13, in WS5-R Batch 4 housekeeping).
- [ ] (P2) Carrier settlement netting — manual at launch.
- Post-launch: **Binder (binding-authority period) entity** — per program/carrier/LOB with UMR/section/YoA resolving from the binder period (WS14; resolves the UMR dual-source and F2 end-state).

### WS8 — Deploy, burn-in, UAT, ops — ◐ (ops punch list round 2)
Done: App Insights SDK, deploy.yml backend-build/tests/audit/smoke (2a76e86, 27f46b9, 7a8625e). Set 2026-07-04: `Email__RedirectAllTo` (staging mail sink), `AppSettings__FrontendBaseUrl` (task-email links were localhost).
- [ ] **App Insights connection string** on sims-api-test (SDK silently no-ops without it — telemetry is currently OFF despite the wiring; T10 ✓-adjacent).
- [ ] **Xero**: per-journal `Idempotency-Key` + per-transaction posted tracking (double-post on resync — X1 ✓ P1); wire the `pending_journal_syncs` enqueue or delete the worker + fix checklist wording (X2 ✓); **prod-org guard** replacing the deleted QBO-sandbox guard (T4); update `docs/deployment.md` secret table (still documents QBO, omits Xero — X5).
- [ ] **Config round 2**: malware-provider value whitelist + prod-NoOp acknowledgment (T5); `GraphApi:MailboxAddress` placeholder validation (T2); AllowedHosts documented/normalized (T9); Key Vault failure logging + per-env vault decision before prod stand-up (T1 ✓ P2-until-prod); auth rate limiter per-IP (T6); `System.Net.Http: Warning` + API keys out of query strings (X3 ✓ partial — keys currently in container logs); empty-Gemini-key guard.
- [ ] Notification double-send: per-send audit saves in TaskNotificationService (X7 — the "worker double-run check").
- [ ] Backup/rollback rehearsal; Xero production-organisation connection decision; KeyVault managed identity confirmation.
- [ ] Full manual UAT script (submission → quote → bind → issue; endorsement **via the new Issue button**; cancellation (+return premium once WS11 lands); void approval; manager queue; BDX month-end; claims import + loss run).
- [ ] Burn-in, sign-off.
- (P2) Dead/inert surfaces — wire or hide (A5.9): workflow `system_events` seed, `PaymentTermsDays`/`BillingMode` consumption, LegiScan panel + permission, AI-settings unconsumed knobs, `UserDelegation` CRUD.

### WS9 — Production reporting — ✅ DONE (all six live)

### WS10 — Claims & loss runs — ✅ DONE except operational
- [ ] Import the launch program's current claims (Sedgwick) + first historical load; grant `claims.view`/`claims.manage` in Role Permissions.

---

## 3b. Live-business workstreams

### WS11 — Return premium & financial integrity (P0, critical path to Gate B)
Unchanged core build (earned-premium calculator pro-rata/short-rate/flat, method-selection UX + authority approval, credit-invoice path with commission chargeback + SL reversal, fee-engine negative guards, premium single-source-of-truth) — see §5.1/5.2 decisions. **Scope extensions (2026-07-04):**
- [ ] **Reinstatement accounting** (M1 ✓): the accounting mirror of the cancellation credit — re-charge premium/SL tax/stamping/commission from the reinstatement date (lapse handling: open question), invoice tied to the RN transaction so it reaches the BDX. Regression: cancel→reinstate nets to zero; CN then RN rows on the BDX.
- [ ] **Intermediary `CreatePayable` branch** (F10/PD15): direct-payable-to-broker when the flag is on (netted London flow needs nothing).
- [ ] Regression list additionally asserts: commission chargeback at the rate paid; `AppliesToFlatCancellations` semantics per Q5 once decided.

### WS12 — Surplus-lines compliance (P0 fail-closed + P1 filing surface)
Unchanged items (fail-closed SL tax with `SlHomeState`; SL document merge; diligent-effort bind blocker; SL filing report). **Additions (2026-07-04):**
- [ ] Wire `SurplusLinesStateSetup.FilingRequired` → default `quote.IsFilingState`, block bind on mismatch (W17 — the specific wiring the plan implied).
- [ ] SL fee-link **category check** (W19): the linked tax/stamping fee definitions must be category Tax/StampingFee so the fail-closed assertion can't be satisfied by the wrong fee.
- [ ] Note: the fee-engine LicenseType trap that silently removes SL tax is fixed in WS5-R Batch 1.

### WS13 — Issuance & notice compliance (P1)
- [ ] Cancellation notices: enforce statutory minimums from the chart — **prerequisite: Kentucky chart rows + `NormalizeState` "KY" mapping (WS5-R Batch 4 A4.5 ✓)**; proof-of-mailing on `PolicyCancellationDetail`; additional-interest copies.
- [ ] Endorsement documents; binder/certificate at bind (decided §5.5); mandatory-form server guard.
- [ ] **Producer licensing**: model ✅ DONE (F9a — per-state licenses w/ expirations, E&O fields, continuous broker agreement, admin UI, `IsQuoteReady`). Remaining: **bind-time hard block** keyed to the risk state via the clearance/control layer + expiration report; decide whether E&O limit/carrier become required; F9b attachments post-UAT. *(Until the block lands, `IsQuoteReady` is display-only — set UAT expectations accordingly, W14.)*

### WS13a — Bind-integrity & compliance additions (NEW 2026-07-04, P1 Gate B)
- [ ] **Subjectivities enforced at bind** (M5): mirror the PostBind gate — `BIND_REQUIREMENTS_INCOMPLETE` for incomplete Bind-stage `IsBlocker` checklist items, authority-approval override; enforce (or fold in) writeup Required conditions; render open subjectivities on the WS13 binder.
- [ ] **OFAC/sanctions screening** (M4): Gate-B minimum = documented manual SOP + evidence on `ComplianceEvidence` at bind/payee creation; build the SDN screening service + clearance hook post-UAT (vendor vs Treasury list: open).
- [ ] Settlement-currency whitelist (M11): restrict `DefaultCurrencyCode` to USD + BDX validation row (accounting is single-currency; confirm both binders settle USD).

### WS15 — Binding-authority capacity controls (NEW 2026-07-04, Gate B)
Nothing in SIMS tracks written premium against binder authority (M2). Lloyd's binders carry section premium-income limits, per-state/class restrictions, and max line sizes — breaching them is a breach of authority and today would be invisible.
- [ ] Capacity config on ProgramCarrier/section (aggregate premium per LOB, optional per-state caps, max line per risk) — populate from the DALE and Beazley binder schedules.
- [ ] Accumulate posted invoice premium (net of returns post-WS11) against the aggregate; bind-time warn (~85%) and block/refer-to-authority-queue (100%); utilization report.
- [ ] **Interim before first live bind**: written-premium report monitored against the binder schedule + documented SOP.
- Open: thresholds, gross vs net basis, exact binder terms (§5 Q-list).

### WS14 — Post-launch backlog (P2)
Prior list unchanged (PFC workflows, renewal worker, FNOL, treaty model pre-Brace, retention SOP, agent statements, installments). **Additions (2026-07-04):** Binder (binding-authority period) entity (F2 end-state/PD14); premium-audit provisions for payroll-rated GL (Q11 — decide before first expiries; policy wording flag earlier); mid-term BOR/producer-of-record change (M8 — stamp AgentId on invoices when built); per-policy UW-file export for coverholder audits (M9); inspections/loss-control decision (Q12, interim = PostBind checklist items); per-environment Key Vault before prod.

---

## 5. Decisions

Resolved earlier: launch scope; BDX day one; §5.1–5.9 (earned-premium ruleset, fees fully earned, SlHomeState, diligent effort, binder-at-bind, producer licensing hard block, claims valuation cadence, tiered role model, day-one reports).

**Resolved 2026-07-04:**
- **Q1 Program required** — always require a program on quotes; remove "No program". (WS5-R Batch 1)
- **Q2 Zero commission** — rare but legitimate (agent + SMM concede to win an account): absence fails closed; deliberate 0%-rate schedule rows are the supported path.
- **Q9 GL expansion states** — PA/MD/VA/KY intentionally rate on the GA loss-cost column. **NC not yet confirmed** (it is in the same GA-copy set).

**Open (full context in SETUP-AUDIT §11):**
- Q3 program sunset semantics · Q4 legacy `POL-` fallback intent · Q5 `AppliesToFlatCancellations` meaning · Q6 `WritingCompanyId` model-or-drop · Q7 quote LOB vs submission LOBs · Q8 IM AI tariff · Q10 quote-validity days/basis · Q11 premium audits mandated? · Q12 inspections mandated? · NC loss-cost confirm · payment-terms home (per-program-LOB vs carrier default, PD16) · reinstatement lapse handling · capacity thresholds + basis · OFAC vendor vs Treasury list.

---

## 7. Sequenced path

**To Gate A (internal UAT):**
1. **WS5 Part A** (GL) per the checklist — resume from Phase 1 now that Batch 0 is deployed.
2. **WS5-R Batch 1** (fail-closed money/identity + bind guards + require-program) **before the T7–T9 bind tests**; Batch 2 alongside; Batch 3 before T10/T13; Batch 4 A4.5 (KY) during the per-state matrix.
3. **WS8**: App Insights connection string; rollback rehearsal; UAT script.
4. Burn-in with no open P0/P1.

**To Gate B (live business):**
5. **WS11** return premium + reinstatement ← critical path.
6. **WS12** SL fail-closed + wiring (small, with WS11) + filing report.
7. **WS7** BDX once-only + mark-submitted (before the first real DALE submission).
8. **WS13/WS13a** notices (post-KY-fix) + binder + licensing bind-block + subjectivities gate + OFAC SOP.
9. **WS15** capacity: interim report + SOP before first live bind; guard build in parallel.
10. **WS5-R Batch 4** IM corrections → **WS5 Part B** (Beazley IM) → orphan audit re-run → WS5 fully closed.
11. **WS10** operational loads; **WS8** Xero hardening before first live month-end.

Gate B = §2 checklist fully satisfied with no open P0/P1.

---

## 8. Audit history

- **8.0 / 8.1 / 8.2 (2026-06-08 and 2026-06-10)** — historical. Every High/P1 row in the 6-10 reaudit table was closed by 2026-06-15 (b9b3704, 2e79b95, f46af13, 2a76e86); statuses live in §3.
- **8.3 (2026-07-04) — setup audit**: 13 parallel review lanes (backend validation, frontend pickers, workflow coherence, data/EF, security, integrations/Xero, enum drift, QA, plan drift, missing features, toggles/defaults, seed coherence, half-built) → ~125 findings deduplicated; 18-claim adversarial verification: **16 CONFIRMED / 2 PARTIAL / 0 refuted**. Headline classes: fail-open money/identity lookups; unguarded setup lifecycle edits; dead admin knobs; rating/seed defects (IM territory, KY legal chart); ops-config gaps. All merged into WS2/WS5-R/WS6/WS7/WS8/WS11–WS13a/WS15 above. **Full detail: `docs/SETUP-AUDIT-2026-07-04.md`.**

## Appendix A — Source-plan crosswalk (additions)

| Workstream | Primary source |
|---|---|
| WS5-R setup hardening | `docs/SETUP-AUDIT-2026-07-04.md` (2026-07-04, 13-lane audit + verification) |
| WS11 reinstatement extension | audit M1 ✓; `PolicyService.cs` ReinstateAsync |
| WS13a bind integrity | audit M4/M5/M11 |
| WS15 capacity | audit M2; DALE 1729 / Beazley AFB 623/2623 binder schedules (obtain terms) |

## Appendix B — Out of scope for UAT (post-launch backlog)
Unchanged from 2026-06-10, plus the WS14 additions listed above.
