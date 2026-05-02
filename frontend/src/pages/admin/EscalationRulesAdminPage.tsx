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
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg text-sm hover:bg-blue-700"
          >
            <Plus className="h-4 w-4" /> New Rule
          </button>
        }
      />

      {showForm && (
        <div className="bg-white border rounded-lg p-5 space-y-4 max-w-xl">
          <h3 className="font-semibold text-slate-700">{editing ? 'Edit Rule' : 'New Escalation Rule'}</h3>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs text-slate-500 mb-1">Task Type (blank = all types)</label>
              <select
                value={form.taskTypeId}
                onChange={(e) => setForm({ ...form, taskTypeId: e.target.value })}
                className="w-full border rounded-lg px-3 py-2 text-sm"
              >
                <option value="">(global — applies to all)</option>
                {taskTypes.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs text-slate-500 mb-1">Hours Overdue *</label>
              <input
                type="number"
                min={1}
                value={form.hoursOverdue}
                onChange={(e) => setForm({ ...form, hoursOverdue: Number(e.target.value) })}
                className="w-full border rounded-lg px-3 py-2 text-sm"
              />
            </div>
            <div className="col-span-2">
              <label className="block text-xs text-slate-500 mb-1">Notify Role *</label>
              <input
                value={form.notifyRoleName}
                onChange={(e) => setForm({ ...form, notifyRoleName: e.target.value })}
                placeholder="e.g. Manager, Admin"
                className="w-full border rounded-lg px-3 py-2 text-sm"
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
              className="px-4 py-2 bg-blue-600 text-white rounded-lg text-sm disabled:opacity-50"
            >
              Save
            </button>
            <button
              onClick={() => { setShowForm(false); setEditing(null); setForm(EMPTY) }}
              className="px-4 py-2 border rounded-lg text-sm"
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      <div className="bg-white border rounded-lg overflow-hidden">
        {rules.length === 0 ? (
          <div className="p-8 text-center text-sm text-slate-400">No escalation rules configured.</div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b bg-slate-50 text-xs text-slate-500 uppercase tracking-wide text-left">
                <th className="px-4 py-3">Task Type</th>
                <th className="px-4 py-3">Hours Overdue</th>
                <th className="px-4 py-3">Notify Role</th>
                <th className="px-4 py-3">Priority+</th>
                <th className="px-4 py-3">Active</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y">
              {rules.map((r) => (
                <tr key={r.id} className="hover:bg-slate-50">
                  <td className="px-4 py-3 text-slate-700">{r.taskTypeName ?? <span className="text-slate-400 italic">All types</span>}</td>
                  <td className="px-4 py-3 text-slate-600">{r.hoursOverdue}h</td>
                  <td className="px-4 py-3 text-slate-600">{r.notifyRoleName}</td>
                  <td className="px-4 py-3">
                    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${r.increasePriority ? 'bg-orange-50 text-orange-700' : 'bg-slate-100 text-slate-500'}`}>
                      {r.increasePriority ? 'Yes' : 'No'}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${r.isActive ? 'bg-green-50 text-green-700' : 'bg-slate-100 text-slate-500'}`}>
                      {r.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex gap-2 justify-end">
                      <button onClick={() => openEdit(r)} className="text-slate-400 hover:text-slate-700"><Edit2 className="h-4 w-4" /></button>
                      <button onClick={() => { if (confirm('Delete this rule?')) remove(r.id) }} className="text-slate-400 hover:text-red-500"><Trash2 className="h-4 w-4" /></button>
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
