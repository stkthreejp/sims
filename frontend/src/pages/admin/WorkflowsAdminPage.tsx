import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2, Edit2, ChevronRight } from 'lucide-react'
import { toast } from 'sonner'
import { adminWorkflowsApi, adminSystemEventsApi, adminTaskTypesApi } from '@/api/admin.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ErrorState } from '@/components/common/ErrorState'
import { getApiErrorMessage } from '@/lib/apiError'
import type { WorkflowTemplate, WorkflowStep, TaskEntityType } from '@/types/task.types'

const ENTITY_TYPES: TaskEntityType[] = ['Submission', 'Policy', 'PolicyTransaction', 'Account']

export function WorkflowsAdminPage() {
  const qc = useQueryClient()
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [createForm, setCreateForm] = useState({ name: '', description: '', isActive: true, triggerEventId: '', entityType: 'Submission' as TaskEntityType })

  const { data: templates = [], isLoading, isError, error, refetch } = useQuery({ queryKey: ['admin', 'workflows'], queryFn: adminWorkflowsApi.getAll })
  const { data: events = [] } = useQuery({ queryKey: ['admin', 'system-events'], queryFn: adminSystemEventsApi.getAll })

  const { mutate: create, isPending: creating } = useMutation({
    mutationFn: () => adminWorkflowsApi.create(createForm),
    onSuccess: (t) => {
      toast.success('Template created'); qc.invalidateQueries({ queryKey: ['admin', 'workflows'] })
      setShowCreate(false); setSelectedId(t.id)
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Create failed')),
  })

  const { mutate: remove } = useMutation({
    mutationFn: (id: string) => adminWorkflowsApi.delete(id),
    onSuccess: () => { toast.success('Deleted'); qc.invalidateQueries({ queryKey: ['admin', 'workflows'] }); if (selectedId) setSelectedId(null) },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Delete failed')),
  })

  if (isLoading) return <LoadingSpinner />
  if (isError) return <ErrorState error={error} onRetry={refetch} />

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="Workflow Templates"
        subtitle={`${templates.length} configured`}
        action={
          <button onClick={() => setShowCreate(true)} className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg text-sm hover:bg-blue-700">
            <Plus className="h-4 w-4" /> New Template
          </button>
        }
      />

      {showCreate && (
        <div className="bg-white border rounded-lg p-5 space-y-4 max-w-lg">
          <h3 className="font-semibold text-slate-700">New Workflow Template</h3>
          <div className="space-y-3">
            <div><label className="text-xs text-slate-500 block mb-1">Name *</label>
              <input value={createForm.name} onChange={(e) => setCreateForm({ ...createForm, name: e.target.value })} className="w-full border rounded-lg px-3 py-2 text-sm" /></div>
            <div><label className="text-xs text-slate-500 block mb-1">Trigger Event *</label>
              <select value={createForm.triggerEventId} onChange={(e) => setCreateForm({ ...createForm, triggerEventId: e.target.value })} className="w-full border rounded-lg px-3 py-2 text-sm">
                <option value="">Select event…</option>
                {events.map((e) => <option key={e.id} value={e.id}>{e.eventName}</option>)}
              </select></div>
            <div><label className="text-xs text-slate-500 block mb-1">Entity Type</label>
              <select value={createForm.entityType} onChange={(e) => setCreateForm({ ...createForm, entityType: e.target.value as TaskEntityType })} className="w-full border rounded-lg px-3 py-2 text-sm">
                {ENTITY_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
              </select></div>
            <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={createForm.isActive} onChange={(e) => setCreateForm({ ...createForm, isActive: e.target.checked })} /> Active</label>
          </div>
          <div className="flex gap-2">
            <button disabled={creating || !createForm.name || !createForm.triggerEventId} onClick={() => create()} className="px-4 py-2 bg-blue-600 text-white rounded-lg text-sm disabled:opacity-50">Create</button>
            <button onClick={() => setShowCreate(false)} className="px-4 py-2 border rounded-lg text-sm">Cancel</button>
          </div>
        </div>
      )}

      <div className="flex gap-5">
        {/* Template list */}
        <div className="w-72 bg-white border rounded-lg overflow-hidden shrink-0">
          {templates.length === 0 ? (
            <div className="p-6 text-sm text-center text-slate-400">No templates yet.</div>
          ) : templates.map((t) => (
            <div
              key={t.id}
              onClick={() => setSelectedId(t.id)}
              className={`flex items-center justify-between px-4 py-3 cursor-pointer border-b last:border-0 hover:bg-slate-50 ${selectedId === t.id ? 'bg-blue-50' : ''}`}
            >
              <div>
                <p className="text-sm font-medium text-slate-800">{t.name}</p>
                <p className="text-xs text-slate-500">{t.triggerEventName} · {t.stepCount} steps</p>
              </div>
              <div className="flex items-center gap-1">
                <button onClick={(e) => { e.stopPropagation(); if (confirm('Delete this template?')) remove(t.id) }} className="text-slate-300 hover:text-red-500 p-1"><Trash2 className="h-3.5 w-3.5" /></button>
                <ChevronRight className="h-4 w-4 text-slate-300" />
              </div>
            </div>
          ))}
        </div>

        {/* Step editor */}
        {selectedId && <StepEditor templateId={selectedId} />}
      </div>
    </div>
  )
}

function StepEditor({ templateId }: { templateId: string }) {
  const qc = useQueryClient()
  const { data: template, isLoading } = useQuery({ queryKey: ['admin', 'workflow', templateId], queryFn: () => adminWorkflowsApi.getById(templateId) })
  const { data: taskTypes = [] } = useQuery({ queryKey: ['admin', 'task-types'], queryFn: () => adminTaskTypesApi.getAll(true) })

  const [steps, setSteps] = useState<Partial<WorkflowStep>[] | null>(null)
  const displaySteps = steps ?? (template?.steps ?? [])

  const { mutate: save, isPending: saving } = useMutation({
    mutationFn: () => adminWorkflowsApi.setSteps(templateId, displaySteps.map((s, i) => ({ ...s, stepOrder: i + 1 }))),
    onSuccess: () => { toast.success('Steps saved'); qc.invalidateQueries({ queryKey: ['admin', 'workflow', templateId] }); setSteps(null) },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Save failed')),
  })

  function addStep() { setSteps([...displaySteps, { stepOrder: displaySteps.length + 1, taskTypeId: '', triggerCondition: '' }]) }
  function removeStep(i: number) { setSteps(displaySteps.filter((_, idx) => idx !== i)) }
  function updateStep(i: number, patch: Partial<WorkflowStep>) { setSteps(displaySteps.map((s, idx) => idx === i ? { ...s, ...patch } : s)) }

  if (isLoading) return <div className="flex-1 flex items-center justify-center"><LoadingSpinner /></div>
  if (!template) return null

  return (
    <div className="flex-1 bg-white border rounded-lg p-5 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="font-semibold text-slate-800">{template.name}</h3>
          <p className="text-xs text-slate-500">{template.triggerEventName} → {template.entityType}</p>
        </div>
        <div className="flex gap-2">
          {steps !== null && (
            <>
              <button onClick={() => setSteps(null)} className="px-3 py-1.5 border rounded-lg text-sm">Discard</button>
              <button disabled={saving} onClick={() => save()} className="px-3 py-1.5 bg-blue-600 text-white rounded-lg text-sm disabled:opacity-50">Save Steps</button>
            </>
          )}
          <button onClick={addStep} className="flex items-center gap-1 px-3 py-1.5 border rounded-lg text-sm hover:bg-slate-50"><Plus className="h-3.5 w-3.5" /> Add Step</button>
        </div>
      </div>

      {displaySteps.length === 0 ? (
        <p className="text-sm text-slate-400">No steps. Add a step to define the workflow.</p>
      ) : (
        <div className="space-y-3">
          {displaySteps.map((step, i) => (
            <div key={step.id ?? i} className="border rounded-lg p-4 space-y-3 bg-slate-50">
              <div className="flex items-center justify-between">
                <span className="text-xs font-semibold text-slate-500 uppercase">Step {i + 1}</span>
                <button onClick={() => removeStep(i)} className="text-slate-400 hover:text-red-500"><Trash2 className="h-3.5 w-3.5" /></button>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div><label className="text-xs text-slate-500 block mb-1">Task Type *</label>
                  <select value={step.taskTypeId ?? ''} onChange={(e) => updateStep(i, { taskTypeId: e.target.value })} className="w-full border rounded-lg px-3 py-2 text-sm bg-white">
                    <option value="">Select…</option>
                    {taskTypes.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
                  </select></div>
                <div><label className="text-xs text-slate-500 block mb-1">Depends On Step</label>
                  <select value={step.dependsOnStepId ?? ''} onChange={(e) => updateStep(i, { dependsOnStepId: e.target.value || undefined })} className="w-full border rounded-lg px-3 py-2 text-sm bg-white">
                    <option value="">(none — root step)</option>
                    {displaySteps.filter((_, idx) => idx !== i && displaySteps[idx]?.id).map((s, idx) => (
                      <option key={s.id} value={s.id}>{idx + 1}. {taskTypes.find((t) => t.id === s.taskTypeId)?.name ?? 'Step ' + (idx + 1)}</option>
                    ))}
                  </select></div>
                <div className="col-span-2"><label className="text-xs text-slate-500 block mb-1">Trigger Condition (e.g. Status=Quoted)</label>
                  <input value={step.triggerCondition ?? ''} onChange={(e) => updateStep(i, { triggerCondition: e.target.value || undefined })} placeholder="optional" className="w-full border rounded-lg px-3 py-2 text-sm" /></div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
