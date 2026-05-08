import { lazy, Suspense, useState, useEffect } from 'react'
import axios from 'axios'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { Toaster } from 'sonner'
import { queryClient } from '@/lib/queryClient'
import { useAuthStore } from '@/store/authStore'
import { authApi } from '@/api/auth.api'
import { AppLayout } from '@/components/layout/AppLayout'
import { AuthLayout } from '@/components/layout/AuthLayout'
import { ErrorBoundary } from '@/components/common/ErrorBoundary'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'

// Auth
const LoginPage = lazy(() => import('@/pages/auth/LoginPage').then((m) => ({ default: m.LoginPage })))

// Core
const DashboardPage = lazy(() => import('@/pages/dashboard/DashboardPage').then((m) => ({ default: m.DashboardPage })))

// Insureds
const InsuredsPage = lazy(() => import('@/pages/insureds/InsuredsPage').then((m) => ({ default: m.InsuredsPage })))
const InsuredDetailPage = lazy(() => import('@/pages/insureds/InsuredDetailPage').then((m) => ({ default: m.InsuredDetailPage })))
const InsuredCreatePage = lazy(() => import('@/pages/insureds/InsuredCreatePage').then((m) => ({ default: m.InsuredCreatePage })))
const InsuredEditPage = lazy(() => import('@/pages/insureds/InsuredEditPage').then((m) => ({ default: m.InsuredEditPage })))

// Submissions
const SubmissionsPage = lazy(() => import('@/pages/submissions/SubmissionsPage').then((m) => ({ default: m.SubmissionsPage })))
const SubmissionDetailPage = lazy(() => import('@/pages/submissions/SubmissionDetailPage').then((m) => ({ default: m.SubmissionDetailPage })))
const SubmissionLossHistoryPage = lazy(() => import('@/pages/submissions/SubmissionLossHistoryPage').then((m) => ({ default: m.SubmissionLossHistoryPage })))
const SubmissionCreatePage = lazy(() => import('@/pages/submissions/SubmissionCreatePage').then((m) => ({ default: m.SubmissionCreatePage })))

// Policies
const PoliciesPage = lazy(() => import('@/pages/policies/PoliciesPage').then((m) => ({ default: m.PoliciesPage })))
const PolicyDetailPage = lazy(() => import('@/pages/policies/PolicyDetailPage').then((m) => ({ default: m.PolicyDetailPage })))

// Agents & Carriers
const AgentsPage = lazy(() => import('@/pages/agents/AgentsPage').then((m) => ({ default: m.AgentsPage })))
const AgentDetailPage = lazy(() => import('@/pages/agents/AgentDetailPage').then((m) => ({ default: m.AgentDetailPage })))
const CarriersPage = lazy(() => import('@/pages/carriers/CarriersPage').then((m) => ({ default: m.CarriersPage })))
const CarrierDetailPage = lazy(() => import('@/pages/carriers/CarrierDetailPage').then((m) => ({ default: m.CarrierDetailPage })))

// Users
const UsersPage = lazy(() => import('@/pages/users/UsersPage').then((m) => ({ default: m.UsersPage })))

// Inbox
const InboxPage = lazy(() => import('@/pages/inbox/InboxPage').then((m) => ({ default: m.InboxPage })))
const InboxDetailPage = lazy(() => import('@/pages/inbox/InboxDetailPage').then((m) => ({ default: m.InboxDetailPage })))

// Documents
const DocumentLibraryPage = lazy(() => import('@/pages/documents/DocumentLibraryPage').then((m) => ({ default: m.DocumentLibraryPage })))
const TemplateEditorPage = lazy(() => import('@/pages/documents/TemplateEditorPage').then((m) => ({ default: m.TemplateEditorPage })))

// Tasks
const TaskQueuePage = lazy(() => import('@/pages/tasks/TaskQueuePage').then((m) => ({ default: m.TaskQueuePage })))

// Admin
const TaskTypesAdminPage = lazy(() => import('@/pages/admin/TaskTypesAdminPage').then((m) => ({ default: m.TaskTypesAdminPage })))
const WorkflowsAdminPage = lazy(() => import('@/pages/admin/WorkflowsAdminPage').then((m) => ({ default: m.WorkflowsAdminPage })))
const HolidayCalendarAdminPage = lazy(() => import('@/pages/admin/HolidayCalendarAdminPage').then((m) => ({ default: m.HolidayCalendarAdminPage })))
const EscalationRulesAdminPage = lazy(() => import('@/pages/admin/EscalationRulesAdminPage').then((m) => ({ default: m.EscalationRulesAdminPage })))
const FeesAdminPage = lazy(() => import('@/pages/admin/FeesAdminPage').then((m) => ({ default: m.FeesAdminPage })))
const AdminRatingPage = lazy(() => import('@/pages/admin/AdminRatingPage').then((m) => ({ default: m.AdminRatingPage })))
const AdminRatingPlanDetailPage = lazy(() => import('@/pages/admin/AdminRatingPlanDetailPage').then((m) => ({ default: m.AdminRatingPlanDetailPage })))
const AdminRatingPlanVersionPage = lazy(() => import('@/pages/admin/AdminRatingPlanVersionPage').then((m) => ({ default: m.AdminRatingPlanVersionPage })))
const AdminShadowRatingPage = lazy(() => import('@/pages/admin/AdminShadowRatingPage'))
const RolePermissionsPage = lazy(() => import('@/pages/admin/RolePermissionsPage').then((m) => ({ default: m.RolePermissionsPage })))
const DatabaseStatusPage = lazy(() => import('@/pages/admin/DatabaseStatusPage').then((m) => ({ default: m.DatabaseStatusPage })))

// Quotes & Billing
const QuoteWriteupPage = lazy(() => import('@/pages/quotes/QuoteWriteupPage'))
const InvoicesPage = lazy(() => import('@/pages/billing/InvoicesPage').then((m) => ({ default: m.InvoicesPage })))
const ReceiptsPage = lazy(() => import('@/pages/billing/ReceiptsPage').then((m) => ({ default: m.ReceiptsPage })))
const CashApplicationPage = lazy(() => import('@/pages/billing/CashApplicationPage').then((m) => ({ default: m.CashApplicationPage })))
const CashDistributionPage = lazy(() => import('@/pages/billing/CashDistributionPage').then((m) => ({ default: m.CashDistributionPage })))
const DisbursementsPage = lazy(() => import('@/pages/billing/DisbursementsPage').then((m) => ({ default: m.DisbursementsPage })))
const StatementReconciliationPage = lazy(() => import('@/pages/billing/StatementReconciliationPage').then((m) => ({ default: m.StatementReconciliationPage })))
const ActivityPage = lazy(() => import('@/pages/billing/ActivityPage').then((m) => ({ default: m.ActivityPage })))
const PeriodClosePage = lazy(() => import('@/pages/billing/PeriodClosePage').then((m) => ({ default: m.PeriodClosePage })))
const SyncHealthPage = lazy(() => import('@/pages/billing/SyncHealthPage').then((m) => ({ default: m.SyncHealthPage })))
const ReportsPage = lazy(() => import('@/pages/reports/ReportsPage').then((m) => ({ default: m.ReportsPage })))

const PageFallback = () => (
  <div className="flex items-center justify-center h-full">
    <LoadingSpinner />
  </div>
)

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  const accessToken = useAuthStore((s) => s.accessToken)
  const setAuth = useAuthStore((s) => s.setAuth)
  const clearAuth = useAuthStore((s) => s.clearAuth)
  const [checkingSession, setCheckingSession] = useState(true)

  useEffect(() => {
    let cancelled = false

    if (!isAuthenticated || accessToken) {
      setCheckingSession(false)
      return
    }

    setCheckingSession(true)
    authApi.refreshSession()
      .then((session) => {
        if (!cancelled) setAuth(session.user, session.accessToken)
      })
      .catch((error) => {
        const status = axios.isAxiosError(error) ? error.response?.status : undefined
        if (!cancelled && status === 401) clearAuth()
      })
      .finally(() => {
        if (!cancelled) setCheckingSession(false)
      })

    return () => { cancelled = true }
  }, [accessToken, clearAuth, isAuthenticated, setAuth])

  if (checkingSession) return <PageFallback />
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
        <ErrorBoundary>
          <Routes>
            {/* Public */}
            <Route element={<AuthLayout />}>
              <Route
                path="/login"
                element={
                  <Suspense fallback={<PageFallback />}>
                    <LoginPage />
                  </Suspense>
                }
              />
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
              <Route path="/submissions/:id/loss-history" element={<SubmissionLossHistoryPage />} />
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
              <Route path="/admin/role-permissions" element={<RolePermissionsPage />} />
              <Route path="/admin/database-status" element={<DatabaseStatusPage />} />
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
        </ErrorBoundary>
      </BrowserRouter>
      <Toaster richColors position="top-right" />
      {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
    </QueryClientProvider>
  )
}
