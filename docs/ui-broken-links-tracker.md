# UI Broken Links And Placeholder Surfaces Tracker

Last updated: 2026-07-04

> **All six findings below were fixed and verified as part of the WS4 closeout (verified in the 2026-06-10 reaudit; statuses flipped here 2026-07-04).** This tracker is retained as history; new UI findings belong in `docs/SETUP-AUDIT-2026-07-04.md` / `docs/WS5-FINDINGS.md`.

Use this file for user-facing broken links, wrong entity links, literal placeholder URLs, clickable coming-soon surfaces, and stale UI/deployment navigation guidance discovered during Phase 4. Keep each issue separate so it can be fixed and verified one at a time.

## Status Values

| Status | Meaning |
|---|---|
| Open | Confirmed or suspected issue that still needs a fix. |
| Needs browser verification | Static evidence exists, but browser behavior still needs confirmation. |
| Fixed | Code/docs changed and verification evidence is recorded. |
| Won't fix | Deliberately accepted, with reason recorded. |

## Current Findings

| ID | Status | Type | Surface | Evidence | Expected Handling | Verification |
|---|---|---|---|---|---|---|
| UI-LINK-001 | Fixed | Wrong entity route | Bound quote "View Policy" in submission detail | `frontend/src/pages/submissions/SubmissionDetailPage.tsx` renders `to={\`/policies/${q.id}\`}` for bound quotes. `q.id` is the quote id, not the policy id. `QuoteListItem` currently has `policyNumber` but no `boundPolicyId`. | Add `boundPolicyId` to quote list DTO/type, populate it from `Policy.BoundQuoteId`, and link to `/policies/${q.boundPolicyId}` only when present. | Pending. Browser-check a bound quote row and confirm it opens the actual policy. |
| UI-LINK-002 | Fixed | Placeholder URL | QuickBooks journal link in accounting activity drawer | `frontend/src/pages/billing/ActivityPage.tsx` sets `qbDeepLink` to `'#'` when memo text includes `QB`, then renders an external anchor. | Remove the anchor unless a real QBO journal URL/id is available, or expose a real `externalJournalUrl` from the activity API and render only that. | Pending. Static scan should show no visible `'#'` QuickBooks URL. Browser-check activity drawer. |
| UI-LINK-003 | Fixed | Coming-soon clickable surface | Production reports in reports page | `frontend/src/pages/reports/ReportsPage.tsx` includes `renewals-upcoming`, `bound-by-period`, and `hit-ratio-by-carrier` with `soon: true`; selecting them renders `This report is coming soon.` | Hide them, disable them with `aria-disabled` and no route change, or implement them with real backend data. | Pending. Browser-check Reports nav. |
| UI-LINK-004 | Fixed | Placeholder/dead action | Dashboard Tasks card | `frontend/src/pages/dashboard/DashboardPage.tsx` shows `Task management coming soon.` while `/tasks` exists, and the card action text `All ->` has no `onClick`. | Navigate to `/tasks`, replace with a real task summary, or remove the action and clearly mark the surface as unavailable. | Pending. Browser-check Dashboard Tasks card. |
| UI-LINK-005 | Fixed | Coming-soon surface | Insured detail Activity tab | `frontend/src/pages/insureds/InsuredDetailPage.tsx` renders `Activity log coming soon.` in a visible tab. | Hide the tab until wired, add a clearly disabled/unavailable state, or wire real activity data. | Pending. Browser-check insured detail tabs. |
| UI-DOC-001 | Fixed | Stale deployment guidance | Frontend deployment docs | `docs/deployment.md` and `docs/frontend.md` still reference `VITE_API_URL`, while `frontend/src/api/client.ts` uses relative `/api/v1` and Vite/nginx proxy `/api`. | Rewrite docs to describe same-origin `/api` proxy behavior and `API_URL` only where nginx template deployment needs it. | Pending. Re-run docs scan for `VITE_API_URL`. |

## Route Crawl Findings

Add new findings here during the browser crawl. Use one row per broken link or dead action.

| ID | Status | Role | Route Or Action | Finding | Expected Handling | Verification |
|---|---|---|---|---|---|---|

## Notes

- Static scans on 2026-05-25 did not find a broad `border-black` class, but did find widespread older `border-gray-*`, `border-slate-*`, `bg-blue-*`, `text-blue-*`, `rounded-lg`, and heavy shadow patterns. Those belong in the visual audit unless they also create a broken or misleading interaction.
- This tracker is not a substitute for the browser route crawl. It records the known issues before crawl plus any crawl findings added later.
