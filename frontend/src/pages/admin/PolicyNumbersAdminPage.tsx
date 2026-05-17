import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Hash, Link2, Pencil, Plus, Save, Trash2, X } from 'lucide-react'
import { toast } from 'sonner'
import { policyNumbersApi } from '@/api/policyNumbers.api'
import { carriersApi } from '@/api/carriers.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ACTIVE_LOBS, LOB_LABELS, type PolicyLineOfBusiness } from '@/types/quote.types'
import type { PolicyNumberAssignment, PolicyNumberAssignmentUpsert, PolicyNumberSequence, PolicyNumberSequenceUpsert } from '@/types/policyNumber.types'

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

const emptyAssignment: PolicyNumberAssignmentUpsert = {
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
  const [assignmentForm, setAssignmentForm] = useState<PolicyNumberAssignmentUpsert>(emptyAssignment)
  const [editingAssignmentId, setEditingAssignmentId] = useState<string | null>(null)

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
      resetSequenceForm()
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
    mutationFn: () => {
      const payload = cleanAssignment(assignmentForm)
      return editingAssignmentId
        ? policyNumbersApi.updateAssignment(editingAssignmentId, payload)
        : policyNumbersApi.createAssignment(payload)
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policy-number-assignments'] })
      resetAssignmentForm()
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

  const resetSequenceForm = () => {
    setSequenceForm(emptySequence)
    setEditingSequenceId(null)
  }

  const resetAssignmentForm = () => {
    setAssignmentForm((f) => ({
      ...emptyAssignment,
      policyNumberSequenceId: f.policyNumberSequenceId || sequences[0]?.id || '',
      carrierId: f.carrierId || carriers[0]?.id || '',
    }))
    setEditingAssignmentId(null)
  }

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

  const editAssignment = (assignment: PolicyNumberAssignment) => {
    setEditingAssignmentId(assignment.id)
    setAssignmentForm({
      policyNumberSequenceId: assignment.policyNumberSequenceId,
      carrierId: assignment.carrierId,
      writingCompanyId: assignment.writingCompanyId ?? undefined,
      lineOfBusiness: assignment.lineOfBusiness,
      state: assignment.state ?? '',
      priority: assignment.priority,
      isActive: assignment.isActive,
    })
  }

  if (loadingSequences || loadingAssignments) return <LoadingSpinner />

  return (
    <div className="space-y-5 p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="m-0 text-[22px] font-semibold tracking-[-0.01em]" style={{ color: 'var(--ink)' }}>Policy Numbering</h1>
          <p className="m-0 mt-1 text-sm" style={{ color: 'var(--ink-3)' }}>Create reusable number sequences and assign them by carrier, line, and state.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <span className="sd-lob">{sequences.length} sequences</span>
          <span className="sd-lob">{assignments.length} assignments</span>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-5 xl:grid-cols-[420px_minmax(0,1fr)]">
        <section className="sd-card overflow-hidden">
          <div className="sd-card-head">
            <h3><Hash className="h-4 w-4" /> {editingSequenceId ? 'Edit sequence' : 'New sequence'}</h3>
            {editingSequenceId && (
              <button onClick={resetSequenceForm} className="sims-icon-btn" title="Cancel edit">
                <X className="h-4 w-4" />
              </button>
            )}
          </div>
          <div className="sd-card-body space-y-3">
            <label className="block">
              <span className="sims-field-label">Sequence name</span>
              <input value={sequenceForm.name} onChange={(e) => setSequenceForm((f) => ({ ...f, name: e.target.value }))} className="sims-input" />
            </label>
            <label className="block">
              <span className="sims-field-label">Format</span>
              <input value={sequenceForm.format} onChange={(e) => setSequenceForm((f) => ({ ...f, format: e.target.value }))} className="sims-input font-mono" />
            </label>
            <div className="grid grid-cols-2 gap-3">
              <label className="block">
                <span className="sims-field-label">Next number</span>
                <input type="number" min={1} value={sequenceForm.nextNumber} onChange={(e) => setSequenceForm((f) => ({ ...f, nextNumber: Number(e.target.value) || 1 }))} className="sims-input" />
              </label>
              <label className="block">
                <span className="sims-field-label">Term suffix</span>
                <input value={sequenceForm.termSuffixFormat} onChange={(e) => setSequenceForm((f) => ({ ...f, termSuffixFormat: e.target.value }))} className="sims-input font-mono" />
              </label>
            </div>
            <div className="grid grid-cols-2 gap-3 rounded-lg border px-3 py-2" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
              <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--ink-2)' }}>
                <input type="checkbox" checked={sequenceForm.resetAnnually} onChange={(e) => setSequenceForm((f) => ({ ...f, resetAnnually: e.target.checked }))} className="h-4 w-4 rounded border-slate-300" />
                Reset annually
              </label>
              <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--ink-2)' }}>
                <input type="checkbox" checked={sequenceForm.allowManualOverride} onChange={(e) => setSequenceForm((f) => ({ ...f, allowManualOverride: e.target.checked }))} className="h-4 w-4 rounded border-slate-300" />
                Manual override
              </label>
              <label className="col-span-2 flex items-center gap-2 text-sm" style={{ color: 'var(--ink-2)' }}>
                <input type="checkbox" checked={sequenceForm.isActive} onChange={(e) => setSequenceForm((f) => ({ ...f, isActive: e.target.checked }))} className="h-4 w-4 rounded border-slate-300" />
                Active sequence
              </label>
            </div>
            <label className="block">
              <span className="sims-field-label">Notes</span>
              <textarea value={sequenceForm.notes ?? ''} onChange={(e) => setSequenceForm((f) => ({ ...f, notes: e.target.value }))} className="sims-textarea" rows={2} />
            </label>
            <div className="rounded-lg border p-3" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
              <p className="m-0 mb-2 text-[10.5px] font-semibold uppercase tracking-[0.04em]" style={{ color: 'var(--ink-3)' }}>Preview</p>
              <div className="space-y-1">
                {(preview?.numbers ?? []).map((number) => (
                  <p key={number} className="m-0 font-mono text-sm" style={{ color: 'var(--ink)' }}>{number}</p>
                ))}
              </div>
            </div>
            <button onClick={() => saveSequence.mutate()} disabled={saveSequence.isPending || !sequenceForm.name || !sequenceForm.format} className="sd-btn primary">
              <Save className="h-4 w-4" /> Save sequence
            </button>
          </div>
        </section>

        <section className="sd-card overflow-hidden">
          <div className="sd-card-head">
            <h3><Link2 className="h-4 w-4" /> {editingAssignmentId ? 'Edit assignment' : 'Assignments'} <span className="cnt">{assignments.length}</span></h3>
            {editingAssignmentId && (
              <button onClick={resetAssignmentForm} className="sims-icon-btn" title="Cancel edit">
                <X className="h-4 w-4" />
              </button>
            )}
          </div>
          <div className="grid grid-cols-1 gap-2 border-b p-4 md:grid-cols-[minmax(180px,2fr)_minmax(160px,1.3fr)_minmax(140px,1fr)_80px_76px_auto]" style={{ borderColor: 'var(--line-2)', background: 'var(--surface-2)' }}>
            <select value={assignmentForm.policyNumberSequenceId} onChange={(e) => setAssignmentForm((f) => ({ ...f, policyNumberSequenceId: e.target.value }))} className="sims-select">
              {sequences.map((sequence) => <option key={sequence.id} value={sequence.id}>{sequence.name}</option>)}
            </select>
            <select value={assignmentForm.carrierId} onChange={(e) => setAssignmentForm((f) => ({ ...f, carrierId: e.target.value }))} className="sims-select">
              {carriers.map((carrier) => <option key={carrier.id} value={carrier.id}>{carrier.name}</option>)}
            </select>
            <select value={assignmentForm.lineOfBusiness} onChange={(e) => setAssignmentForm((f) => ({ ...f, lineOfBusiness: e.target.value as PolicyLineOfBusiness }))} className="sims-select">
              {ACTIVE_LOBS.map((lob) => <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>)}
            </select>
            <input value={assignmentForm.state ?? ''} onChange={(e) => setAssignmentForm((f) => ({ ...f, state: e.target.value.toUpperCase().slice(0, 2) }))} placeholder="State" className="sims-input" />
            <input type="number" value={assignmentForm.priority} onChange={(e) => setAssignmentForm((f) => ({ ...f, priority: Number(e.target.value) || 0 }))} className="sims-input" />
            <button onClick={() => saveAssignment.mutate()} disabled={saveAssignment.isPending || !assignmentForm.policyNumberSequenceId || !assignmentForm.carrierId} className="sd-btn primary">
              {editingAssignmentId ? <Save className="h-4 w-4" /> : <Plus className="h-4 w-4" />}
              {editingAssignmentId ? 'Save' : 'Add'}
            </button>
          </div>
          <div className="overflow-auto">
            <table className="sd-table">
              <thead>
                <tr>
                  <th>Carrier</th>
                  <th>Line</th>
                  <th>Sequence</th>
                  <th>State</th>
                  <th className="num">Priority</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {assignments.map((assignment) => (
                  <tr key={assignment.id}>
                    <td className="primary-cell">{assignment.carrierName}</td>
                    <td>{LOB_LABELS[assignment.lineOfBusiness]}</td>
                    <td>{assignment.sequenceName}</td>
                    <td>{assignment.state ?? 'All states'}</td>
                    <td className="num">{assignment.priority}</td>
                    <td>
                      <span className={`sd-pill ${assignment.isActive ? 'bound' : 'expired'}`}>{assignment.isActive ? 'Active' : 'Inactive'}</span>
                    </td>
                    <td>
                      <div className="flex justify-end gap-1">
                        <button onClick={() => editAssignment(assignment)} className="sims-icon-btn" title="Edit assignment">
                          <Pencil className="h-4 w-4" />
                        </button>
                        <button onClick={() => deleteAssignment.mutate(assignment.id)} className="sims-icon-btn hover:text-red-600" title="Remove assignment">
                          <Trash2 className="h-4 w-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
                {assignments.length === 0 && (
                  <tr>
                    <td colSpan={7} className="py-8 text-center" style={{ color: 'var(--ink-4)' }}>No assignments yet.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <section className="sd-card overflow-hidden">
        <div className="sd-card-head">
          <h3>Sequences <span className="cnt">{sequences.length}</span></h3>
        </div>
        <div className="overflow-auto">
          <table className="sd-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Format</th>
                <th className="num">Next</th>
                <th>Options</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {sequences.map((sequence) => (
                <tr key={sequence.id}>
                  <td className="primary-cell">{sequence.name}</td>
                  <td className="id">{sequence.format}{sequence.termSuffixFormat}</td>
                  <td className="num">{sequence.nextNumber}</td>
                  <td>
                    <div className="flex flex-wrap gap-1">
                      {sequence.resetAnnually && <span className="sd-lob">Annual reset</span>}
                      {sequence.allowManualOverride && <span className="sd-lob">Manual override</span>}
                      {!sequence.resetAnnually && !sequence.allowManualOverride && <span style={{ color: 'var(--ink-4)' }}>Standard</span>}
                    </div>
                  </td>
                  <td>
                    <span className={`sd-pill ${sequence.isActive ? 'bound' : 'expired'}`}>
                      {sequence.isActive && <Check className="h-3 w-3" />}
                      {sequence.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td>
                    <div className="flex justify-end gap-1">
                      <button onClick={() => editSequence(sequence)} className="sims-icon-btn" title="Edit sequence">
                        <Pencil className="h-4 w-4" />
                      </button>
                      <button onClick={() => deleteSequence.mutate(sequence.id)} className="sims-icon-btn hover:text-red-600" title="Remove sequence">
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
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

function cleanAssignment(assignment: PolicyNumberAssignmentUpsert): PolicyNumberAssignmentUpsert {
  return {
    ...assignment,
    state: assignment.state || undefined,
    priority: Number(assignment.priority) || 0,
  }
}
