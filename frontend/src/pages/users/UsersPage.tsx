import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Search, Users, Plus, X, Check, Pencil, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { usersApi } from '@/api/users.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ErrorState } from '@/components/common/ErrorState'
import { EmptyState } from '@/components/common/EmptyState'
import { getApiErrorMessage } from '@/lib/apiError'
import { usePermissions } from '@/hooks/usePermissions'
import { formatDateTime } from '@/lib/utils'
import type { User, UserStatus, UserCreate, UserUpdate } from '@/types/user.types'

const statusStyles: Record<UserStatus, string> = {
  Active: 'bg-green-100 text-green-700',
  Inactive: 'bg-slate-100 text-slate-500',
  Locked: 'bg-red-100 text-red-700',
}

const ALL_ROLES = ['Admin', 'Underwriter', 'CSR', 'ReadOnly']

// ── Edit / Create Modal ───────────────────────────────────────────────────────

function UserModal({
  user,
  onClose,
}: {
  user: User | null   // null = create mode
  onClose: () => void
}) {
  const qc = useQueryClient()

  const [firstName, setFirstName] = useState(user?.firstName ?? '')
  const [lastName, setLastName] = useState(user?.lastName ?? '')
  const [email, setEmail] = useState(user?.email ?? '')
  const [userName, setUserName] = useState(user?.userName ?? '')
  const [phoneNumber, setPhoneNumber] = useState(user?.phoneNumber ?? '')
  const [password, setPassword] = useState('')
  const [status, setStatus] = useState<UserStatus>(user?.status ?? 'Active')
  const [roles, setRoles] = useState<string[]>(user?.roles ?? ['ReadOnly'])

  const toggleRole = (role: string) => {
    setRoles((prev) =>
      prev.includes(role) ? prev.filter((r) => r !== role) : [...prev, role]
    )
  }

  const createMutation = useMutation({
    mutationFn: (data: UserCreate) => usersApi.create(data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['users'] })
      toast.success('User created')
      onClose()
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed to create user')),
  })

  const updateMutation = useMutation({
    mutationFn: (data: UserUpdate) => usersApi.update(user!.id, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['users'] })
      toast.success('User updated')
      onClose()
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed to update user')),
  })

  const isPending = createMutation.isPending || updateMutation.isPending

  const handleSubmit = () => {
    if (!firstName.trim() || !email.trim()) {
      toast.error('First name and email are required')
      return
    }
    if (roles.length === 0) {
      toast.error('At least one role is required')
      return
    }
    if (user) {
      updateMutation.mutate({ email, firstName, lastName, phoneNumber: phoneNumber || undefined, status, roles })
    } else {
      if (!userName.trim() || !password.trim()) {
        toast.error('Username and password are required')
        return
      }
      createMutation.mutate({ userName, email, firstName, lastName, phoneNumber: phoneNumber || undefined, password, roles })
    }
  }

  return (
    <div className="sims-modal-backdrop">
      <div className="sims-modal">
        <div className="sims-modal-head">
          <h2 className="sims-modal-title">{user ? 'Edit User' : 'Create User'}</h2>
          <button onClick={onClose} className="sims-icon-btn" aria-label="Close"><X size={16} strokeWidth={1.7} /></button>
        </div>

        <div className="sims-modal-body space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="sims-field-label">First Name *</label>
              <input value={firstName} onChange={(e) => setFirstName(e.target.value)}
                className="sims-input" />
            </div>
            <div>
              <label className="sims-field-label">Last Name</label>
              <input value={lastName} onChange={(e) => setLastName(e.target.value)}
                className="sims-input" />
            </div>
          </div>

          <div>
            <label className="sims-field-label">Email *</label>
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)}
              className="sims-input" />
          </div>

        {!user && (
          <>
            <div>
              <label className="sims-field-label">Username *</label>
              <input value={userName} onChange={(e) => setUserName(e.target.value)}
                className="sims-input" />
            </div>
            <div>
              <label className="sims-field-label">Password *</label>
              <input type="password" value={password} onChange={(e) => setPassword(e.target.value)}
                className="sims-input" />
            </div>
          </>
        )}

          <div>
            <label className="sims-field-label">Phone</label>
            <input value={phoneNumber} onChange={(e) => setPhoneNumber(e.target.value)}
              className="sims-input" />
          </div>

        {user && (
          <div>
            <label className="sims-field-label">Status</label>
            <select value={status} onChange={(e) => setStatus(e.target.value as UserStatus)}
              className="sims-select">
              <option value="Active">Active</option>
              <option value="Inactive">Inactive</option>
              <option value="Locked">Locked</option>
            </select>
          </div>
        )}

          <div>
            <label className="sims-field-label">Roles *</label>
            <div className="flex flex-wrap gap-2">
              {ALL_ROLES.map((role) => (
                <button
                  key={role}
                  type="button"
                  onClick={() => toggleRole(role)}
                  className="sd-btn sm"
                  style={{
                    height: 28,
                    border: `1px solid ${roles.includes(role) ? 'var(--accent)' : 'var(--line)'}`,
                    background: roles.includes(role) ? 'var(--accent-soft)' : 'var(--surface)',
                    color: roles.includes(role) ? 'var(--accent-ink)' : 'var(--ink-2)',
                    borderRadius: 'var(--r-pill)',
                  }}
                >
                  {role}
                </button>
              ))}
            </div>
          </div>
        </div>

        <div className="sims-modal-foot">
          <button onClick={onClose} className="sd-btn outline sm">
            Cancel
          </button>
          <button
            onClick={handleSubmit}
            disabled={isPending}
            className="sd-btn primary sm"
          >
            <Check size={14} /> {user ? 'Save Changes' : 'Create User'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Main page ─────────────────────────────────────────────────────────────────

export function UsersPage() {
  const qc = useQueryClient()
  const { canManageUsers } = usePermissions()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [editingUser, setEditingUser] = useState<User | null | undefined>(undefined) // undefined=closed, null=create, User=edit

  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey: ['users', 'list', { search, page }],
    queryFn: () => usersApi.getAll({ search, page, pageSize: 25 }),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => usersApi.delete(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['users'] })
      toast.success('User deleted')
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed to delete user')),
  })

  return (
    <div>
      <PageHeader title="Users" subtitle="Manage system users and roles" />

      <div className="bg-white rounded-lg border border-slate-200">
        <div className="p-4 border-b border-slate-100 flex gap-3">
          <div className="relative flex-1">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
            <input
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1) }}
              placeholder="Search users…"
              className="w-full pl-9 pr-3 py-2 border border-slate-200 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          {canManageUsers && (
            <button
              onClick={() => setEditingUser(null)}
              className="flex items-center gap-1.5 px-3 py-2 bg-blue-600 text-white rounded-md text-sm hover:bg-blue-700"
            >
              <Plus className="h-4 w-4" /> Add User
            </button>
          )}
        </div>

        {isLoading ? <LoadingSpinner /> : isError ? (
          <ErrorState error={error} onRetry={refetch} />
        ) : data?.items.length === 0 ? (
          <EmptyState icon={Users} title="No users found" />
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-100 bg-slate-50 text-left">
                <th className="px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wide">Name</th>
                <th className="px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wide">Username</th>
                <th className="px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wide">Roles</th>
                <th className="px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wide">Status</th>
                <th className="px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wide">Last Login</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {data?.items.map((u) => (
                <tr key={u.id} className="hover:bg-slate-50 group">
                  <td className="px-4 py-3">
                    <p className="font-medium text-slate-900">{u.fullName}</p>
                    <p className="text-xs text-slate-500">{u.email}</p>
                  </td>
                  <td className="px-4 py-3 text-slate-600 font-mono text-xs">{u.userName}</td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap gap-1">
                      {u.roles.map((r) => (
                        <span key={r} className="px-1.5 py-0.5 text-xs rounded bg-blue-50 text-blue-700 font-medium">{r}</span>
                      ))}
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${statusStyles[u.status]}`}>
                      {u.status}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-slate-500 text-xs">{formatDateTime(u.lastLoginAt)}</td>
                  <td className="px-4 py-3">
                    {canManageUsers && (
                      <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity justify-end">
                        <button
                          onClick={() => setEditingUser(u)}
                          className="p-1.5 rounded text-slate-400 hover:text-blue-600 hover:bg-blue-50"
                          title="Edit"
                        >
                          <Pencil className="h-3.5 w-3.5" />
                        </button>
                        <button
                          onClick={() => { if (confirm(`Delete user "${u.userName}"?`)) deleteMutation.mutate(u.id) }}
                          className="p-1.5 rounded text-slate-400 hover:text-red-600 hover:bg-red-50"
                          title="Delete"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {editingUser !== undefined && (
        <UserModal user={editingUser} onClose={() => setEditingUser(undefined)} />
      )}
    </div>
  )
}
