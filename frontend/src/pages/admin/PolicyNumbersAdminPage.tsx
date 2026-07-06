import { useEffect, useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Hash, Link2, Pencil, Plus, Save, Trash2, X } from 'lucide-react'
import { toast } from 'sonner'
import { policyNumbersApi } from '@/api/policyNumbers.api'
import { carriersApi } from '@/api/carriers.api'
import { programConfigurationsApi } from '@/api/programConfigurations.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ErrorState } from '@/components/common/ErrorState'
import { getApiErrorMessage } from '@/lib/apiError'
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

// Tokens the policy-number generator understands (see PolicyNumberService.BuildBaseNumber).
const FORMAT_TOKENS: { token: string; hint: string }[] = [
  { token: '{CARRIER}', hint: 'Carrier short name' },
  { token: '{LOB}', hint: 'Line-of-business code (GL/IM/AL/APD)' },
  { token: '{STATE}', hint: 'Risk state (2 letters)' },
  { token: '{COMPANY}', hint: 'Writing company' },
  { token: '{YY}', hint: '2-digit year' },
  { token: '{YYYY}', hint: '4-digit year' },
  { token: '{SEQ:00000}', hint: 'Zero-padded sequence' },
]
const TERM_TOKENS: { token: string; hint: string }[] = [
  { token: '{TERM:00}', hint: 'Zero-padded term number' },
  { token: '{TERM}', hint: 'Term number' },
]

export function PolicyNumbersAdminPage() {
  const qc = useQueryClient()
  const [sequenceForm, setSequenceForm] = useState<PolicyNumberSequenceUpsert>(emptySequence)
  const formatInputRef = useRef<HTMLInputElement>(null)
  const termInputRef = useRef<HTMLInputElement>(null)

  // Insert a format token at the caret (or append) so users don't memorize the tag list (F13).
  const insertToken = (field: 'format' | 'termSuffixFormat', token: string, el: HTMLInputElement | null) => {
    const current = sequenceForm[field]
    const start = el?.selectionStart ?? current.length
    const end = el?.selectionEnd ?? current.length
    const next = current.slice(0, start) + token + current.slice(end)
    setSequenceForm((f) => ({ ...f, [field]: next }))
    requestAnimationFrame(() => {
      if (!el) return
      el.focus()
      const caret = start + token.length
      el.setSelectionRange(caret, caret)
    })
  }
  const [editingSequenceId, setEditingSequenceId] = useState<string | null>(null)
  const [assignmentForm, setAssignmentForm] = useState<PolicyNumberAssignmentUpsert>(emptyAssignment)
  const [editingAssignmentId, setEditingAssignmentId] = useState<string | null>(null)

  const { data: sequences = [], isLoading: loadingSequences, isError: sequencesError, error: sequencesErr, refetch: refetchSequences } = useQuery({
    queryKey: ['policy-number-sequences'],
    queryFn: () => policyNumbersApi.getSequences(true),
  })

  const { data: assignments = [], isLoading: loadingAssignments, isError: assignmentsError, error: assignmentsErr, refetch: refetchAssignments } = useQuery({
    queryKey: ['policy-number-assignments'],
    queryFn: () => policyNumbersApi.getAssignments(true),
  })

  const { data: carriers = [] } = useQuery({
    queryKey: ['carriers', 'active'],
    queryFn: () => carriersApi.getAll(true),
  })

  const { data: programs = [] } = useQuery({
    queryKey: ['program-configurations', 'options', 'all'],
    queryFn: () => programConfigurationsApi.getOptions(true),
  })

  const selectedAssignmentProgram = programs.find((program) => program.id === assignmentForm.programConfigurationId)
  const selectedProgramCarriers = selectedAssignmentProgram?.carriers.filter((programCarrier) => programCarrier.isActive) ?? []
  const assignmentCarrierOptions = assignmentForm.programConfigurationId
    ? carriers.filter((carrier) => selectedProgramCarriers.some((programCarrier) => programCarrier.carrierId === carrier.id))
    : carriers
  const selectedProgramCarrier = selectedProgramCarriers.find((programCarrier) => programCarrier.carrierId === assignmentForm.carrierId)
  const assignmentLobOptions = selectedProgramCarrier
    ? selectedProgramCarrier.linesOfBusiness
        .filter((lob) => lob.isActive)
        .map((lob) => lob.lineOfBusiness)
    : ACTIVE_LOBS
  const selectedProgramLob = selectedProgramCarrier?.linesOfBusiness.find((lob) => lob.lineOfBusiness === assignmentForm.lineOfBusiness && lob.isActive)
  const assignmentStateOptions = useMemo(
    () => selectedProgramLob?.states.filter((state) => state.isActive).map((state) => state.stateCode) ?? [],
    [selectedProgramLob],
  )

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
    if (!assignmentForm.carrierId && assignmentCarrierOptions.length > 0) {
      setAssignmentForm((f) => ({ ...f, carrierId: assignmentCarrierOptions[0].id }))
    }
  }, [assignmentCarrierOptions, assignmentForm.carrierId])

  useEffect(() => {
    if (assignmentLobOptions.length > 0 && !assignmentLobOptions.includes(assignmentForm.lineOfBusiness)) {
      setAssignmentForm((f) => ({ ...f, lineOfBusiness: assignmentLobOptions[0], state: '' }))
    }
  }, [assignmentForm.lineOfBusiness, assignmentLobOptions])

  useEffect(() => {
    if (assignmentForm.state && selectedProgramLob && !assignmentStateOptions.includes(assignmentForm.state)) {
      setAssignmentForm((f) => ({ ...f, state: '' }))
    }
  }, [assignmentForm.state, assignmentStateOptions, selectedProgramLob])

  const saveSequence = useMutation({
    mutationFn: () => editingSequenceId
      ? policyNumbersApi.updateSequence(editingSequenceId, cleanSequence(sequenceForm))
      : policyNumbersApi.createSequence(cleanSequence(sequenceForm)),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policy-number-sequences'] })
      resetSequenceForm()
      toast.success('Policy number sequence saved')
    },
    onError: (e) => toast.error(getApiErrorMessage(e, 'Sequence could not be saved')),
  })

  const deleteSequence = useMutation({
    mutationFn: policyNumbersApi.deleteSequence,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policy-number-sequences'] })
      toast.success('Sequence removed')
    },
    onError: (e) => toast.error(getApiErrorMessage(e, 'Sequence could not be removed')),
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
    onError: (e) => toast.error(getApiErrorMessage(e, 'Assignment could not be saved')),
  })

  const deleteAssignment = useMutation({
    mutationFn: policyNumbersApi.deleteAssignment,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policy-number-assignments'] })
      toast.success('Assignment removed')
    },
    onError: (e) => toast.error(getApiErrorMessage(e, 'Assignment could not be removed')),
  })

  const resetSequenceForm = () => {
    setSequenceForm(emptySequence)
    setEditingSequenceId(null)
  }

  const resetAssignmentForm = () => {
    setAssignmentForm((f) => ({
      ...emptyAssignment,
      programConfigurationId: f.programConfigurationId,
      policyNumberSequenceId: f.policyNumberSequenceId || sequences[0]?.id || '',
      carrierId: f.carrierId || assignmentCarrierOptions[0]?.id || '',
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
      programConfigurationId: assignment.programConfigurationId ?? undefined,
      carrierId: assignment.carrierId,
      writingCompanyId: assignment.writingCompanyId ?? undefined,
      lineOfBusiness: assignment.lineOfBusiness,
      state: assignment.state ?? '',
      priority: assignment.priority,
      isActive: assignment.isActive,
    })
  }

  if (loadingSequences || loadingAssignments) return <LoadingSpinner />
  if (sequencesError) return <ErrorState error={sequencesErr} onRetry={refetchSequences} />
  if (assignmentsError) return <ErrorState error={assignmentsErr} onRetry={refetchAssignments} />

  return (
    <div className="space-y-5 p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="m-0 text-[22px] font-semibold tracking-[-0.01em]" style={{ color: 'var(--ink)' }}>Policy Numbering</h1>
          <p className="m-0 mt-1 text-sm" style={{ color: 'var(--ink-3)' }}>Program validates the setup path; policy numbers assign by carrier, line, and state.</p>
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
              <input ref={formatInputRef} value={sequenceForm.format} onChange={(e) => setSequenceForm((f) => ({ ...f, format: e.target.value }))} className="sims-input" style={policyNumberStyle} />
            </label>
            <div className="flex flex-wrap gap-1.5">
              {FORMAT_TOKENS.map((t) => (
                <button key={t.token} type="button" title={t.hint} onClick={() => insertToken('format', t.token, formatInputRef.current)}
                  className="rounded border px-1.5 py-0.5 text-[11px]" style={{ borderColor: 'var(--line)', color: 'var(--ink-2)', background: 'var(--surface-2)', ...policyNumberStyle }}>
                  {t.token}
                </button>
              ))}
            </div>
            <div className="grid grid-cols-2 gap-3">
              <label className="block">
                <span className="sims-field-label">Next number</span>
                <input type="number" min={1} value={sequenceForm.nextNumber} onChange={(e) => setSequenceForm((f) => ({ ...f, nextNumber: Number(e.target.value) || 1 }))} className="sims-input" />
              </label>
              <label className="block">
                <span className="sims-field-label">Term suffix</span>
                <input ref={termInputRef} value={sequenceForm.termSuffixFormat} onChange={(e) => setSequenceForm((f) => ({ ...f, termSuffixFormat: e.target.value }))} className="sims-input" style={policyNumberStyle} />
                <div className="mt-1.5 flex flex-wrap gap-1.5">
                  {TERM_TOKENS.map((t) => (
                    <button key={t.token} type="button" title={t.hint} onClick={() => insertToken('termSuffixFormat', t.token, termInputRef.current)}
                      className="rounded border px-1.5 py-0.5 text-[11px]" style={{ borderColor: 'var(--line)', color: 'var(--ink-2)', background: 'var(--surface-2)', ...policyNumberStyle }}>
                      {t.token}
                    </button>
                  ))}
                </div>
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
                  <p key={number} className="m-0 text-sm" style={{ ...policyNumberStyle, color: 'var(--ink)' }}>{number}</p>
                ))}
              </div>
            </div>
            <button onClick={() => {
              // Warn if lowering NextNumber on an existing sequence — reissuing already-minted numbers (audit A5).
              const stored = editingSequenceId ? sequences.find((s) => s.id === editingSequenceId) : undefined
              if (stored && Number(sequenceForm.nextNumber) < stored.nextNumber &&
                  !confirm(`Next number ${sequenceForm.nextNumber} is lower than the current ${stored.nextNumber}. This can mint DUPLICATE policy numbers. Continue?`)) return
              saveSequence.mutate()
            }} disabled={saveSequence.isPending || !sequenceForm.name || !sequenceForm.format} className="sd-btn primary">
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
          <div className="grid grid-cols-1 items-end gap-2 border-b p-4 md:grid-cols-[minmax(150px,1.5fr)_minmax(180px,1.8fr)_minmax(160px,1.3fr)_minmax(140px,1fr)_80px_76px_auto]" style={{ borderColor: 'var(--line-2)', background: 'var(--surface-2)' }}>
            <label className="block">
              <span className="sims-field-label">Program</span>
              <select value={assignmentForm.programConfigurationId ?? ''} onChange={(e) => setAssignmentForm((f) => ({ ...f, programConfigurationId: e.target.value || undefined, carrierId: '', state: '' }))} className="sims-select">
                <option value="">All programs</option>
                {programs.map((program) => <option key={program.id} value={program.id}>{program.name}</option>)}
              </select>
            </label>
            <label className="block">
              <span className="sims-field-label">Sequence</span>
              <select value={assignmentForm.policyNumberSequenceId} onChange={(e) => setAssignmentForm((f) => ({ ...f, policyNumberSequenceId: e.target.value }))} className="sims-select">
                {sequences.map((sequence) => <option key={sequence.id} value={sequence.id}>{sequence.name}</option>)}
              </select>
            </label>
            <label className="block">
              <span className="sims-field-label">Carrier</span>
              <select value={assignmentForm.carrierId} onChange={(e) => setAssignmentForm((f) => ({ ...f, carrierId: e.target.value, state: '' }))} className="sims-select">
                {assignmentCarrierOptions.map((carrier) => <option key={carrier.id} value={carrier.id}>{carrier.name}</option>)}
              </select>
            </label>
            <label className="block">
              <span className="sims-field-label">LOB</span>
              <select value={assignmentForm.lineOfBusiness} onChange={(e) => setAssignmentForm((f) => ({ ...f, lineOfBusiness: e.target.value as PolicyLineOfBusiness, state: '' }))} className="sims-select">
                {assignmentLobOptions.map((lob) => <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>)}
              </select>
            </label>
            <label className="block">
              <span className="sims-field-label">State</span>
              {assignmentForm.programConfigurationId && selectedProgramLob ? (
                <select value={assignmentForm.state ?? ''} onChange={(e) => setAssignmentForm((f) => ({ ...f, state: e.target.value }))} className="sims-select">
                  <option value="">All states</option>
                  {assignmentStateOptions.map((state) => <option key={state} value={state}>{state}</option>)}
                </select>
              ) : (
                <input value={assignmentForm.state ?? ''} onChange={(e) => setAssignmentForm((f) => ({ ...f, state: e.target.value.toUpperCase().slice(0, 2) }))} placeholder="State" className="sims-input" />
              )}
            </label>
            <label className="block">
              <span className="sims-field-label">Priority</span>
              <input type="number" value={assignmentForm.priority} onChange={(e) => setAssignmentForm((f) => ({ ...f, priority: Number(e.target.value) || 0 }))} className="sims-input" />
            </label>
            <button onClick={() => saveAssignment.mutate()} disabled={saveAssignment.isPending || !assignmentForm.policyNumberSequenceId || !assignmentForm.carrierId} className="sd-btn primary">
              {editingAssignmentId ? <Save className="h-4 w-4" /> : <Plus className="h-4 w-4" />}
              {editingAssignmentId ? 'Save' : 'Add'}
            </button>
          </div>
          <div className="overflow-auto">
            <table className="sd-table">
              <thead>
                <tr>
                  <th>Program</th>
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
                    <td>{assignment.programName ?? 'All programs'}</td>
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
                        <button onClick={() => {
                          const scope = [LOB_LABELS[assignment.lineOfBusiness], assignment.state ?? 'All states'].filter(Boolean).join(' · ')
                          if (confirm(`Delete this policy-number assignment (${scope} → ${assignment.sequenceName})?\n\nBinds matching this scope will fall back to legacy numbering. This cannot be undone.`)) deleteAssignment.mutate(assignment.id)
                        }} className="sims-icon-btn hover:text-red-600" title="Remove assignment">
                          <Trash2 className="h-4 w-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
                {assignments.length === 0 && (
                  <tr>
                    <td colSpan={8} className="py-8 text-center" style={{ color: 'var(--ink-4)' }}>No assignments yet.</td>
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
                  <td style={policyNumberStyle}>{sequence.format}{sequence.termSuffixFormat}</td>
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
                      <button onClick={() => {
                        const refs = assignments.filter((a) => a.policyNumberSequenceId === sequence.id).length
                        const warn = refs > 0
                          ? `\n\n${refs} assignment${refs === 1 ? '' : 's'} reference this sequence and will stop generating numbers.`
                          : ''
                        if (confirm(`Delete policy-number sequence "${sequence.name}"?${warn}\n\nThis cannot be undone.`)) deleteSequence.mutate(sequence.id)
                      }} className="sims-icon-btn hover:text-red-600" title="Remove sequence">
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
    programConfigurationId: assignment.programConfigurationId || undefined,
    state: assignment.state || undefined,
    priority: Number(assignment.priority) || 0,
  }
}

const policyNumberStyle = { fontFamily: 'var(--font-mono)' }
