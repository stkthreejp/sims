# SIMS UI Design Audit Plan

## Purpose

Audit the existing SIMS frontend against the May 2026 SIMS UI Guide and track which pages and components are already visually consistent, which need small adjustments, and which have not been updated to the guide yet.

The current standard is **visual consistency with the guide**, not strict token-by-token compliance. Exact implementation cleanup can happen during the redesign pass.

## Source Of Truth

- `C:\Users\JeremiahPODonovan\Downloads\SIMS.zip`
- Extracted working copy: `temp/sims-ui-guide/SIMS-UI-Guide`
- Canonical guide files:
  - `SIMS UI Guide.html`
  - `tokens.css`
  - `assets/smm-symbol.png`

The app already mirrors the guide tokens in `frontend/src/index.css`.

## Audit Scope

Include all visible frontend surfaces:

- Routed pages
- Drawers
- Modals
- Panels and page sections
- Shared UI primitives
- Editor/document surfaces
- Layout shell components

Embedded UI must be tracked separately from the page that opens it. For example, a page can be marked good while its modal is marked needs tweaking or never updated.

## Status Categories

| Status | Meaning |
|---|---|
| Good | Visually consistent with the SIMS UI Guide. Minor code cleanup may still be possible, but the user-facing design is acceptable. |
| Needs tweaking | Mostly aligned or partially modernized, but has visible mismatches such as old buttons, spacing, tables, colors, cards, forms, or status treatments. |
| Never updated | Still primarily uses the older visual language and does not read as part of the SIMS UI Guide system. |
| Blocked / needs data | Cannot be fairly judged without a valid record, route state, backend data, or workflow setup. |

## Audit Checklist

Use this checklist during the visual pass:

- Page anatomy follows the guide: app shell, page head, optional metrics, main content.
- Page spacing and density match the guide.
- Cards use SIMS surface, border, radius, and subtle elevation style.
- Tables use compact density, token colors, proper headers, hover states, and tabular numeric alignment.
- Buttons use the SIMS hierarchy: primary, outline, ghost, danger, small variants where appropriate.
- Statuses use pills/badges instead of ad hoc colored labels.
- Forms use consistent label, field, focus, validation, and helper text treatment.
- Tabs, filters, chips, and segmented controls match guide patterns.
- Empty, loading, and error states feel consistent and restrained.
- Icons use the established stroke style and inherit color from context.
- No obvious old visual language dominates: `text-slate-*`, `text-gray-*`, `bg-blue-*`, `rounded-lg`, heavy shadows, nested cards, or oversized controls.

## Working Audit Chart

This first chart is a code-informed starting point. It should be confirmed and corrected during the browser visual audit.

| Item | Type | Location | Current Status | Notes | Priority |
|---|---|---|---|---|---|
| App layout | Layout shell | `frontend/src/components/layout/AppLayout.tsx` | Good | Uses app background token and standard shell structure. | Low |
| Sidebar | Layout shell | `frontend/src/components/layout/Sidebar.tsx` | Good | Strong token usage and guide-aligned navigation. | Low |
| Topbar | Layout shell | `frontend/src/components/layout/Topbar.tsx` | Good | Strong token usage and guide-aligned top search/user area. | Low |
| Auth layout | Layout shell | `frontend/src/components/layout/AuthLayout.tsx` | Needs tweaking | Mixed token and older utility styling. | Medium |
| Page header | Shared primitive | `frontend/src/components/common/PageHeader.tsx` | Good | Uses SIMS typography and ink tokens. | Low |
| Status badge | Shared primitive | `frontend/src/components/common/StatusBadge.tsx` | Good | Uses SIMS status tokens. | Low |
| Empty state | Shared primitive | `frontend/src/components/common/EmptyState.tsx` | Good | Updated to SIMS token styling; visual browser confirmation still useful. | Low |
| Error boundary | Shared primitive | `frontend/src/components/common/ErrorBoundary.tsx` | Good | Updated to guide-style restrained error card and SIMS button treatment. | Low |
| Address autocomplete | Shared primitive | `frontend/src/components/common/AddressAutocomplete.tsx` | Good | Updated to SIMS field styling and token-based focus state. | Low |
| Loading spinner | Shared primitive | `frontend/src/components/common/LoadingSpinner.tsx` | Good | Updated to quieter token-based spinner. | Low |
| Dashboard | Page | `/dashboard` | Good | Strong token usage and guide-style dashboard density. | Low |
| Reports | Page | `/reports` | Good | Strong token usage and visually aligned. | Low |
| Submissions list | Page | `/submissions` | Good | Uses dedicated `subs-*` guide classes. | Low |
| Submission detail | Page | `/submissions/:id` | Good | Uses dedicated `sd-*` guide classes. | Low |
| Submission loss history | Page | `/submissions/:id/loss-history` | Good | Strong token usage and aligned table/card treatment. | Low |
| Submission create | Page | `/submissions/new` | Needs tweaking | Uses shared header but still has older form/card styling. | Medium |
| Insured detail | Page | `/insureds/:id` | Good | Strong token usage and aligned detail layout. | Low |
| Insureds list | Page | `/insureds` | Needs tweaking | Uses shared header but still has older list/table styling. | Medium |
| Insured create | Page | `/insureds/new` | Needs tweaking | Older form layout and controls. | Medium |
| Insured edit | Page | `/insureds/:id/edit` | Needs tweaking | Older form layout and controls. | Medium |
| Policies list | Page | `/policies` | Needs tweaking | Uses shared header but older table/list styling remains. | Medium |
| Policy detail | Page | `/policies/:id` | Never updated | Mostly older utility styling. | High |
| Quote detail | Page | `/quotes/:quoteId` | Never updated | Large page with heavy old utility styling. | High |
| Quote writeup | Page | `/quotes/:quoteId/writeup` | Needs tweaking | Partially aligned, but many old controls and panels remain. | High |
| Quote rating panel | Panel | `frontend/src/components/quotes/QuoteRatingPanel.tsx` | Never updated | Old slate/blue cards, tables, buttons, and form controls. | High |
| Quote auto safety panel | Panel | `frontend/src/components/quotes/QuoteAutoSafetyPanel.tsx` | Never updated | Large panel with extensive old utility styling. | High |
| Task queue | Page | `/tasks` | Needs tweaking | Mixed token and older styling. | Medium |
| Task detail drawer | Drawer | `frontend/src/pages/tasks/TaskDetailDrawer.tsx` | Needs tweaking | Separate drawer surface with old controls and slate styling. | Medium |
| Agents list | Page | `/agents` | Needs tweaking | Uses shared header but old list, form, and table styling remain. | Medium |
| Agent detail | Page | `/agents/:id` | Never updated | Large detail surface with old cards/forms/tables. | High |
| Carriers list | Page | `/carriers` | Needs tweaking | Uses shared header but old cards/list styling remain. | Medium |
| Carrier detail | Page | `/carriers/:id` | Never updated | Large detail surface with old cards/forms/tables. | High |
| Users | Page + modal | `/users` | Needs tweaking | Old table and user modal styling. Modal should be audited separately in visual pass. | Medium |
| Inbox list | Page | `/inbox` | Needs tweaking | Smaller surface but old utility styling remains. | Medium |
| Inbox detail | Page | `/inbox/:id` | Never updated | Mostly older utility styling. | High |
| Document library | Page | `/document-library` | Needs tweaking | Uses shared header but old list/card styling remains. | Medium |
| Template editor page | Page | `/document-library/new`, `/document-library/:id` | Never updated | Older editor page shell and controls. | High |
| Template editor | Editor surface | `frontend/src/components/editor/TemplateEditor.tsx` | Never updated | Large editor surface with old toolbar/control styling. | High |
| Documents section | Panel | `frontend/src/components/documents/DocumentsSection.tsx` | Never updated | Old document list/upload styling. | High |
| Generate document modal | Modal | `frontend/src/components/documents/GenerateDocumentModal.tsx` | Never updated | Old modal controls and slate/blue styling. | Medium |
| Cash balance badge | Shared accounting component | `frontend/src/components/accounting/CashBalanceBadge.tsx` | Needs tweaking | Needs visual confirmation; no strong token signals. | Low |
| Billing activity | Page | `/billing/activity` | Needs tweaking | Uses shared header but many old tables/cards/buttons remain. | Medium |
| Cash application | Page | `/billing/cash-application` | Needs tweaking | Old billing workflow styling. | Medium |
| Cash distribution | Page | `/billing/cash-distribution` | Needs tweaking | Old billing workflow styling. | Medium |
| Disbursements | Page | `/billing/disbursements` | Needs tweaking | Old billing workflow styling. | Medium |
| Invoices | Page | `/billing/invoices` | Needs tweaking | Old billing workflow styling. | Medium |
| Period close | Page | `/billing/period-close` | Needs tweaking | Old cards, checklist states, and action styling. | Medium |
| Receipts | Page | `/billing/receipts` | Needs tweaking | Old billing workflow styling. | Medium |
| Statement reconciliation | Page | `/billing/statement-reconciliation` | Needs tweaking | Old billing workflow styling. | Medium |
| Sync health | Page | `/billing/sync-health` | Needs tweaking | Old operational status styling. | Medium |
| Admin shadow rating | Page | `/admin/rating/shadow` | Good | Strong token usage relative to other admin pages. | Low |
| Admin jobs | Page | `/admin/jobs` | Needs tweaking | Mixed shared header and old cards/tables. | Medium |
| Admin rating | Page | `/admin/rating` | Needs tweaking | Old rating admin styling. | Medium |
| Admin rating plan detail | Page | `/admin/rating/plans/:planId` | Needs tweaking | Old admin detail styling. | Medium |
| Admin rating plan version | Page | `/admin/rating/versions/:versionId` | Needs tweaking | Large page with extensive old form/table styling. | High |
| Database status | Page | `/admin/database-status` | Needs tweaking | Old status/card styling. | Medium |
| Escalation rules | Page | `/admin/escalation-rules` | Needs tweaking | Old table/form styling. | Medium |
| Fees admin | Page | `/admin/fees` | Needs tweaking | Large admin workflow with old form/table styling. | High |
| Holiday calendar | Page | `/admin/holiday-calendar` | Needs tweaking | Old table/form styling. | Medium |
| Legal requirements | Page | `/admin/legal-requirements` | Needs tweaking | Large admin workflow with extensive old styling. | High |
| Role permissions | Page | `/admin/role-permissions` | Needs tweaking | Old permission matrix styling. | Medium |
| Task types admin | Page | `/admin/task-types` | Needs tweaking | Old table/form styling. | Medium |
| Workflows admin | Page | `/admin/workflows` | Needs tweaking | Old workflow admin styling. | Medium |
| Login | Page | `/login` | Never updated | Old auth card styling; should be aligned with SIMS brand guide. | Medium |

## Recommended Rollout Order

1. Confirm this chart visually in the browser and mark any route that is blocked by data.
2. Fix shared primitives first: empty state, error state, loading spinner, address autocomplete, modal/drawer base patterns.
3. Update high-visibility workflow pages: quote detail, quote writeup, policy detail, agent detail, carrier detail, inbox detail.
4. Update embedded high-impact panels: quote rating, auto safety, documents section, template editor, generate document modal.
5. Sweep list/create/edit pages: insureds, policies, agents, carriers, submissions create, document library.
6. Bring billing and admin pages into alignment in batches by shared pattern: list/table pages first, then complex workflow pages.

## Visual Audit Progress

| Date | Auditor | Scope | Result |
|---|---|---|---|
| 2026-05-13 | Codex | Code-informed first pass | Initial chart created; browser visual confirmation still needed. |
| 2026-05-13 | Codex | Shared primitives batch 1 | Updated EmptyState, ErrorBoundary, LoadingSpinner, and AddressAutocomplete to SIMS visual style. Type check passed. |
