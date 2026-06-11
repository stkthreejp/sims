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

// Compliance
const ComplianceDocumentationPage = lazy(() => import('@/pages/compliance/ComplianceDocumentationPage').then((m) => ({ default: m.ComplianceDocumentationPage })))
const ComplianceDocumentDetailPage = lazy(() => import('@/pages/compliance/ComplianceDocumentDetailPage').then((m) => ({ default: m.ComplianceDocumentDetailPage })))
const ComplianceAttestationsPage = lazy(() => import('@/pages/compliance/ComplianceAttestationsPage').then((m) => ({ default: m.ComplianceAttestationsPage })))
const ComplianceReviewsPage = lazy(() => import('@/pages/compliance/ComplianceReviewsPage').then((m) => ({ default: m.ComplianceReviewsPage })))
const ComplianceEvidenceReportPage = lazy(() => import('@/pages/compliance/ComplianceEvidenceReportPage').then((m) => ({ default: m.ComplianceEvidenceReportPage })))

// Tasks
const TaskQueuePage = lazy(() => import('@/pages/tasks/TaskQueuePage').then((m) => ({ default: m.TaskQueuePage })))

// Admin
const TaskTypesAdminPage = lazy(() => import('@/pages/admin/TaskTypesAdminPage').then((m) => ({ default: m.TaskTypesAdminPage })))
const WorkflowsAdminPage = lazy(() => import('@/pages/admin/WorkflowsAdminPage').then((m) => ({ default: m.WorkflowsAdminPage })))
const HolidayCalendarAdminPage = lazy(() => import('@/pages/admin/HolidayCalendarAdminPage').then((m) => ({ default: m.HolidayCalendarAdminPage })))
const EscalationRulesAdminPage = lazy(() => import('@/pages/admin/EscalationRulesAdminPage').then((m) => ({ default: m.EscalationRulesAdminPage })))
const FeesAdminPage = lazy(() => import('@/pages/admin/FeesAdminPage').then((m) => ({ default: m.FeesAdminPage })))
const PolicyFormsAdminPage = lazy(() => import('@/pages/admin/PolicyFormsAdminPage').then((m) => ({ default: m.PolicyFormsAdminPage })))
const PolicyNumbersAdminPage = lazy(() => import('@/pages/admin/PolicyNumbersAdminPage').then((m) => ({ default: m.PolicyNumbersAdminPage })))
const AdminRatingPage = lazy(() => import('@/pages/admin/AdminRatingPage').then((m) => ({ default: m.AdminRatingPage })))
const AdminRatingPlanDetailPage = lazy(() => import('@/pages/admin/AdminRatingPlanDetailPage').then((m) => ({ default: m.AdminRatingPlanDetailPage })))
const AdminRatingPlanVersionPage = lazy(() => import('@/pages/admin/AdminRatingPlanVersionPage').then((m) => ({ default: m.AdminRatingPlanVersionPage })))
const AdminShadowRatingPage = lazy(() => import('@/pages/admin/AdminShadowRatingPage'))
const RolePermissionsPage = lazy(() => import('@/pages/admin/RolePermissionsPage').then((m) => ({ default: m.RolePermissionsPage })))
const DatabaseStatusPage = lazy(() => import('@/pages/admin/DatabaseStatusPage').then((m) => ({ default: m.DatabaseStatusPage })))
const AdminJobsPage = lazy(() => import('@/pages/admin/AdminJobsPage').then((m) => ({ default: m.AdminJobsPage })))
const LegalRequirementsPage = lazy(() => import('@/pages/admin/LegalRequirementsPage').then((m) => ({ default: m.LegalRequirementsPage })))
const AiSettingsAdminPage = lazy(() => import('@/pages/admin/AiSettingsAdminPage').then((m) => ({ default: m.AiSettingsAdminPage })))
const ProgramConfigurationAdminPage = lazy(() => import('@/pages/admin/ProgramConfigurationAdminPage').then((m) => ({ default: m.ProgramConfigurationAdminPage })))
const IntermediariesAdminPage = lazy(() => import('@/pages/admin/IntermediariesAdminPage').then((m) => ({ default: m.IntermediariesAdminPage })))
const UnderwritingControlsAdminPage = lazy(() => import('@/pages/admin/UnderwritingControlsAdminPage').then((m) => ({ default: m.UnderwritingControlsAdminPage })))
const SurplusLinesAdminPage = lazy(() => import('@/pages/admin/SurplusLinesAdminPage').then((m) => ({ default: m.SurplusLinesAdminPage })))
const AdminBordereauxProfilesPage = lazy(() => import('@/pages/admin/AdminBordereauxProfilesPage'))

// Quotes & Billing
const QuoteDetailPage = lazy(() => import('@/pages/quotes/QuoteDetailPage').then((m) => ({ default: m.QuoteDetailPage })))
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
const BordereauxWorkbenchPage = lazy(() => import('@/pages/reports/BordereauxWorkbenchPage').then((m) => ({ default: m.BordereauxWorkbenchPage })))
const NotFoundPage = lazy(() => import('@/pages/NotFoundPage').then((m) => ({ default: m.NotFoundPage })))

const PageFallback = () => (
  <div className="flex items-center justify-center h-full">
    <LoadingSpinner />
  </div>
)

const Permissions = {
  InsuredsView: 'insureds.view',
  InsuredsCreate: 'insureds.create',
  InsuredsEdit: 'insureds.edit',
  PoliciesView: 'policies.view',
  AdminUsersView: 'admin.users.view',
  AdminRolesManage: 'admin.roles.manage',
  AdminSystemManage: 'admin.system.manage',
  AdminUnderwritingControlsManage: 'admin.underwriting-controls.manage',
  UnderwritingManage: 'underwriting.manage',
  AccountingManage: 'accounting.manage',
  RatingManage: 'rating.manage',
  RatingAdmin: 'rating.admin',
  ReportsView: 'reports.view',
  NavSubmissions: 'nav.submissions',
  NavInbox: 'nav.inbox',
  NavAgents: 'nav.agents',
  NavCarriers: 'nav.carriers',
  NavDocumentLibrary: 'nav.document-library',
  NavComplianceDocumentation: 'nav.compliance-documentation',
  NavBilling: 'nav.billing',
  NavReports: 'nav.reports',
  NavAdminRating: 'nav.admin.rating',
  NavAdminTasks: 'nav.admin.tasks',
  NavAdminFees: 'nav.admin.fees',
  NavAdminBordereaux: 'nav.admin.bordereaux',
  AccountingAdmin: 'accounting.admin',
} as const

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

function PermissionRoute({ permission, children }: { permission: string | string[]; children: React.ReactNode }) {
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const required = Array.isArray(permission) ? permission : [permission]

  return required.some(hasPermission) ? <>{children}</> : <Navigate to="/dashboard" replace />
}

// All listed permissions must be present (AND logic) — mirrors compound sidebar guards
function PermissionAllRoute({ permissions, children }: { permissions: string[]; children: React.ReactNode }) {
  const hasPermission = useAuthStore((s) => s.hasPermission)

  return permissions.every(hasPermission) ? <>{children}</> : <Navigate to="/dashboard" replace />
}

const withPermission = (permission: string | string[], children: React.ReactNode) => (
  <PermissionRoute permission={permission}>{children}</PermissionRoute>
)

const withAllPermissions = (permissions: string[], children: React.ReactNode) => (
  <PermissionAllRoute permissions={permissions}>{children}</PermissionAllRoute>
)

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

              <Route path="/insureds" element={withPermission(Permissions.InsuredsView, <InsuredsPage />)} />
              <Route path="/insureds/new" element={withPermission(Permissions.InsuredsCreate, <InsuredCreatePage />)} />
              <Route path="/insureds/:id" element={withPermission(Permissions.InsuredsView, <InsuredDetailPage />)} />
              <Route path="/insureds/:id/edit" element={withPermission(Permissions.InsuredsEdit, <InsuredEditPage />)} />

              <Route path="/submissions/new" element={withPermission(Permissions.UnderwritingManage, <SubmissionCreatePage />)} />
              <Route path="/submissions/:id/loss-history" element={withPermission(Permissions.UnderwritingManage, <SubmissionLossHistoryPage />)} />
              <Route path="/submissions/:id" element={withPermission(Permissions.NavSubmissions, <SubmissionDetailPage />)} />
              <Route path="/submissions" element={withPermission(Permissions.NavSubmissions, <SubmissionsPage />)} />

              <Route path="/policies" element={withPermission(Permissions.PoliciesView, <PoliciesPage />)} />
              <Route path="/policies/:id" element={withPermission(Permissions.PoliciesView, <PolicyDetailPage />)} />

              <Route path="/agents" element={withPermission(Permissions.NavAgents, <AgentsPage />)} />
              <Route path="/agents/:id" element={withPermission(Permissions.NavAgents, <AgentDetailPage />)} />
              <Route path="/carriers" element={withPermission(Permissions.NavCarriers, <CarriersPage />)} />
              <Route path="/carriers/:id" element={withPermission(Permissions.NavCarriers, <CarrierDetailPage />)} />
              <Route path="/users" element={withPermission(Permissions.AdminUsersView, <UsersPage />)} />

              <Route path="/inbox" element={withPermission(Permissions.NavInbox, <InboxPage />)} />
              <Route path="/inbox/:id" element={withPermission(Permissions.NavInbox, <InboxDetailPage />)} />

              <Route path="/document-library" element={withPermission(Permissions.NavDocumentLibrary, <DocumentLibraryPage />)} />
              <Route path="/document-library/new" element={withPermission(Permissions.NavDocumentLibrary, <TemplateEditorPage />)} />
              <Route path="/document-library/:id" element={withPermission(Permissions.NavDocumentLibrary, <TemplateEditorPage />)} />
              <Route path="/compliance-documentation" element={withPermission(Permissions.NavComplianceDocumentation, <ComplianceDocumentationPage />)} />
              <Route path="/compliance-documentation/attestations" element={withPermission(Permissions.NavComplianceDocumentation, <ComplianceAttestationsPage />)} />
              <Route path="/compliance-documentation/reviews" element={withPermission(Permissions.NavComplianceDocumentation, <ComplianceReviewsPage />)} />
              <Route path="/compliance-documentation/:id/report" element={withPermission(Permissions.NavComplianceDocumentation, <ComplianceEvidenceReportPage />)} />
              <Route path="/compliance-documentation/:id" element={withPermission(Permissions.NavComplianceDocumentation, <ComplianceDocumentDetailPage />)} />

              {/* /tasks is intentionally available to all authenticated users — task queue is not role-restricted */}
              <Route path="/tasks" element={<TaskQueuePage />} />

              <Route path="/admin/task-types" element={withAllPermissions([Permissions.NavAdminTasks, Permissions.AdminSystemManage], <TaskTypesAdminPage />)} />
              <Route path="/admin/workflows" element={withAllPermissions([Permissions.NavAdminTasks, Permissions.AdminSystemManage], <WorkflowsAdminPage />)} />
              <Route path="/admin/holiday-calendar" element={withAllPermissions([Permissions.NavAdminTasks, Permissions.AdminSystemManage], <HolidayCalendarAdminPage />)} />
              <Route path="/admin/escalation-rules" element={withAllPermissions([Permissions.NavAdminTasks, Permissions.AdminSystemManage], <EscalationRulesAdminPage />)} />
              <Route path="/admin/fees" element={withAllPermissions([Permissions.NavAdminFees, Permissions.AdminSystemManage], <FeesAdminPage />)} />
              <Route path="/admin/policy-forms" element={withPermission(Permissions.UnderwritingManage, <PolicyFormsAdminPage />)} />
              <Route path="/admin/policy-numbers" element={withPermission(Permissions.UnderwritingManage, <PolicyNumbersAdminPage />)} />
              <Route path="/admin/rating" element={withAllPermissions([Permissions.NavAdminRating, Permissions.RatingAdmin], <AdminRatingPage />)} />
              <Route path="/admin/rating/plans/:planId" element={withAllPermissions([Permissions.NavAdminRating, Permissions.RatingAdmin], <AdminRatingPlanDetailPage />)} />
              <Route path="/admin/rating/versions/:versionId" element={withAllPermissions([Permissions.NavAdminRating, Permissions.RatingAdmin], <AdminRatingPlanVersionPage />)} />
              <Route path="/admin/rating/shadow" element={withAllPermissions([Permissions.NavAdminRating, Permissions.RatingAdmin], <AdminShadowRatingPage />)} />
              <Route path="/admin/role-permissions" element={withPermission(Permissions.AdminRolesManage, <RolePermissionsPage />)} />
              <Route path="/admin/database-status" element={withPermission(Permissions.AdminSystemManage, <DatabaseStatusPage />)} />
              <Route path="/admin/jobs" element={withPermission(Permissions.AdminSystemManage, <AdminJobsPage />)} />
              <Route path="/admin/legal-requirements" element={withPermission(Permissions.AdminSystemManage, <LegalRequirementsPage />)} />
              <Route path="/admin/ai-settings" element={withPermission(Permissions.AdminSystemManage, <AiSettingsAdminPage />)} />
              <Route path="/admin/programs" element={withPermission(Permissions.AdminUnderwritingControlsManage, <ProgramConfigurationAdminPage />)} />
              <Route path="/admin/intermediaries" element={withPermission(Permissions.AdminSystemManage, <IntermediariesAdminPage />)} />
              <Route path="/admin/underwriting-controls" element={withPermission(Permissions.AdminUnderwritingControlsManage, <UnderwritingControlsAdminPage />)} />
              <Route path="/admin/surplus-lines" element={withPermission(Permissions.AdminSystemManage, <SurplusLinesAdminPage />)} />
              <Route path="/admin/bordereaux-profiles" element={withAllPermissions([Permissions.NavAdminBordereaux, Permissions.AccountingAdmin], <AdminBordereauxProfilesPage />)} />
              <Route path="/quotes/:quoteId" element={withPermission(Permissions.PoliciesView, <QuoteDetailPage />)} />
              <Route path="/quotes/:quoteId/writeup" element={withPermission(Permissions.PoliciesView, <QuoteWriteupPage />)} />
              <Route path="/billing/invoices" element={withAllPermissions([Permissions.NavBilling, Permissions.AccountingManage], <InvoicesPage />)} />
              <Route path="/billing/receipts" element={withAllPermissions([Permissions.NavBilling, Permissions.AccountingManage], <ReceiptsPage />)} />
              <Route path="/billing/cash-application" element={withAllPermissions([Permissions.NavBilling, Permissions.AccountingManage], <CashApplicationPage />)} />
              <Route path="/billing/cash-distribution" element={withAllPermissions([Permissions.NavBilling, Permissions.AccountingManage], <CashDistributionPage />)} />
              <Route path="/billing/disbursements" element={withAllPermissions([Permissions.NavBilling, Permissions.AccountingManage], <DisbursementsPage />)} />
              <Route path="/billing/statement-reconciliation" element={withAllPermissions([Permissions.NavBilling, Permissions.AccountingManage], <StatementReconciliationPage />)} />
              <Route path="/billing/activity" element={withAllPermissions([Permissions.NavBilling, Permissions.AccountingManage], <ActivityPage />)} />
              <Route path="/billing/period-close" element={withAllPermissions([Permissions.NavBilling, Permissions.AccountingManage], <PeriodClosePage />)} />
              <Route path="/billing/sync-health" element={withAllPermissions([Permissions.NavBilling, Permissions.AccountingManage], <SyncHealthPage />)} />
              <Route path="/reports" element={withAllPermissions([Permissions.NavReports, Permissions.ReportsView], <ReportsPage />)} />
              <Route path="/reports/bordereaux" element={withAllPermissions([Permissions.NavReports, Permissions.AccountingAdmin], <BordereauxWorkbenchPage />)} />
            </Route>

            <Route path="*" element={<NotFoundPage />} />
          </Routes>
        </ErrorBoundary>
      </BrowserRouter>
      <Toaster richColors position="top-right" />
      {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
    </QueryClientProvider>
  )
}
