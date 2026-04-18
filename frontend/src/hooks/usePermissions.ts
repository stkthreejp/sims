import { useAuthStore } from '@/store/authStore'

/**
 * Returns boolean flags for every permission in the system.
 * Use these to conditionally show/hide action buttons across the UI.
 */
export function usePermissions() {
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const hasRole = useAuthStore((s) => s.hasRole)

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

    isAdmin,
  }
}
