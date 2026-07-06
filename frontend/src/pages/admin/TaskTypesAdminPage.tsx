import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2, Edit2 } from 'lucide-react'
import { toast } from 'sonner'
import { adminTaskTypesApi } from '@/api/admin.api'
import { rolesApi } from '@/api/roles.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ErrorState } from '@/components/common/ErrorState'
import { getApiErrorMessage } from '@/lib/apiError'
import type { TaskTypeListItem, TaskPriority } from '@/types/task.types'

const PRIORITY_OPTIONS: TaskPriority[] = ['Low', 'Medium', 'High']

interface FormState { name: string; description: string; defaultPriority: TaskPriority; assignedRoleTemplate: string; dueDateFormula: string; isActive: boolean }
const EMPTY: FormState = { name: '', description: '', defaultPriority: 'Medium', assignedRoleTemplate: '', dueDateFormula: '', isActive: true }

export function TaskTypesAdminPage() {
  const qc = useQueryClient()
  const [editing, setEditing] = useState<string | null>(null)
  const [form, setForm] = useState<FormState>(EMPTY)
  const [showForm, setShowForm] = useState(false)

  const { data: types = [], isLoading, isError, error, refetch } = useQuery({
    queryKey: ['admin', 'task-types'],
    queryFn: () => adminTaskTypesApi.getAll(),
  })

  const { data: roles = [] } = useQuery({
    queryKey: ['roles'],
    queryFn: rolesApi.getAll,
  })

  const { mutate: save, isPending: saving } = useMutation({
    mutationFn: () => editing
      ? adminTaskTypesApi.update(editing, form)
      : adminTaskTypesApi.create(form),
    onSuccess: () => {
      toast.success(editing ? 'Task type updated' : 'Task type created')
      qc.invalidateQueries({ queryKey: ['admin', 'task-types'] })
      setEditing(null); setShowForm(false); setForm(EMPTY)
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Save failed')),
  })

  const { mutate: remove } = useMutation({
    mutationFn: (id: string) => adminTaskTypesApi.delete(id),
    onSuccess: () => { toast.success('Deleted'); qc.invalidateQueries({ queryKey: ['admin', 'task-types'] }) },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Delete failed')),
  })

  function openEdit(t: TaskTypeListItem) {
    adminTaskTypesApi.getById(t.id).then((full) => {
      setForm({ name: full.name, description: full.description ?? '', defaultPriority: full.defaultPriority, assignedRoleTemplate: full.assignedRoleTemplate ?? '', dueDateFormula: full.dueDateFormula ?? '', isActive: full.isActive })
      setEditing(t.id); setShowForm(true)
    })
  }

  if (isLoading) return <LoadingSpinner />
  if (isError) return (
    <div className="p-6 space-y-5">
      <PageHeader title="Task Types" />
      <ErrorState error={error} onRetry={refetch} />
    </div>
  )

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="Task Types"
        subtitle={`${types.length} configured`}
        action={
          <button onClick={() => { setEditing(null); setForm(EMPTY); setShowForm(true) }} className="sd-btn primary">
            <Plus className="h-4 w-4" /> New Task Type
          </button>
        }
      />

      {showForm && (
        <div className="admin-panel max-w-xl p-5 space-y-4">
          <h3 className="admin-panel-title">{editing ? 'Edit Task Type' : 'New Task Type'}</h3>
          <div className="grid grid-cols-2 gap-4">
            <div className="col-span-2">
              <label className="sims-field-label">Name *</label>
              <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className="sims-input" />
            </div>
            <div className="col-span-2">
              <label className="sims-field-label">Description</label>
              <input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} className="sims-input" />
            </div>
            <div>
              <label className="sims-field-label">Default Priority</label>
              <select value={form.defaultPriority} onChange={(e) => setForm({ ...form, defaultPriority: e.target.value as TaskPriority })} className="sims-select">
                {PRIORITY_OPTIONS.map((p) => <option key={p} value={p}>{p}</option>)}
              </select>
            </div>
            <div>
              <label className="sims-field-label">Assigned Role Template</label>
              <select value={form.assignedRoleTemplate} onChange={(e) => setForm({ ...form, assignedRoleTemplate: e.target.value })} className="sims-select">
                <option value="">(unassigned)</option>
                {roles.map((role) => <option key={role.id} value={role.name}>{role.name}</option>)}
                {form.assignedRoleTemplate && !roles.some((role) => role.name === form.assignedRoleTemplate) && (
                  <option value={form.assignedRoleTemplate}>{form.assignedRoleTemplate} (unknown role)</option>
                )}
              </select>
            </div>
            <div className="col-span-2">
              <label className="sims-field-label">Due Date Formula</label>
              <input value={form.dueDateFormula} onChange={(e) => setForm({ ...form, dueDateFormula: e.target.value })} placeholder="e.g. EffectiveDate-30d" className="sims-input" />
              <p className="text-xs text-slate-500 mt-1">Format: &lt;anchor&gt;±&lt;n&gt;d — e.g. <code>EffectiveDate-30d</code> (30 days before effective date) or <code>CreatedDate+7d</code>. Validated server-side.</p>
            </div>
            <div className="flex items-center gap-2">
              <input type="checkbox" id="active" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />
              <label htmlFor="active" className="text-sm text-slate-700">Active</label>
            </div>
          </div>
          <div className="flex gap-2">
            <button disabled={saving || !form.name} onClick={() => save()} className="sd-btn primary">Save</button>
            <button onClick={() => { setShowForm(false); setEditing(null); setForm(EMPTY) }} className="sd-btn outline">Cancel</button>
          </div>
        </div>
      )}

      <div className="admin-panel">
        {types.length === 0 ? (
          <div className="admin-empty m-4">No task types configured.</div>
        ) : (
          <table className="sd-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Priority</th>
                <th>Children</th>
                <th>Active</th>
                <th />
              </tr>
            </thead>
            <tbody className="divide-y">
              {types.map((t) => (
                <tr key={t.id} className="hover:bg-slate-50">
                  <td className="primary-cell">{t.name}</td>
                  <td>{t.defaultPriority}</td>
                  <td>{t.childCount}</td>
                  <td>
                    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${t.isActive ? 'bg-green-50 text-green-700' : 'bg-slate-100 text-slate-500'}`}>
                      {t.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td>
                    <div className="flex gap-2 justify-end">
                      <button onClick={() => openEdit(t)} className="admin-icon-action" aria-label="Edit task type"><Edit2 className="h-4 w-4" /></button>
                      <button onClick={() => { if (confirm('Delete this task type?')) remove(t.id) }} className="admin-icon-action danger" aria-label="Delete task type"><Trash2 className="h-4 w-4" /></button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}
