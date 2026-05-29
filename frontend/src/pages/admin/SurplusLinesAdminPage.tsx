import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import axios from 'axios'
import { Check, Copy, Pencil, Plus, Save, X } from 'lucide-react'
import { toast } from 'sonner'
import { carriersApi } from '@/api/carriers.api'
import { feesApi } from '@/api/fees.api'
import { programConfigurationsApi } from '@/api/programConfigurations.api'
import { surplusLinesApi } from '@/api/surplusLines.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { PageHeader } from '@/components/common/PageHeader'
import type { FeeDefinition, PayeeOption } from '@/types/fee.types'
import { ACTIVE_LOBS, LOB_LABELS, type PolicyLineOfBusiness } from '@/types/quote.types'
import type { SurplusLinesStateSetup, SurplusLinesStateSetupUpsert } from '@/types/surplusLines.types'

const US_STATES = ['AL','AK','AZ','AR','CA','CO','CT','DE','FL','GA','HI','ID','IL','IN','IA','KS','KY','LA','ME','MD','MA','MI','MN','MS','MO','MT','NE','NV','NH','NJ','NM','NY','NC','ND','OH','OK','OR','PA','RI','SC','SD','TN','TX','UT','VA','VT','WA','WV','WI','WY','DC']

const today = () => new Date().toISOString().slice(0, 10)

const emptySetup = (): SurplusLinesStateSetupUpsert => ({
  stateCode: 'TX',
  programConfigurationId: null,
  carrierId: null,
  lineOfBusiness: null,
  effectiveDate: today(),
  expirationDate: null,
  isActive: true,
  filingRequired: true,
  licenseHolderType: 'SMM',
  filingBrokerName: 'Specialty Market Managers, LLC',
  licenseNumber: '',
  licenseState: 'TX',
  brokerAddressLine1: '',
  brokerAddressLine2: null,
  brokerCity: '',
  brokerState: 'TX',
  brokerZipCode: '',
  brokerCountry: 'USA',
  stampingWording: null,
  requiredNoticeText: null,
  paperworkNotes: null,
  filingNotes: null,
  surplusLinesTaxFeeDefinitionId: null,
  stampingFeeDefinitionId: null,
  filingFeeDefinitionId: null,
  filingPayeeId: null,
  createFilingPayable: false,
  filingPaymentTermsDays: 30,
  filingFrequency: 'Monthly',
  filingDueDayOfMonth: null,
  filingMethod: null,
  filingPortalUrl: null,
  requiredFilingFormsJson: '[]',
  diligentSearchRequired: false,
  diligentSearchNotes: null,
  affidavitRequired: false,
  affidavitNotes: null,
})

const inputCls = 'w-full rounded border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-blue-400'
const iconBtn = 'inline-flex items-center justify-center rounded border border-slate-200 p-2 text-slate-600 hover:bg-slate-50'

export function SurplusLinesAdminPage() {
  const qc = useQueryClient()
  const [form, setForm] = useState<SurplusLinesStateSetupUpsert>(emptySetup)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [copyTarget, setCopyTarget] = useState('SC')

  const { data: setups = [], isLoading } = useQuery({
    queryKey: ['admin', 'surplus-lines', 'setups'],
    queryFn: () => surplusLinesApi.getSetups(true),
  })
  const { data: programs = [] } = useQuery({
    queryKey: ['admin', 'program-configurations'],
    queryFn: () => programConfigurationsApi.getAll(true),
  })
  const { data: carriers = [] } = useQuery({
    queryKey: ['carriers', 'active'],
    queryFn: () => carriersApi.getAll(true),
  })
  const { data: fees = [] } = useQuery({
    queryKey: ['admin', 'fees', 'definitions'],
    queryFn: feesApi.getDefinitions,
  })
  const { data: payees = [] } = useQuery({
    queryKey: ['admin', 'fees', 'payees'],
    queryFn: feesApi.getPayees,
  })

  const selectedSetup = useMemo(
    () => setups.find((setup) => setup.id === selectedId) ?? setups[0] ?? null,
    [setups, selectedId],
  )
  const taxFees = useMemo(() => fees.filter((fee) => fee.feeCategory === 'Tax'), [fees])
  const stampingFees = useMemo(() => fees.filter((fee) => fee.feeCategory === 'StampingFee' || fee.feeCategory === 'Other'), [fees])

  const refresh = () => qc.invalidateQueries({ queryKey: ['admin', 'surplus-lines', 'setups'] })

  const saveSetup = useMutation({
    mutationFn: () => editingId
      ? surplusLinesApi.updateSetup(editingId, cleanSetup(form))
      : surplusLinesApi.createSetup(cleanSetup(form)),
    onSuccess: (saved) => {
      toast.success('Surplus lines setup saved')
      setSelectedId(saved.id)
      setEditingId(null)
      setForm(emptySetup())
      refresh()
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Surplus lines setup could not be saved')),
  })

  const copySetup = useMutation({
    mutationFn: () => {
      if (!selectedSetup) throw new Error('Select a setup to copy')
      return surplusLinesApi.copySetup(selectedSetup.id, copyTarget)
    },
    onSuccess: (saved) => {
      toast.success('Surplus lines setup copied')
      setSelectedId(saved.id)
      refresh()
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Surplus lines setup could not be copied')),
  })

  function editSetup(setup: SurplusLinesStateSetup) {
    setEditingId(setup.id)
    setSelectedId(setup.id)
    setForm({
      stateCode: setup.stateCode,
      programConfigurationId: setup.programConfigurationId,
      carrierId: setup.carrierId,
      lineOfBusiness: setup.lineOfBusiness,
      effectiveDate: setup.effectiveDate,
      expirationDate: setup.expirationDate,
      isActive: setup.isActive,
      filingRequired: setup.filingRequired,
      licenseHolderType: setup.licenseHolderType,
      filingBrokerName: setup.filingBrokerName,
      licenseNumber: setup.licenseNumber,
      licenseState: setup.licenseState,
      brokerAddressLine1: setup.brokerAddressLine1,
      brokerAddressLine2: setup.brokerAddressLine2,
      brokerCity: setup.brokerCity,
      brokerState: setup.brokerState,
      brokerZipCode: setup.brokerZipCode,
      brokerCountry: setup.brokerCountry,
      stampingWording: setup.stampingWording,
      requiredNoticeText: setup.requiredNoticeText,
      paperworkNotes: setup.paperworkNotes,
      filingNotes: setup.filingNotes,
      surplusLinesTaxFeeDefinitionId: setup.surplusLinesTaxFeeDefinitionId,
      stampingFeeDefinitionId: setup.stampingFeeDefinitionId,
      filingFeeDefinitionId: setup.filingFeeDefinitionId,
      filingPayeeId: setup.filingPayeeId,
      createFilingPayable: setup.createFilingPayable,
      filingPaymentTermsDays: setup.filingPaymentTermsDays,
      filingFrequency: setup.filingFrequency,
      filingDueDayOfMonth: setup.filingDueDayOfMonth,
      filingMethod: setup.filingMethod,
      filingPortalUrl: setup.filingPortalUrl,
      requiredFilingFormsJson: setup.requiredFilingFormsJson,
      diligentSearchRequired: setup.diligentSearchRequired,
      diligentSearchNotes: setup.diligentSearchNotes,
      affidavitRequired: setup.affidavitRequired,
      affidavitNotes: setup.affidavitNotes,
    })
  }

  if (isLoading) return <LoadingSpinner />

  return (
    <div className="space-y-5 p-6">
      <PageHeader
        title="Surplus Lines Setup"
        subtitle="State filing broker, license, wording, paperwork, fee setup, and vendor filing details"
      />

      <section className="rounded-lg border bg-white">
        <div className="flex items-center justify-between gap-3 border-b px-5 py-4">
          <h2 className="text-sm font-semibold text-slate-800">Configured states <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{setups.length}</span></h2>
          <div className="flex items-center gap-2">
            <select value={copyTarget} onChange={(e) => setCopyTarget(e.target.value)} className="rounded border border-slate-300 px-2 py-1 text-xs">
              {US_STATES.map((state) => <option key={state} value={state}>{state}</option>)}
            </select>
            <button type="button" className={iconBtn} title="Copy selected setup to state" disabled={!selectedSetup || copySetup.isPending || selectedSetup.stateCode === copyTarget} onClick={() => copySetup.mutate()}>
              <Copy className="h-4 w-4" />
            </button>
          </div>
        </div>
        <div className="overflow-auto">
          <table className="sd-table">
            <thead>
              <tr>
                <th>State</th>
                <th>Scope</th>
                <th>Broker</th>
                <th>Fees / Filing</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {setups.map((setup) => (
                <tr key={setup.id} className={selectedSetup?.id === setup.id ? 'bg-blue-50/60' : ''}>
                  <td className="primary-cell">
                    <button type="button" onClick={() => setSelectedId(setup.id)} className="font-medium text-blue-700 hover:underline">{setup.stateCode}</button>
                    <div className="text-xs text-slate-500">{dateRange(setup.effectiveDate, setup.expirationDate)}</div>
                  </td>
                  <td>
                    <div>{[setup.programName ?? 'All programs', setup.carrierName ?? 'All carriers'].join(' / ')}</div>
                    <div className="text-xs text-slate-500">{setup.lineOfBusinessLabel ?? 'All LOBs'}</div>
                  </td>
                  <td>
                    <div>{setup.filingBrokerName}</div>
                    <div className="text-xs text-slate-500">{setup.licenseHolderType} / {setup.licenseNumber}</div>
                  </td>
                  <td className="text-xs text-slate-600">
                    <div>{[setup.surplusLinesTaxFeeName, setup.stampingFeeName, setup.filingFeeName].filter(Boolean).join(', ') || 'No fee links'}</div>
                    <div className="mt-1 text-slate-500">{filingHandlingText(setup)}</div>
                    {setup.feeValidationMessages.length > 0 && (
                      <div className="mt-1 font-medium text-amber-700">{setup.feeValidationMessages.length} fee setup issue{setup.feeValidationMessages.length === 1 ? '' : 's'}</div>
                    )}
                  </td>
                  <td><StatusPill active={setup.isActive} filingRequired={setup.filingRequired} /></td>
                  <td>
                    <div className="flex justify-end">
                      <button type="button" className={iconBtn} title="Edit setup" onClick={() => editSetup(setup)}>
                        <Pencil className="h-4 w-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {setups.length === 0 && (
                <tr>
                  <td colSpan={6} className="py-8 text-center text-sm text-slate-500">No surplus lines setups configured yet.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      <section className="rounded-lg border bg-white">
        <div className="flex items-center justify-between gap-3 border-b px-5 py-4">
          <h2 className="text-sm font-semibold text-slate-800">{editingId ? 'Edit setup' : 'New setup'}</h2>
          {editingId && (
            <button type="button" className={iconBtn} title="Cancel edit" onClick={() => { setEditingId(null); setForm(emptySetup()) }}>
              <X className="h-4 w-4" />
            </button>
          )}
        </div>
        <div className="space-y-5 p-5">
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            <SelectField label="State" value={form.stateCode} onChange={(value) => setForm((f) => ({ ...f, stateCode: value, licenseState: value, brokerState: value }))}>
              {US_STATES.map((state) => <option key={state} value={state}>{state}</option>)}
            </SelectField>
            <TextInput label="Effective" type="date" value={form.effectiveDate} onChange={(value) => setForm((f) => ({ ...f, effectiveDate: value }))} />
            <TextInput label="Expiration" type="date" value={form.expirationDate ?? ''} onChange={(value) => setForm((f) => ({ ...f, expirationDate: value || null }))} />
            <SelectField label="License holder" value={form.licenseHolderType} onChange={(value) => setForm((f) => ({ ...f, licenseHolderType: value }))}>
              <option value="SMM">SMM</option>
              <option value="Jeremiah">Jeremiah</option>
              <option value="Vendor">Vendor</option>
              <option value="Other">Other</option>
            </SelectField>
            <SelectField label="Program" value={form.programConfigurationId ?? ''} onChange={(value) => setForm((f) => ({ ...f, programConfigurationId: value || null }))}>
              <option value="">All programs</option>
              {programs.map((program) => <option key={program.id} value={program.id}>{program.name}</option>)}
            </SelectField>
            <SelectField label="Carrier" value={form.carrierId ?? ''} onChange={(value) => setForm((f) => ({ ...f, carrierId: value || null }))}>
              <option value="">All carriers</option>
              {carriers.map((carrier) => <option key={carrier.id} value={carrier.id}>{carrier.name}</option>)}
            </SelectField>
            <SelectField label="LOB" value={form.lineOfBusiness ?? ''} onChange={(value) => setForm((f) => ({ ...f, lineOfBusiness: value ? value as PolicyLineOfBusiness : null }))}>
              <option value="">All LOBs</option>
              {ACTIVE_LOBS.map((lob) => <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>)}
            </SelectField>
            <div className="grid grid-cols-2 gap-3">
              <CheckInput label="Active" checked={form.isActive} onChange={(value) => setForm((f) => ({ ...f, isActive: value }))} />
              <CheckInput label="Filing required" checked={form.filingRequired} onChange={(value) => setForm((f) => ({ ...f, filingRequired: value }))} />
            </div>
          </div>

          <div className="border-t pt-5">
            <div className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-500">Broker / license</div>
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
              <div className="xl:col-span-2">
                <TextInput label="Filing broker" value={form.filingBrokerName} onChange={(value) => setForm((f) => ({ ...f, filingBrokerName: value }))} />
              </div>
              <TextInput label="License number" value={form.licenseNumber} onChange={(value) => setForm((f) => ({ ...f, licenseNumber: value }))} />
              <TextInput label="License state" value={form.licenseState} onChange={(value) => setForm((f) => ({ ...f, licenseState: value.toUpperCase().slice(0, 2) }))} />
              <TextInput label="Address line 1" value={form.brokerAddressLine1} onChange={(value) => setForm((f) => ({ ...f, brokerAddressLine1: value }))} />
              <TextInput label="Address line 2" value={form.brokerAddressLine2 ?? ''} onChange={(value) => setForm((f) => ({ ...f, brokerAddressLine2: value || null }))} />
              <TextInput label="City" value={form.brokerCity} onChange={(value) => setForm((f) => ({ ...f, brokerCity: value }))} />
              <TextInput label="Broker state" value={form.brokerState} onChange={(value) => setForm((f) => ({ ...f, brokerState: value.toUpperCase().slice(0, 2) }))} />
              <TextInput label="Zip" value={form.brokerZipCode} onChange={(value) => setForm((f) => ({ ...f, brokerZipCode: value }))} />
              <TextInput label="Country" value={form.brokerCountry} onChange={(value) => setForm((f) => ({ ...f, brokerCountry: value.toUpperCase() }))} />
            </div>
          </div>

          <div className="border-t pt-5">
            <div className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-500">Fees</div>
            <div className="grid gap-3 md:grid-cols-3">
              <FeeSelect label="SL tax fee" value={form.surplusLinesTaxFeeDefinitionId} fees={taxFees} onChange={(value) => setForm((f) => ({ ...f, surplusLinesTaxFeeDefinitionId: value }))} />
              <FeeSelect label="Stamp fee" value={form.stampingFeeDefinitionId} fees={stampingFees} onChange={(value) => setForm((f) => ({ ...f, stampingFeeDefinitionId: value }))} />
              <FeeSelect label="Filing fee" value={form.filingFeeDefinitionId} fees={fees} onChange={(value) => setForm((f) => ({ ...f, filingFeeDefinitionId: value }))} />
            </div>
          </div>

          <div className="border-t pt-5">
            <div className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-500">Filing handling</div>
            <div className="grid gap-3 md:grid-cols-4">
              <CheckInput
                label="Filed by vendor"
                checked={form.createFilingPayable}
                onChange={(value) => setForm((f) => ({ ...f, createFilingPayable: value, filingPayeeId: value ? f.filingPayeeId : null }))}
              />
              {!form.createFilingPayable ? (
                <div className="rounded border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-600 md:col-span-3">
                  Handled by SMM; payable to state.
                </div>
              ) : (
                <div className="grid gap-3 rounded border border-blue-100 bg-blue-50/40 p-3 md:col-span-4 md:grid-cols-4">
                  <PayeeSelect label="Vendor payee" value={form.filingPayeeId} payees={payees} onChange={(value) => setForm((f) => ({ ...f, filingPayeeId: value }))} />
                  <TextInput
                    label="Payment terms days"
                    type="number"
                    value={form.filingPaymentTermsDays?.toString() ?? ''}
                    onChange={(value) => setForm((f) => ({ ...f, filingPaymentTermsDays: value ? Number(value) : null }))}
                  />
                  <SelectField label="Filing frequency" value={form.filingFrequency ?? ''} onChange={(value) => setForm((f) => ({ ...f, filingFrequency: value || null }))}>
                    <option value="">Not set</option>
                    <option value="Monthly">Monthly</option>
                    <option value="Quarterly">Quarterly</option>
                    <option value="Annual">Annual</option>
                    <option value="Transaction">Per transaction</option>
                  </SelectField>
                  <TextInput
                    label="Due day of month"
                    type="number"
                    value={form.filingDueDayOfMonth?.toString() ?? ''}
                    onChange={(value) => setForm((f) => ({ ...f, filingDueDayOfMonth: value ? Number(value) : null }))}
                  />
                  <SelectField label="Filing method" value={form.filingMethod ?? ''} onChange={(value) => setForm((f) => ({ ...f, filingMethod: value || null }))}>
                    <option value="">Not set</option>
                    <option value="Portal">Portal</option>
                    <option value="Email">Email</option>
                    <option value="Paper">Paper</option>
                    <option value="Vendor">Vendor</option>
                    <option value="Other">Other</option>
                  </SelectField>
                  <div className="md:col-span-3">
                    <TextInput label="Vendor portal URL" value={form.filingPortalUrl ?? ''} onChange={(value) => setForm((f) => ({ ...f, filingPortalUrl: value || null }))} />
                  </div>
                </div>
              )}
            </div>
          </div>

          <div className="border-t pt-5">
            <div className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-500">State paperwork</div>
            <div className="grid gap-3 lg:grid-cols-2">
              <TextArea
                label="Required filing forms"
                value={formsJsonToText(form.requiredFilingFormsJson)}
                onChange={(value) => setForm((f) => ({ ...f, requiredFilingFormsJson: formsTextToJson(value) }))}
              />
              <div className="grid gap-3 md:grid-cols-2">
                <CheckInput label="Diligent search required" checked={form.diligentSearchRequired} onChange={(value) => setForm((f) => ({ ...f, diligentSearchRequired: value }))} />
                <CheckInput label="Affidavit required" checked={form.affidavitRequired} onChange={(value) => setForm((f) => ({ ...f, affidavitRequired: value }))} />
                <TextArea label="Diligent search notes" value={form.diligentSearchNotes ?? ''} onChange={(value) => setForm((f) => ({ ...f, diligentSearchNotes: value }))} />
                <TextArea label="Affidavit notes" value={form.affidavitNotes ?? ''} onChange={(value) => setForm((f) => ({ ...f, affidavitNotes: value }))} />
              </div>
            </div>
          </div>

          <div className="border-t pt-5">
            <div className="mb-3 text-xs font-semibold uppercase tracking-wide text-slate-500">Wording / notes</div>
            <div className="grid gap-3 lg:grid-cols-2">
              <TextArea label="Stamping wording" value={form.stampingWording ?? ''} onChange={(value) => setForm((f) => ({ ...f, stampingWording: value }))} />
              <TextArea label="Required notice" value={form.requiredNoticeText ?? ''} onChange={(value) => setForm((f) => ({ ...f, requiredNoticeText: value }))} />
              <TextArea label="Paperwork notes" value={form.paperworkNotes ?? ''} onChange={(value) => setForm((f) => ({ ...f, paperworkNotes: value }))} />
              <TextArea label="Filing notes" value={form.filingNotes ?? ''} onChange={(value) => setForm((f) => ({ ...f, filingNotes: value }))} />
            </div>
          </div>

          <button
            type="button"
            onClick={() => saveSetup.mutate()}
            disabled={saveSetup.isPending || !form.stateCode || !form.licenseNumber.trim() || !form.filingBrokerName.trim() || (form.createFilingPayable && !form.filingPayeeId)}
            className="inline-flex w-full items-center justify-center gap-2 rounded bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
          >
            {editingId ? <Save className="h-4 w-4" /> : <Plus className="h-4 w-4" />}
            {editingId ? 'Save Setup' : 'Add Setup'}
          </button>
        </div>
      </section>

      {selectedSetup && (
        <section className="rounded-lg border bg-white">
          <div className="border-b px-5 py-4">
            <h2 className="text-sm font-semibold text-slate-800">{selectedSetup.stateCode} details</h2>
          </div>
          <div className="grid gap-4 p-5 md:grid-cols-2 xl:grid-cols-3">
            <DetailBlock label="Filing broker" value={selectedSetup.filingBrokerName} />
            <DetailBlock label="License" value={`${selectedSetup.licenseNumber} (${selectedSetup.licenseState})`} />
            <DetailBlock label="Address" value={[selectedSetup.brokerAddressLine1, selectedSetup.brokerAddressLine2, selectedSetup.brokerCity, selectedSetup.brokerState, selectedSetup.brokerZipCode, selectedSetup.brokerCountry].filter(Boolean).join(', ')} />
            <DetailBlock label="Linked fees" value={[selectedSetup.surplusLinesTaxFeeName, selectedSetup.stampingFeeName, selectedSetup.filingFeeName].filter(Boolean).join(', ') || 'No fee links'} />
            <DetailBlock label="Filing handling" value={filingHandlingText(selectedSetup)} />
            <DetailBlock label="Vendor cadence" value={selectedSetup.createFilingPayable ? [selectedSetup.filingFrequency, selectedSetup.filingDueDayOfMonth ? `Due day ${selectedSetup.filingDueDayOfMonth}` : null, selectedSetup.filingMethod].filter(Boolean).join(' / ') || 'Not set' : 'Not vendor-filed'} />
            <DetailBlock label="Vendor portal" value={selectedSetup.createFilingPayable ? selectedSetup.filingPortalUrl || 'None' : 'Not vendor-filed'} />
            <DetailBlock label="Required forms" value={formsJsonToText(selectedSetup.requiredFilingFormsJson) || 'None'} />
            {selectedSetup.feeValidationMessages.length > 0 && (
              <div className="rounded border border-amber-200 bg-amber-50 p-3 md:col-span-2 xl:col-span-3">
                <div className="text-xs font-semibold uppercase tracking-wide text-amber-700">Fee setup issues</div>
                <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-amber-800">
                  {selectedSetup.feeValidationMessages.map((message) => <li key={message}>{message}</li>)}
                </ul>
              </div>
            )}
            <DetailBlock label="Stamping wording" value={selectedSetup.stampingWording || 'None'} />
            <DetailBlock label="Required notice" value={selectedSetup.requiredNoticeText || 'None'} />
            <DetailBlock label="Diligent search" value={selectedSetup.diligentSearchRequired ? selectedSetup.diligentSearchNotes || 'Required' : 'Not required'} />
            <DetailBlock label="Affidavit" value={selectedSetup.affidavitRequired ? selectedSetup.affidavitNotes || 'Required' : 'Not required'} />
            <DetailBlock label="Paperwork notes" value={selectedSetup.paperworkNotes || 'None'} />
            <DetailBlock label="Filing notes" value={selectedSetup.filingNotes || 'None'} />
          </div>
        </section>
      )}
    </div>
  )
}

function TextInput({ label, value, onChange, type = 'text' }: { label: string; value: string; onChange: (value: string) => void; type?: string }) {
  return (
    <label className="block">
      <span className="sims-field-label">{label}</span>
      <input type={type} value={value} onChange={(e) => onChange(e.target.value)} className={inputCls} />
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

function FeeSelect({ label, value, fees, onChange }: { label: string; value: number | null; fees: FeeDefinition[]; onChange: (value: number | null) => void }) {
  return (
    <SelectField label={label} value={value?.toString() ?? ''} onChange={(raw) => onChange(raw ? Number(raw) : null)}>
      <option value="">None</option>
      {fees.map((fee) => <option key={fee.id} value={fee.id}>{fee.displayName}</option>)}
    </SelectField>
  )
}

function PayeeSelect({ label, value, payees, onChange }: { label: string; value: number | null; payees: PayeeOption[]; onChange: (value: number | null) => void }) {
  return (
    <SelectField label={label} value={value?.toString() ?? ''} onChange={(raw) => onChange(raw ? Number(raw) : null)}>
      <option value="">None</option>
      {payees.map((payee) => <option key={payee.id} value={payee.id}>{payee.name} ({payee.payeeType})</option>)}
    </SelectField>
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

function StatusPill({ active, filingRequired }: { active: boolean; filingRequired: boolean }) {
  return (
    <span className={`sd-pill ${active ? 'bound' : 'expired'}`}>
      {active && <Check className="h-3 w-3" />}
      {active ? (filingRequired ? 'Active filing' : 'Active no filing') : 'Inactive'}
    </span>
  )
}

function DetailBlock({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-1 whitespace-pre-wrap text-sm text-slate-800">{value}</div>
    </div>
  )
}

function dateRange(effectiveDate: string, expirationDate: string | null) {
  return `${effectiveDate}${expirationDate ? ` to ${expirationDate}` : ''}`
}

function filingHandlingText(setup: SurplusLinesStateSetup) {
  if (!setup.createFilingPayable) return 'Direct filing by SMM; payable to state'

  const terms = setup.filingPaymentTermsDays != null ? ` / Net ${setup.filingPaymentTermsDays}` : ''
  return `Filed by vendor: ${setup.filingPayeeName ?? 'No vendor payee selected'}${terms}`
}

function cleanSetup(setup: SurplusLinesStateSetupUpsert): SurplusLinesStateSetupUpsert {
  return {
    ...setup,
    stateCode: setup.stateCode.toUpperCase(),
    licenseState: setup.licenseState.toUpperCase(),
    brokerState: setup.brokerState.toUpperCase(),
    brokerCountry: setup.brokerCountry.trim().toUpperCase() || 'USA',
    expirationDate: setup.expirationDate || null,
    brokerAddressLine2: trimToNull(setup.brokerAddressLine2),
    stampingWording: trimToNull(setup.stampingWording),
    requiredNoticeText: trimToNull(setup.requiredNoticeText),
    paperworkNotes: trimToNull(setup.paperworkNotes),
    filingNotes: trimToNull(setup.filingNotes),
    filingPayeeId: setup.createFilingPayable ? setup.filingPayeeId : null,
    filingPaymentTermsDays: setup.createFilingPayable ? setup.filingPaymentTermsDays : null,
    filingFrequency: setup.createFilingPayable ? trimToNull(setup.filingFrequency) : null,
    filingDueDayOfMonth: setup.createFilingPayable ? setup.filingDueDayOfMonth : null,
    filingMethod: setup.createFilingPayable ? trimToNull(setup.filingMethod) : null,
    filingPortalUrl: setup.createFilingPayable ? trimToNull(setup.filingPortalUrl) : null,
    requiredFilingFormsJson: setup.requiredFilingFormsJson || '[]',
    diligentSearchNotes: trimToNull(setup.diligentSearchNotes),
    affidavitNotes: trimToNull(setup.affidavitNotes),
  }
}

function trimToNull(value: string | null) {
  return value && value.trim() ? value.trim() : null
}

function getApiErrorMessage(e: unknown, fallback: string) {
  if (axios.isAxiosError(e)) {
    const data = e.response?.data
    if (typeof data === 'string') return data
    return data?.errorMessage ?? data?.message ?? data?.title ?? fallback
  }
  if (e instanceof Error) return e.message
  return fallback
}

function formsJsonToText(json: string | null) {
  if (!json) return ''
  try {
    const parsed = JSON.parse(json)
    return Array.isArray(parsed)
      ? parsed.filter((value): value is string => typeof value === 'string').join('\n')
      : ''
  } catch {
    return ''
  }
}

function formsTextToJson(text: string) {
  const forms = text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)

  return JSON.stringify(forms)
}
