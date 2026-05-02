import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2, Edit2 } from 'lucide-react'
import { toast } from 'sonner'
import { adminTaskTypesApi } from '@/api/admin.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { TaskTypeListItem, TaskPriority } from '@/types/task.types'

const PRIORITY_OPTIONS: TaskPriority[] = ['Low', 'Medium', 'High']

interface FormState { name: string; description: string; defaultPriority: TaskPriority; assignedRoleTemplate: string; dueDateFormula: string; isActive: boolean }
const EMPTY: FormState = { name: '', description: '', defaultPriority: 'Medium', assignedRoleTemplate: '', dueDateFormula: '', isActive: true }

export function TaskTypesAdminPage() {
  const qc = useQueryClient()
  const [editing, setEditing] = useState<string | null>(null)
  const [form, setForm] = useState<FormState>(EMPTY)
  const [showForm, setShowForm] = useState(false)

  const { data: types = [], isLoading } = useQuery({
    queryKey: ['admin', 'task-types'],
    queryFn: () => adminTaskTypesApi.getAll(),
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
    onError: () => toast.error('Save failed'),
  })

  const { mutate: remove } = useMutation({
    mutationFn: (id: string) => adminTaskTypesApi.delete(id),
    onSuccess: () => { toast.success('Deleted'); qc.invalidateQueries({ queryKey: ['admin', 'task-types'] }) },
    onError: () => toast.error('Delete failed'),
  })

  function openEdit(t: TaskTypeListItem) {
    adminTaskTypesApi.getById(t.id).then((full) => {
      setForm({ name: full.name, description: full.description ?? '', defaultPriority: full.defaultPriority, assignedRoleTemplate: full.assignedRoleTemplate ?? '', dueDateFormula: full.dueDateFormula ?? '', isActive: full.isActive })
      setEditing(t.id); setShowForm(true)
    })
  }

  if (isLoading) return <LoadingSpinner />

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="Task Types"
        subtitle={`${types.length} configured`}
        action={
          <button onClick={() => { setEditing(null); setForm(EMPTY); setShowForm(true) }} className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg text-sm hover:bg-blue-700">
            <Plus className="h-4 w-4" /> New Task Type
          </button>
        }
      />

      {showForm && (
        <div className="bg-white border rounded-lg p-5 space-y-4 max-w-xl">
          <h3 className="font-semibold text-slate-700">{editing ? 'Edit Task Type' : 'New Task Type'}</h3>
          <div className="grid grid-cols-2 gap-4">
            <div className="col-span-2">
              <label className="block text-xs text-slate-500 mb-1">Name *</label>
              <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className="w-full border rounded-lg px-3 py-2 text-sm" />
            </div>
            <div className="col-span-2">
              <label className="block text-xs text-slate-500 mb-1">Description</label>
              <input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} className="w-full border rounded-lg px-3 py-2 text-sm" />
            </div>
            <div>
              <label className="block text-xs text-slate-500 mb-1">Default Priority</label>
              <select value={form.defaultPriority} onChange={(e) => setForm({ ...form, defaultPriority: e.target.value as TaskPriority })} className="w-full border rounded-lg px-3 py-2 text-sm">
                {PRIORITY_OPTIONS.map((p) => <option key={p} value={p}>{p}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs text-slate-500 mb-1">Assigned Role Template</label>
              <input value={form.assignedRoleTemplate} onChange={(e) => setForm({ ...form, assignedRoleTemplate: e.target.value })} placeholder="e.g. UnderwriterId" className="w-full border rounded-lg px-3 py-2 text-sm" />
            </div>
            <div className="col-span-2">
              <label className="block text-xs text-slate-500 mb-1">Due Date Formula</label>
              <input value={form.dueDateFormula} onChange={(e) => setForm({ ...form, dueDateFormula: e.target.value })} placeholder="e.g. EffectiveDate-30d" className="w-full border rounded-lg px-3 py-2 text-sm" />
            </div>
            <div className="flex items-center gap-2">
              <input type="checkbox" id="active" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />
              <label htmlFor="active" className="text-sm text-slate-700">Active</label>
            </div>
          </div>
          <div className="flex gap-2">
            <button disabled={saving || !form.name} onClick={() => save()} className="px-4 py-2 bg-blue-600 text-white rounded-lg text-sm disabled:opacity-50">Save</button>
            <button onClick={() => { setShowForm(false); setEditing(null); setForm(EMPTY) }} className="px-4 py-2 border rounded-lg text-sm">Cancel</button>
          </div>
        </div>
      )}

      <div className="bg-white border rounded-lg overflow-hidden">
        {types.length === 0 ? (
          <div className="p-8 text-center text-sm text-slate-400">No task types configured.</div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b bg-slate-50 text-xs text-slate-500 uppercase tracking-wide text-left">
                <th className="px-4 py-3">Name</th>
                <th className="px-4 py-3">Priority</th>
                <th className="px-4 py-3">Children</th>
                <th className="px-4 py-3">Active</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y">
              {types.map((t) => (
                <tr key={t.id} className="hover:bg-slate-50">
                  <td className="px-4 py-3 font-medium text-slate-800">{t.name}</td>
                  <td className="px-4 py-3 text-slate-600">{t.defaultPriority}</td>
                  <td className="px-4 py-3 text-slate-600">{t.childCount}</td>
                  <td className="px-4 py-3">
                    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${t.isActive ? 'bg-green-50 text-green-700' : 'bg-slate-100 text-slate-500'}`}>
                      {t.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex gap-2 justify-end">
                      <button onClick={() => openEdit(t)} className="text-slate-400 hover:text-slate-700"><Edit2 className="h-4 w-4" /></button>
                      <button onClick={() => { if (confirm('Delete this task type?')) remove(t.id) }} className="text-slate-400 hover:text-red-500"><Trash2 className="h-4 w-4" /></button>
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
