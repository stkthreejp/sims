# Phase 4 UI Links And Workflow QA Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Clean up SIMS user-facing UI styling, eliminate broken or placeholder links, and prove core workflows work across the launch roles.

**Architecture:** Treat `docs/ui-design-audit-plan.md` as the visual source of truth and `docs/ui-broken-links-tracker.md` as the link/placeholder defect queue. Fix high-confidence broken links first, then sweep styling by page priority using the SIMS token/button/panel patterns already present in `frontend/src/index.css`.

**Tech Stack:** React + TypeScript + Vite frontend, ASP.NET Core 8 API, EF Core/PostgreSQL, browser route crawl, manual UAT.

---

## Assumptions

- Work directly on `main`; this project explicitly does not use feature branches or worktrees.
- Keep changes surgical. Do not restyle a page marked `Good` unless browser review shows a visible mismatch or broken interaction.
- "Broken link" includes wrong route ids, literal `#` URLs, clickable placeholder/coming-soon surfaces, and stale deployment docs that would send someone to the wrong configuration path.
- Browser QA needs usable test data for at least one insured, submission, quote, bound policy, endorsement/cancellation-capable policy, accounting void candidate, and manager queue item.

## Source Inputs

- `docs/ui-design-audit-plan.md`: existing visual audit, status categories, rollout order, and page-by-page styling priorities.
- `docs/go-live-plan.md`: Phase 4 must-haves and gate tests.
- User additions on 2026-05-25: expand the styling sweep to catch obvious old styling such as harsh black/gray borders, old blue buttons, and controls that do not match the SIMS guide.

## Files And Responsibilities

- `docs/ui-design-audit-plan.md`: update page/component visual and wiring statuses as each pass completes.
- `docs/ui-broken-links-tracker.md`: track broken links, placeholder links, stale config docs, and route-crawl findings one by one.
- `frontend/src/App.tsx`: source of routed pages for route-crawl coverage.
- `frontend/src/components/layout/Sidebar.tsx`: source of role-visible sidebar route coverage.
- `frontend/src/hooks/usePermissions.ts`: role navigation and action visibility rules to verify.
- `frontend/src/pages/submissions/SubmissionDetailPage.tsx`: bound quote "View Policy" action.
- `backend/src/SIMS.Application/DTOs/Quotes/QuoteDto.cs`: add list DTO support if bound policy ids are needed in quote list rows.
- `backend/src/SIMS.Application/Services/QuoteService.cs`: populate bound policy ids for quote list rows without using quote ids as policy ids.
- `frontend/src/types/quote.types.ts`: mirror quote list DTO shape.
- `frontend/src/pages/billing/ActivityPage.tsx`: remove or complete the QuickBooks `#` link behavior.
- `frontend/src/types/activity.types.ts` and `backend/src/SIMS.Application/DTOs/Accounting/ActivityDtos.cs`: add external journal link/id only if QuickBooks deep links are completed.
- `frontend/src/pages/reports/ReportsPage.tsx`: hide, disable, or clearly label coming-soon production reports.
- `frontend/src/pages/dashboard/DashboardPage.tsx`: remove or complete the dashboard tasks placeholder/action.
- `frontend/src/pages/insureds/InsuredDetailPage.tsx`: hide, label, or complete the activity placeholder tab.
- `docs/deployment.md` and `docs/frontend.md`: reconcile stale `VITE_API_URL` guidance with current `/api/v1` same-origin/proxy behavior.

## Success Criteria

- No known user-facing link navigates to the wrong entity id.
- No literal `href="#"` or equivalent placeholder URL remains in visible UI.
- Coming-soon surfaces are either hidden, non-clickable and clearly labeled, or fully implemented.
- Pages with visible old styling are moved toward SIMS tokens: `sims-input`, `sd-btn`, `sims-icon-btn`, SIMS panel/card treatment, token colors, and restrained status badges.
- Route crawl covers sidebar links and major dynamic links for Admin, Underwriter, CSR, and ReadOnly.
- Manual UAT completes: submission to quote to bind to policy issue; endorsement; cancellation; post-bind follow-up; accounting void approval; manager queue.
- Verification commands run and results are recorded in the audit or tracker.

---

### Task 1: Baseline Audit And Link Tracker

**Files:**
- Modify: `docs/ui-design-audit-plan.md`
- Modify: `docs/ui-broken-links-tracker.md`

- [ ] **Step 1: Confirm the worktree before editing**

Run:
```powershell
git status --short
```

Expected: existing unrelated changes may be present. Do not revert them.

- [ ] **Step 2: Re-run the static link and placeholder scan**

Run:
```powershell
rg -n -i "quickbooks|view policy|coming soon|href=.*#|to=.*#|VITE_API_URL|/api/v1|proxy" docs frontend/src
```

Expected: the tracker includes every confirmed issue from the scan. False positives such as template syntax or color hex values are ignored.

- [ ] **Step 3: Re-run the old styling scan**

Run:
```powershell
rg -n "border-black|border-gray|border-slate|bg-blue|text-blue|rounded-lg|shadow-2xl|shadow-xl|shadow-lg|text-gray|text-slate|bg-gray|bg-slate" frontend/src/pages frontend/src/components
```

Expected: results are used to prioritize styling work, not blindly replaced. Token-compatible or intentional uses can remain after browser confirmation.

- [ ] **Step 4: Update tracking docs**

Add newly found broken links or placeholder actions to `docs/ui-broken-links-tracker.md`. Add a dated progress row to `docs/ui-design-audit-plan.md` only after a page or batch is actually reviewed or changed.

---

### Task 2: Fix Bound Quote View Policy Links

**Files:**
- Modify: `backend/src/SIMS.Application/DTOs/Quotes/QuoteDto.cs`
- Modify: `backend/src/SIMS.Application/Services/QuoteService.cs`
- Modify: `frontend/src/types/quote.types.ts`
- Modify: `frontend/src/pages/submissions/SubmissionDetailPage.tsx`
- Test: existing backend test project if quote list mapping coverage exists; otherwise add focused service test near quote service tests.

- [ ] **Step 1: Add bound policy id to quote list DTOs**

Add `BoundPolicyId` to `QuoteListItemDto` and `boundPolicyId` to `QuoteListItem`.

Expected shape:
```csharp
public Guid? BoundPolicyId { get; set; }
```

```ts
boundPolicyId: string | null
```

- [ ] **Step 2: Populate bound policy ids for quote list rows**

In `QuoteService`, replace direct `items.Select(MapToListItemDto)` usage with a mapping path that can attach policy ids for bound quotes. The lookup should query `Policy` by `BoundQuoteId` for the quote ids already loaded and should ignore deleted policies.

Expected behavior:
```csharp
var policyIdsByQuoteId = await Db.Set<Policy>()
    .Where(p => quoteIds.Contains(p.BoundQuoteId) && !p.IsDeleted)
    .GroupBy(p => p.BoundQuoteId)
    .Select(g => new { QuoteId = g.Key, PolicyId = g.OrderByDescending(p => p.BoundDate).Select(p => p.Id).First() })
    .ToDictionaryAsync(x => x.QuoteId, x => x.PolicyId);
```

- [ ] **Step 3: Change the submission quote action**

In `SubmissionDetailPage.tsx`, replace:
```tsx
<Link to={`/policies/${q.id}`} className="sd-btn sm outline" onClick={(e) => e.stopPropagation()}>View Policy</Link>
```

with behavior that only renders the link when `q.boundPolicyId` exists:
```tsx
{q.status === 'Bound' && q.boundPolicyId && (
  <Link to={`/policies/${q.boundPolicyId}`} className="sd-btn sm outline" onClick={(e) => e.stopPropagation()}>View Policy</Link>
)}
```

If a quote is bound but has no policy id, render a non-clickable restrained status note and add the case to `docs/ui-broken-links-tracker.md`.

- [ ] **Step 4: Verify**

Run:
```powershell
cd backend; dotnet test
```

Run:
```powershell
cd frontend; npx tsc --noEmit
```

Expected: backend tests pass and TypeScript passes. Browser check a bound quote row from a submission and confirm "View Policy" opens the actual policy.

---

### Task 3: Remove Or Complete QuickBooks Placeholder Link

**Files:**
- Modify: `frontend/src/pages/billing/ActivityPage.tsx`
- Optional modify: `frontend/src/types/activity.types.ts`
- Optional modify: `backend/src/SIMS.Application/DTOs/Accounting/ActivityDtos.cs`
- Optional modify: `backend/src/SIMS.Application/Services/ActivityService.cs`

- [ ] **Step 1: Decide whether QBO deep links are available now**

Check whether `JournalEntryRollup.ExternalId` is available to the activity API for the selected event. If it is not available in the activity response, choose the simpler launch-safe fix: remove the anchor and show no QuickBooks link.

- [ ] **Step 2: Remove literal `#` behavior**

Delete this pattern from `ActivityPage.tsx`:
```ts
const qbDeepLink = event.lines.some((l) => l.memo?.includes('QB'))
  ? '#'
  : null
```

Do not render an `<a>` unless a real URL exists.

- [ ] **Step 3: If completing QBO links, expose a real field**

Add a nullable activity field such as:
```csharp
string? ExternalJournalUrl
```

and mirror it in TypeScript:
```ts
externalJournalUrl: string | null
```

Render the link only when the field is non-empty.

- [ ] **Step 4: Verify**

Run:
```powershell
cd frontend; npx tsc --noEmit
```

Expected: activity drawer never renders a clickable `#` link. If QBO is wired, clicking the link opens a real external journal URL.

---

### Task 4: Hide, Label, Or Complete Coming-Soon Surfaces

**Files:**
- Modify: `frontend/src/pages/reports/ReportsPage.tsx`
- Modify: `frontend/src/pages/dashboard/DashboardPage.tsx`
- Modify: `frontend/src/pages/insureds/InsuredDetailPage.tsx`
- Modify: `docs/ui-broken-links-tracker.md`

- [ ] **Step 1: Reports production placeholders**

For `renewals-upcoming`, `bound-by-period`, and `hit-ratio-by-carrier`, choose one launch-safe behavior:
- Hide them from `REPORT_CATEGORIES`.
- Keep them visible but disabled with `aria-disabled`, no route change, and a subdued "soon" label.
- Implement the report with real backend data.

Expected: clicking a report nav item never opens an empty "This report is coming soon" page unless the item is deliberately disabled and visibly unavailable.

- [ ] **Step 2: Dashboard tasks placeholder**

Because `/tasks` exists, change the dashboard Tasks card action to navigate to `/tasks` or remove the action. Replace "Task management coming soon." with either real task summary data or a neutral empty state that points to `/tasks`.

Expected: no visible action button does nothing.

- [ ] **Step 3: Insured activity placeholder**

For the insured activity tab, either hide the tab until activity is wired or label it as unavailable without making it feel like a completed workflow.

Expected: users are not led into a dead-end activity pane.

- [ ] **Step 4: Verify**

Run:
```powershell
cd frontend; npx tsc --noEmit
```

Expected: no TypeScript errors. Browser confirms the placeholder surfaces are hidden, disabled, or functional.

---

### Task 5: Reconcile Frontend Deployment Docs

**Files:**
- Modify: `docs/deployment.md`
- Modify: `docs/frontend.md`
- Review: `frontend/src/api/client.ts`
- Review: `frontend/vite.config.ts`
- Review: `frontend/nginx.conf`
- Review: `frontend/nginx.conf.template`

- [ ] **Step 1: Document current runtime behavior**

Record that frontend API calls use relative `/api/v1` from `frontend/src/api/client.ts`.

- [ ] **Step 2: Replace stale `VITE_API_URL` guidance**

Remove or rewrite frontend deployment instructions that say:
```text
VITE_API_URL=https://<your-api-app>.azurewebsites.net
```

Expected wording: local Vite proxies `/api` to `http://localhost:5000`; deployed frontend must provide a same-origin reverse proxy for `/api`, or the nginx template must receive the backend `API_URL`.

- [ ] **Step 3: Verify docs against code**

Run:
```powershell
rg -n "VITE_API_URL|/api/v1|proxy|API_URL" docs frontend
```

Expected: any remaining `VITE_API_URL` reference is either removed or explicitly marked obsolete.

---

### Task 6: Styling Debt Sweep

**Files:**
- Modify only the page/component currently being swept.
- Update: `docs/ui-design-audit-plan.md`

- [ ] **Step 1: Prioritize by audit status and visible risk**

Start with rows marked `High` or large old-style scan counts:
- `frontend/src/pages/admin/AdminRatingPlanVersionPage.tsx`
- `frontend/src/pages/admin/FeesAdminPage.tsx`
- `frontend/src/pages/admin/LegalRequirementsPage.tsx`
- `frontend/src/pages/billing/ActivityPage.tsx`
- `frontend/src/pages/billing/InvoicesPage.tsx`
- `frontend/src/pages/billing/DisbursementsPage.tsx`
- `frontend/src/pages/quotes/QuoteDetailPage.tsx`
- `frontend/src/pages/policies/PolicyDetailPage.tsx`
- `frontend/src/pages/agents/AgentsPage.tsx`
- `frontend/src/pages/carriers/CarriersPage.tsx`
- `frontend/src/pages/auth/LoginPage.tsx`

- [ ] **Step 2: For each page, replace only visible mismatches**

Use the SIMS patterns already in the app:
- Buttons: `sd-btn`, `sims-icon-btn`, or existing page-specific SIMS button variants.
- Fields: `sims-input`, `sims-select`, `sims-textarea`, token focus rings.
- Panels: `var(--surface)`, `var(--line)`, `var(--line-2)`, `var(--r-xl)`, `var(--shadow-sm)`.
- Badges: existing `StatusBadge` or token-based pill styles.
- Tables: compact headers, token borders, hover states, tabular numeric alignment.

Remove obvious old launch blockers:
- `border-black`
- harsh raw `border`
- `border-gray-*` and `border-slate-*` on primary surfaces
- `bg-blue-*` primary buttons where `sd-btn primary` exists
- heavy `shadow-xl` or `shadow-2xl` on normal panels
- nested card-on-card layouts

- [ ] **Step 3: Browser review the page**

Check desktop and a narrow viewport. Confirm text fits buttons, controls do not overlap, and the page does not read as old Tailwind styling.

- [ ] **Step 4: Verify and record**

Run:
```powershell
cd frontend; npx tsc --noEmit
```

Update the audit row status and add a dated progress row only for the page or component actually reviewed.

---

### Task 7: Browser Route Crawl

**Files:**
- Read: `frontend/src/App.tsx`
- Read: `frontend/src/components/layout/Sidebar.tsx`
- Read: `frontend/src/hooks/usePermissions.ts`
- Update: `docs/ui-broken-links-tracker.md`
- Update: `docs/ui-design-audit-plan.md`

- [ ] **Step 1: Build the route list**

Include all sidebar routes and these dynamic route families:
```text
/insureds/:id
/insureds/:id/edit
/submissions/:id
/submissions/:id/loss-history
/quotes/:quoteId
/quotes/:quoteId/writeup
/policies/:id
/agents/:id
/carriers/:id
/inbox/:id
/document-library/:id
/compliance-documentation/:id
/compliance-documentation/:id/report
/admin/rating/plans/:planId
/admin/rating/versions/:versionId
/reports/bordereaux
```

- [ ] **Step 2: Crawl by role**

Run the crawl for Admin, Underwriter, CSR, and ReadOnly. For each role, record:
- visible sidebar links
- hidden links that should be hidden
- routes that redirect unexpectedly
- 404/error boundary hits
- console errors
- dead buttons or links

- [ ] **Step 3: Track defects one by one**

Every broken route, wrong entity link, dead action, or placeholder link gets one row in `docs/ui-broken-links-tracker.md` with owner status `Open`.

- [ ] **Step 4: Verify**

Expected: each role can navigate all visible sidebar links without route errors, and restricted links are absent or blocked cleanly.

---

### Task 8: Manual Workflow UAT

**Files:**
- Update: `docs/ui-broken-links-tracker.md`
- Update: `docs/ui-design-audit-plan.md`

- [ ] **Step 1: Submission to quote to bind to policy issue**

Create or use a valid submission, create a quote, rate if required, bind, open the resulting policy, and issue it.

Expected: every page transition lands on the correct entity and every blocker explains what must be resolved.

- [ ] **Step 2: Endorsement**

Start an endorsement on an active policy and complete the available workflow.

Expected: transaction pages, approval/checklist surfaces, generated artifacts, and policy status are coherent.

- [ ] **Step 3: Cancellation**

Start cancellation, issue required notice if applicable, and complete cancellation.

Expected: legal/cancellation guidance surfaces are styled, links are valid, and statuses update.

- [ ] **Step 4: Post-bind follow-up**

Open reports or task queues for post-bind work.

Expected: follow-up work appears in a real queue/report or the placeholder is hidden/disabled.

- [ ] **Step 5: Accounting void approval**

Open an accounting activity event, attempt a valid void path, and verify blocked voids show a clear reason.

Expected: no QuickBooks placeholder links, no dead actions, and void approval state is understandable.

- [ ] **Step 6: Manager queue**

Open manager queue report/workflow and resolve or inspect approval/referral items.

Expected: action links open the correct submission, quote, policy, or approval context.

---

### Task 9: Final Verification And Closeout

**Files:**
- Modify: `docs/ui-design-audit-plan.md`
- Modify: `docs/ui-broken-links-tracker.md`

- [ ] **Step 1: Run full frontend verification**

Run:
```powershell
cd frontend; npx tsc --noEmit
```

Run:
```powershell
cd frontend; npm run build
```

Expected: both pass.

- [ ] **Step 2: Run backend verification if API DTOs changed**

Run:
```powershell
cd backend; dotnet test
```

Expected: tests pass.

- [ ] **Step 3: Re-scan for known link placeholders**

Run:
```powershell
rg -n -i "href=.*#|to=.*#|quickbooks.*#|coming soon|VITE_API_URL" docs frontend/src
```

Expected: no unresolved launch-blocking result remains. Any intentional result has a tracker row and visible UI label.

- [ ] **Step 4: Close tracker rows**

For each fixed row in `docs/ui-broken-links-tracker.md`, update status to `Fixed` and add the verification evidence.

- [ ] **Step 5: Commit directly to main**

Run:
```powershell
git add docs/ui-design-audit-plan.md docs/ui-broken-links-tracker.md frontend/src backend/src docs/deployment.md docs/frontend.md
git commit -m "chore: complete phase 4 ui links and workflow qa"
```

Expected: commit succeeds on `main`.

## Self-Review Notes

- Phase 4 must-haves are covered by Tasks 2, 3, 4, and 5.
- Styling expansion requested on 2026-05-25 is covered by Task 6.
- Gate tests are covered by Tasks 7, 8, and 9.
- Broken links are deliberately tracked outside the plan in `docs/ui-broken-links-tracker.md` so they can be handled one by one.
