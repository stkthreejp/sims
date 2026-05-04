import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { Toaster } from 'sonner'
import { queryClient } from '@/lib/queryClient'
import { useAuthStore } from '@/store/authStore'
import { AppLayout } from '@/components/layout/AppLayout'
import { AuthLayout } from '@/components/layout/AuthLayout'
import { LoginPage } from '@/pages/auth/LoginPage'
import { DashboardPage } from '@/pages/dashboard/DashboardPage'
import { InsuredsPage } from '@/pages/insureds/InsuredsPage'
import { InsuredDetailPage } from '@/pages/insureds/InsuredDetailPage'
import { InsuredCreatePage } from '@/pages/insureds/InsuredCreatePage'
import { InsuredEditPage } from '@/pages/insureds/InsuredEditPage'
import { SubmissionsPage } from '@/pages/submissions/SubmissionsPage'
import { SubmissionDetailPage } from '@/pages/submissions/SubmissionDetailPage'
import { SubmissionCreatePage } from '@/pages/submissions/SubmissionCreatePage'
import { PoliciesPage } from '@/pages/policies/PoliciesPage'
import { PolicyDetailPage } from '@/pages/policies/PolicyDetailPage'
import { AgentsPage } from '@/pages/agents/AgentsPage'
import { AgentDetailPage } from '@/pages/agents/AgentDetailPage'
import { UsersPage } from '@/pages/users/UsersPage'
import { CarriersPage } from '@/pages/carriers/CarriersPage'
import { CarrierDetailPage } from '@/pages/carriers/CarrierDetailPage'
import { DocumentLibraryPage } from '@/pages/documents/DocumentLibraryPage'
import { TemplateEditorPage } from '@/pages/documents/TemplateEditorPage'
import { InboxPage } from '@/pages/inbox/InboxPage'
import { InboxDetailPage } from '@/pages/inbox/InboxDetailPage'
import { TaskQueuePage } from '@/pages/tasks/TaskQueuePage'
import { TaskTypesAdminPage } from '@/pages/admin/TaskTypesAdminPage'
import { WorkflowsAdminPage } from '@/pages/admin/WorkflowsAdminPage'
import { HolidayCalendarAdminPage } from '@/pages/admin/HolidayCalendarAdminPage'
import { EscalationRulesAdminPage } from '@/pages/admin/EscalationRulesAdminPage'
import { FeesAdminPage } from '@/pages/admin/FeesAdminPage'
import { AdminRatingPage } from '@/pages/admin/AdminRatingPage'
import { AdminRatingPlanDetailPage } from '@/pages/admin/AdminRatingPlanDetailPage'
import { AdminRatingPlanVersionPage } from '@/pages/admin/AdminRatingPlanVersionPage'
import AdminShadowRatingPage from '@/pages/admin/AdminShadowRatingPage'
import QuoteWriteupPage from '@/pages/quotes/QuoteWriteupPage'
import { InvoicesPage } from '@/pages/billing/InvoicesPage'
import { ReceiptsPage } from '@/pages/billing/ReceiptsPage'
import { CashApplicationPage } from '@/pages/billing/CashApplicationPage'
import { CashDistributionPage } from '@/pages/billing/CashDistributionPage'
import { DisbursementsPage } from '@/pages/billing/DisbursementsPage'
import { StatementReconciliationPage } from '@/pages/billing/StatementReconciliationPage'
import { ActivityPage } from '@/pages/billing/ActivityPage'
import { PeriodClosePage } from '@/pages/billing/PeriodClosePage'
import { SyncHealthPage } from '@/pages/billing/SyncHealthPage'
import { ReportsPage } from '@/pages/reports/ReportsPage'

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
        <Routes>
          {/* Public */}
          <Route element={<AuthLayout />}>
            <Route path="/login" element={<LoginPage />} />
          </Route>

          {/* Protected */}
          <Route
            element={
              <ProtectedRoute>
                <AppLayout />
              </ProtectedRoute>
            }
          >
            <Route index element={<Navigate to="/dashboard" replace />} />
            <Route path="/dashboard" element={<DashboardPage />} />

            <Route path="/insureds" element={<InsuredsPage />} />
            <Route path="/insureds/new" element={<InsuredCreatePage />} />
            <Route path="/insureds/:id" element={<InsuredDetailPage />} />
            <Route path="/insureds/:id/edit" element={<InsuredEditPage />} />

            <Route path="/submissions/new" element={<SubmissionCreatePage />} />
            <Route path="/submissions/:id" element={<SubmissionDetailPage />} />
            <Route path="/submissions" element={<SubmissionsPage />} />

            <Route path="/policies" element={<PoliciesPage />} />
            <Route path="/policies/:id" element={<PolicyDetailPage />} />

            <Route path="/agents" element={<AgentsPage />} />
            <Route path="/agents/:id" element={<AgentDetailPage />} />
            <Route path="/carriers" element={<CarriersPage />} />
            <Route path="/carriers/:id" element={<CarrierDetailPage />} />
            <Route path="/users" element={<UsersPage />} />

            <Route path="/inbox" element={<InboxPage />} />
            <Route path="/inbox/:id" element={<InboxDetailPage />} />

            <Route path="/document-library" element={<DocumentLibraryPage />} />
            <Route path="/document-library/new" element={<TemplateEditorPage />} />
            <Route path="/document-library/:id" element={<TemplateEditorPage />} />

            <Route path="/tasks" element={<TaskQueuePage />} />

            <Route path="/admin/task-types" element={<TaskTypesAdminPage />} />
            <Route path="/admin/workflows" element={<WorkflowsAdminPage />} />
            <Route path="/admin/holiday-calendar" element={<HolidayCalendarAdminPage />} />
            <Route path="/admin/escalation-rules" element={<EscalationRulesAdminPage />} />
            <Route path="/admin/fees" element={<FeesAdminPage />} />
            <Route path="/admin/rating" element={<AdminRatingPage />} />
            <Route path="/admin/rating/plans/:planId" element={<AdminRatingPlanDetailPage />} />
            <Route path="/admin/rating/versions/:versionId" element={<AdminRatingPlanVersionPage />} />
            <Route path="/admin/rating/shadow" element={<AdminShadowRatingPage />} />
            <Route path="/quotes/:quoteId/writeup" element={<QuoteWriteupPage />} />
            <Route path="/billing/invoices" element={<InvoicesPage />} />
            <Route path="/billing/receipts" element={<ReceiptsPage />} />
            <Route path="/billing/cash-application" element={<CashApplicationPage />} />
            <Route path="/billing/cash-distribution" element={<CashDistributionPage />} />
            <Route path="/billing/disbursements" element={<DisbursementsPage />} />
            <Route path="/billing/statement-reconciliation" element={<StatementReconciliationPage />} />
            <Route path="/billing/activity" element={<ActivityPage />} />
            <Route path="/billing/period-close" element={<PeriodClosePage />} />
            <Route path="/billing/sync-health" element={<SyncHealthPage />} />
            <Route path="/reports" element={<ReportsPage />} />
          </Route>

          <Route path="*" element={<Navigate to="/dashboard" replace />} />
        </Routes>
      </BrowserRouter>
      <Toaster richColors position="top-right" />
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  )
}
