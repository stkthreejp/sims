import { useShallow } from 'zustand/react/shallow'
import { useAuthStore } from '@/store/authStore'

export function usePermissions() {
  const { hasPermission, hasRole } = useAuthStore(
    useShallow((s) => ({ hasPermission: s.hasPermission, hasRole: s.hasRole }))
  )

  const isAdmin = hasRole('Admin')

  return {
    // Insureds
    canViewInsureds: hasPermission('insureds.view'),
    canCreateInsureds: hasPermission('insureds.create'),
    canEditInsureds: hasPermission('insureds.edit'),
    canDeleteInsureds: hasPermission('insureds.delete'),

    // Policies / Quotes
    canViewPolicies: hasPermission('policies.view'),
    canCreatePolicies: hasPermission('policies.create'),
    canEditPolicies: hasPermission('policies.edit'),
    canDeletePolicies: hasPermission('policies.delete'),
    canBindPolicies: hasPermission('policies.bind'),
    canIssuePolicies: hasPermission('policies.issue'),
    canEndorsePolicies: hasPermission('policies.endorse'),
    canRenewPolicies: hasPermission('policies.renew'),
    canCancelPolicies: hasPermission('policies.cancel'),

    // Notes
    canCreateNotes: hasPermission('policies.notes.create'),
    canEditNotes: hasPermission('policies.notes.edit'),
    canDeleteNotes: hasPermission('policies.notes.delete'),

    // Attachments
    canUploadAttachments: hasPermission('policies.attachments.upload'),
    canDeleteAttachments: hasPermission('policies.attachments.delete'),

    // Admin
    canViewUsers: hasPermission('admin.users.view'),
    canManageUsers: hasPermission('admin.users.manage'),
    canViewRoles: hasPermission('admin.roles.view'),
    canManageRoles: hasPermission('admin.roles.manage'),
    canManageSystem: hasPermission('admin.system.manage'),
    canManageUnderwritingControls: hasPermission('admin.underwriting-controls.manage'),
    canManageUnderwriting: hasPermission('underwriting.manage'),
    canOverrideClearance: hasPermission('underwriting.clearance.override'),
    canManageAccounting: hasPermission('accounting.manage'),
    canAdminAccounting: hasPermission('accounting.admin'),
    canManageRating: hasPermission('rating.manage'),
    canAdminRating: hasPermission('rating.admin'),
    canViewReportData: hasPermission('reports.view'),
    canViewClaims: hasPermission('claims.view'),
    canManageClaims: hasPermission('claims.manage'),

    // Navigation sections (control sidebar visibility)
    canViewSubmissions: hasPermission('nav.submissions'),
    canViewInbox: hasPermission('nav.inbox'),
    canViewAgents: hasPermission('nav.agents'),
    canViewCarriers: hasPermission('nav.carriers'),
    canViewDocumentLibrary: hasPermission('nav.document-library'),
    canViewComplianceDocumentation: hasPermission('nav.compliance-documentation'),
    canViewReports: hasPermission('nav.reports') && hasPermission('reports.view'),
    canViewBilling: hasPermission('nav.billing') && hasPermission('accounting.manage'),
    canViewRatingAdmin: hasPermission('nav.admin.rating') && hasPermission('rating.admin'),
    canViewTaskAdmin: hasPermission('nav.admin.tasks') && hasPermission('admin.system.manage'),
    canViewFeesAdmin: hasPermission('nav.admin.fees') && hasPermission('admin.system.manage'),
    canViewBordereauxAdmin: hasPermission('nav.admin.bordereaux') && hasPermission('accounting.admin'),

    isAdmin,
  }
}
