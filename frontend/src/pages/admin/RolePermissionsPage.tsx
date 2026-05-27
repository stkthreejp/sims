import { useState, useMemo, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ShieldCheck, Save, Info } from 'lucide-react'
import { toast } from 'sonner'
import { rolesApi } from '@/api/roles.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { Role, Permission } from '@/types/role.types'

// Friendly display order for categories
const CATEGORY_ORDER = [
  'Navigation',
  'Insureds',
  'Policies',
  'Notes',
  'Attachments',
  'Admin',
]

// Local draft state: roleId -> Set of permissionIds
type Draft = Record<string, Set<number>>

function buildDraft(roles: Role[], permissions: Permission[]): Draft {
  const permByName = new Map(permissions.map((p) => [p.name, p.id]))
  const draft: Draft = {}
  for (const role of roles) {
    draft[role.id] = new Set(
      role.permissions.flatMap((name) => {
        const id = permByName.get(name)
        return id !== undefined ? [id] : []
      })
    )
  }
  return draft
}

export function RolePermissionsPage() {
  const qc = useQueryClient()

  const { data: roles, isLoading: rolesLoading } = useQuery({
    queryKey: ['roles'],
    queryFn: rolesApi.getAll,
  })

  const { data: permissions, isLoading: permsLoading } = useQuery({
    queryKey: ['permissions'],
    queryFn: rolesApi.getPermissions,
  })

  // Local draft — initialized once when data loads
  const [draft, setDraft] = useState<Draft | null>(null)
  const [dirty, setDirty] = useState<Set<string>>(new Set())

  // Init draft when data arrives (only once)
  useEffect(() => {
    if (roles && permissions && !draft) {
      setDraft(buildDraft(roles, permissions))
    }
  }, [roles, permissions, draft])

  // Group permissions by category, in defined order
  const grouped = useMemo(() => {
    const map = new Map<string, Permission[]>()
    for (const cat of CATEGORY_ORDER) map.set(cat, [])
    for (const p of permissions ?? []) {
      if (!map.has(p.category)) map.set(p.category, [])
      map.get(p.category)!.push(p)
    }
    return map
  }, [permissions])

  const saveMutation = useMutation({
    mutationFn: ({ roleId, ids }: { roleId: string; ids: number[] }) =>
      rolesApi.updatePermissions(roleId, ids),
    onSuccess: (_, { roleId }) => {
      setDirty((prev) => { const s = new Set(prev); s.delete(roleId); return s })
      qc.invalidateQueries({ queryKey: ['roles'] })
      toast.success('Permissions saved')
    },
    onError: (err: any) =>
      toast.error(err?.response?.data?.errorMessage ?? 'Failed to save permissions'),
  })

  const toggle = (roleId: string, permId: number) => {
    setDraft((prev) => {
      if (!prev) return prev
      const current = new Set(prev[roleId])
      if (current.has(permId)) current.delete(permId)
      else current.add(permId)
      return { ...prev, [roleId]: current }
    })
    setDirty((prev) => new Set(prev).add(roleId))
  }

  const save = (role: Role) => {
    if (!draft) return
    saveMutation.mutate({ roleId: role.id, ids: Array.from(draft[role.id]) })
  }

  if (rolesLoading || permsLoading || !draft || !roles || !permissions) {
    return <LoadingSpinner />
  }

  // Editable roles (Admin is read-only)
  const editableRoles = roles.filter((r) => r.name !== 'Admin')
  const adminRole = roles.find((r) => r.name === 'Admin')

  const roleColumns = adminRole ? [adminRole, ...editableRoles] : editableRoles

  return (
    <div>
      <PageHeader
        title="Role Permissions"
        description="Control which sections and actions each role can access. Changes take effect on next login."
      />

      <div className="admin-panel overflow-x-auto">
        <table className="sd-table">
          <thead>
            <tr>
              <th className="w-56">
                Permission
              </th>
              {roleColumns.map((role) => (
                <th key={role.id} className="px-4 py-3 text-center min-w-[130px]">
                  <div className="flex flex-col items-center gap-1.5">
                    <span className="text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-2)' }}>
                      {role.name}
                    </span>
                    {role.name === 'Admin' ? (
                      <span className="text-[10px] text-slate-400 italic">system — full access</span>
                    ) : (
                      <button
                        onClick={() => save(role)}
                        disabled={!dirty.has(role.id) || saveMutation.isPending}
                        className="sd-btn primary sm"
                      >
                        <Save className="h-2.5 w-2.5" />
                        Save
                      </button>
                    )}
                  </div>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {Array.from(grouped.entries()).map(([category, perms]) => {
              if (perms.length === 0) return null
              return (
                <>
                  {/* Category header row */}
                  <tr key={`cat-${category}`} style={{ background: 'var(--surface-2)' }}>
                    <td
                      colSpan={roleColumns.length + 1}
                      className="px-5 py-2 text-[10px] font-bold uppercase tracking-widest"
                      style={{ color: 'var(--ink-4)' }}
                    >
                      {category}
                    </td>
                  </tr>
                  {perms.map((perm) => (
                    <tr
                      key={perm.id}
                      className="transition-colors"
                    >
                      <td className="px-5 py-2.5">
                        <span className="text-xs font-medium" style={{ color: 'var(--ink-2)' }}>{perm.displayName}</span>
                        <span className="ml-2 text-[10px] font-mono" style={{ color: 'var(--ink-4)' }}>{perm.name}</span>
                      </td>
                      {roleColumns.map((role) => {
                        const isAdmin = role.name === 'Admin'
                        const checked = isAdmin || draft[role.id]?.has(perm.id)
                        return (
                          <td key={role.id} className="px-4 py-2.5 text-center">
                            <input
                              type="checkbox"
                              checked={checked}
                              disabled={isAdmin}
                              onChange={() => !isAdmin && toggle(role.id, perm.id)}
                              className="h-4 w-4 rounded border-slate-300 text-blue-600 accent-blue-600 cursor-pointer disabled:cursor-default disabled:opacity-50"
                            />
                          </td>
                        )
                      })}
                    </tr>
                  ))}
                </>
              )
            })}
          </tbody>
        </table>

        <div className="flex items-center gap-1.5 px-5 py-3 text-[11px]" style={{ borderTop: '1px solid var(--line-2)', color: 'var(--ink-4)' }}>
          <Info className="h-3 w-3 flex-shrink-0" />
          Admin always has full access and cannot be edited. Users must log out and back in for changes to take effect.
        </div>
      </div>
    </div>
  )
}
