# Frontend

## Tech Stack

| Category | Library | Version |
|---|---|---|
| Framework | React | 18.3 |
| Language | TypeScript | 5.7 |
| Build tool | Vite | 6.0 |
| Routing | React Router | v6 |
| State management | Zustand | 5.0 |
| Data fetching | TanStack React Query | 5.62 |
| HTTP client | Axios | 1.7 |
| UI components | Radix UI | various |
| Styling | Tailwind CSS | 3.4 |
| Icons | Lucide React | — |
| Forms | React Hook Form + Zod | 7.54 / 3.24 |
| Rich text editor | TipTap | 3.22 |
| Word document parsing | Mammoth | 1.12 |
| Auth | @azure/msal-browser | 5.6 |
| Toasts | Sonner | — |
| Date utilities | date-fns | 4.1 |
| Maps | Google Maps JS API | — |

## Pages (36 total)

### Authentication
- `LoginPage` — Microsoft Azure AD login

### Dashboard
- `DashboardPage` — Main landing page

### Submissions
- `SubmissionsPage` — Submission list with filters
- `SubmissionDetailPage` — Full submission detail with LOB tabs
- `SubmissionCreatePage` — New submission wizard

### Policies
- `PoliciesPage` — Policy list
- `PolicyDetailPage` — Policy detail with transactions

### Insureds
- `InsuredsPage` / `InsuredDetailPage` / `InsuredCreatePage` / `InsuredEditPage`

### Agents
- `AgentsPage` / `AgentDetailPage`

### Carriers
- `CarriersPage` / `CarrierDetailPage`

### Tasks
- `TaskQueuePage` — Task queue with filters and assignment
- `TaskDetailDrawer` — Slide-over task detail panel

### Billing & Accounting
- `InvoicesPage` — Invoice list and management
- `ReceiptsPage` — Receipt tracking
- `CashApplicationPage` — Apply payments to invoices
- `CashDistributionPage` — Distribute funds
- `DisbursementsPage` — Vendor disbursements
- `StatementReconciliationPage` — Payee reconciliation
- `ActivityPage` — Transaction activity log
- `PeriodClosePage` — Month-end close workflow
- `SyncHealthPage` — QBO sync status and health

### Inbox
- `InboxPage` / `InboxDetailPage` — Email ingestion and processing

### Documents
- `DocumentLibraryPage` — Document template library
- `TemplateEditorPage` — TipTap-powered template editor with Word import

### Admin
- `FeesAdminPage` — Fee structure configuration
- `EscalationRulesAdminPage` — Task escalation rules
- `HolidayCalendarAdminPage` — Holiday definitions
- `TaskTypesAdminPage` — Task type definitions
- `WorkflowsAdminPage` — Workflow template management

### Users
- `UsersPage` — User management

## API Modules (26 total)

All modules wrap Axios calls and are organized by domain. The base client (`api/client.ts`) handles auth headers, token refresh, and error normalization.

| Module | Description |
|---|---|
| `auth.api.ts` | Login, logout, token refresh |
| `users.api.ts` | User CRUD, permissions |
| `agents.api.ts` | Agent management |
| `agentCommissions.api.ts` | Agent commission queries |
| `carriers.api.ts` | Carrier management |
| `carrierCommissions.api.ts` | Carrier commission queries |
| `insureds.api.ts` | Insured CRUD |
| `policies.api.ts` | Policy queries |
| `quotes.api.ts` | Quote management |
| `submissions.api.ts` | Submission CRUD |
| `submissionLob.api.ts` | LOB-specific submission operations |
| `attachments.api.ts` | File upload/download |
| `inboundEmails.api.ts` | Email ingestion |
| `notes.api.ts` | Notes and comments |
| `tasks.api.ts` | Task CRUD, assignment |
| `invoices.api.ts` | Invoice queries |
| `activity.api.ts` | Activity/transaction history |
| `receipts.api.ts` | Receipt management |
| `disbursements.api.ts` | Disbursement queries |
| `cashDistribution.api.ts` | Distribution management |
| `payeeStatements.api.ts` | Payee reconciliation |
| `periodClose.api.ts` | Period close operations |
| `fees.api.ts` | Fee administration |
| `admin.api.ts` | General admin |
| `documentTemplates.api.ts` | Template management |
| `documentGeneration.api.ts` | Generate documents |
| `rollup.api.ts` | Financial rollup data |

## Environment Variables

```bash
VITE_API_URL=http://localhost:5000    # Backend API base URL
```

Set in `.env.local` for local development.
