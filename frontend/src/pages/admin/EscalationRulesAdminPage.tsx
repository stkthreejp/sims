import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2, Edit2 } from 'lucide-react'
import { toast } from 'sonner'
import { adminEscalationRulesApi, adminTaskTypesApi } from '@/api/admin.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { EscalationRule } from '@/types/task.types'

interface FormState {
  taskTypeId: string
  hoursOverdue: number
  notifyRoleName: string
  increasePriority: boolean
  isActive: boolean
}

const EMPTY: FormState = { taskTypeId: '', hoursOverdue: 24, notifyRoleName: '', increasePriority: true, isActive: true }

export function EscalationRulesAdminPage() {
  const qc = useQueryClient()
  const [editing, setEditing] = useState<string | null>(null)
  const [form, setForm] = useState<FormState>(EMPTY)
  const [showForm, setShowForm] = useState(false)

  const { data: rules = [], isLoading } = useQuery({
    queryKey: ['admin', 'escalation-rules'],
    queryFn: adminEscalationRulesApi.getAll,
  })

  const { data: taskTypes = [] } = useQuery({
    queryKey: ['admin', 'task-types'],
    queryFn: () => adminTaskTypesApi.getAll(true),
  })

  const { mutate: save, isPending: saving } = useMutation({
    mutationFn: () => editing
      ? adminEscalationRulesApi.update(editing, form)
      : adminEscalationRulesApi.create(form),
    onSuccess: () => {
      toast.success(editing ? 'Rule updated' : 'Rule created')
      qc.invalidateQueries({ queryKey: ['admin', 'escalation-rules'] })
      setEditing(null); setShowForm(false); setForm(EMPTY)
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Save failed'),
  })

  const { mutate: remove } = useMutation({
    mutationFn: (id: string) => adminEscalationRulesApi.delete(id),
    onSuccess: () => { toast.success('Deleted'); qc.invalidateQueries({ queryKey: ['admin', 'escalation-rules'] }) },
    onError: () => toast.error('Delete failed'),
  })

  function openEdit(r: EscalationRule) {
    setForm({ taskTypeId: r.taskTypeId ?? '', hoursOverdue: r.hoursOverdue, notifyRoleName: r.notifyRoleName, increasePriority: r.increasePriority, isActive: r.isActive })
    setEditing(r.id); setShowForm(true)
  }

  if (isLoading) return <LoadingSpinner />

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="Escalation Rules"
        subtitle={`${rules.length} rules configured`}
        action={
          <button
            onClick={() => { setEditing(null); setForm(EMPTY); setShowForm(true) }}
            className="sd-btn primary"
          >
            <Plus className="h-4 w-4" /> New Rule
          </button>
        }
      />

      {showForm && (
        <div className="admin-panel max-w-xl p-5 space-y-4">
          <h3 className="admin-panel-title">{editing ? 'Edit Rule' : 'New Escalation Rule'}</h3>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="sims-field-label">Task Type (blank = all types)</label>
              <select
                value={form.taskTypeId}
                onChange={(e) => setForm({ ...form, taskTypeId: e.target.value })}
                className="sims-select"
              >
                <option value="">(global — applies to all)</option>
                {taskTypes.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
              </select>
            </div>
            <div>
              <label className="sims-field-label">Hours Overdue *</label>
              <input
                type="number"
                min={1}
                value={form.hoursOverdue}
                onChange={(e) => setForm({ ...form, hoursOverdue: Number(e.target.value) })}
                className="sims-input"
              />
            </div>
            <div className="col-span-2">
              <label className="sims-field-label">Notify Role *</label>
              <input
                value={form.notifyRoleName}
                onChange={(e) => setForm({ ...form, notifyRoleName: e.target.value })}
                placeholder="e.g. Manager, Admin"
                className="sims-input"
              />
            </div>
            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                id="increasePriority"
                checked={form.increasePriority}
                onChange={(e) => setForm({ ...form, increasePriority: e.target.checked })}
              />
              <label htmlFor="increasePriority" className="text-sm text-slate-700">Increase priority</label>
            </div>
            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                id="ruleActive"
                checked={form.isActive}
                onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
              />
              <label htmlFor="ruleActive" className="text-sm text-slate-700">Active</label>
            </div>
          </div>
          <div className="flex gap-2">
            <button
              disabled={saving || !form.notifyRoleName || form.hoursOverdue < 1}
              onClick={() => save()}
              className="sd-btn primary"
            >
              Save
            </button>
            <button
              onClick={() => { setShowForm(false); setEditing(null); setForm(EMPTY) }}
              className="sd-btn outline"
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      <div className="admin-panel">
        {rules.length === 0 ? (
          <div className="admin-empty m-4">No escalation rules configured.</div>
        ) : (
          <table className="sd-table">
            <thead>
              <tr>
                <th>Task Type</th>
                <th>Hours Overdue</th>
                <th>Notify Role</th>
                <th>Priority+</th>
                <th>Active</th>
                <th />
              </tr>
            </thead>
            <tbody className="divide-y">
              {rules.map((r) => (
                <tr key={r.id} className="hover:bg-slate-50">
                  <td>{r.taskTypeName ?? <span className="italic" style={{ color: 'var(--ink-4)' }}>All types</span>}</td>
                  <td>{r.hoursOverdue}h</td>
                  <td>{r.notifyRoleName}</td>
                  <td>
                    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${r.increasePriority ? 'bg-orange-50 text-orange-700' : 'bg-slate-100 text-slate-500'}`}>
                      {r.increasePriority ? 'Yes' : 'No'}
                    </span>
                  </td>
                  <td>
                    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${r.isActive ? 'bg-green-50 text-green-700' : 'bg-slate-100 text-slate-500'}`}>
                      {r.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td>
                    <div className="flex gap-2 justify-end">
                      <button onClick={() => openEdit(r)} className="admin-icon-action" aria-label="Edit rule"><Edit2 className="h-4 w-4" /></button>
                      <button onClick={() => { if (confirm('Delete this rule?')) remove(r.id) }} className="admin-icon-action danger" aria-label="Delete rule"><Trash2 className="h-4 w-4" /></button>
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
