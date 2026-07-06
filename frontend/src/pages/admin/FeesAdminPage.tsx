import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, ChevronRight, ArrowLeft, RefreshCw, X } from 'lucide-react'
import { toast } from 'sonner'
import { feesApi } from '@/api/fees.api'
import { carriersApi } from '@/api/carriers.api'
import { premiumChargesApi } from '@/api/premiumCharges.api'
import { programConfigurationsApi } from '@/api/programConfigurations.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ErrorState } from '@/components/common/ErrorState'
import { getApiErrorMessage } from '@/lib/apiError'
import { parseDateOnly, todayLocal } from '@/lib/utils'
import type { FeeDefinition, FeeRuleVersion } from '@/types/fee.types'
import type { ProgramConfiguration } from '@/types/programConfiguration.types'
import { ACTIVE_LOBS, LOB_LABELS, type PolicyLineOfBusiness } from '@/types/quote.types'
import {
  ADDITIONAL_INTEREST_CHARGE_METHOD_LABELS,
  ADDITIONAL_INTEREST_COVERAGE_LABELS,
} from '@/types/submissionLob.types'
import type {
  AdditionalInterestChargeMethod,
  AdditionalInterestCoverageType,
  CarrierAdditionalInterestRate,
  CarrierAdditionalInterestRateCreate,
} from '@/types/submissionLob.types'

const US_STATES = ['AL','AK','AZ','AR','CA','CO','CT','DE','FL','GA','HI','ID','IL','IN','IA','KS','KY','LA','ME','MD','MA','MI','MN','MS','MO','MT','NE','NV','NH','NJ','NM','NY','NC','ND','OH','OK','OR','PA','RI','SC','SD','TN','TX','UT','VT','VA','WA','WV','WI','WY','DC']

type View = 'list' | 'versions' | 'edit-version' | 'new-version'
type AdminTab = 'fees' | 'premium-charges'

type VersionForm = Omit<FeeRuleVersion, 'id' | 'feeCode' | 'feeDisplayName' | 'programName'>

const EMPTY: VersionForm = {
  feeDefinitionId: 0,
  programConfigurationId: null,
  carrierId: null, companyId: null, producerId: null, lineOfBusiness: null,
  stateCode: null, city: null, licenseType: null,
  effectiveDate: '', disabledDate: null,
  calcType: 'Flat', flatAmount: null, percentRate: null,
  percentOfNet: false, minimumAmount: null,
  maxPercent: null, maxAmount: null,
  commissionable: false, installmentBehavior: 'PerInstallment',
  splitByParticipation: false, fullyEarned: false, fullyEarnedDays: null,
  excludeTerrorism: false, multiplyByLocations: false, multiplyByVehicles: false,
  applyOnlyOnce: false, mandatoryCharge: false,
  sendToAccounting: true, applyAutomatically: true,
  applyWhenPackagePolicyOnly: false, doNotApplyWhenPackagePolicyOnly: false,
  applyToChildLines: false, onlyAppliesToIssuanceState: false, appliesToFlatCancellations: false,
  premiumMinThreshold: null, premiumMaxThreshold: null, premiumThresholdBasis: null,
  stateCountMin: null, stateCountMax: null,
  roundingMode: 'NearestCent', excludeWhenNotFiling: false, excludeOnEndorsements: false,
  excludeOnRenewal: false, excludeOnOriginalBinder: false, excludeOnMultiCarrierPolicy: false,
  payHomeState: false, excludedPolicyTransactionTypes: null,
  payableRouting: 'NotPayable', payablePayeeId: null, masterPayeeWhenHomeState: false, notes: null,
  premiumBrackets: [], nonTaxableStates: [],
}

type PremiumChargeForm = CarrierAdditionalInterestRateCreate

const emptyPremiumChargeForm = (): PremiumChargeForm => ({
  carrierId: undefined,
  lineOfBusiness: undefined,
  coverageType: 'AdditionalInsured',
  chargeMethod: 'PerInterest',
  perInterestAmount: undefined,
  blanketAmount: undefined,
  minimumCharge: undefined,
  maximumCharge: undefined,
  state: undefined,
  effectiveDate: undefined,
  expirationDate: undefined,
  isActive: true,
})

function SectionHeader({ label }: { label: string }) {
  return <h3 className="text-xs font-semibold uppercase tracking-wider pt-2 pb-1 border-b" style={{ color: 'var(--ink-4)', borderColor: 'var(--line-2)' }}>{label}</h3>
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="text-xs font-medium" style={{ color: 'var(--ink-3)' }}>{label}</span>
      <div className="mt-1">{children}</div>
    </label>
  )
}

const inputCls = 'sd-input w-full'
const selectCls = inputCls

export function FeesAdminPage() {
  const qc = useQueryClient()
  const [activeTab, setActiveTab] = useState<AdminTab>('fees')
  const [view, setView] = useState<View>('list')
  const [selectedDef, setSelectedDef] = useState<FeeDefinition | null>(null)
  const [editingVersion, setEditingVersion] = useState<FeeRuleVersion | null>(null)
  const [form, setForm] = useState<VersionForm>(EMPTY)
  const [newVersionFrom, setNewVersionFrom] = useState<number | undefined>()
  const [showNewDef, setShowNewDef] = useState(false)
  const [showTaxability, setShowTaxability] = useState(false)
  const [defForm, setDefForm] = useState({ code: '', displayName: '', feeCategory: 'PolicyFee', isTaxable: true, calculationOrder: 100, ledgerAccountId: 0 })
  const [nonTaxableEdit, setNonTaxableEdit] = useState<string[]>([])
  const [showPremiumChargeForm, setShowPremiumChargeForm] = useState(false)
  const [editingPremiumChargeId, setEditingPremiumChargeId] = useState<string | null>(null)
  const [premiumChargeForm, setPremiumChargeForm] = useState<PremiumChargeForm>(emptyPremiumChargeForm())

  const { data: definitions = [], isLoading, isError, error, refetch } = useQuery({
    queryKey: ['admin', 'fees', 'definitions'],
    queryFn: () => feesApi.getDefinitions(),
  })

  const { data: carriers = [] } = useQuery({
    queryKey: ['carriers', 'active'],
    queryFn: () => carriersApi.getAll(true),
  })

  const { data: programs = [] } = useQuery({
    queryKey: ['admin', 'program-configurations'],
    queryFn: () => programConfigurationsApi.getAll(true),
  })

  const { data: ledgerAccounts = [] } = useQuery({
    queryKey: ['admin', 'fees', 'ledger-accounts'],
    queryFn: () => feesApi.getLedgerAccounts(),
  })

  const { data: payees = [] } = useQuery({
    queryKey: ['admin', 'fees', 'payees'],
    queryFn: () => feesApi.getPayees(),
  })

  const { data: premiumCharges = [], isLoading: loadingPremiumCharges } = useQuery({
    queryKey: ['admin', 'premium-charges', 'additional-interests'],
    queryFn: () => premiumChargesApi.getAdditionalInterestRates(),
    enabled: activeTab === 'premium-charges',
  })

  const { data: versions = [], isLoading: loadingVersions } = useQuery({
    queryKey: ['admin', 'fees', 'versions', selectedDef?.id],
    queryFn: () => feesApi.getVersions(selectedDef!.id),
    enabled: !!selectedDef && view !== 'list',
  })

  const { data: auditLog = [] } = useQuery({
    queryKey: ['admin', 'fees', 'audit', editingVersion?.id],
    queryFn: () => feesApi.getAuditLog(editingVersion!.id),
    enabled: !!editingVersion && view === 'edit-version',
  })

  const { mutate: createDef, isPending: savingDef } = useMutation({
    mutationFn: () => feesApi.createDefinition(defForm as any),
    onSuccess: () => { toast.success('Fee type created'); qc.invalidateQueries({ queryKey: ['admin', 'fees', 'definitions'] }); setShowNewDef(false) },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Save failed')),
  })

  const { mutate: saveVersion, isPending: savingVersion } = useMutation({
    mutationFn: () => newVersionFrom
      ? feesApi.newVersionFromExisting(newVersionFrom, form as any)
      : feesApi.createVersion(form as any),
    onSuccess: (saved) => {
      toast.success('Version saved')
      qc.invalidateQueries({ queryKey: ['admin', 'fees', 'versions', selectedDef?.id] })
      setEditingVersion(saved); setView('edit-version'); setNewVersionFrom(undefined)
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Save failed')),
  })

  const { mutate: disableVersion } = useMutation({
    mutationFn: () => feesApi.disableVersion(editingVersion!.id, todayLocal()),
    onSuccess: () => { toast.success('Version disabled'); qc.invalidateQueries({ queryKey: ['admin', 'fees', 'versions', selectedDef?.id] }); setView('versions') },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed')),
  })

  const { mutate: saveTaxability } = useMutation({
    mutationFn: () => feesApi.setStateTaxability(selectedDef!.id, nonTaxableEdit),
    onSuccess: () => { toast.success('Saved'); qc.invalidateQueries({ queryKey: ['admin', 'fees', 'versions', selectedDef?.id] }); setShowTaxability(false) },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Save failed')),
  })

  const { mutate: savePremiumCharge, isPending: savingPremiumCharge } = useMutation({
    mutationFn: () => editingPremiumChargeId
      ? premiumChargesApi.updateAdditionalInterestRate(editingPremiumChargeId, premiumChargeForm)
      : premiumChargesApi.createAdditionalInterestRate(premiumChargeForm),
    onSuccess: () => {
      toast.success('Premium charge saved')
      qc.invalidateQueries({ queryKey: ['admin', 'premium-charges', 'additional-interests'] })
      setShowPremiumChargeForm(false)
      setEditingPremiumChargeId(null)
      setPremiumChargeForm(emptyPremiumChargeForm())
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Save failed')),
  })

  const { mutate: deletePremiumCharge } = useMutation({
    mutationFn: (id: string) => premiumChargesApi.deleteAdditionalInterestRate(id),
    onSuccess: () => {
      toast.success('Premium charge removed')
      qc.invalidateQueries({ queryKey: ['admin', 'premium-charges', 'additional-interests'] })
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Delete failed')),
  })

  function openNewVersion(cloneFrom?: FeeRuleVersion) {
    setForm(cloneFrom
      ? { ...cloneFrom, effectiveDate: '', disabledDate: null, feeDefinitionId: selectedDef!.id }
      : { ...EMPTY, feeDefinitionId: selectedDef!.id })
    setNewVersionFrom(cloneFrom?.id)
    setView('new-version')
  }

  function openEditVersion(v: FeeRuleVersion) {
    setEditingVersion(v); setForm({ ...v }); setView('edit-version')
  }

  function set<K extends keyof VersionForm>(key: K, val: VersionForm[K]) {
    setForm(p => ({ ...p, [key]: val }))
  }

  function setEffectiveDate(effectiveDate: string) {
    setForm(p => {
      if (!p.programConfigurationId) return { ...p, effectiveDate }

      const program = programs.find(candidate => candidate.id === p.programConfigurationId) ?? null
      const options = getFeeProgramScopeOptions(program, effectiveDate, p.carrierId, p.lineOfBusiness as PolicyLineOfBusiness | null)
      const carrierValid = !p.carrierId || options.carriers.some(carrier => carrier.id === p.carrierId)
      const lobValid = carrierValid && (!p.lineOfBusiness || options.linesOfBusiness.some(lob => lob.value === p.lineOfBusiness))
      const stateValid = lobValid && (!p.stateCode || options.states.includes(p.stateCode))

      return {
        ...p,
        effectiveDate,
        carrierId: carrierValid ? p.carrierId : null,
        lineOfBusiness: lobValid ? p.lineOfBusiness : null,
        stateCode: stateValid ? p.stateCode : null,
      }
    })
  }

  function setProgramScope(programConfigurationId: string | null) {
    setForm(p => ({
      ...p,
      programConfigurationId,
      carrierId: null,
      lineOfBusiness: null,
      stateCode: null,
    }))
  }

  function setCarrierScope(carrierId: string | null) {
    setForm(p => ({
      ...p,
      carrierId,
      lineOfBusiness: null,
      stateCode: null,
    }))
  }

  function setLobScope(lineOfBusiness: string | null) {
    setForm(p => ({
      ...p,
      lineOfBusiness,
      stateCode: null,
    }))
  }

  function setPayableRouting(value: VersionForm['payableRouting']) {
    setForm(p => ({
      ...p,
      payableRouting: value,
      payablePayeeId: value === 'Entity' ? p.payablePayeeId : null,
    }))
  }

  function setPremium<K extends keyof PremiumChargeForm>(key: K, val: PremiumChargeForm[K]) {
    setPremiumChargeForm(p => ({ ...p, [key]: val }))
  }

  function editPremiumCharge(row: CarrierAdditionalInterestRate) {
    setEditingPremiumChargeId(row.id)
    setPremiumChargeForm({
      carrierId: row.carrierId ?? undefined,
      lineOfBusiness: row.lineOfBusiness ?? undefined,
      coverageType: row.coverageType,
      chargeMethod: row.chargeMethod,
      perInterestAmount: row.perInterestAmount ?? undefined,
      blanketAmount: row.blanketAmount ?? undefined,
      minimumCharge: row.minimumCharge ?? undefined,
      maximumCharge: row.maximumCharge ?? undefined,
      state: row.state ?? undefined,
      effectiveDate: row.effectiveDate ?? undefined,
      expirationDate: row.expirationDate ?? undefined,
      isActive: row.isActive,
    })
    setShowPremiumChargeForm(true)
  }

  const isSuperseded = (v: FeeRuleVersion) => v.disabledDate !== null && parseDateOnly(v.disabledDate) <= new Date()

  if (isLoading) return <LoadingSpinner />
  if (isError) return <ErrorState error={error} onRetry={refetch} />

  const missingVendorPayee = form.payableRouting === 'Entity' && !form.payablePayeeId
  const selectedProgram = programs.find(program => program.id === form.programConfigurationId) ?? null
  const programScopeOptions = getFeeProgramScopeOptions(
    selectedProgram,
    form.effectiveDate,
    form.carrierId,
    form.lineOfBusiness as PolicyLineOfBusiness | null,
  )
  const carrierOptions = selectedProgram ? programScopeOptions.carriers : carriers
  const lobOptions = selectedProgram ? programScopeOptions.linesOfBusiness.map(lob => lob.value) : ACTIVE_LOBS
  const stateOptions = selectedProgram ? programScopeOptions.states : US_STATES
  const programScopeMissingCarrier = !!selectedProgram && (!!form.lineOfBusiness || !!form.stateCode) && !form.carrierId
  const programScopeMissingLob = !!selectedProgram && !!form.stateCode && !form.lineOfBusiness
  const incompleteProgramScope = programScopeMissingCarrier || programScopeMissingLob

  // ── VERSION EDITOR (shared by new + edit) ──────────────────────────────────
  const VersionEditor = (
    <div className="border rounded-lg flex flex-col" style={{ maxHeight: 'calc(100vh - 200px)', background: 'var(--surface)', borderColor: 'var(--line)' }}>
      <div className="px-6 py-4 border-b flex items-center justify-between flex-shrink-0" style={{ borderColor: 'var(--line)' }}>
        <div className="flex items-center gap-3">
          <button onClick={() => setView('versions')} style={{ color: 'var(--ink-4)' }}><ArrowLeft className="h-4 w-4" /></button>
          <div>
            <h2 className="text-base font-semibold" style={{ color: 'var(--ink)' }}>
              {view === 'new-version' ? 'New Version' : `Edit Version`} — {selectedDef?.displayName}
            </h2>
            {view === 'edit-version' && editingVersion && (
              <p className="text-xs" style={{ color: 'var(--ink-4)' }}>Effective {editingVersion.effectiveDate}{editingVersion.disabledDate ? ` → disabled ${editingVersion.disabledDate}` : ''}</p>
            )}
          </div>
        </div>
        <div className="flex gap-2">
          {view === 'edit-version' && editingVersion && !isSuperseded(editingVersion) && (
            <button onClick={() => { if (confirm('Disable this version as of today?')) disableVersion() }}
              className="px-3 py-1.5 text-xs border rounded" style={{ borderColor: 'var(--bad-fg)', color: 'var(--bad-fg)' }}>Disable</button>
          )}
          <button onClick={() => saveVersion()} disabled={savingVersion || missingVendorPayee || incompleteProgramScope}
            className="sd-btn primary px-4 py-1.5 text-xs disabled:opacity-50">
            {savingVersion ? 'Saving…' : 'Save Version'}
          </button>
        </div>
      </div>

      <div className="overflow-y-auto flex-1 p-6 space-y-5">
        {/* Effective date */}
        <SectionHeader label="Effective Date" />
        <div className="grid grid-cols-2 gap-5">
          <Field label="Effective Date *">
            <input type="date" value={form.effectiveDate} onChange={e => setEffectiveDate(e.target.value)} className={inputCls} />
          </Field>
        </div>

        {/* Calculation */}
        <SectionHeader label="Calculation" />
        <div className="grid grid-cols-2 gap-5">
          <Field label="Calc Type">
            <select value={form.calcType} onChange={e => set('calcType', e.target.value as any)} className={selectCls}>
              <option value="Flat">Flat Amount</option>
              <option value="Percent">Percentage</option>
              <option value="Stratified">Stratified (Brackets)</option>
            </select>
          </Field>
          {form.calcType === 'Flat' && (
            <Field label="Flat Amount ($)">
              <input type="number" step="0.01" value={form.flatAmount ?? ''} onChange={e => set('flatAmount', e.target.value ? Number(e.target.value) : null)} className={inputCls} />
            </Field>
          )}
          {form.calcType === 'Percent' && (
            <>
              <Field label="Percent Rate (e.g. 0.0485 = 4.85%)">
                <input type="number" step="0.000001" value={form.percentRate ?? ''} onChange={e => set('percentRate', e.target.value ? Number(e.target.value) : null)} className={inputCls} />
              </Field>
              <label className="flex items-center gap-2 cursor-pointer col-span-2">
                <input type="checkbox" checked={form.percentOfNet} onChange={e => set('percentOfNet', e.target.checked)} className="rounded" />
                <span className="text-sm" style={{ color: 'var(--ink-2)' }}>% of Net Premium (instead of gross)</span>
              </label>
            </>
          )}
          {form.calcType === 'Stratified' && (
            <div className="col-span-2">
              <table className="w-full text-sm border rounded" style={{ borderColor: 'var(--line)' }}>
                <thead style={{ background: 'var(--surface-2)' }}>
                  <tr>
                    <th className="px-3 py-2 text-left text-xs" style={{ color: 'var(--ink-3)' }}>From ($)</th>
                    <th className="px-3 py-2 text-left text-xs" style={{ color: 'var(--ink-3)' }}>To ($, blank = ∞)</th>
                    <th className="px-3 py-2 text-left text-xs" style={{ color: 'var(--ink-3)' }}>Rate (decimal)</th>
                    <th className="px-3 py-2 w-8" />
                  </tr>
                </thead>
                <tbody>
                  {form.premiumBrackets.map((b, i) => (
                    <tr key={i} className="border-t" style={{ borderColor: 'var(--line-2)' }}>
                      <td className="px-2 py-1.5"><input type="number" step="0.01" value={b.tierFrom} onChange={e => { const bs = [...form.premiumBrackets]; bs[i] = { ...b, tierFrom: Number(e.target.value) }; set('premiumBrackets', bs) }} className="sd-input w-full px-2 py-1 text-xs" /></td>
                      <td className="px-2 py-1.5"><input type="number" step="0.01" value={b.tierTo ?? ''} onChange={e => { const bs = [...form.premiumBrackets]; bs[i] = { ...b, tierTo: e.target.value ? Number(e.target.value) : null }; set('premiumBrackets', bs) }} className="sd-input w-full px-2 py-1 text-xs" /></td>
                      <td className="px-2 py-1.5"><input type="number" step="0.000001" value={b.percentRate} onChange={e => { const bs = [...form.premiumBrackets]; bs[i] = { ...b, percentRate: Number(e.target.value) }; set('premiumBrackets', bs) }} className="sd-input w-full px-2 py-1 text-xs" /></td>
                      <td className="px-2 py-1.5"><button onClick={() => set('premiumBrackets', form.premiumBrackets.filter((_, j) => j !== i))}><X className="h-3 w-3" style={{ color: 'var(--bad-fg)' }} /></button></td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <button onClick={() => set('premiumBrackets', [...form.premiumBrackets, { tierFrom: 0, tierTo: null, percentRate: 0 }])} className="mt-2 text-xs hover:underline" style={{ color: 'var(--accent-ink)' }}>+ Add Tier</button>
            </div>
          )}
          <Field label="Minimum Amount ($)">
            <input type="number" step="0.01" value={form.minimumAmount ?? ''} onChange={e => set('minimumAmount', e.target.value ? Number(e.target.value) : null)} className={inputCls} />
          </Field>
          <Field label="Rounding Mode">
            <select value={form.roundingMode} onChange={e => set('roundingMode', e.target.value as any)} className={selectCls}>
              {['NearestCent','RoundUp','RoundDown','NearestDollar','RoundUpDollar','RoundDownDollar'].map(m => <option key={m} value={m}>{m}</option>)}
            </select>
          </Field>
        </div>

        {/* Scope */}
        <SectionHeader label="Scope (blank = applies to all)" />
        <div className="grid grid-cols-3 gap-4">
          <Field label="Program">
            <select value={form.programConfigurationId ?? ''} onChange={e => setProgramScope(e.target.value || null)} className={selectCls}>
              <option value="">All Programs</option>
              {programs.map(program => <option key={program.id} value={program.id}>{program.name}</option>)}
            </select>
          </Field>
          <Field label="Carrier">
            <select value={form.carrierId ?? ''} onChange={e => setCarrierScope(e.target.value || null)} className={selectCls}>
              <option value="">{selectedProgram ? 'Program Carrier Default' : 'All Carriers'}</option>
              {carrierOptions.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
            {programScopeMissingCarrier && <p className="mt-1 text-xs" style={{ color: 'var(--bad-fg)' }}>Select a carrier for this Program scope.</p>}
          </Field>
          <Field label="Line of Business">
            <select
              value={form.lineOfBusiness ?? ''}
              onChange={e => setLobScope(e.target.value || null)}
              disabled={!!selectedProgram && !form.carrierId}
              className={selectCls}
            >
              <option value="">{selectedProgram ? 'All LOBs for Carrier' : 'All LOBs'}</option>
              {lobOptions.map(lob => <option key={lob} value={lob}>{LOB_LABELS[lob as PolicyLineOfBusiness] ?? lob}</option>)}
            </select>
          </Field>
          <Field label="State">
            <select
              value={form.stateCode ?? ''}
              onChange={e => set('stateCode', e.target.value || null)}
              disabled={!!selectedProgram && (!form.carrierId || !form.lineOfBusiness)}
              className={selectCls}
            >
              <option value="">{selectedProgram ? 'All States for LOB' : 'All States'}</option>
              {stateOptions.map(s => <option key={s} value={s}>{s}</option>)}
            </select>
            {programScopeMissingLob && <p className="mt-1 text-xs" style={{ color: 'var(--bad-fg)' }}>Select a line of business before choosing a state.</p>}
          </Field>
          <Field label="License Type">
            <select value={form.licenseType ?? ''} onChange={e => set('licenseType', e.target.value || null)} className={selectCls}>
              <option value="">All</option>
              <option value="Admitted">Admitted</option>
              <option value="Non-Admitted">Non-Admitted</option>
            </select>
          </Field>
        </div>

        {/* Maximums */}
        <SectionHeader label="Maximums" />
        <div className="grid grid-cols-2 gap-5">
          <Field label="Max % (e.g. 0.10 = 10%)">
            <input type="number" step="0.000001" value={form.maxPercent ?? ''} onChange={e => set('maxPercent', e.target.value ? Number(e.target.value) : null)} className={inputCls} />
          </Field>
          <Field label="Max Amount ($)">
            <input type="number" step="0.01" value={form.maxAmount ?? ''} onChange={e => set('maxAmount', e.target.value ? Number(e.target.value) : null)} className={inputCls} />
          </Field>
        </div>

        {/* Auto Apply */}
        <SectionHeader label="Auto Apply" />
        <div className="grid grid-cols-3 gap-4">
          <label className="flex items-center gap-2 cursor-pointer col-span-3">
            <input type="checkbox" checked={form.applyAutomatically} onChange={e => set('applyAutomatically', e.target.checked)} className="rounded" />
            <span className="text-sm" style={{ color: 'var(--ink-2)' }}>Apply Automatically</span>
          </label>
          {([
            ['applyWhenPackagePolicyOnly', 'Apply when Package Policy Only'],
            ['doNotApplyWhenPackagePolicyOnly', 'Do not apply when Package Policy Only'],
            ['applyToChildLines', 'Apply to Child Lines'],
            ['applyOnlyOnce', 'Apply Only Once'],
            ['onlyAppliesToIssuanceState', 'Only applies to state of issuance'],
            ['appliesToFlatCancellations', 'Only applies to flat cancellations'],
            ['mandatoryCharge', 'Mandatory Charge'],
          ] as [keyof VersionForm, string][]).map(([key, label]) => (
            <label key={key} className="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={!!form[key]} onChange={e => set(key, e.target.checked as any)} className="rounded" />
              <span className="text-sm" style={{ color: 'var(--ink-2)' }}>{label}</span>
            </label>
          ))}
          <Field label="Min Premium ($)">
            <input type="number" step="0.01" value={form.premiumMinThreshold ?? ''} onChange={e => set('premiumMinThreshold', e.target.value ? Number(e.target.value) : null)} className={inputCls} />
          </Field>
          <Field label="Max Premium ($)">
            <input type="number" step="0.01" value={form.premiumMaxThreshold ?? ''} onChange={e => set('premiumMaxThreshold', e.target.value ? Number(e.target.value) : null)} className={inputCls} />
          </Field>
          <Field label="Threshold Basis">
            <select value={form.premiumThresholdBasis ?? ''} onChange={e => set('premiumThresholdBasis', e.target.value || null)} className={selectCls}>
              <option value="">—</option>
              <option value="ByLine">By Line</option>
              <option value="ByPolicy">By Policy</option>
            </select>
          </Field>
          <Field label="Min States">
            <input type="number" step="1" value={form.stateCountMin ?? ''} onChange={e => set('stateCountMin', e.target.value ? Number(e.target.value) : null)} className={inputCls} />
          </Field>
          <Field label="Max States">
            <input type="number" step="1" value={form.stateCountMax ?? ''} onChange={e => set('stateCountMax', e.target.value ? Number(e.target.value) : null)} className={inputCls} />
          </Field>
        </div>

        {/* Payable / Exclusions / Flags */}
        <SectionHeader label="Payable Routing" />
        <div className="grid grid-cols-2 gap-5">
          <Field label="Routing">
            <select value={form.payableRouting} onChange={e => setPayableRouting(e.target.value as VersionForm['payableRouting'])} className={selectCls}>
              <option value="NotPayable">Not Payable</option>
              <option value="Company">Payable to Company</option>
              <option value="Entity">Payable to Third Party / Vendor</option>
            </select>
          </Field>
          {form.payableRouting === 'Entity' && (
            <Field label="Third Party / Vendor">
              <select value={form.payablePayeeId ?? ''} onChange={e => set('payablePayeeId', e.target.value ? Number(e.target.value) : null)} className={selectCls}>
                <option value="">Select payee</option>
                {payees.map(payee => (
                  <option key={payee.id} value={payee.id}>
                    {payee.name} ({payee.payeeType})
                  </option>
                ))}
              </select>
              {missingVendorPayee && <p className="mt-1 text-xs" style={{ color: 'var(--bad-fg)' }}>Select the vendor that will receive the monthly payable.</p>}
            </Field>
          )}
          <label className="flex items-center gap-2 cursor-pointer">
            <input type="checkbox" checked={form.masterPayeeWhenHomeState} onChange={e => set('masterPayeeWhenHomeState', e.target.checked)} className="rounded" />
            <span className="text-sm" style={{ color: 'var(--ink-2)' }}>Master payee when home state</span>
          </label>
        </div>

        <SectionHeader label="Exclusions and Behavior Flags" />
        <div className="grid grid-cols-3 gap-3">
          {([
            ['commissionable', 'Commissionable'],
            ['sendToAccounting', 'Send to Accounting'],
            ['excludeTerrorism', 'Exclude Terrorism'],
            ['multiplyByLocations', 'Multiply by Locations'],
            ['multiplyByVehicles', 'Multiply by Vehicles'],
            ['splitByParticipation', 'Split by Participation'],
            ['excludeWhenNotFiling', 'Exclude When Not Filing'],
            ['excludeOnEndorsements', 'Exclude on Endorsements'],
            ['excludeOnRenewal', 'Exclude on Renewal'],
            ['excludeOnOriginalBinder', 'Exclude on Original Binder'],
            ['excludeOnMultiCarrierPolicy', 'Exclude on Multi-Carrier Policy'],
            ['payHomeState', 'Pay Home State'],
          ] as [keyof VersionForm, string][]).map(([key, label]) => (
            <label key={key} className="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={!!form[key]} onChange={e => set(key, e.target.checked as any)} className="rounded" />
              <span className="text-sm" style={{ color: 'var(--ink-2)' }}>{label}</span>
            </label>
          ))}
        </div>

        <div className="grid grid-cols-2 gap-4">
          <Field label="Installment Behavior">
            <select value={form.installmentBehavior} onChange={e => set('installmentBehavior', e.target.value)} className={selectCls}>
              <option value="PerInstallment">Per Installment</option>
              <option value="DownpaymentOnly">Downpayment Only</option>
            </select>
          </Field>
          <Field label="Exclude on Policy Transaction Types">
            <input value={form.excludedPolicyTransactionTypes ?? ''} onChange={e => set('excludedPolicyTransactionTypes', e.target.value || null)} placeholder="Renewal, Cancellation, Audit" className={inputCls} />
          </Field>
        </div>

        <Field label="Notes">
          <textarea value={form.notes ?? ''} onChange={e => set('notes', e.target.value || null)} rows={2} className={inputCls} />
        </Field>

        {/* Audit log (edit mode only) */}
        {view === 'edit-version' && auditLog.length > 0 && (
          <>
            <SectionHeader label="Change History" />
            <div className="space-y-1">
              {auditLog.map(entry => (
                <div key={entry.id} className="flex gap-4 text-xs py-1.5 border-b" style={{ borderColor: 'var(--line-2)' }}>
                  <span className="w-36 flex-shrink-0" style={{ color: 'var(--ink-4)' }}>{new Date(entry.editedAt).toLocaleString()}</span>
                  <span className="font-medium w-24 flex-shrink-0">{entry.changeType}</span>
                  <span style={{ color: 'var(--ink-3)' }}>{entry.notes ?? '—'}</span>
                </div>
              ))}
            </div>
          </>
        )}
      </div>
    </div>
  )

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="Charges & Fees"
        subtitle="Premium charges, taxes, and fee rules with carrier and LOB-specific versions"
        action={
          activeTab === 'fees' ? (
            <button onClick={() => setShowNewDef(true)} className="sd-btn primary flex items-center gap-2 px-4 py-2 rounded-lg text-sm">
              <Plus className="h-4 w-4" /> New Fee Type
            </button>
          ) : (
            <button onClick={() => { setEditingPremiumChargeId(null); setPremiumChargeForm(emptyPremiumChargeForm()); setShowPremiumChargeForm(true) }} className="sd-btn primary flex items-center gap-2 px-4 py-2 rounded-lg text-sm">
              <Plus className="h-4 w-4" /> New Premium Charge
            </button>
          )
        }
      />

      <div className="inline-flex rounded-lg border p-1" style={{ borderColor: 'var(--line)', background: 'var(--surface)' }}>
        {([
          ['fees', 'Taxes & Fees'],
          ['premium-charges', 'Premium Charges'],
        ] as [AdminTab, string][]).map(([key, label]) => (
          <button
            key={key}
            onClick={() => setActiveTab(key)}
            className={`px-3 py-1.5 text-sm rounded-md ${activeTab === key ? 'sd-btn primary' : ''}`}
            style={activeTab !== key ? { color: 'var(--ink-3)' } : undefined}
          >
            {label}
          </button>
        ))}
      </div>

      {activeTab === 'fees' && (
      <div className="flex gap-6">
        {/* Definitions sidebar */}
        <div className="w-64 flex-shrink-0 rounded-lg overflow-hidden self-start border" style={{ background: 'var(--surface)', borderColor: 'var(--line)' }}>
          <div className="px-4 py-3 border-b text-xs font-semibold uppercase tracking-wider" style={{ borderColor: 'var(--line-2)', color: 'var(--ink-3)' }}>Fee Types</div>
          <ul className="divide-y" style={{ '--tw-divide-opacity': 1 } as React.CSSProperties}>
            {definitions.map((def) => (
              <li key={def.id}>
                <button
                  onClick={() => { setSelectedDef(def); setView('versions'); setEditingVersion(null) }}
                  className={`w-full text-left px-4 py-3 flex items-center justify-between ${selectedDef?.id === def.id ? 'border-l-2 border-l-blue-500' : ''}`}
                  style={selectedDef?.id === def.id ? { background: 'var(--surface-2)' } : undefined}
                >
                  <div className="min-w-0">
                    <div className="text-sm font-medium truncate" style={{ color: 'var(--ink)' }}>{def.displayName}</div>
                    <div className="text-xs" style={{ color: 'var(--ink-4)' }}>{def.feeCategory} · order {def.calculationOrder}</div>
                  </div>
                  <ChevronRight className="h-4 w-4 flex-shrink-0" style={{ color: 'var(--ink-4)' }} />
                </button>
              </li>
            ))}
            {definitions.length === 0 && <li className="px-4 py-6 text-sm text-center" style={{ color: 'var(--ink-4)' }}>No fee types yet</li>}
          </ul>
        </div>

        {/* Main panel */}
        <div className="flex-1 min-w-0">
          {view === 'list' && (
            <div className="flex items-center justify-center h-48 text-sm border rounded-lg" style={{ color: 'var(--ink-4)', background: 'var(--surface)', borderColor: 'var(--line)' }}>
              Select a fee type to see its rule versions
            </div>
          )}

          {view === 'versions' && selectedDef && (
            <div className="border rounded-lg" style={{ background: 'var(--surface)', borderColor: 'var(--line)' }}>
              <div className="px-6 py-4 border-b flex items-center justify-between" style={{ borderColor: 'var(--line)' }}>
                <div>
                  <h2 className="text-base font-semibold" style={{ color: 'var(--ink)' }}>{selectedDef.displayName}</h2>
                  <p className="text-xs mt-0.5" style={{ color: 'var(--ink-4)' }}>{selectedDef.feeCategory} · {selectedDef.isTaxable ? 'Taxable' : 'Non-taxable'} · calc order {selectedDef.calculationOrder}</p>
                </div>
                <div className="flex gap-2">
                  <button onClick={() => { setNonTaxableEdit(versions[0]?.nonTaxableStates ?? []); setShowTaxability(true) }}
                    disabled={loadingVersions || versions.length === 0}
                    title={loadingVersions ? 'Loading versions…' : versions.length === 0 ? 'Add a rule version before editing taxability' : undefined}
                    className="sd-btn outline px-3 py-1.5 text-xs rounded disabled:opacity-40 disabled:cursor-not-allowed">Taxable States</button>
                  <button onClick={() => openNewVersion()}
                    className="sd-btn primary flex items-center gap-1 px-3 py-1.5 text-xs rounded">
                    <Plus className="h-3 w-3" /> New Version
                  </button>
                </div>
              </div>
              {loadingVersions ? <div className="p-8 flex justify-center"><LoadingSpinner /></div> : (
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b text-xs uppercase tracking-wider" style={{ borderColor: 'var(--line-2)', color: 'var(--ink-3)' }}>
                      <th className="px-6 py-3 text-left">Effective</th>
                      <th className="px-6 py-3 text-left">Disabled</th>
                      <th className="px-6 py-3 text-left">Scope</th>
                      <th className="px-6 py-3 text-left">Calc</th>
                      <th className="px-6 py-3 text-left">Rate / Amount</th>
                      <th className="px-6 py-3" />
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-50">
                    {versions.map((v) => {
                      const sup = isSuperseded(v)
                      return (
                        <tr key={v.id} onClick={() => openEditVersion(v)} className={`cursor-pointer hover:bg-gray-50 ${sup ? 'opacity-40' : ''}`}>
                          <td className={`px-6 py-3 ${sup ? 'line-through' : 'font-medium'}`} style={!sup ? { color: 'var(--ink)' } : undefined}>{v.effectiveDate}</td>
                          <td className="px-6 py-3" style={{ color: 'var(--ink-3)' }}>{v.disabledDate ?? <span style={{ color: 'var(--good-fg)' }}>Active</span>}</td>
                          <td className="px-6 py-3" style={{ color: 'var(--ink-3)' }}>{[
                            v.programName ?? (v.programConfigurationId ? programs.find(program => program.id === v.programConfigurationId)?.name ?? 'Program' : null),
                            v.carrierId ? carriers.find(c => c.id === v.carrierId)?.name ?? 'Carrier' : null,
                            v.stateCode,
                            v.lineOfBusiness ? LOB_LABELS[v.lineOfBusiness as PolicyLineOfBusiness] ?? v.lineOfBusiness : null,
                            v.licenseType,
                          ].filter(Boolean).join(' · ') || 'All'}</td>
                          <td className="px-6 py-3" style={{ color: 'var(--ink-2)' }}>{v.calcType}</td>
                          <td className="px-6 py-3" style={{ color: 'var(--ink-2)' }}>
                            {v.calcType === 'Flat' && v.flatAmount != null && `$${Number(v.flatAmount).toFixed(2)}`}
                            {v.calcType === 'Percent' && v.percentRate != null && `${(Number(v.percentRate) * 100).toFixed(4)}%`}
                            {v.calcType === 'Stratified' && `${v.premiumBrackets.length} tiers`}
                          </td>
                          <td className="px-6 py-3 text-right" onClick={e => e.stopPropagation()}>
                            {!sup && <button onClick={() => openNewVersion(v)} className="text-xs hover:underline" style={{ color: 'var(--accent-ink)' }}><RefreshCw className="h-3 w-3 inline mr-1" />New Version</button>}
                          </td>
                        </tr>
                      )
                    })}
                    {versions.length === 0 && (
                      <tr><td colSpan={6} className="px-6 py-10 text-center" style={{ color: 'var(--ink-4)' }}>No versions yet. Click "New Version" to add one.</td></tr>
                    )}
                  </tbody>
                </table>
              )}
            </div>
          )}

          {(view === 'edit-version' || view === 'new-version') && VersionEditor}
        </div>
      </div>
      )}

      {activeTab === 'premium-charges' && (
        <div className="border rounded-lg" style={{ background: 'var(--surface)', borderColor: 'var(--line)' }}>
          <div className="px-6 py-4 border-b" style={{ borderColor: 'var(--line)' }}>
            <h2 className="text-base font-semibold" style={{ color: 'var(--ink)' }}>Additional Interest Premium Charges</h2>
            <p className="text-xs mt-0.5" style={{ color: 'var(--ink-4)' }}>Blank carrier or LOB means the rule applies to all.</p>
          </div>

          {showPremiumChargeForm && (
            <div className="p-4 border-b space-y-3" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
              <div className="grid grid-cols-4 gap-3">
                <Field label="Carrier">
                  <select value={premiumChargeForm.carrierId ?? ''} onChange={e => setPremium('carrierId', e.target.value || undefined)} className={selectCls}>
                    <option value="">All Carriers</option>
                    {carriers.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                  </select>
                </Field>
                <Field label="Line of Business">
                  <select value={premiumChargeForm.lineOfBusiness ?? ''} onChange={e => setPremium('lineOfBusiness', e.target.value || undefined)} className={selectCls}>
                    <option value="">All LOBs</option>
                    {ACTIVE_LOBS.map(lob => <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>)}
                  </select>
                </Field>
                <Field label="Interest Type">
                  <select value={premiumChargeForm.coverageType} onChange={e => setPremium('coverageType', e.target.value as AdditionalInterestCoverageType)} className={selectCls}>
                    {(Object.keys(ADDITIONAL_INTEREST_COVERAGE_LABELS) as AdditionalInterestCoverageType[]).map(k => <option key={k} value={k}>{ADDITIONAL_INTEREST_COVERAGE_LABELS[k]}</option>)}
                  </select>
                </Field>
                <Field label="Charge Method">
                  <select value={premiumChargeForm.chargeMethod} onChange={e => setPremium('chargeMethod', e.target.value as AdditionalInterestChargeMethod)} className={selectCls}>
                    {(Object.keys(ADDITIONAL_INTEREST_CHARGE_METHOD_LABELS) as AdditionalInterestChargeMethod[]).map(k => <option key={k} value={k}>{ADDITIONAL_INTEREST_CHARGE_METHOD_LABELS[k]}</option>)}
                  </select>
                </Field>
                <Field label="Per Interest Amount">
                  <input type="number" step="0.01" value={premiumChargeForm.perInterestAmount ?? ''} onChange={e => setPremium('perInterestAmount', e.target.value ? Number(e.target.value) : undefined)} className={inputCls} />
                </Field>
                <Field label="Blanket Amount">
                  <input type="number" step="0.01" value={premiumChargeForm.blanketAmount ?? ''} onChange={e => setPremium('blanketAmount', e.target.value ? Number(e.target.value) : undefined)} className={inputCls} />
                </Field>
                <Field label="Minimum">
                  <input type="number" step="0.01" value={premiumChargeForm.minimumCharge ?? ''} onChange={e => setPremium('minimumCharge', e.target.value ? Number(e.target.value) : undefined)} className={inputCls} />
                </Field>
                <Field label="Maximum">
                  <input type="number" step="0.01" value={premiumChargeForm.maximumCharge ?? ''} onChange={e => setPremium('maximumCharge', e.target.value ? Number(e.target.value) : undefined)} className={inputCls} />
                </Field>
                <Field label="State">
                  <select value={premiumChargeForm.state ?? ''} onChange={e => setPremium('state', e.target.value || undefined)} className={selectCls}>
                    <option value="">All States</option>
                    {US_STATES.map(s => <option key={s} value={s}>{s}</option>)}
                  </select>
                </Field>
                <Field label="Effective Date">
                  <input type="date" value={premiumChargeForm.effectiveDate ?? ''} onChange={e => setPremium('effectiveDate', e.target.value || undefined)} className={inputCls} />
                </Field>
                <Field label="Expiration Date">
                  <input type="date" value={premiumChargeForm.expirationDate ?? ''} onChange={e => setPremium('expirationDate', e.target.value || undefined)} className={inputCls} />
                </Field>
                <label className="flex items-center gap-2 cursor-pointer pt-6">
                  <input type="checkbox" checked={premiumChargeForm.isActive} onChange={e => setPremium('isActive', e.target.checked)} className="rounded" />
                  <span className="text-sm" style={{ color: 'var(--ink-2)' }}>Active</span>
                </label>
              </div>
              <div className="flex gap-2">
                <button onClick={() => savePremiumCharge()} disabled={savingPremiumCharge} className="sd-btn primary px-4 py-1.5 text-xs disabled:opacity-50">
                  {savingPremiumCharge ? 'Saving...' : 'Save Premium Charge'}
                </button>
                <button onClick={() => { setShowPremiumChargeForm(false); setEditingPremiumChargeId(null); setPremiumChargeForm(emptyPremiumChargeForm()) }} className="sd-btn outline px-4 py-1.5 text-xs">Cancel</button>
              </div>
            </div>
          )}

          {loadingPremiumCharges ? <div className="p-8 flex justify-center"><LoadingSpinner /></div> : (
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-xs uppercase tracking-wider" style={{ borderColor: 'var(--line-2)', color: 'var(--ink-3)' }}>
                  <th className="px-6 py-3 text-left">Carrier</th>
                  <th className="px-6 py-3 text-left">LOB</th>
                  <th className="px-6 py-3 text-left">Interest</th>
                  <th className="px-6 py-3 text-left">Method</th>
                  <th className="px-6 py-3 text-left">Amount</th>
                  <th className="px-6 py-3 text-left">State</th>
                  <th className="px-6 py-3 text-left">Status</th>
                  <th className="px-6 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {premiumCharges.map(row => {
                  const carrierName = row.carrierId ? carriers.find(c => c.id === row.carrierId)?.name ?? 'Carrier' : 'All Carriers'
                  const amount = row.chargeMethod === 'PerInterest'
                    ? row.perInterestAmount != null ? `$${Number(row.perInterestAmount).toFixed(2)} each` : '-'
                    : row.chargeMethod === 'BlanketFlat'
                      ? row.blanketAmount != null ? `$${Number(row.blanketAmount).toFixed(2)} blanket` : '-'
                      : '-'
                  return (
                    <tr key={row.id} className="hover:bg-gray-50">
                      <td className="px-6 py-3 font-medium" style={{ color: 'var(--ink)' }}>{carrierName}</td>
                      <td className="px-6 py-3" style={{ color: 'var(--ink-3)' }}>{row.lineOfBusiness ? LOB_LABELS[row.lineOfBusiness as PolicyLineOfBusiness] ?? row.lineOfBusiness : 'All LOBs'}</td>
                      <td className="px-6 py-3" style={{ color: 'var(--ink-2)' }}>{ADDITIONAL_INTEREST_COVERAGE_LABELS[row.coverageType]}</td>
                      <td className="px-6 py-3" style={{ color: 'var(--ink-3)' }}>{ADDITIONAL_INTEREST_CHARGE_METHOD_LABELS[row.chargeMethod]}</td>
                      <td className="px-6 py-3" style={{ color: 'var(--ink-2)' }}>{amount}</td>
                      <td className="px-6 py-3" style={{ color: 'var(--ink-3)' }}>{row.state ?? 'All'}</td>
                      <td className="px-6 py-3">{row.isActive ? <span style={{ color: 'var(--good-fg)' }}>Active</span> : <span style={{ color: 'var(--ink-4)' }}>Inactive</span>}</td>
                      <td className="px-6 py-3 text-right">
                        <button onClick={() => editPremiumCharge(row)} className="text-xs hover:underline mr-3" style={{ color: 'var(--accent-ink)' }}>Edit</button>
                        <button onClick={() => { if (confirm('Delete this premium charge?')) deletePremiumCharge(row.id) }} className="text-xs hover:underline" style={{ color: 'var(--bad-fg)' }}>Delete</button>
                      </td>
                    </tr>
                  )
                })}
                {premiumCharges.length === 0 && (
                  <tr><td colSpan={8} className="px-6 py-10 text-center" style={{ color: 'var(--ink-4)' }}>No premium charges yet. Click "New Premium Charge" to add one.</td></tr>
                )}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* New Fee Type Modal */}
      {showNewDef && (
        <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4">
          <div className="rounded-xl shadow-xl w-full max-w-md" style={{ background: 'var(--surface)' }}>
            <div className="px-6 py-4 border-b flex items-center justify-between" style={{ borderColor: 'var(--line)' }}>
              <h3 className="font-semibold" style={{ color: 'var(--ink)' }}>New Fee Type</h3>
              <button onClick={() => setShowNewDef(false)}><X className="h-4 w-4" style={{ color: 'var(--ink-4)' }} /></button>
            </div>
            <div className="p-6 space-y-4">
              <Field label="Code (e.g. TX_SL_TAX) *">
                <input type="text" value={defForm.code} onChange={e => setDefForm(p => ({ ...p, code: e.target.value.toUpperCase() }))} className={inputCls} />
              </Field>
              <Field label="Display Name *">
                <input type="text" value={defForm.displayName} onChange={e => setDefForm(p => ({ ...p, displayName: e.target.value }))} className={inputCls} />
              </Field>
              <Field label="Category">
                <select value={defForm.feeCategory} onChange={e => setDefForm(p => ({ ...p, feeCategory: e.target.value }))} className={selectCls}>
                  {['Tax','StampingFee','PolicyFee','BrokerFee','Inspection','Other'].map(c => <option key={c} value={c}>{c}</option>)}
                </select>
              </Field>
              <div className="grid grid-cols-2 gap-4">
                <Field label="Calc Order">
                  <input type="number" value={defForm.calculationOrder} onChange={e => setDefForm(p => ({ ...p, calculationOrder: Number(e.target.value) }))} className={inputCls} />
                </Field>
                <Field label="Ledger Account">
                  <select value={defForm.ledgerAccountId || ''} onChange={e => setDefForm(p => ({ ...p, ledgerAccountId: Number(e.target.value) }))} className={selectCls}>
                    <option value="">Select ledger account</option>
                    {ledgerAccounts.map(account => (
                      <option key={account.id} value={account.id}>
                        {account.internalCode} - {account.externalLabel} ({account.accountType})
                      </option>
                    ))}
                  </select>
                </Field>
              </div>
              <label className="flex items-center gap-2 cursor-pointer">
                <input type="checkbox" checked={defForm.isTaxable} onChange={e => setDefForm(p => ({ ...p, isTaxable: e.target.checked }))} className="rounded" />
                <span className="text-sm" style={{ color: 'var(--ink-2)' }}>Taxable (can be taxed by SL taxes)</span>
              </label>
            </div>
            <div className="px-6 py-4 border-t flex justify-end gap-2" style={{ borderColor: 'var(--line)' }}>
              <button onClick={() => setShowNewDef(false)} className="sd-btn outline px-4 py-2 text-sm">Cancel</button>
              <button onClick={() => createDef()} disabled={savingDef || !defForm.code || !defForm.displayName || !defForm.ledgerAccountId}
                className="sd-btn primary px-4 py-2 text-sm disabled:opacity-50">
                {savingDef ? 'Creating…' : 'Create'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* State Taxability Modal */}
      {showTaxability && selectedDef && (
        <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4">
          <div className="rounded-xl shadow-xl w-full max-w-lg" style={{ maxHeight: '80vh', display: 'flex', flexDirection: 'column', background: 'var(--surface)' }}>
            <div className="px-6 py-4 border-b flex items-center justify-between flex-shrink-0" style={{ borderColor: 'var(--line)' }}>
              <div>
                <h3 className="font-semibold" style={{ color: 'var(--ink)' }}>Taxable States — {selectedDef.displayName}</h3>
                <p className="text-xs mt-0.5" style={{ color: 'var(--ink-4)' }}>Uncheck states where this fee is NOT taxable by SL tax</p>
              </div>
              <button onClick={() => setShowTaxability(false)}><X className="h-4 w-4" style={{ color: 'var(--ink-4)' }} /></button>
            </div>
            <div className="p-6 overflow-y-auto flex-1">
              <div className="grid grid-cols-5 gap-2">
                {US_STATES.map(st => (
                  <label key={st} className="flex items-center gap-1.5 cursor-pointer">
                    <input type="checkbox" checked={!nonTaxableEdit.includes(st)}
                      onChange={e => setNonTaxableEdit(prev => e.target.checked ? prev.filter(s => s !== st) : [...prev, st])}
                      className="rounded" />
                    <span className="text-sm" style={{ color: 'var(--ink-2)' }}>{st}</span>
                  </label>
                ))}
              </div>
            </div>
            <div className="px-6 py-4 border-t flex items-center justify-between gap-2 flex-shrink-0" style={{ borderColor: 'var(--line)' }}>
              <span className="text-xs" style={{ color: 'var(--ink-3)' }}>
                {nonTaxableEdit.length === 0
                  ? 'All states taxable'
                  : `${nonTaxableEdit.length} state${nonTaxableEdit.length === 1 ? '' : 's'} non-taxable: ${[...nonTaxableEdit].sort().join(', ')}`}
              </span>
              <div className="flex gap-2">
                <button onClick={() => setShowTaxability(false)} className="sd-btn outline px-4 py-2 text-sm">Cancel</button>
                <button
                  onClick={() => {
                    if (nonTaxableEdit.length === 0 &&
                        !confirm('This will mark EVERY state taxable for this fee (no non-taxable states). Continue?')) return
                    saveTaxability()
                  }}
                  className="sd-btn primary px-4 py-2 text-sm">Save</button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

function getFeeProgramScopeOptions(
  program: ProgramConfiguration | null,
  effectiveDate: string,
  carrierId: string | null,
  lineOfBusiness: PolicyLineOfBusiness | null,
) {
  const activeCarriers = (program?.carriers ?? []).filter((carrier) => isActiveOn(carrier, effectiveDate))
  const matchingCarriers = carrierId
    ? activeCarriers.filter((carrier) => carrier.carrierId === carrierId)
    : activeCarriers
  const activeLobs = matchingCarriers
    .flatMap((carrier) => carrier.linesOfBusiness)
    .filter((lob) => isActiveOn(lob, effectiveDate))
  const matchingLobs = lineOfBusiness
    ? activeLobs.filter((lob) => lob.lineOfBusiness === lineOfBusiness)
    : activeLobs

  return {
    carriers: activeCarriers.map((carrier) => ({ id: carrier.carrierId, name: carrier.carrierName })),
    linesOfBusiness: uniqueBy(
      activeLobs.map((lob) => ({ value: lob.lineOfBusiness, label: lob.lineOfBusinessLabel })),
      (lob) => lob.value,
    ),
    states: [...new Set(
      matchingLobs
        .flatMap((lob) => lob.states)
        .filter((state) => isActiveOn(state, effectiveDate))
        .map((state) => state.stateCode),
    )].sort(),
  }
}

function isActiveOn(item: { isActive: boolean; effectiveDate: string; expirationDate: string | null }, effectiveDate: string) {
  if (!item.isActive) return false
  if (!effectiveDate) return true
  return item.effectiveDate <= effectiveDate && (!item.expirationDate || item.expirationDate >= effectiveDate)
}

function uniqueBy<T>(items: T[], getKey: (item: T) => string) {
  const seen = new Set<string>()
  return items.filter((item) => {
    const key = getKey(item)
    if (seen.has(key)) return false
    seen.add(key)
    return true
  })
}
