import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, ChevronRight, ArrowLeft, RefreshCw, X } from 'lucide-react'
import { toast } from 'sonner'
import { feesApi } from '@/api/fees.api'
import { carriersApi } from '@/api/carriers.api'
import { premiumChargesApi } from '@/api/premiumCharges.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { FeeDefinition, FeeRuleVersion } from '@/types/fee.types'
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

type VersionForm = Omit<FeeRuleVersion, 'id' | 'feeCode' | 'feeDisplayName'>

const EMPTY: VersionForm = {
  feeDefinitionId: 0,
  carrierId: null, companyId: null, producerId: null, lineOfBusiness: null,
  stateCode: null, city: null, licenseType: null,
  effectiveDate: '', disabledDate: null,
  calcType: 'Flat', flatAmount: null, percentRate: null,
  percentOfNet: false, minimumAmount: null,
  maxPercent: null, maxAmount: null,
  commissionable: false, installmentBehavior: 'PerInstallment',
  splitByParticipation: false, fullyEarned: false, fullyEarnedDays: null,
  excludeTerrorism: false, multiplyByLocations: false, multiplyByVehicles: false,
  sendToAccounting: true, applyAutomatically: true,
  premiumMinThreshold: null, premiumMaxThreshold: null, premiumThresholdBasis: null,
  roundingMode: 'NearestCent', excludeWhenNotFiling: false, excludeOnEndorsements: false,
  payableRouting: 'NotPayable', payablePayeeId: null, notes: null,
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
  return <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider pt-2 pb-1 border-b border-gray-100">{label}</h3>
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="text-xs font-medium text-gray-600">{label}</span>
      <div className="mt-1">{children}</div>
    </label>
  )
}

const inputCls = 'w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-blue-400'
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

  const { data: definitions = [], isLoading } = useQuery({
    queryKey: ['admin', 'fees', 'definitions'],
    queryFn: () => feesApi.getDefinitions(),
  })

  const { data: carriers = [] } = useQuery({
    queryKey: ['carriers', 'active'],
    queryFn: () => carriersApi.getAll(true),
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
    onError: () => toast.error('Save failed'),
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
    onError: () => toast.error('Save failed'),
  })

  const { mutate: disableVersion } = useMutation({
    mutationFn: () => feesApi.disableVersion(editingVersion!.id, new Date().toISOString().slice(0, 10)),
    onSuccess: () => { toast.success('Version disabled'); qc.invalidateQueries({ queryKey: ['admin', 'fees', 'versions', selectedDef?.id] }); setView('versions') },
    onError: () => toast.error('Failed'),
  })

  const { mutate: saveTaxability } = useMutation({
    mutationFn: () => feesApi.setStateTaxability(selectedDef!.id, nonTaxableEdit),
    onSuccess: () => { toast.success('Saved'); qc.invalidateQueries({ queryKey: ['admin', 'fees', 'versions', selectedDef?.id] }); setShowTaxability(false) },
    onError: () => toast.error('Save failed'),
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
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Save failed'),
  })

  const { mutate: deletePremiumCharge } = useMutation({
    mutationFn: (id: string) => premiumChargesApi.deleteAdditionalInterestRate(id),
    onSuccess: () => {
      toast.success('Premium charge removed')
      qc.invalidateQueries({ queryKey: ['admin', 'premium-charges', 'additional-interests'] })
    },
    onError: () => toast.error('Delete failed'),
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

  const isSuperseded = (v: FeeRuleVersion) => v.disabledDate !== null && new Date(v.disabledDate) <= new Date()

  if (isLoading) return <LoadingSpinner />

  // ── VERSION EDITOR (shared by new + edit) ──────────────────────────────────
  const VersionEditor = (
    <div className="bg-white border border-gray-200 rounded-lg flex flex-col" style={{ maxHeight: 'calc(100vh - 200px)' }}>
      <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between flex-shrink-0">
        <div className="flex items-center gap-3">
          <button onClick={() => setView('versions')} className="text-gray-400 hover:text-gray-600"><ArrowLeft className="h-4 w-4" /></button>
          <div>
            <h2 className="text-base font-semibold text-gray-900">
              {view === 'new-version' ? 'New Version' : `Edit Version`} — {selectedDef?.displayName}
            </h2>
            {view === 'edit-version' && editingVersion && (
              <p className="text-xs text-gray-400">Effective {editingVersion.effectiveDate}{editingVersion.disabledDate ? ` → disabled ${editingVersion.disabledDate}` : ''}</p>
            )}
          </div>
        </div>
        <div className="flex gap-2">
          {view === 'edit-version' && editingVersion && !isSuperseded(editingVersion) && (
            <button onClick={() => { if (confirm('Disable this version as of today?')) disableVersion() }}
              className="px-3 py-1.5 text-xs border border-red-300 text-red-600 rounded hover:bg-red-50">Disable</button>
          )}
          <button onClick={() => saveVersion()} disabled={savingVersion}
            className="px-4 py-1.5 text-xs bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50">
            {savingVersion ? 'Saving…' : 'Save Version'}
          </button>
        </div>
      </div>

      <div className="overflow-y-auto flex-1 p-6 space-y-5">
        {/* Effective date */}
        <SectionHeader label="Effective Date" />
        <div className="grid grid-cols-2 gap-5">
          <Field label="Effective Date *">
            <input type="date" value={form.effectiveDate} onChange={e => set('effectiveDate', e.target.value)} className={inputCls} />
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
                <span className="text-sm text-gray-700">% of Net Premium (instead of gross)</span>
              </label>
            </>
          )}
          {form.calcType === 'Stratified' && (
            <div className="col-span-2">
              <table className="w-full text-sm border border-gray-200 rounded">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-3 py-2 text-left text-xs text-gray-500">From ($)</th>
                    <th className="px-3 py-2 text-left text-xs text-gray-500">To ($, blank = ∞)</th>
                    <th className="px-3 py-2 text-left text-xs text-gray-500">Rate (decimal)</th>
                    <th className="px-3 py-2 w-8" />
                  </tr>
                </thead>
                <tbody>
                  {form.premiumBrackets.map((b, i) => (
                    <tr key={i} className="border-t border-gray-100">
                      <td className="px-2 py-1.5"><input type="number" step="0.01" value={b.tierFrom} onChange={e => { const bs = [...form.premiumBrackets]; bs[i] = { ...b, tierFrom: Number(e.target.value) }; set('premiumBrackets', bs) }} className="w-full border border-gray-200 rounded px-2 py-1 text-xs" /></td>
                      <td className="px-2 py-1.5"><input type="number" step="0.01" value={b.tierTo ?? ''} onChange={e => { const bs = [...form.premiumBrackets]; bs[i] = { ...b, tierTo: e.target.value ? Number(e.target.value) : null }; set('premiumBrackets', bs) }} className="w-full border border-gray-200 rounded px-2 py-1 text-xs" /></td>
                      <td className="px-2 py-1.5"><input type="number" step="0.000001" value={b.percentRate} onChange={e => { const bs = [...form.premiumBrackets]; bs[i] = { ...b, percentRate: Number(e.target.value) }; set('premiumBrackets', bs) }} className="w-full border border-gray-200 rounded px-2 py-1 text-xs" /></td>
                      <td className="px-2 py-1.5"><button onClick={() => set('premiumBrackets', form.premiumBrackets.filter((_, j) => j !== i))}><X className="h-3 w-3 text-red-400" /></button></td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <button onClick={() => set('premiumBrackets', [...form.premiumBrackets, { tierFrom: 0, tierTo: null, percentRate: 0 }])} className="mt-2 text-xs text-blue-600 hover:underline">+ Add Tier</button>
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
          <Field label="Carrier">
            <select value={form.carrierId ?? ''} onChange={e => set('carrierId', e.target.value || null)} className={selectCls}>
              <option value="">All Carriers</option>
              {carriers.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
          </Field>
          <Field label="State">
            <select value={form.stateCode ?? ''} onChange={e => set('stateCode', e.target.value || null)} className={selectCls}>
              <option value="">All States</option>
              {US_STATES.map(s => <option key={s} value={s}>{s}</option>)}
            </select>
          </Field>
          <Field label="License Type">
            <select value={form.licenseType ?? ''} onChange={e => set('licenseType', e.target.value || null)} className={selectCls}>
              <option value="">All</option>
              <option value="Admitted">Admitted</option>
              <option value="Non-Admitted">Non-Admitted</option>
            </select>
          </Field>
          <Field label="Line of Business">
            <select value={form.lineOfBusiness ?? ''} onChange={e => set('lineOfBusiness', e.target.value || null)} className={selectCls}>
              <option value="">All LOBs</option>
              {ACTIVE_LOBS.map(lob => <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>)}
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
            <span className="text-sm text-gray-700">Apply Automatically</span>
          </label>
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
        </div>

        {/* Payable / Exclusions / Flags */}
        <SectionHeader label="Payable Routing" />
        <div className="grid grid-cols-2 gap-5">
          <Field label="Routing">
            <select value={form.payableRouting} onChange={e => set('payableRouting', e.target.value as any)} className={selectCls}>
              <option value="NotPayable">Not Payable</option>
              <option value="Company">Payable to Company</option>
              <option value="Entity">Payable to Entity</option>
            </select>
          </Field>
          {form.payableRouting === 'Entity' && (
            <Field label="Payee ID">
              <input type="number" value={form.payablePayeeId ?? ''} onChange={e => set('payablePayeeId', e.target.value ? Number(e.target.value) : null)} className={inputCls} />
            </Field>
          )}
        </div>

        <SectionHeader label="Behavior Flags" />
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
          ] as [keyof VersionForm, string][]).map(([key, label]) => (
            <label key={key} className="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={!!form[key]} onChange={e => set(key, e.target.checked as any)} className="rounded" />
              <span className="text-sm text-gray-700">{label}</span>
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
                <div key={entry.id} className="flex gap-4 text-xs py-1.5 border-b border-gray-50">
                  <span className="text-gray-400 w-36 flex-shrink-0">{new Date(entry.editedAt).toLocaleString()}</span>
                  <span className="font-medium w-24 flex-shrink-0">{entry.changeType}</span>
                  <span className="text-gray-500">{entry.notes ?? '—'}</span>
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
            <button onClick={() => setShowNewDef(true)} className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg text-sm hover:bg-blue-700">
              <Plus className="h-4 w-4" /> New Fee Type
            </button>
          ) : (
            <button onClick={() => { setEditingPremiumChargeId(null); setPremiumChargeForm(emptyPremiumChargeForm()); setShowPremiumChargeForm(true) }} className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg text-sm hover:bg-blue-700">
              <Plus className="h-4 w-4" /> New Premium Charge
            </button>
          )
        }
      />

      <div className="inline-flex rounded-lg border border-gray-200 bg-white p-1">
        {([
          ['fees', 'Taxes & Fees'],
          ['premium-charges', 'Premium Charges'],
        ] as [AdminTab, string][]).map(([key, label]) => (
          <button
            key={key}
            onClick={() => setActiveTab(key)}
            className={`px-3 py-1.5 text-sm rounded-md ${activeTab === key ? 'bg-blue-600 text-white' : 'text-gray-600 hover:bg-gray-50'}`}
          >
            {label}
          </button>
        ))}
      </div>

      {activeTab === 'fees' && (
      <div className="flex gap-6">
        {/* Definitions sidebar */}
        <div className="w-64 flex-shrink-0 bg-white border border-gray-200 rounded-lg overflow-hidden self-start">
          <div className="px-4 py-3 border-b border-gray-100 text-xs font-semibold text-gray-500 uppercase tracking-wider">Fee Types</div>
          <ul className="divide-y divide-gray-100">
            {definitions.map((def) => (
              <li key={def.id}>
                <button
                  onClick={() => { setSelectedDef(def); setView('versions'); setEditingVersion(null) }}
                  className={`w-full text-left px-4 py-3 flex items-center justify-between hover:bg-gray-50 ${selectedDef?.id === def.id ? 'bg-blue-50 border-l-2 border-l-blue-500' : ''}`}
                >
                  <div className="min-w-0">
                    <div className="text-sm font-medium text-gray-900 truncate">{def.displayName}</div>
                    <div className="text-xs text-gray-400">{def.feeCategory} · order {def.calculationOrder}</div>
                  </div>
                  <ChevronRight className="h-4 w-4 text-gray-400 flex-shrink-0" />
                </button>
              </li>
            ))}
            {definitions.length === 0 && <li className="px-4 py-6 text-sm text-gray-400 text-center">No fee types yet</li>}
          </ul>
        </div>

        {/* Main panel */}
        <div className="flex-1 min-w-0">
          {view === 'list' && (
            <div className="flex items-center justify-center h-48 text-sm text-gray-400 bg-white border border-gray-200 rounded-lg">
              Select a fee type to see its rule versions
            </div>
          )}

          {view === 'versions' && selectedDef && (
            <div className="bg-white border border-gray-200 rounded-lg">
              <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
                <div>
                  <h2 className="text-base font-semibold text-gray-900">{selectedDef.displayName}</h2>
                  <p className="text-xs text-gray-400 mt-0.5">{selectedDef.feeCategory} · {selectedDef.isTaxable ? 'Taxable' : 'Non-taxable'} · calc order {selectedDef.calculationOrder}</p>
                </div>
                <div className="flex gap-2">
                  <button onClick={() => { setNonTaxableEdit(versions[0]?.nonTaxableStates ?? []); setShowTaxability(true) }}
                    className="px-3 py-1.5 text-xs border border-gray-300 rounded text-gray-700 hover:bg-gray-50">Taxable States</button>
                  <button onClick={() => openNewVersion()}
                    className="flex items-center gap-1 px-3 py-1.5 text-xs bg-blue-600 text-white rounded hover:bg-blue-700">
                    <Plus className="h-3 w-3" /> New Version
                  </button>
                </div>
              </div>
              {loadingVersions ? <div className="p-8 flex justify-center"><LoadingSpinner /></div> : (
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-100 text-xs text-gray-500 uppercase tracking-wider">
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
                          <td className={`px-6 py-3 ${sup ? 'line-through' : 'text-gray-900 font-medium'}`}>{v.effectiveDate}</td>
                          <td className="px-6 py-3 text-gray-500">{v.disabledDate ?? <span className="text-green-600">Active</span>}</td>
                          <td className="px-6 py-3 text-gray-500">{[
                            v.carrierId ? carriers.find(c => c.id === v.carrierId)?.name ?? 'Carrier' : null,
                            v.stateCode,
                            v.lineOfBusiness ? LOB_LABELS[v.lineOfBusiness as PolicyLineOfBusiness] ?? v.lineOfBusiness : null,
                            v.licenseType,
                          ].filter(Boolean).join(' · ') || 'All'}</td>
                          <td className="px-6 py-3 text-gray-700">{v.calcType}</td>
                          <td className="px-6 py-3 text-gray-700">
                            {v.calcType === 'Flat' && v.flatAmount != null && `$${Number(v.flatAmount).toFixed(2)}`}
                            {v.calcType === 'Percent' && v.percentRate != null && `${(Number(v.percentRate) * 100).toFixed(4)}%`}
                            {v.calcType === 'Stratified' && `${v.premiumBrackets.length} tiers`}
                          </td>
                          <td className="px-6 py-3 text-right" onClick={e => e.stopPropagation()}>
                            {!sup && <button onClick={() => openNewVersion(v)} className="text-xs text-blue-600 hover:underline"><RefreshCw className="h-3 w-3 inline mr-1" />New Version</button>}
                          </td>
                        </tr>
                      )
                    })}
                    {versions.length === 0 && (
                      <tr><td colSpan={6} className="px-6 py-10 text-center text-gray-400">No versions yet. Click "New Version" to add one.</td></tr>
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
        <div className="bg-white border border-gray-200 rounded-lg">
          <div className="px-6 py-4 border-b border-gray-200">
            <h2 className="text-base font-semibold text-gray-900">Additional Interest Premium Charges</h2>
            <p className="text-xs text-gray-400 mt-0.5">Blank carrier or LOB means the rule applies to all.</p>
          </div>

          {showPremiumChargeForm && (
            <div className="p-4 border-b border-gray-200 bg-gray-50 space-y-3">
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
                  <span className="text-sm text-gray-700">Active</span>
                </label>
              </div>
              <div className="flex gap-2">
                <button onClick={() => savePremiumCharge()} disabled={savingPremiumCharge} className="px-4 py-1.5 text-xs bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50">
                  {savingPremiumCharge ? 'Saving...' : 'Save Premium Charge'}
                </button>
                <button onClick={() => { setShowPremiumChargeForm(false); setEditingPremiumChargeId(null); setPremiumChargeForm(emptyPremiumChargeForm()) }} className="px-4 py-1.5 text-xs border border-gray-300 rounded text-gray-700 hover:bg-gray-50">Cancel</button>
              </div>
            </div>
          )}

          {loadingPremiumCharges ? <div className="p-8 flex justify-center"><LoadingSpinner /></div> : (
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100 text-xs text-gray-500 uppercase tracking-wider">
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
                      <td className="px-6 py-3 font-medium text-gray-900">{carrierName}</td>
                      <td className="px-6 py-3 text-gray-600">{row.lineOfBusiness ? LOB_LABELS[row.lineOfBusiness as PolicyLineOfBusiness] ?? row.lineOfBusiness : 'All LOBs'}</td>
                      <td className="px-6 py-3 text-gray-700">{ADDITIONAL_INTEREST_COVERAGE_LABELS[row.coverageType]}</td>
                      <td className="px-6 py-3 text-gray-600">{ADDITIONAL_INTEREST_CHARGE_METHOD_LABELS[row.chargeMethod]}</td>
                      <td className="px-6 py-3 text-gray-700">{amount}</td>
                      <td className="px-6 py-3 text-gray-500">{row.state ?? 'All'}</td>
                      <td className="px-6 py-3">{row.isActive ? <span className="text-green-600">Active</span> : <span className="text-gray-400">Inactive</span>}</td>
                      <td className="px-6 py-3 text-right">
                        <button onClick={() => editPremiumCharge(row)} className="text-xs text-blue-600 hover:underline mr-3">Edit</button>
                        <button onClick={() => { if (confirm('Delete this premium charge?')) deletePremiumCharge(row.id) }} className="text-xs text-red-500 hover:underline">Delete</button>
                      </td>
                    </tr>
                  )
                })}
                {premiumCharges.length === 0 && (
                  <tr><td colSpan={8} className="px-6 py-10 text-center text-gray-400">No premium charges yet. Click "New Premium Charge" to add one.</td></tr>
                )}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* New Fee Type Modal */}
      {showNewDef && (
        <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl w-full max-w-md">
            <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
              <h3 className="font-semibold text-gray-900">New Fee Type</h3>
              <button onClick={() => setShowNewDef(false)}><X className="h-4 w-4 text-gray-400" /></button>
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
                <Field label="Ledger Account ID">
                  <input type="number" value={defForm.ledgerAccountId || ''} onChange={e => setDefForm(p => ({ ...p, ledgerAccountId: Number(e.target.value) }))} className={inputCls} />
                </Field>
              </div>
              <label className="flex items-center gap-2 cursor-pointer">
                <input type="checkbox" checked={defForm.isTaxable} onChange={e => setDefForm(p => ({ ...p, isTaxable: e.target.checked }))} className="rounded" />
                <span className="text-sm text-gray-700">Taxable (can be taxed by SL taxes)</span>
              </label>
            </div>
            <div className="px-6 py-4 border-t border-gray-200 flex justify-end gap-2">
              <button onClick={() => setShowNewDef(false)} className="px-4 py-2 text-sm border border-gray-300 rounded text-gray-700 hover:bg-gray-50">Cancel</button>
              <button onClick={() => createDef()} disabled={savingDef || !defForm.code || !defForm.displayName}
                className="px-4 py-2 text-sm bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50">
                {savingDef ? 'Creating…' : 'Create'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* State Taxability Modal */}
      {showTaxability && selectedDef && (
        <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl w-full max-w-lg" style={{ maxHeight: '80vh', display: 'flex', flexDirection: 'column' }}>
            <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between flex-shrink-0">
              <div>
                <h3 className="font-semibold text-gray-900">Taxable States — {selectedDef.displayName}</h3>
                <p className="text-xs text-gray-400 mt-0.5">Uncheck states where this fee is NOT taxable by SL tax</p>
              </div>
              <button onClick={() => setShowTaxability(false)}><X className="h-4 w-4 text-gray-400" /></button>
            </div>
            <div className="p-6 overflow-y-auto flex-1">
              <div className="grid grid-cols-5 gap-2">
                {US_STATES.map(st => (
                  <label key={st} className="flex items-center gap-1.5 cursor-pointer">
                    <input type="checkbox" checked={!nonTaxableEdit.includes(st)}
                      onChange={e => setNonTaxableEdit(prev => e.target.checked ? prev.filter(s => s !== st) : [...prev, st])}
                      className="rounded" />
                    <span className="text-sm text-gray-700">{st}</span>
                  </label>
                ))}
              </div>
            </div>
            <div className="px-6 py-4 border-t border-gray-200 flex justify-end gap-2 flex-shrink-0">
              <button onClick={() => setShowTaxability(false)} className="px-4 py-2 text-sm border border-gray-300 rounded text-gray-700 hover:bg-gray-50">Cancel</button>
              <button onClick={() => saveTaxability()} className="px-4 py-2 text-sm bg-blue-600 text-white rounded hover:bg-blue-700">Save</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
