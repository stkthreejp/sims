# Lane O — Operations, Compliance, Documents & Navigation Shell

## Route vs. Nav table

| Route | Sidebar entry | Notes |
|---|---|---|
| `/compliance-documentation/attestations` | none (orphan) | Reachable only via a button on the Compliance register; gated by `nav.compliance-documentation`, which attestation recipients may not hold |
| `/compliance-documentation/reviews` | none (orphan) | Same — button-only entry from the register |
| `/compliance-documentation/:id/report` | none (orphan) | Button-only from document detail |
| `/reports/bordereaux` | none (orphan) | Only via ReportsPage internal left nav ("Bordereaux Workbench") |
| `/tasks` | "My Tasks" | Route intentionally unguarded (App.tsx:257) — consistent |
| `/quotes/:id/writeup`, `/submissions/:id/loss-history` | none | Entity-flow deep pages, acceptable |
| All billing/admin routes | present | Sidebar guards match route guards — no route/nav permission mismatch found |
| Dead/vestigial nav-shell controls | — | Topbar bell button (no onClick), Dashboard "Today" / "View all →" / queue-tab toggle / "Follow up" buttons (all no-ops) |

No sidebar item points to a stub page; ClaimsPage is a real (import-centric, read-only) page.

**O1 [P1] — Blocked tasks vanish from the queue; Blocked/Closed/Cancelled filters are dead** — `frontend/src/pages/tasks/TaskQueuePage.tsx:11,64-67,107-122` vs `backend/src/SIMS.Application/Services/TaskInstanceService.cs:42-44` — `/tasks/my-queue` returns only `Open|InProgress`, but the UI offers Blocked/Closed/Cancelled status filters and a "Closed" metric that are always empty/0. Worse: marking a task "Blocked" in TaskDetailDrawer removes it from the only queue view — the user's only recovery is stumbling onto the entity page. — Fix: include Blocked in the my-queue query (or add a status param) and drop dead filter options (S).

**O2 [P1] — "Save Details" on compliance documents silently discards unsaved editor content** — `frontend/src/pages/compliance/ComplianceDocumentDetailPage.tsx:75-90,93-96,99-115` — `invalidate()` invalidates the `['compliance-documents']` prefix, which also refetches `['compliance-documents', id]`; the sync `useEffect` then resets `content`/`changeSummary` from the server, clobbering un-drafted TipTap edits after a metadata save. — Fix: invalidate list keys with `exact`, or skip the form-reset when `isDirty` (S).

**O3 [P1] — No unsaved-changes guard anywhere; back buttons and sidebar nav lose work** — `frontend/src/pages/documents/TemplateEditorPage.tsx:43,172-175`, `frontend/src/pages/compliance/ComplianceDocumentDetailPage.tsx:35,194-197` — both pages track `isDirty` (compliance even renders "Unsaved changes"), but grep confirms zero `useBlocker`/`beforeunload` in `frontend/src`. Clicking "Library"/"Compliance" or any sidebar item discards a template or policy draft with no prompt. — Fix: add `useBlocker` + `beforeunload` keyed on `isDirty` in both pages (M).

**O4 [P1] — Tag picker offers ~25 merge tags the generator never populates (render blank)** — `frontend/src/lib/templateTags.ts:16-266` vs `backend/src/SIMS.Application/Services/OutboundCommunicationService.cs:293-528` — the backend dictionary never sets `PageNumber`, `QuoteStatus`, `PolicyStatus`, `Deductible`, `CoverageLimit`, `CoverageDescription`, `InsuredDBA`, `InsuredType`, `InsuredAddressLine1/2`, `InsuredCity/State/Zip/County`, `CarrierAddress(+Line1/City/State/Zip/Phone/Email)`, `AgentAddressLine1`, `UnderwriterPhone`, `LinesRequested`, `BoundDate`, `IssuedDate`, `CommissionRate/Amount` — yet all are offered in the picker, so generated documents ship with silent blanks (supersedes the "5 known tags" estimate; it's ~25). — Fix: prune templateTags.ts to backend-backed keys or add the missing keys server-side; add a merge-time "unresolved tag" warning (M).

**O5 [P1] — Attestation recipients can be locked out of `/compliance-documentation/attestations`** — `frontend/src/App.tsx:252`, `backend/src/SIMS.Application/Services/ComplianceDocumentService.cs:598` — campaigns target any active user, but the My Attestations route requires `nav.compliance-documentation`; a recipient without it is silently bounced to the dashboard and can never attest. No sidebar entry either. — Fix: guard the attestations route on authentication only (like `/tasks`) and add a sidebar/topbar entry point (S).

**O6 [P1] — Task emails cannot deep-link to a task, and some link nowhere** — `backend/src/SIMS.Infrastructure/Services/TaskNotificationService.cs:39,293-299`, `frontend/src/App.tsx:258`, `frontend/src/pages/tasks/TaskQueuePage.tsx:53` — beyond the unconfigured `FrontendBaseUrl` (defaults to `localhost:5173`), links resolve to entity pages only, return empty string for `PolicyTransaction`/`ComplianceDocument` tasks, and the frontend has no `/tasks/:id` route or `?task=` param to open the drawer. — Fix: support `/tasks?task=<id>` (open drawer from URL) and point notification links there (M).

**O7 [P1] — Dashboard KPIs are computed from capped page fetches and will silently go wrong at volume** — `frontend/src/pages/dashboard/DashboardPage.tsx:203-215,227-250` — "Bound Premium · All Time" sums the first 500 quotes, open-submission counts and the funnel use the oldest 200 submissions, and the sparkline forward-fills zero weeks with fabricated values (`buildSparkline` line 62). — Fix: replace with a server-side dashboard summary endpoint (M).

**O8 [P1] — Dashboard is permission-blind and error-blind** — `frontend/src/pages/dashboard/DashboardPage.tsx:203-221,280-284,298-307` — it fetches submissions/quotes/insureds regardless of the user's permissions and never checks `isLoading`/`isError`, so a billing-only user sees "You have 0 items needing attention" (403s swallowed as empty). "New submission" is rendered for everyone but the route needs `underwriting.manage`, so clicking silently bounces back to the dashboard. — Fix: gate cards/buttons on `usePermissions` and render error/loading states (M).

**O9 [P1] — Login flow drops the requested URL** — `frontend/src/App.tsx:172` and `frontend/src/pages/auth/LoginPage.tsx:23,45` — `ProtectedRoute` navigates to `/login` without `state.from`, and LoginPage always navigates to `/dashboard`. Every emailed entity/task link opened while logged out strands the user on the dashboard. — Fix: pass `location` in redirect state and navigate back after auth (S).

**O10 [P2] — Silent permission redirects disorient users** — `frontend/src/App.tsx:175-187` — `PermissionRoute`/`PermissionAllRoute` `Navigate` to `/dashboard` with no toast or "no access" page. — Fix: render a small "You don't have access" screen or toast before redirect (S).

**O11 [P2] — Inbox has no dismiss/junk action; spam accumulates forever** — `frontend/src/pages/inbox/InboxPage.tsx:12-16`, `frontend/src/api/inboundEmails.api.ts:11-33` — the only way to clear an unprocessed email is to create a submission from it; no archive/mark-processed API in the client. — Fix: add a dismiss endpoint + row action (M, needs backend).

**O12 [P2] — TaskDetailDrawer lacks an entity link and reassign despite the API supporting it** — `frontend/src/pages/tasks/TaskDetailDrawer.tsx:96-114`, `frontend/src/api/tasks.api.ts:17-18` — the drawer shows "Entity: Submission" as plain text (no link), and `tasksApi.reassign` has no UI. Notes can only be saved as a side effect of a status change. — Fix: add entity link, reassign picker, and a standalone "add note" (M).

**O13 [P2] — ErrorBoundary state is sticky across navigation and can't recover from stale chunks** — `frontend/src/components/common/ErrorBoundary.tsx:46-52`, `frontend/src/components/layout/AppLayout.tsx:18-22` — after a crash, clicking other sidebar items keeps showing "Something went wrong" until "Try again"; for a failed lazy-chunk load "Try again" re-throws immediately; no "Reload page". — Fix: reset boundary on `location.pathname` change (key prop) and add a hard-reload button (S).

**O14 [P2] — Claims import errors are effectively invisible and can crash the table** — `frontend/src/pages/claims/ClaimsPage.tsx:154-155,347` — toast says "(N errors — see batch detail)" but no batch detail exists; errors surface only in a hover `title` built by `JSON.parse(b.errorSummaryJson)` inline in render, which throws on malformed JSON and takes down the page. — Fix: wrap parse in try/catch helper and render an expandable error row (S).

**O15 [P2] — "New Document" instantly creates a server record; status dropdown bypasses review workflow** — `frontend/src/pages/compliance/ComplianceDocumentationPage.tsx:42-57`, `ComplianceDocumentDetailPage.tsx:240` — one click creates "Untitled Compliance Document" (category IT); detail sidebar lets anyone set Status directly to "Active" via Save Details, undermining the Submit-for-Review → Publish workflow. — Fix: creation dialog for title/category; restrict direct status edits (M).

**O16 [P2] — Generated-document tab is popup-blocked** — `frontend/src/components/documents/GenerateDocumentModal.tsx:37` — `window.open(data.url)` runs in an async mutation callback (non-user-initiated); the doc saves but the tab often silently fails to open, so users regenerate duplicates. — Fix: success state with explicit "Open document" link (S).

**O17 [P2] — New tab / duplicated tab always forces re-login** — `frontend/src/store/authStore.ts:37`, `frontend/src/App.tsx:147-169` — auth persists in `sessionStorage` (per-tab); in a fresh tab `isAuthenticated=false`, so `ProtectedRoute` never attempts `refreshSession()` and goes straight to `/login` even with a valid refresh cookie. Middle-clicking any grid row breaks flow. — Fix: attempt `refreshSession()` when unauthenticated too, before redirecting (S).

**O18 [P2] — Reports default view and catalog ignore accounting permissions** — `frontend/src/pages/reports/ReportsPage.tsx:1749-1755` — anyone with `nav.reports + reports.view` lands on Trust Reconciliation and sees all Accounting reports; on backend 403 `ReportShell` prints raw `error.message`. — Fix: gate the Accounting category on `canManageAccounting`, map 403 to friendly message (S).

**O19 [P2] — Topbar "Search…" is insureds-only and permission-blind** — `frontend/src/components/layout/Topbar.tsx:29-36,61` — the global-looking search navigates to `/insureds?search=`; for users without `insureds.view` it silently bounces to dashboard, and it never finds policies/submissions/claims. — Fix: label it "Search insureds", hide without permission, or build a typed omni-search (S/L).

**O20 [P2] — Word import on Document Library has no error handling** — `frontend/src/pages/documents/DocumentLibraryPage.tsx:86-99` — `handleImportDoc` awaits `importWordDocument(file)` without try/catch (unlike TemplateEditorPage:150-159); a corrupt .docx produces an unhandled rejection and nothing happens on screen. Template delete uses bare `confirm()` for a hard delete. — Fix: mirror the try/catch + toast from TemplateEditorPage (S).

**O21 [P2] — Compliance metric cards vs. table filters double-filter inconsistently** — `frontend/src/pages/compliance/ComplianceDocumentationPage.tsx:33-40,61-72,121-124` — metric counts come from global `getSummary`, but clicking a metric filters the currently server-filtered list client-side (category/search still applied), so "Overdue: 4" can show 1 row. — Fix: reset all filters when a metric is chosen, or compute metric counts from the same dataset (S).

**O22 [P3] — StatusBadge barely used; four bespoke pill implementations drift** — `StatusBadge.tsx` (14 usages, none in tasks/compliance/claims) vs `TaskQueuePage.tsx:213-222` (TonePill), `ComplianceDocumentationPage.tsx:232-242`, `ComplianceAttestationsPage.tsx:225-233`, `ClaimsPage.tsx:106-119` — same statuses get different colors per page; StatusBadge renders unstyled for unmapped statuses. — Fix: extend StatusBadge with a tone map + fallback and adopt it (M).

**O23 [P3] — No per-page browser tab titles or breadcrumbs** — `frontend/index.html:7` — every tab reads "SIMS — Insurance Management"; no `document.title` management anywhere. Dashboard and compliance detail also skip `PageHeader`. — Fix: tiny `usePageTitle(title)` hook called from PageHeader (S).

**O24 [P3] — Non-dialog modals lack Escape/focus handling** — `frontend/src/pages/inbox/InboxDetailPage.tsx:312-533`, `components/editor/TemplateEditor.tsx:441-488`, compliance modals — only TaskDetailDrawer uses native `<dialog>`; the div-backdrop modals can't be closed with Esc and leak focus. — Fix: standardize on `<dialog>` or an Esc/focus-trap wrapper (M).

**O25 [P3] — Inbox "Create Submission" disable state is unexplained** — `frontend/src/pages/inbox/InboxDetailPage.tsx:211` — button disables when every attachment is unchecked, no tooltip. — Fix: add helper text ("select at least one attachment") (S).

**O26 [P3] — Dashboard task rows and eff-soon quotes don't link precisely** — `frontend/src/pages/dashboard/DashboardPage.tsx:546,377-380` — task rows navigate to `/tasks` generally; "Eff. Date in 3 Days" KPI's "View all →" does nothing. — Fix: wire View all to a filtered view; open task drawer by id once O6 lands (S).

**O27 [P3] — 404 page for logged-out users instead of login** — `frontend/src/App.tsx:297`, `NotFoundPage.tsx` — catch-all `*` sits outside `ProtectedRoute`; mistyped URL while logged out renders the bare 404 (layout collapses vertically) with a "Go to Dashboard" button that bounces through login without returning. — Fix: send unknown paths through the protected layout; min-height on NotFoundPage (S).
