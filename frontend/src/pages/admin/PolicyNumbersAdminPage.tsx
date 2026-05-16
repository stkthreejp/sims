import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Hash, Link2, Plus, Save, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { policyNumbersApi } from '@/api/policyNumbers.api'
import { carriersApi } from '@/api/carriers.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ACTIVE_LOBS, LOB_LABELS, type PolicyLineOfBusiness } from '@/types/quote.types'
import type { PolicyNumberSequence, PolicyNumberSequenceUpsert } from '@/types/policyNumber.types'

const emptySequence: PolicyNumberSequenceUpsert = {
  name: '',
  format: '{CARRIER}-{LOB}-{YY}-{SEQ:00000}',
  nextNumber: 1,
  resetAnnually: false,
  termSuffixFormat: '-{TERM:00}',
  renewalBehavior: 'CopyBaseAndIncrementTermSuffix',
  allowManualOverride: false,
  isActive: true,
  notes: '',
}

const emptyAssignment = {
  policyNumberSequenceId: '',
  carrierId: '',
  lineOfBusiness: 'InlandMarine' as PolicyLineOfBusiness,
  state: '',
  priority: 0,
  isActive: true,
}

export function PolicyNumbersAdminPage() {
  const qc = useQueryClient()
  const [sequenceForm, setSequenceForm] = useState<PolicyNumberSequenceUpsert>(emptySequence)
  const [editingSequenceId, setEditingSequenceId] = useState<string | null>(null)
  const [assignmentForm, setAssignmentForm] = useState(emptyAssignment)

  const { data: sequences = [], isLoading: loadingSequences } = useQuery({
    queryKey: ['policy-number-sequences'],
    queryFn: () => policyNumbersApi.getSequences(true),
  })

  const { data: assignments = [], isLoading: loadingAssignments } = useQuery({
    queryKey: ['policy-number-assignments'],
    queryFn: () => policyNumbersApi.getAssignments(true),
  })

  const { data: carriers = [] } = useQuery({
    queryKey: ['carriers', 'active'],
    queryFn: () => carriersApi.getAll(true),
  })

  const previewCarrierName = useMemo(
    () => carriers.find((c) => c.id === assignmentForm.carrierId)?.name,
    [assignmentForm.carrierId, carriers],
  )

  const { data: preview } = useQuery({
    queryKey: ['policy-number-preview', sequenceForm.format, sequenceForm.nextNumber, sequenceForm.termSuffixFormat, assignmentForm.lineOfBusiness, assignmentForm.state, previewCarrierName],
    queryFn: () => policyNumbersApi.preview({
      format: sequenceForm.format,
      nextNumber: Number(sequenceForm.nextNumber) || 1,
      termSuffixFormat: sequenceForm.termSuffixFormat,
      lineOfBusiness: assignmentForm.lineOfBusiness,
      state: assignmentForm.state || undefined,
      carrierName: previewCarrierName,
      count: 5,
    }),
  })

  useEffect(() => {
    if (!assignmentForm.policyNumberSequenceId && sequences.length > 0) {
      setAssignmentForm((f) => ({ ...f, policyNumberSequenceId: sequences[0].id }))
    }
  }, [assignmentForm.policyNumberSequenceId, sequences])

  useEffect(() => {
    if (!assignmentForm.carrierId && carriers.length > 0) {
      setAssignmentForm((f) => ({ ...f, carrierId: carriers[0].id }))
    }
  }, [assignmentForm.carrierId, carriers])

  const saveSequence = useMutation({
    mutationFn: () => editingSequenceId
      ? policyNumbersApi.updateSequence(editingSequenceId, cleanSequence(sequenceForm))
      : policyNumbersApi.createSequence(cleanSequence(sequenceForm)),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policy-number-sequences'] })
      setSequenceForm(emptySequence)
      setEditingSequenceId(null)
      toast.success('Policy number sequence saved')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Sequence could not be saved'),
  })

  const deleteSequence = useMutation({
    mutationFn: policyNumbersApi.deleteSequence,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policy-number-sequences'] })
      toast.success('Sequence removed')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Sequence could not be removed'),
  })

  const saveAssignment = useMutation({
    mutationFn: () => policyNumbersApi.createAssignment({
      ...assignmentForm,
      state: assignmentForm.state || undefined,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policy-number-assignments'] })
      setAssignmentForm((f) => ({ ...emptyAssignment, policyNumberSequenceId: f.policyNumberSequenceId, carrierId: f.carrierId }))
      toast.success('Assignment saved')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Assignment could not be saved'),
  })

  const deleteAssignment = useMutation({
    mutationFn: policyNumbersApi.deleteAssignment,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policy-number-assignments'] })
      toast.success('Assignment removed')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Assignment could not be removed'),
  })

  const editSequence = (sequence: PolicyNumberSequence) => {
    setEditingSequenceId(sequence.id)
    setSequenceForm({
      name: sequence.name,
      format: sequence.format,
      nextNumber: sequence.nextNumber,
      resetAnnually: sequence.resetAnnually,
      termSuffixFormat: sequence.termSuffixFormat,
      renewalBehavior: sequence.renewalBehavior,
      allowManualOverride: sequence.allowManualOverride,
      isActive: sequence.isActive,
      notes: sequence.notes ?? '',
    })
  }

  if (loadingSequences || loadingAssignments) return <LoadingSpinner />

  return (
    <div className="p-6 space-y-6 max-w-7xl">
      <div>
        <h1 className="text-xl font-semibold text-slate-900">Policy Numbering</h1>
        <p className="text-sm text-slate-500 mt-1">Create policy number sequences and assign them by carrier and line of business.</p>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-[420px_1fr] gap-5">
        <section className="bg-white border rounded-lg">
          <div className="px-4 py-3 border-b flex items-center gap-2">
            <Hash className="h-4 w-4 text-slate-400" />
            <h2 className="text-sm font-semibold text-slate-800">{editingSequenceId ? 'Edit sequence' : 'New sequence'}</h2>
          </div>
          <div className="p-4 space-y-3">
            <input value={sequenceForm.name} onChange={(e) => setSequenceForm((f) => ({ ...f, name: e.target.value }))} placeholder="Sequence name" className="w-full border rounded px-2 py-1.5 text-sm" />
            <input value={sequenceForm.format} onChange={(e) => setSequenceForm((f) => ({ ...f, format: e.target.value }))} placeholder="Format" className="w-full border rounded px-2 py-1.5 text-sm font-mono" />
            <div className="grid grid-cols-2 gap-2">
              <input type="number" min={1} value={sequenceForm.nextNumber} onChange={(e) => setSequenceForm((f) => ({ ...f, nextNumber: Number(e.target.value) || 1 }))} placeholder="Next number" className="border rounded px-2 py-1.5 text-sm" />
              <input value={sequenceForm.termSuffixFormat} onChange={(e) => setSequenceForm((f) => ({ ...f, termSuffixFormat: e.target.value }))} placeholder="Term suffix" className="border rounded px-2 py-1.5 text-sm font-mono" />
            </div>
            <div className="grid grid-cols-2 gap-2">
              <label className="flex items-center gap-2 text-sm text-slate-600">
                <input type="checkbox" checked={sequenceForm.resetAnnually} onChange={(e) => setSequenceForm((f) => ({ ...f, resetAnnually: e.target.checked }))} />
                Reset annually
              </label>
              <label className="flex items-center gap-2 text-sm text-slate-600">
                <input type="checkbox" checked={sequenceForm.allowManualOverride} onChange={(e) => setSequenceForm((f) => ({ ...f, allowManualOverride: e.target.checked }))} />
                Manual override
              </label>
            </div>
            <textarea value={sequenceForm.notes ?? ''} onChange={(e) => setSequenceForm((f) => ({ ...f, notes: e.target.value }))} placeholder="Notes" className="w-full border rounded px-2 py-1.5 text-sm" rows={2} />
            <div className="bg-slate-50 border rounded p-3">
              <p className="text-xs font-semibold text-slate-500 mb-2">Preview</p>
              <div className="space-y-1">
                {(preview?.numbers ?? []).map((number) => (
                  <p key={number} className="font-mono text-sm text-slate-800">{number}</p>
                ))}
              </div>
            </div>
            <div className="flex gap-2">
              <button onClick={() => saveSequence.mutate()} disabled={saveSequence.isPending || !sequenceForm.name || !sequenceForm.format} className="inline-flex items-center gap-1.5 px-3 py-2 bg-blue-600 text-white text-sm rounded disabled:opacity-50">
                <Save className="h-4 w-4" /> Save sequence
              </button>
              {editingSequenceId && (
                <button onClick={() => { setEditingSequenceId(null); setSequenceForm(emptySequence) }} className="px-3 py-2 border rounded text-sm text-slate-600 hover:bg-slate-50">
                  Cancel
                </button>
              )}
            </div>
          </div>
        </section>

        <section className="bg-white border rounded-lg">
          <div className="px-4 py-3 border-b flex items-center gap-2">
            <Link2 className="h-4 w-4 text-slate-400" />
            <h2 className="text-sm font-semibold text-slate-800">Assignments</h2>
          </div>
          <div className="p-4 border-b bg-slate-50 grid grid-cols-1 md:grid-cols-5 gap-2">
            <select value={assignmentForm.policyNumberSequenceId} onChange={(e) => setAssignmentForm((f) => ({ ...f, policyNumberSequenceId: e.target.value }))} className="border rounded px-2 py-1.5 text-sm md:col-span-2">
              {sequences.map((sequence) => <option key={sequence.id} value={sequence.id}>{sequence.name}</option>)}
            </select>
            <select value={assignmentForm.carrierId} onChange={(e) => setAssignmentForm((f) => ({ ...f, carrierId: e.target.value }))} className="border rounded px-2 py-1.5 text-sm">
              {carriers.map((carrier) => <option key={carrier.id} value={carrier.id}>{carrier.name}</option>)}
            </select>
            <select value={assignmentForm.lineOfBusiness} onChange={(e) => setAssignmentForm((f) => ({ ...f, lineOfBusiness: e.target.value as PolicyLineOfBusiness }))} className="border rounded px-2 py-1.5 text-sm">
              {ACTIVE_LOBS.map((lob) => <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>)}
            </select>
            <div className="flex gap-2">
              <input value={assignmentForm.state} onChange={(e) => setAssignmentForm((f) => ({ ...f, state: e.target.value.toUpperCase().slice(0, 2) }))} placeholder="State" className="w-20 border rounded px-2 py-1.5 text-sm" />
              <button onClick={() => saveAssignment.mutate()} disabled={saveAssignment.isPending || !assignmentForm.policyNumberSequenceId || !assignmentForm.carrierId} className="inline-flex items-center gap-1 px-3 py-1.5 bg-blue-600 text-white text-sm rounded disabled:opacity-50">
                <Plus className="h-4 w-4" /> Add
              </button>
            </div>
          </div>
          <div className="divide-y">
            {assignments.map((assignment) => (
              <div key={assignment.id} className="p-3 grid grid-cols-[1fr_auto] gap-3 items-center">
                <div>
                  <p className="text-sm font-medium text-slate-800">{assignment.carrierName} / {LOB_LABELS[assignment.lineOfBusiness]}</p>
                  <p className="text-xs text-slate-500">{assignment.sequenceName}{assignment.state ? ` / ${assignment.state}` : ' / all states'}{assignment.writingCompanyId ? ' / writing company scoped' : ''}</p>
                </div>
                <button onClick={() => deleteAssignment.mutate(assignment.id)} className="px-2 py-1 border rounded text-slate-500 hover:text-red-600">
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
            ))}
            {assignments.length === 0 && <p className="p-4 text-sm text-slate-400">No assignments yet.</p>}
          </div>
        </section>
      </div>

      <section className="bg-white border rounded-lg">
        <div className="px-4 py-3 border-b">
          <h2 className="text-sm font-semibold text-slate-800">Sequences</h2>
        </div>
        <div className="divide-y">
          {sequences.map((sequence) => (
            <button key={sequence.id} onClick={() => editSequence(sequence)} className="w-full text-left p-3 hover:bg-slate-50 grid grid-cols-[1fr_auto] gap-3">
              <div>
                <p className="text-sm font-medium text-slate-800">{sequence.name}</p>
                <p className="text-xs text-slate-500 font-mono">{sequence.format}{sequence.termSuffixFormat}</p>
              </div>
              <div className="flex items-center gap-2">
                <span className={`inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded ${sequence.isActive ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500'}`}>
                  {sequence.isActive && <Check className="h-3 w-3" />}
                  {sequence.isActive ? 'Active' : 'Inactive'}
                </span>
                <button onClick={(e) => { e.stopPropagation(); deleteSequence.mutate(sequence.id) }} className="px-2 py-1 border rounded text-slate-500 hover:text-red-600">
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
            </button>
          ))}
        </div>
      </section>
    </div>
  )
}

function cleanSequence(sequence: PolicyNumberSequenceUpsert): PolicyNumberSequenceUpsert {
  return {
    ...sequence,
    nextNumber: Number(sequence.nextNumber) || 1,
    notes: sequence.notes || undefined,
  }
}
