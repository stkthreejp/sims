import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Copy, Layers3, Pencil, Plus, Save, X } from 'lucide-react'
import { toast } from 'sonner'
import { carriersApi } from '@/api/carriers.api'
import { programConfigurationsApi } from '@/api/programConfigurations.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ErrorState } from '@/components/common/ErrorState'
import { PageHeader } from '@/components/common/PageHeader'
import { getApiErrorMessage } from '@/lib/apiError'
import { todayLocal } from '@/lib/utils'
import type {
  ProgramCarrier,
  ProgramCarrierLineOfBusiness,
  ProgramCarrierLineOfBusinessUpsert,
  ProgramCarrierLobState,
  ProgramCarrierLobStateUpsert,
  ProgramCarrierUpsert,
  ProgramConfiguration,
  ProgramConfigurationUpsert,
  ProgramOrphanIssue,
} from '@/types/programConfiguration.types'
import { ACTIVE_LOBS, LOB_LABELS, type PolicyLineOfBusiness } from '@/types/quote.types'

const US_STATES = ['AL','AK','AZ','AR','CA','CO','CT','DE','FL','GA','HI','ID','IL','IN','IA','KS','KY','LA','ME','MD','MA','MI','MN','MS','MO','MT','NE','NV','NH','NJ','NM','NY','NC','ND','OH','OK','OR','PA','RI','SC','SD','TN','TX','UT','VA','VT','WA','WV','WI','WY','DC']

const emptyProgram: ProgramConfigurationUpsert = {
  name: '',
  code: '',
  isActive: true,
  notes: '',
}

const today = () => todayLocal()

const emptyCarrier = (): ProgramCarrierUpsert => ({
  carrierId: '',
  isActive: true,
  effectiveDate: today(),
  expirationDate: null,
  notes: '',
})

const emptyLob = (): ProgramCarrierLineOfBusinessUpsert => ({
  lineOfBusiness: 'InlandMarine',
  isActive: true,
  effectiveDate: today(),
  expirationDate: null,
  billingMode: '',
  paymentTermsDays: null,
  londonUmr: '',
  londonSectionNumber: '',
  londonClassOfBusiness: '',
  londonRiskCode: '',
  londonInsuranceType: 'DIRECT',
  notes: '',
})

const emptyState = (): ProgramCarrierLobStateUpsert => ({
  stateCode: 'TX',
  isActive: true,
  effectiveDate: today(),
  expirationDate: null,
  notes: '',
})

const inputCls = 'w-full rounded border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-blue-400'
const miniBtn = 'inline-flex items-center gap-1 rounded border border-slate-200 px-2 py-1 text-xs font-medium text-slate-600 hover:bg-slate-50'

export function ProgramConfigurationAdminPage() {
  const qc = useQueryClient()
  const [form, setForm] = useState<ProgramConfigurationUpsert>(emptyProgram)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [selectedProgramId, setSelectedProgramId] = useState<string | null>(null)
  const [carrierForm, setCarrierForm] = useState<ProgramCarrierUpsert>(emptyCarrier())
  const [editingCarrierId, setEditingCarrierId] = useState<string | null>(null)
  const [lobParentCarrierId, setLobParentCarrierId] = useState<string | null>(null)
  const [lobForm, setLobForm] = useState<ProgramCarrierLineOfBusinessUpsert>(emptyLob())
  const [editingLobId, setEditingLobId] = useState<string | null>(null)
  const [stateParent, setStateParent] = useState<{ carrierId: string; lobId: string } | null>(null)
  const [stateForm, setStateForm] = useState<ProgramCarrierLobStateUpsert>(emptyState())
  const [editingStateId, setEditingStateId] = useState<string | null>(null)
  const [copySource, setCopySource] = useState('TX')
  const [copyTarget, setCopyTarget] = useState('SC')

  const { data: programs = [], isLoading, isError, error, refetch } = useQuery({
    queryKey: ['admin', 'program-configurations'],
    queryFn: () => programConfigurationsApi.getAll(true),
  })

  const { data: carriers = [] } = useQuery({
    queryKey: ['carriers', 'active'],
    queryFn: () => carriersApi.getAll(true),
  })

  const selectedProgram = useMemo(
    () => programs.find((program) => program.id === selectedProgramId) ?? programs[0] ?? null,
    [programs, selectedProgramId],
  )

  useEffect(() => {
    if (!selectedProgramId && programs.length > 0) setSelectedProgramId(programs[0].id)
  }, [programs, selectedProgramId])

  // Reset all child-edit state when the selected program changes, so a Save started
  // under program A can never target program B's spine (audit A1 — wrong-target saves).
  useEffect(() => {
    setCarrierForm(emptyCarrier())
    setEditingCarrierId(null)
    setLobParentCarrierId(null)
    setLobForm(emptyLob())
    setEditingLobId(null)
    setStateParent(null)
    setStateForm(emptyState())
    setEditingStateId(null)
  }, [selectedProgram?.id])

  const refreshPrograms = () => {
    qc.invalidateQueries({ queryKey: ['admin', 'program-configurations'] })
    // The quote/PN/forms/BDX screens read the spine under this separate key family;
    // invalidate it too or their pickers show the pre-save spine for up to 5 min (audit A7).
    qc.invalidateQueries({ queryKey: ['program-configurations'] })
  }

  const [orphanIssues, setOrphanIssues] = useState<ProgramOrphanIssue[] | null>(null)
  const orphanAudit = useMutation({
    mutationFn: () => programConfigurationsApi.getOrphanAudit(),
    onSuccess: (audit) => {
      setOrphanIssues(audit.issues)
      if (audit.issues.length === 0) toast.success('Orphan audit clean — no findings')
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Orphan audit failed')),
  })

  const saveProgram = useMutation({
    mutationFn: () => {
      const payload = cleanProgram(form)
      return editingId
        ? programConfigurationsApi.update(editingId, payload)
        : programConfigurationsApi.create(payload)
    },
    onSuccess: (saved) => {
      toast.success('Program configuration saved')
      setForm(emptyProgram)
      setEditingId(null)
      setSelectedProgramId(saved.id)
      refreshPrograms()
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Program configuration could not be saved')),
  })

  const saveCarrier = useMutation({
    mutationFn: () => {
      if (!selectedProgram) throw new Error('Select a program first')
      const payload = cleanCarrier(carrierForm)
      return editingCarrierId
        ? programConfigurationsApi.updateCarrier(selectedProgram.id, editingCarrierId, payload)
        : programConfigurationsApi.addCarrier(selectedProgram.id, payload)
    },
    onSuccess: () => {
      toast.success('Carrier setup saved')
      setCarrierForm(emptyCarrier())
      setEditingCarrierId(null)
      refreshPrograms()
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Carrier setup could not be saved')),
  })

  const saveLob = useMutation({
    mutationFn: () => {
      if (!selectedProgram || !lobParentCarrierId) throw new Error('Select a carrier first')
      const payload = cleanLob(lobForm)
      return editingLobId
        ? programConfigurationsApi.updateLineOfBusiness(selectedProgram.id, lobParentCarrierId, editingLobId, payload)
        : programConfigurationsApi.addLineOfBusiness(selectedProgram.id, lobParentCarrierId, payload)
    },
    onSuccess: () => {
      toast.success('Line of business setup saved')
      setLobForm(emptyLob())
      setEditingLobId(null)
      refreshPrograms()
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Line of business setup could not be saved')),
  })

  const saveState = useMutation({
    mutationFn: () => {
      if (!selectedProgram || !stateParent) throw new Error('Select a line of business first')
      const payload = cleanState(stateForm)
      return editingStateId
        ? programConfigurationsApi.updateState(selectedProgram.id, stateParent.carrierId, stateParent.lobId, editingStateId, payload)
        : programConfigurationsApi.addState(selectedProgram.id, stateParent.carrierId, stateParent.lobId, payload)
    },
    onSuccess: () => {
      toast.success('State setup saved')
      setStateForm(emptyState())
      setEditingStateId(null)
      refreshPrograms()
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'State setup could not be saved')),
  })

  const copyState = useMutation({
    mutationFn: () => {
      if (!selectedProgram || !stateParent) throw new Error('Select a line of business first')
      return programConfigurationsApi.copyState(selectedProgram.id, stateParent.carrierId, stateParent.lobId, copySource, copyTarget)
    },
    onSuccess: () => {
      toast.success('State setup copied')
      refreshPrograms()
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'State setup could not be copied')),
  })

  function editProgram(program: ProgramConfiguration) {
    setEditingId(program.id)
    setSelectedProgramId(program.id)
    setForm({
      name: program.name,
      code: program.code,
      isActive: program.isActive,
      notes: program.notes ?? '',
    })
  }

  function editCarrier(carrier: ProgramCarrier) {
    setEditingCarrierId(carrier.id)
    setCarrierForm({
      carrierId: carrier.carrierId,
      isActive: carrier.isActive,
      effectiveDate: carrier.effectiveDate,
      expirationDate: carrier.expirationDate,
      notes: carrier.notes ?? '',
    })
  }

  function editLob(programCarrierId: string, lob: ProgramCarrierLineOfBusiness) {
    setLobParentCarrierId(programCarrierId)
    setEditingLobId(lob.id)
    setLobForm({
      lineOfBusiness: lob.lineOfBusiness,
      isActive: lob.isActive,
      effectiveDate: lob.effectiveDate,
      expirationDate: lob.expirationDate,
      billingMode: lob.billingMode ?? '',
      paymentTermsDays: lob.paymentTermsDays,
      londonUmr: lob.londonUmr ?? '',
      londonSectionNumber: lob.londonSectionNumber ?? '',
      londonClassOfBusiness: lob.londonClassOfBusiness ?? '',
      londonRiskCode: lob.londonRiskCode ?? '',
      londonInsuranceType: lob.londonInsuranceType ?? 'DIRECT',
      notes: lob.notes ?? '',
    })
  }

  function editState(programCarrierId: string, lobId: string, state: ProgramCarrierLobState) {
    setStateParent({ carrierId: programCarrierId, lobId })
    setEditingStateId(state.id)
    setStateForm({
      stateCode: state.stateCode,
      isActive: state.isActive,
      effectiveDate: state.effectiveDate,
      expirationDate: state.expirationDate,
      notes: state.notes ?? '',
    })
  }

  if (isLoading) return <LoadingSpinner />
  if (isError) return <ErrorState error={error} onRetry={refetch} />

  return (
    <div className="space-y-5 p-6">
      <PageHeader
        title="Program Configuration"
        subtitle="Set up the Program > Carrier > LOB > State foundation for quotes, policies, fees, documents, and reporting"
      />

      <div className="rounded-lg border bg-white">
        <div className="flex items-center justify-between gap-3 px-5 py-3">
          <div>
            <h2 className="text-sm font-semibold text-slate-800">Orphan audit</h2>
            <p className="text-xs text-slate-500">Checks every program for missing carriers, lines of business, and states.</p>
          </div>
          <button
            type="button"
            className={miniBtn}
            onClick={() => orphanAudit.mutate()}
            disabled={orphanAudit.isPending}
          >
            <Layers3 className="h-3.5 w-3.5" />
            {orphanAudit.isPending ? 'Running…' : 'Run orphan audit'}
          </button>
        </div>
        {orphanIssues !== null && (
          <div className="border-t px-5 py-3">
            {orphanIssues.length === 0 ? (
              <p className="text-sm text-emerald-700">No findings — every program path has carriers, lines of business, and states.</p>
            ) : (
              <ul className="space-y-1">
                {orphanIssues.map((issue, i) => (
                  <li key={i} className="text-sm">
                    <span className={issue.severity === 'error' ? 'font-semibold text-red-700' : 'font-semibold text-amber-700'}>
                      {issue.severity === 'error' ? 'Error' : 'Warning'}
                    </span>
                    <span className="text-slate-500"> · {issue.path} — </span>
                    <span className="text-slate-800">{issue.issue}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </div>

      <div className="grid gap-5 xl:grid-cols-[360px_1fr]">
        <section className="rounded-lg border bg-white">
          <div className="flex items-center justify-between gap-3 border-b px-5 py-4">
            <h2 className="text-sm font-semibold text-slate-800">{editingId ? 'Edit program' : 'New program'}</h2>
            {editingId && (
              <button type="button" onClick={() => { setForm(emptyProgram); setEditingId(null) }} className="sims-icon-btn" title="Cancel edit">
                <X className="h-4 w-4" />
              </button>
            )}
          </div>
          <div className="space-y-3 p-5">
            <TextInput label="Program name" value={form.name} onChange={(value) => setForm((f) => ({ ...f, name: value }))} />
            <TextInput label="Program code" value={form.code} onChange={(value) => setForm((f) => ({ ...f, code: value.toUpperCase() }))} mono />
            <CheckInput label="Active program" checked={form.isActive} onChange={(value) => setForm((f) => ({ ...f, isActive: value }))} />
            <TextArea label="Notes" value={form.notes ?? ''} onChange={(value) => setForm((f) => ({ ...f, notes: value }))} />
            <button
              type="button"
              onClick={() => saveProgram.mutate()}
              disabled={saveProgram.isPending || !form.name.trim() || !form.code.trim()}
              className="inline-flex w-full items-center justify-center gap-2 rounded bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {editingId ? <Save className="h-4 w-4" /> : <Plus className="h-4 w-4" />}
              {editingId ? 'Save Program' : 'Add Program'}
            </button>
          </div>
        </section>

        <section className="rounded-lg border bg-white">
          <div className="border-b px-5 py-4">
            <h2 className="text-sm font-semibold text-slate-800">Programs <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{programs.length}</span></h2>
          </div>
          <div className="overflow-auto">
            <table className="sd-table">
              <thead>
                <tr>
                  <th>Program</th>
                  <th>Code</th>
                  <th>Setup</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {programs.map((program) => (
                  <tr key={program.id} className={selectedProgram?.id === program.id ? 'bg-blue-50/60' : ''}>
                    <td className="primary-cell">
                      <button type="button" onClick={() => setSelectedProgramId(program.id)} className="text-left font-medium text-blue-700 hover:underline">
                        {program.name}
                      </button>
                    </td>
                    <td className="id">{program.code}</td>
                    <td>{program.carriers.length} carriers</td>
                    <td><StatusPill active={program.isActive} /></td>
                    <td>
                      <div className="flex justify-end">
                        <button type="button" onClick={() => editProgram(program)} className="sims-icon-btn" title="Edit program">
                          <Pencil className="h-4 w-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
                {programs.length === 0 && (
                  <tr>
                    <td colSpan={5} className="py-8 text-center" style={{ color: 'var(--ink-4)' }}>No programs configured yet.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      {selectedProgram && (
        <section className="rounded-lg border bg-white">
          <div className="flex items-center justify-between gap-3 border-b px-5 py-4">
            <div>
              <h2 className="text-sm font-semibold text-slate-800">{selectedProgram.name} setup</h2>
              <p className="mt-1 text-xs text-slate-500">Program to carrier to line of business to state</p>
            </div>
            <StatusPill active={selectedProgram.isActive} />
          </div>

          <div className="grid gap-5 p-5 xl:grid-cols-[360px_1fr]">
            <div className="space-y-4">
              <SetupForm title={editingCarrierId ? 'Edit carrier' : 'Add carrier'}>
                <SelectField label="Carrier" value={carrierForm.carrierId} onChange={(value) => setCarrierForm((f) => ({ ...f, carrierId: value }))}>
                  <option value="">Select carrier</option>
                  {carriers.map((carrier) => <option key={carrier.id} value={carrier.id}>{carrier.name}</option>)}
                </SelectField>
                <FoundationFields
                  isActive={carrierForm.isActive}
                  effectiveDate={carrierForm.effectiveDate}
                  expirationDate={carrierForm.expirationDate ?? ''}
                  notes={carrierForm.notes ?? ''}
                  onChange={(patch) => setCarrierForm((f) => ({ ...f, ...patch }))}
                />
                <FormActions
                  isEditing={!!editingCarrierId}
                  disabled={!carrierForm.carrierId || saveCarrier.isPending}
                  onSave={() => saveCarrier.mutate()}
                  onCancel={() => { setCarrierForm(emptyCarrier()); setEditingCarrierId(null) }}
                />
              </SetupForm>

              <SetupForm title={editingLobId ? 'Edit LOB' : 'Add LOB'}>
                <SelectField label="Carrier setup" value={lobParentCarrierId ?? ''} onChange={(value) => setLobParentCarrierId(value || null)}>
                  <option value="">Select program carrier</option>
                  {selectedProgram.carriers.map((carrier) => <option key={carrier.id} value={carrier.id}>{carrier.carrierName}</option>)}
                </SelectField>
                <SelectField label="Line of business" value={lobForm.lineOfBusiness} onChange={(value) => setLobForm((f) => ({ ...f, lineOfBusiness: value as PolicyLineOfBusiness }))}>
                  {ACTIVE_LOBS.map((lob) => <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>)}
                </SelectField>
                <FoundationFields
                  isActive={lobForm.isActive}
                  effectiveDate={lobForm.effectiveDate}
                  expirationDate={lobForm.expirationDate ?? ''}
                  notes={lobForm.notes ?? ''}
                  onChange={(patch) => setLobForm((f) => ({ ...f, ...patch }))}
                />
                <div className="grid grid-cols-2 gap-3">
                  <TextInput label="Billing mode" value={lobForm.billingMode ?? ''} onChange={(value) => setLobForm((f) => ({ ...f, billingMode: value }))} />
                  <TextInput label="Payment terms" type="number" value={lobForm.paymentTermsDays?.toString() ?? ''} onChange={(value) => setLobForm((f) => ({ ...f, paymentTermsDays: value ? Number(value) : null }))} />
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <TextInput label="London UMR" value={lobForm.londonUmr ?? ''} onChange={(value) => setLobForm((f) => ({ ...f, londonUmr: value }))} />
                  <TextInput label="London section" value={lobForm.londonSectionNumber ?? ''} onChange={(value) => setLobForm((f) => ({ ...f, londonSectionNumber: value }))} />
                  <TextInput label="London class" value={lobForm.londonClassOfBusiness ?? ''} onChange={(value) => setLobForm((f) => ({ ...f, londonClassOfBusiness: value }))} />
                  <TextInput label="London risk code" value={lobForm.londonRiskCode ?? ''} onChange={(value) => setLobForm((f) => ({ ...f, londonRiskCode: value }))} />
                  <TextInput label="London insurance type" value={lobForm.londonInsuranceType ?? ''} onChange={(value) => setLobForm((f) => ({ ...f, londonInsuranceType: value }))} />
                </div>
                <FormActions
                  isEditing={!!editingLobId}
                  disabled={!lobParentCarrierId || saveLob.isPending}
                  onSave={() => saveLob.mutate()}
                  onCancel={() => { setLobForm(emptyLob()); setEditingLobId(null); setLobParentCarrierId(null) }}
                />
              </SetupForm>

              <SetupForm title={editingStateId ? 'Edit state' : 'Add state'}>
                <SelectField label="LOB setup" value={stateParent ? `${stateParent.carrierId}:${stateParent.lobId}` : ''} onChange={(value) => {
                  const [carrierId, lobId] = value.split(':')
                  setStateParent(carrierId && lobId ? { carrierId, lobId } : null)
                }}>
                  <option value="">Select program carrier LOB</option>
                  {selectedProgram.carriers.flatMap((carrier) => carrier.linesOfBusiness.map((lob) => (
                    <option key={lob.id} value={`${carrier.id}:${lob.id}`}>{carrier.carrierName} / {lob.lineOfBusinessLabel}</option>
                  )))}
                </SelectField>
                <SelectField label="State" value={stateForm.stateCode} onChange={(value) => setStateForm((f) => ({ ...f, stateCode: value }))}>
                  {US_STATES.map((state) => <option key={state} value={state}>{state}</option>)}
                </SelectField>
                <FoundationFields
                  isActive={stateForm.isActive}
                  effectiveDate={stateForm.effectiveDate}
                  expirationDate={stateForm.expirationDate ?? ''}
                  notes={stateForm.notes ?? ''}
                  onChange={(patch) => setStateForm((f) => ({ ...f, ...patch }))}
                />
                <FormActions
                  isEditing={!!editingStateId}
                  disabled={!stateParent || saveState.isPending}
                  onSave={() => saveState.mutate()}
                  onCancel={() => { setStateForm(emptyState()); setEditingStateId(null); setStateParent(null) }}
                />
              </SetupForm>

              <SetupForm title="Copy state setup">
                <div className="grid grid-cols-2 gap-3">
                  <SelectField label="From" value={copySource} onChange={setCopySource}>
                    {US_STATES.map((state) => <option key={state} value={state}>{state}</option>)}
                  </SelectField>
                  <SelectField label="To" value={copyTarget} onChange={setCopyTarget}>
                    {US_STATES.map((state) => <option key={state} value={state}>{state}</option>)}
                  </SelectField>
                </div>
                <button
                  type="button"
                  onClick={() => copyState.mutate()}
                  disabled={!stateParent || !copySource || !copyTarget || copySource === copyTarget || copyState.isPending}
                  className="inline-flex w-full items-center justify-center gap-2 rounded border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                >
                  <Copy className="h-4 w-4" />
                  Copy Selected LOB State
                </button>
              </SetupForm>
            </div>

            <div className="space-y-3">
              {selectedProgram.carriers.map((carrier) => (
                <div key={carrier.id} className="rounded border border-slate-200">
                  <div className="flex items-center justify-between gap-3 border-b bg-slate-50 px-4 py-3">
                    <div>
                      <div className="font-medium text-slate-900">{carrier.carrierName}</div>
                      <div className="text-xs text-slate-500">{dateRange(carrier.effectiveDate, carrier.expirationDate)}</div>
                    </div>
                    <div className="flex items-center gap-2">
                      <StatusPill active={carrier.isActive} />
                      <button type="button" className={miniBtn} onClick={() => editCarrier(carrier)}><Pencil className="h-3 w-3" /> Edit</button>
                    </div>
                  </div>
                  <div className="space-y-3 p-4">
                    {carrier.linesOfBusiness.map((lob) => (
                      <div key={lob.id} className="rounded border border-slate-200">
                        <div className="flex items-center justify-between gap-3 px-3 py-2">
                          <div>
                            <div className="text-sm font-medium text-slate-800">{lob.lineOfBusinessLabel}</div>
                            <div className="text-xs text-slate-500">{dateRange(lob.effectiveDate, lob.expirationDate)}</div>
                            {(lob.billingMode || lob.paymentTermsDays != null) && (
                              <div className="mt-1 text-xs text-slate-500">{[lob.billingMode, lob.paymentTermsDays != null ? `Net ${lob.paymentTermsDays}` : null].filter(Boolean).join(' / ')}</div>
                            )}
                            {(lob.londonUmr || lob.londonRiskCode) && (
                              <div className="mt-1 text-xs text-slate-500">{[lob.londonUmr, lob.londonRiskCode].filter(Boolean).join(' / ')}</div>
                            )}
                          </div>
                          <div className="flex items-center gap-2">
                            <StatusPill active={lob.isActive} />
                            <button type="button" className={miniBtn} onClick={() => editLob(carrier.id, lob)}><Pencil className="h-3 w-3" /> Edit</button>
                          </div>
                        </div>
                        <div className="flex flex-wrap gap-2 border-t border-slate-100 px-3 py-3">
                          {lob.states.map((state) => (
                            <button
                              key={state.id}
                              type="button"
                              onClick={() => editState(carrier.id, lob.id, state)}
                              className={`inline-flex items-center gap-2 rounded border px-2 py-1 text-xs ${state.isActive ? 'border-emerald-200 bg-emerald-50 text-emerald-700' : 'border-slate-200 bg-slate-50 text-slate-500'}`}
                            >
                              {state.stateCode}
                              <Pencil className="h-3 w-3" />
                            </button>
                          ))}
                          {lob.states.length === 0 && <span className="text-xs text-slate-400">No states configured.</span>}
                        </div>
                      </div>
                    ))}
                    {carrier.linesOfBusiness.length === 0 && <div className="text-sm text-slate-400">No lines of business configured.</div>}
                  </div>
                </div>
              ))}
              {selectedProgram.carriers.length === 0 && (
                <div className="flex min-h-[220px] items-center justify-center rounded border border-dashed border-slate-300 text-sm text-slate-500">
                  <div className="text-center">
                    <Layers3 className="mx-auto mb-2 h-6 w-6 text-slate-400" />
                    Add a carrier to start the nested setup.
                  </div>
                </div>
              )}
            </div>
          </div>
        </section>
      )}
    </div>
  )
}

function SetupForm({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="space-y-3 rounded border border-slate-200 p-4">
      <h3 className="text-sm font-semibold text-slate-800">{title}</h3>
      {children}
    </div>
  )
}

function FoundationFields({
  isActive,
  effectiveDate,
  expirationDate,
  notes,
  onChange,
}: {
  isActive: boolean
  effectiveDate: string
  expirationDate: string
  notes: string
  onChange: (patch: Partial<ProgramCarrierUpsert & ProgramCarrierLineOfBusinessUpsert & ProgramCarrierLobStateUpsert>) => void
}) {
  return (
    <>
      <div className="grid grid-cols-2 gap-3">
        <TextInput label="Effective" type="date" value={effectiveDate} onChange={(value) => onChange({ effectiveDate: value })} />
        <TextInput label="Expiration" type="date" value={expirationDate} onChange={(value) => onChange({ expirationDate: value || null })} />
      </div>
      <CheckInput label="Active" checked={isActive} onChange={(value) => onChange({ isActive: value })} />
      <TextArea label="Notes" value={notes} onChange={(value) => onChange({ notes: value })} />
    </>
  )
}

function FormActions({ isEditing, disabled, onSave, onCancel }: { isEditing: boolean; disabled: boolean; onSave: () => void; onCancel: () => void }) {
  return (
    <div className="flex gap-2">
      <button type="button" onClick={onSave} disabled={disabled} className="inline-flex flex-1 items-center justify-center gap-2 rounded bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50">
        {isEditing ? <Save className="h-4 w-4" /> : <Plus className="h-4 w-4" />}
        {isEditing ? 'Save' : 'Add'}
      </button>
      {isEditing && (
        <button type="button" onClick={onCancel} className="inline-flex items-center justify-center rounded border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
          <X className="h-4 w-4" />
        </button>
      )}
    </div>
  )
}

function TextInput({ label, value, onChange, mono = false, type = 'text' }: { label: string; value: string; onChange: (value: string) => void; mono?: boolean; type?: string }) {
  return (
    <label className="block">
      <span className="sims-field-label">{label}</span>
      <input type={type} value={value} onChange={(e) => onChange(e.target.value)} className={`${inputCls} ${mono ? 'font-mono' : ''}`} />
    </label>
  )
}

function TextArea({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <label className="block">
      <span className="sims-field-label">{label}</span>
      <textarea value={value} onChange={(e) => onChange(e.target.value)} className={inputCls} rows={3} />
    </label>
  )
}

function SelectField({ label, value, onChange, children }: { label: string; value: string; onChange: (value: string) => void; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="sims-field-label">{label}</span>
      <select value={value} onChange={(e) => onChange(e.target.value)} className={inputCls}>
        {children}
      </select>
    </label>
  )
}

function CheckInput({ label, checked, onChange }: { label: string; checked: boolean; onChange: (value: boolean) => void }) {
  return (
    <label className="flex items-center gap-2 rounded border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-700">
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} />
      {label}
    </label>
  )
}

function StatusPill({ active }: { active: boolean }) {
  return (
    <span className={`sd-pill ${active ? 'bound' : 'expired'}`}>
      {active && <Check className="h-3 w-3" />}
      {active ? 'Active' : 'Inactive'}
    </span>
  )
}

function dateRange(effectiveDate: string, expirationDate: string | null) {
  return `${effectiveDate}${expirationDate ? ` to ${expirationDate}` : ''}`
}

function cleanProgram(program: ProgramConfigurationUpsert): ProgramConfigurationUpsert {
  return {
    ...program,
    notes: program.notes?.trim() ? program.notes : null,
  }
}

function cleanCarrier(carrier: ProgramCarrierUpsert): ProgramCarrierUpsert {
  return {
    ...carrier,
    expirationDate: carrier.expirationDate || null,
    notes: carrier.notes?.trim() ? carrier.notes : null,
  }
}

function cleanLob(lob: ProgramCarrierLineOfBusinessUpsert): ProgramCarrierLineOfBusinessUpsert {
  return {
    ...lob,
    expirationDate: lob.expirationDate || null,
    billingMode: lob.billingMode?.trim() ? lob.billingMode : null,
    paymentTermsDays: lob.paymentTermsDays ?? null,
    londonUmr: lob.londonUmr?.trim() ? lob.londonUmr : null,
    londonSectionNumber: lob.londonSectionNumber?.trim() ? lob.londonSectionNumber : null,
    londonClassOfBusiness: lob.londonClassOfBusiness?.trim() ? lob.londonClassOfBusiness : null,
    londonRiskCode: lob.londonRiskCode?.trim() ? lob.londonRiskCode : null,
    londonInsuranceType: lob.londonInsuranceType?.trim() ? lob.londonInsuranceType : null,
    notes: lob.notes?.trim() ? lob.notes : null,
  }
}

function cleanState(state: ProgramCarrierLobStateUpsert): ProgramCarrierLobStateUpsert {
  return {
    ...state,
    stateCode: state.stateCode.toUpperCase(),
    expirationDate: state.expirationDate || null,
    notes: state.notes?.trim() ? state.notes : null,
  }
}
