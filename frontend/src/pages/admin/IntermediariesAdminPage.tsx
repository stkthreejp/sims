import { useEffect, useMemo, useState } from 'react'
import type { ElementType, ReactNode } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Banknote, Building2, Check, Handshake, Pencil, Plus, Trash2, X } from 'lucide-react'
import { toast } from 'sonner'
import { intermediariesApi } from '@/api/intermediaries.api'
import { feesApi } from '@/api/fees.api'
import { programConfigurationsApi } from '@/api/programConfigurations.api'
import { EmptyState } from '@/components/common/EmptyState'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { PageHeader } from '@/components/common/PageHeader'
import type { Intermediary, IntermediaryBrokerageSetup, IntermediaryBrokerageSetupUpsert, IntermediaryUpsert } from '@/types/intermediary.types'
import type { PolicyLineOfBusiness } from '@/types/quote.types'

type IntermediaryForm = {
  name: string
  referenceNumber: string
  email: string
  phone: string
  addressLine1: string
  addressLine2: string
  city: string
  state: string
  zipCode: string
  country: string
  bankName: string
  bankAccountName: string
  bankAccountLast4: string
  bankRoutingNumber: string
  bankSwiftCode: string
  bankInstructions: string
  isActive: boolean
  notes: string
}

type SetupForm = {
  programConfigurationId: string
  carrierId: string
  lineOfBusiness: '' | PolicyLineOfBusiness
  effectiveDate: string
  expirationDate: string
  brokerageRate: string
  createPayable: boolean
  payablePayeeId: string
  isActive: boolean
  notes: string
}

const emptyIntermediaryForm = (): IntermediaryForm => ({
  name: '',
  referenceNumber: '',
  email: '',
  phone: '',
  addressLine1: '',
  addressLine2: '',
  city: '',
  state: '',
  zipCode: '',
  country: 'USA',
  bankName: '',
  bankAccountName: '',
  bankAccountLast4: '',
  bankRoutingNumber: '',
  bankSwiftCode: '',
  bankInstructions: '',
  isActive: true,
  notes: '',
})

const emptySetupForm = (): SetupForm => ({
  programConfigurationId: '',
  carrierId: '',
  lineOfBusiness: '',
  effectiveDate: new Date().toISOString().slice(0, 10),
  expirationDate: '',
  brokerageRate: '',
  createPayable: false,
  payablePayeeId: '',
  isActive: true,
  notes: '',
})

function intermediaryToForm(intermediary: Intermediary): IntermediaryForm {
  return {
    name: intermediary.name,
    referenceNumber: intermediary.referenceNumber ?? '',
    email: intermediary.email ?? '',
    phone: intermediary.phone ?? '',
    addressLine1: intermediary.addressLine1 ?? '',
    addressLine2: intermediary.addressLine2 ?? '',
    city: intermediary.city ?? '',
    state: intermediary.state ?? '',
    zipCode: intermediary.zipCode ?? '',
    country: intermediary.country ?? 'USA',
    bankName: intermediary.bankName ?? '',
    bankAccountName: intermediary.bankAccountName ?? '',
    bankAccountLast4: intermediary.bankAccountLast4 ?? '',
    bankRoutingNumber: intermediary.bankRoutingNumber ?? '',
    bankSwiftCode: intermediary.bankSwiftCode ?? '',
    bankInstructions: intermediary.bankInstructions ?? '',
    isActive: intermediary.isActive,
    notes: intermediary.notes ?? '',
  }
}

function setupToForm(setup: IntermediaryBrokerageSetup): SetupForm {
  return {
    programConfigurationId: setup.programConfigurationId,
    carrierId: setup.carrierId,
    lineOfBusiness: setup.lineOfBusiness ?? '',
    effectiveDate: setup.effectiveDate,
    expirationDate: setup.expirationDate ?? '',
    brokerageRate: setup.brokerageRate?.toString() ?? '',
    createPayable: setup.createPayable,
    payablePayeeId: setup.payablePayeeId?.toString() ?? '',
    isActive: setup.isActive,
    notes: setup.notes ?? '',
  }
}

const blankToNull = (value: string) => {
  const trimmed = value.trim()
  return trimmed ? trimmed : null
}

type EffectiveDatedActive = {
  isActive: boolean
  effectiveDate: string
  expirationDate: string | null
}

function isActiveOnDate(row: EffectiveDatedActive, date: string) {
  return row.isActive && (!date || (row.effectiveDate <= date && (!row.expirationDate || row.expirationDate >= date)))
}

function toIntermediaryPayload(form: IntermediaryForm): IntermediaryUpsert {
  return {
    name: form.name.trim(),
    referenceNumber: blankToNull(form.referenceNumber),
    email: blankToNull(form.email),
    phone: blankToNull(form.phone),
    addressLine1: blankToNull(form.addressLine1),
    addressLine2: blankToNull(form.addressLine2),
    city: blankToNull(form.city),
    state: blankToNull(form.state)?.toUpperCase() ?? null,
    zipCode: blankToNull(form.zipCode),
    country: blankToNull(form.country)?.toUpperCase() ?? null,
    bankName: blankToNull(form.bankName),
    bankAccountName: blankToNull(form.bankAccountName),
    bankAccountLast4: blankToNull(form.bankAccountLast4),
    bankRoutingNumber: blankToNull(form.bankRoutingNumber),
    bankSwiftCode: blankToNull(form.bankSwiftCode),
    bankInstructions: blankToNull(form.bankInstructions),
    isActive: form.isActive,
    notes: blankToNull(form.notes),
  }
}

function toSetupPayload(form: SetupForm): IntermediaryBrokerageSetupUpsert {
  return {
    programConfigurationId: form.programConfigurationId,
    carrierId: form.carrierId,
    lineOfBusiness: form.lineOfBusiness || null,
    effectiveDate: form.effectiveDate,
    expirationDate: blankToNull(form.expirationDate),
    brokerageRate: form.brokerageRate ? Number(form.brokerageRate) : null,
    createPayable: form.createPayable,
    payablePayeeId: form.createPayable && form.payablePayeeId ? Number(form.payablePayeeId) : null,
    isActive: form.isActive,
    notes: blankToNull(form.notes),
  }
}

function SectionHeader({ icon: Icon, title }: { icon: ElementType; title: string }) {
  return (
    <h2 className="text-sm font-semibold text-slate-800 flex items-center gap-2 pt-2">
      <Icon className="h-4 w-4 text-slate-400" />
      {title}
    </h2>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block">
      <span className="sims-field-label">{label}</span>
      {children}
    </label>
  )
}

export function IntermediariesAdminPage() {
  const qc = useQueryClient()
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [isCreating, setIsCreating] = useState(false)
  const [form, setForm] = useState<IntermediaryForm>(emptyIntermediaryForm())
  const [showSetupForm, setShowSetupForm] = useState(false)
  const [editingSetupId, setEditingSetupId] = useState<string | null>(null)
  const [setupForm, setSetupForm] = useState<SetupForm>(emptySetupForm())

  const { data: intermediaries = [], isLoading: loadingList } = useQuery({
    queryKey: ['admin', 'intermediaries'],
    queryFn: () => intermediariesApi.getAll(true),
  })

  const { data: intermediary, isLoading: loadingDetail } = useQuery({
    queryKey: ['admin', 'intermediaries', selectedId],
    queryFn: () => intermediariesApi.getById(selectedId!),
    enabled: !!selectedId && !isCreating,
  })

  const { data: programs = [] } = useQuery({
    queryKey: ['admin', 'program-configurations', 'all'],
    queryFn: () => programConfigurationsApi.getAll(true),
  })

  const { data: payees = [] } = useQuery({
    queryKey: ['admin', 'fees', 'payees'],
    queryFn: () => feesApi.getPayees(),
  })

  useEffect(() => {
    if (!selectedId && !isCreating && intermediaries.length > 0) {
      setSelectedId(intermediaries[0].id)
    }
  }, [intermediaries, isCreating, selectedId])

  useEffect(() => {
    if (intermediary && !isCreating) {
      setForm(intermediaryToForm(intermediary))
      setShowSetupForm(false)
      setEditingSetupId(null)
    }
  }, [intermediary, isCreating])

  const selectedProgram = programs.find((program) => program.id === setupForm.programConfigurationId)
  const activeProgramCarriers = useMemo(
    () => selectedProgram?.carriers.filter((carrier) => isActiveOnDate(carrier, setupForm.effectiveDate)) ?? [],
    [selectedProgram, setupForm.effectiveDate]
  )
  const selectedProgramCarrier = activeProgramCarriers.find((carrier) => carrier.carrierId === setupForm.carrierId)
  const carrierOptions = useMemo(() => {
    return activeProgramCarriers.map((carrier) => ({
      id: carrier.carrierId,
      name: carrier.carrierName,
    }))
  }, [activeProgramCarriers])
  const lobOptions = useMemo(
    () => selectedProgramCarrier?.linesOfBusiness.filter((lob) => isActiveOnDate(lob, setupForm.effectiveDate)) ?? [],
    [selectedProgramCarrier, setupForm.effectiveDate]
  )

  const saveIntermediaryMutation = useMutation({
    mutationFn: () => {
      const payload = toIntermediaryPayload(form)
      return isCreating
        ? intermediariesApi.create(payload)
        : intermediariesApi.update(selectedId!, payload)
    },
    onSuccess: (saved) => {
      toast.success(isCreating ? 'Intermediary created' : 'Intermediary updated')
      qc.invalidateQueries({ queryKey: ['admin', 'intermediaries'] })
      setIsCreating(false)
      setSelectedId(saved.id)
      setForm(intermediaryToForm(saved))
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Save failed'),
  })

  const deleteIntermediaryMutation = useMutation({
    mutationFn: (id: string) => intermediariesApi.delete(id),
    onSuccess: () => {
      toast.success('Intermediary deleted')
      qc.invalidateQueries({ queryKey: ['admin', 'intermediaries'] })
      setSelectedId(null)
      setForm(emptyIntermediaryForm())
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Delete failed'),
  })

  const saveSetupMutation = useMutation({
    mutationFn: () => {
      const payload = toSetupPayload(setupForm)
      return editingSetupId
        ? intermediariesApi.updateBrokerageSetup(selectedId!, editingSetupId, payload)
        : intermediariesApi.createBrokerageSetup(selectedId!, payload)
    },
    onSuccess: () => {
      toast.success('Brokerage setup saved')
      qc.invalidateQueries({ queryKey: ['admin', 'intermediaries'] })
      qc.invalidateQueries({ queryKey: ['admin', 'intermediaries', selectedId] })
      resetSetupForm()
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Save failed'),
  })

  const deleteSetupMutation = useMutation({
    mutationFn: (setupId: string) => intermediariesApi.deleteBrokerageSetup(selectedId!, setupId),
    onSuccess: () => {
      toast.success('Brokerage setup deleted')
      qc.invalidateQueries({ queryKey: ['admin', 'intermediaries'] })
      qc.invalidateQueries({ queryKey: ['admin', 'intermediaries', selectedId] })
    },
    onError: () => toast.error('Delete failed'),
  })

  function startCreate() {
    setIsCreating(true)
    setSelectedId(null)
    setForm(emptyIntermediaryForm())
    resetSetupForm()
  }

  function selectIntermediary(id: string) {
    setIsCreating(false)
    setSelectedId(id)
  }

  function resetSetupForm() {
    setShowSetupForm(false)
    setEditingSetupId(null)
    setSetupForm(emptySetupForm())
  }

  function editSetup(setup: IntermediaryBrokerageSetup) {
    setEditingSetupId(setup.id)
    setSetupForm(setupToForm(setup))
    setShowSetupForm(true)
  }

  function saveSetup() {
    if (!selectedId) return
    if (!setupForm.programConfigurationId) { toast.error('Program is required'); return }
    if (!setupForm.carrierId) { toast.error('Carrier is required'); return }
    if (!carrierOptions.some((carrier) => carrier.id === setupForm.carrierId)) { toast.error('Carrier is not active for this program/date'); return }
    if (setupForm.lineOfBusiness && !lobOptions.some((lob) => lob.lineOfBusiness === setupForm.lineOfBusiness)) { toast.error('LOB is not active for this program/carrier/date'); return }
    if (!setupForm.effectiveDate) { toast.error('Effective date is required'); return }
    if (setupForm.createPayable && !setupForm.payablePayeeId) { toast.error('Payable payee is required'); return }
    saveSetupMutation.mutate()
  }

  const selectedName = isCreating ? 'New Intermediary' : intermediary?.name ?? 'Intermediary'

  if (loadingList) return <LoadingSpinner />

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="Intermediaries"
        subtitle="Maintain broker records and effective-dated brokerage setup"
        action={
          <button onClick={startCreate} className="sd-btn primary sm">
            <Plus className="h-3.5 w-3.5" /> New Intermediary
          </button>
        }
      />

      <div className="flex flex-col gap-5">
        <div className="bg-white border border-slate-200 rounded-lg overflow-hidden max-h-80 overflow-y-auto">
          <div className="px-4 py-3 border-b bg-slate-50 text-xs font-semibold text-slate-500 uppercase tracking-wide">
            Brokers
          </div>
          {intermediaries.length === 0 ? (
            <EmptyState
              icon={Handshake}
              title="No intermediaries yet"
              action={<button onClick={startCreate} className="sd-btn outline sm">Add intermediary</button>}
            />
          ) : (
            <div className="divide-y divide-slate-100">
              {intermediaries.map((row) => {
                const selected = row.id === selectedId && !isCreating
                const location = [row.city, row.state].filter(Boolean).join(', ')

                return (
                  <button
                    key={row.id}
                    onClick={() => selectIntermediary(row.id)}
                    className={`w-full text-left px-4 py-3 hover:bg-slate-50 ${selected ? 'bg-blue-50 border-l-2 border-l-blue-600' : ''}`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0">
                        <div className="text-sm font-semibold text-slate-900 truncate">{row.name}</div>
                        <div className="text-xs text-slate-500 truncate">
                          {[row.referenceNumber, location || null].filter(Boolean).join(' - ') || 'No reference'}
                        </div>
                      </div>
                      <span className={`text-[11px] px-1.5 py-0.5 rounded-full ${row.isActive ? 'bg-green-100 text-green-700' : 'bg-slate-100 text-slate-500'}`}>
                        {row.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </div>
                    <div className="mt-1 text-[11px] text-slate-400">
                      {row.activeBrokerageSetupCount} active / {row.brokerageSetupCount} setup rows
                    </div>
                  </button>
                )
              })}
            </div>
          )}
        </div>

        <div className="space-y-5 min-w-0">
          {(loadingDetail && !isCreating) ? (
            <div className="bg-white border border-slate-200 rounded-lg p-8 flex justify-center">
              <LoadingSpinner />
            </div>
          ) : (
            <div className="bg-white border border-slate-200 rounded-lg">
              <div className="px-5 py-4 border-b flex items-center justify-between">
                <div>
                  <h2 className="text-base font-semibold text-slate-900">{selectedName}</h2>
                  {!isCreating && intermediary && (
                    <p className="text-xs text-slate-400">Updated {new Date(intermediary.updatedAt).toLocaleDateString()}</p>
                  )}
                </div>
                <div className="flex gap-2">
                  {!isCreating && selectedId && intermediary && (
                    <button
                      onClick={() => {
                        if (confirm(`Delete ${intermediary.name}?`)) deleteIntermediaryMutation.mutate(selectedId)
                      }}
                      className="sd-btn outline sm text-red-600"
                      disabled={deleteIntermediaryMutation.isPending}
                    >
                      <Trash2 className="h-3.5 w-3.5" /> Delete
                    </button>
                  )}
                  {isCreating && (
                    <button onClick={() => { setIsCreating(false); setSelectedId(intermediaries[0]?.id ?? null) }} className="sd-btn outline sm">
                      <X className="h-3.5 w-3.5" /> Cancel
                    </button>
                  )}
                  <button
                    onClick={() => {
                      if (!form.name.trim()) { toast.error('Name is required'); return }
                      saveIntermediaryMutation.mutate()
                    }}
                    disabled={saveIntermediaryMutation.isPending}
                    className="sd-btn primary sm"
                  >
                    <Check className="h-3.5 w-3.5" /> Save
                  </button>
                </div>
              </div>

              <div className="p-5 space-y-5">
                <SectionHeader icon={Handshake} title="Contact" />
                <div className="grid grid-cols-3 gap-3">
                  <Field label="Name *">
                    <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className="sims-input" />
                  </Field>
                  <Field label="Reference #">
                    <input value={form.referenceNumber} onChange={(e) => setForm({ ...form, referenceNumber: e.target.value })} className="sims-input" />
                  </Field>
                  <label className="flex items-center gap-2 pt-6">
                    <input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} className="rounded" />
                    <span className="text-sm text-slate-700">Active</span>
                  </label>
                  <Field label="Email">
                    <input value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} className="sims-input" />
                  </Field>
                  <Field label="Phone">
                    <input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} className="sims-input" />
                  </Field>
                  <Field label="Notes">
                    <input value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} className="sims-input" />
                  </Field>
                </div>

                <SectionHeader icon={Building2} title="Address" />
                <div className="grid grid-cols-4 gap-3">
                  <Field label="Address Line 1">
                    <input value={form.addressLine1} onChange={(e) => setForm({ ...form, addressLine1: e.target.value })} className="sims-input" />
                  </Field>
                  <Field label="Address Line 2">
                    <input value={form.addressLine2} onChange={(e) => setForm({ ...form, addressLine2: e.target.value })} className="sims-input" />
                  </Field>
                  <Field label="City">
                    <input value={form.city} onChange={(e) => setForm({ ...form, city: e.target.value })} className="sims-input" />
                  </Field>
                  <Field label="State">
                    <input value={form.state} onChange={(e) => setForm({ ...form, state: e.target.value.toUpperCase() })} maxLength={2} className="sims-input uppercase" />
                  </Field>
                  <Field label="ZIP">
                    <input value={form.zipCode} onChange={(e) => setForm({ ...form, zipCode: e.target.value })} className="sims-input" />
                  </Field>
                  <Field label="Country">
                    <input value={form.country} onChange={(e) => setForm({ ...form, country: e.target.value.toUpperCase() })} maxLength={3} className="sims-input uppercase" />
                  </Field>
                </div>

                <SectionHeader icon={Banknote} title="Bank Details" />
                <div className="grid grid-cols-3 gap-3">
                  <Field label="Bank Name">
                    <input value={form.bankName} onChange={(e) => setForm({ ...form, bankName: e.target.value })} className="sims-input" />
                  </Field>
                  <Field label="Account Name">
                    <input value={form.bankAccountName} onChange={(e) => setForm({ ...form, bankAccountName: e.target.value })} className="sims-input" />
                  </Field>
                  <Field label="Account Last 4">
                    <input value={form.bankAccountLast4} onChange={(e) => setForm({ ...form, bankAccountLast4: e.target.value.replace(/\D/g, '').slice(0, 4) })} className="sims-input" />
                  </Field>
                  <Field label="Routing Number">
                    <input value={form.bankRoutingNumber} onChange={(e) => setForm({ ...form, bankRoutingNumber: e.target.value })} className="sims-input" />
                  </Field>
                  <Field label="SWIFT Code">
                    <input value={form.bankSwiftCode} onChange={(e) => setForm({ ...form, bankSwiftCode: e.target.value.toUpperCase() })} className="sims-input uppercase" />
                  </Field>
                  <Field label="Instructions">
                    <input value={form.bankInstructions} onChange={(e) => setForm({ ...form, bankInstructions: e.target.value })} className="sims-input" />
                  </Field>
                </div>
              </div>
            </div>
          )}

          {!isCreating && intermediary && (
            <div className="bg-white border border-slate-200 rounded-lg">
              <div className="px-5 py-4 border-b flex items-center justify-between">
                <div>
                  <h2 className="text-base font-semibold text-slate-900">Brokerage Setup</h2>
                  <p className="text-xs text-slate-400">Program, carrier, LOB, effective dates, and direct payable handling.</p>
                </div>
                {!showSetupForm && (
                  <button onClick={() => { setSetupForm(emptySetupForm()); setEditingSetupId(null); setShowSetupForm(true) }} className="sd-btn primary sm">
                    <Plus className="h-3.5 w-3.5" /> Add Setup
                  </button>
                )}
              </div>

              {showSetupForm && (
                <div className="p-4 border-b bg-slate-50 space-y-3">
                  <div className="grid grid-cols-4 gap-3">
                    <Field label="Program *">
                      <select
                        value={setupForm.programConfigurationId}
                        onChange={(e) => setSetupForm({ ...setupForm, programConfigurationId: e.target.value, carrierId: '', lineOfBusiness: '' })}
                        className="sims-input"
                      >
                        <option value="">Select program</option>
                        {programs.map((program) => <option key={program.id} value={program.id}>{program.name}</option>)}
                      </select>
                    </Field>
                    <Field label="Carrier *">
                      <select
                        value={setupForm.carrierId}
                        onChange={(e) => setSetupForm({ ...setupForm, carrierId: e.target.value, lineOfBusiness: '' })}
                        disabled={!setupForm.programConfigurationId}
                        className="sims-input disabled:bg-slate-100"
                      >
                        <option value="">Select carrier</option>
                        {carrierOptions.map((carrier) => <option key={carrier.id} value={carrier.id}>{carrier.name}</option>)}
                      </select>
                    </Field>
                    <Field label="Line of Business">
                      <select
                        value={setupForm.lineOfBusiness}
                        onChange={(e) => setSetupForm({ ...setupForm, lineOfBusiness: e.target.value as SetupForm['lineOfBusiness'] })}
                        disabled={!setupForm.carrierId}
                        className="sims-input disabled:bg-slate-100"
                      >
                        <option value="">All Lines</option>
                        {lobOptions.map((lob) => <option key={lob.id} value={lob.lineOfBusiness}>{lob.lineOfBusinessLabel}</option>)}
                      </select>
                    </Field>
                    <Field label="Brokerage Rate">
                      <input
                        type="number"
                        step="0.000001"
                        min="0"
                        max="1"
                        value={setupForm.brokerageRate}
                        onChange={(e) => setSetupForm({ ...setupForm, brokerageRate: e.target.value })}
                        placeholder="0.075"
                        className="sims-input"
                      />
                    </Field>
                    <Field label="Effective Date *">
                      <input type="date" value={setupForm.effectiveDate} onChange={(e) => setSetupForm({ ...setupForm, effectiveDate: e.target.value, carrierId: '', lineOfBusiness: '' })} className="sims-input" />
                    </Field>
                    <Field label="Expiration Date">
                      <input type="date" value={setupForm.expirationDate} onChange={(e) => setSetupForm({ ...setupForm, expirationDate: e.target.value })} className="sims-input" />
                    </Field>
                    <Field label="Payable Payee">
                      <select
                        value={setupForm.payablePayeeId}
                        onChange={(e) => setSetupForm({ ...setupForm, payablePayeeId: e.target.value })}
                        disabled={!setupForm.createPayable}
                        className="sims-input disabled:bg-slate-100"
                      >
                        <option value="">Select payee</option>
                        {payees.map((payee) => <option key={payee.id} value={payee.id}>{payee.name} ({payee.payeeType})</option>)}
                      </select>
                    </Field>
                    <div className="flex flex-col gap-2 pt-5">
                      <label className="flex items-center gap-2">
                        <input
                          type="checkbox"
                          checked={setupForm.createPayable}
                          onChange={(e) => setSetupForm({ ...setupForm, createPayable: e.target.checked, payablePayeeId: e.target.checked ? setupForm.payablePayeeId : '' })}
                          className="rounded"
                        />
                        <span className="text-sm text-slate-700">Create payable</span>
                      </label>
                      <label className="flex items-center gap-2">
                        <input type="checkbox" checked={setupForm.isActive} onChange={(e) => setSetupForm({ ...setupForm, isActive: e.target.checked })} className="rounded" />
                        <span className="text-sm text-slate-700">Active</span>
                      </label>
                    </div>
                    <div className="col-span-4">
                      <Field label="Notes">
                        <input value={setupForm.notes} onChange={(e) => setSetupForm({ ...setupForm, notes: e.target.value })} className="sims-input" />
                      </Field>
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <button onClick={saveSetup} disabled={saveSetupMutation.isPending} className="sd-btn primary sm">
                      <Check className="h-3.5 w-3.5" /> Save Setup
                    </button>
                    <button onClick={resetSetupForm} className="sd-btn outline sm">
                      <X className="h-3.5 w-3.5" /> Cancel
                    </button>
                  </div>
                </div>
              )}

              {intermediary.brokerageSetups.length === 0 ? (
                <EmptyState
                  icon={Handshake}
                  title="No brokerage setup rows"
                  action={<button onClick={() => setShowSetupForm(true)} className="sd-btn outline sm">Add setup</button>}
                />
              ) : (
                <table className="w-full text-sm">
                  <thead className="bg-slate-50 border-b text-xs text-slate-500 uppercase tracking-wide">
                    <tr>
                      <th className="text-left px-4 py-3 font-medium">Program</th>
                      <th className="text-left px-4 py-3 font-medium">Carrier</th>
                      <th className="text-left px-4 py-3 font-medium">LOB</th>
                      <th className="text-left px-4 py-3 font-medium">Rate</th>
                      <th className="text-left px-4 py-3 font-medium">Dates</th>
                      <th className="text-left px-4 py-3 font-medium">Payee</th>
                      <th className="text-left px-4 py-3 font-medium">Status</th>
                      <th className="px-4 py-3" />
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {intermediary.brokerageSetups.map((setup) => (
                      <tr key={setup.id} className="hover:bg-slate-50">
                        <td className="px-4 py-3 font-medium text-slate-800">{setup.programName}</td>
                        <td className="px-4 py-3 text-slate-600">{setup.carrierName}</td>
                        <td className="px-4 py-3 text-slate-600">{setup.lineOfBusinessLabel}</td>
                        <td className="px-4 py-3 text-slate-700">
                          {setup.brokerageRate == null ? '-' : `${(Number(setup.brokerageRate) * 100).toFixed(3)}%`}
                        </td>
                        <td className="px-4 py-3 text-slate-600">
                          {setup.effectiveDate}{setup.expirationDate ? ` to ${setup.expirationDate}` : ''}
                        </td>
                        <td className="px-4 py-3 text-slate-600">{setup.createPayable ? setup.payablePayeeName ?? 'Payee required' : 'No payable'}</td>
                        <td className="px-4 py-3">
                          <span className={`text-xs px-2 py-0.5 rounded-full ${setup.isActive ? 'bg-green-100 text-green-700' : 'bg-slate-100 text-slate-500'}`}>
                            {setup.isActive ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-right">
                          <button onClick={() => editSetup(setup)} className="sims-icon-btn hover:text-sky-600" title="Edit setup">
                            <Pencil className="h-3.5 w-3.5" />
                          </button>
                          <button
                            onClick={() => { if (confirm('Delete this brokerage setup row?')) deleteSetupMutation.mutate(setup.id) }}
                            className="sims-icon-btn hover:text-red-500"
                            title="Delete setup"
                          >
                            <Trash2 className="h-3.5 w-3.5" />
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
