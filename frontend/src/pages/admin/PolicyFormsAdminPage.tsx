import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Download, FileText, PackagePlus, Play, Plus, Settings, Trash2, Upload } from 'lucide-react'
import { toast } from 'sonner'
import { policyFormsApi } from '@/api/policyForms.api'
import { carriersApi } from '@/api/carriers.api'
import { policiesApi } from '@/api/policies.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ACTIVE_LOBS, LOB_LABELS, type PolicyLineOfBusiness } from '@/types/quote.types'
import type { PolicyListItem } from '@/types/policy.types'
import type { DocumentTag, PolicyFormFieldMappingUpsert, PolicyFormTemplate, PolicyFormType, PolicyPackageConfiguration, PolicyPackageFormUpsert } from '@/types/policyForm.types'

const FORM_TYPES: PolicyFormType[] = ['Mandatory', 'Conditional', 'AdHoc']
const US_STATES = [
  'AL', 'AK', 'AZ', 'AR', 'CA', 'CO', 'CT', 'DE', 'FL', 'GA',
  'HI', 'ID', 'IL', 'IN', 'IA', 'KS', 'KY', 'LA', 'ME', 'MD',
  'MA', 'MI', 'MN', 'MS', 'MO', 'MT', 'NE', 'NV', 'NH', 'NJ',
  'NM', 'NY', 'NC', 'ND', 'OH', 'OK', 'OR', 'PA', 'RI', 'SC',
  'SD', 'TN', 'TX', 'UT', 'VT', 'VA', 'WA', 'WV', 'WI', 'WY',
  'DC',
]
const TRIGGER_FIELDS = [
  { path: 'Quote.PremiumAmount', label: 'Base premium', kind: 'number' },
  { path: 'Quote.TotalPremium', label: 'Total premium', kind: 'number' },
  { path: 'Rating.GrandTotalPremium', label: 'Rated grand total', kind: 'number' },
  { path: 'Rating.DebrisRemoval', label: 'Debris Removal premium', kind: 'number' },
  { path: 'Rating.RentalReimbursement', label: 'Rental Reimbursement premium', kind: 'number' },
  { path: 'Rating.TowingStorageRecovery', label: 'Towing, Storage & Recovery premium', kind: 'number' },
  { path: 'Rating.NewlyAcquiredEquipment', label: 'Newly Acquired Equipment premium', kind: 'number' },
  { path: 'Rating.Tria', label: 'TRIA premium', kind: 'number' },
  { path: 'Rating.EndorsementPremium', label: 'Endorsement premium', kind: 'number' },
  { path: 'Submission.LossPayeeCount', label: 'Loss payee count', kind: 'number' },
  { path: 'Quote.IsFilingState', label: 'Filing state', kind: 'boolean' },
  { path: 'Quote.LineOfBusiness', label: 'Line of business', kind: 'lob' },
] as const
const DATA_PATH_OPTIONS = [
  'Policy.PolicyNumber',
  'Policy.EffectiveDate',
  'Policy.ExpirationDate',
  'Policy.BoundDate',
  'Policy.IssuedDate',
  'Policy.PremiumAmount',
  'Policy.TaxesAndFees',
  'Policy.TotalPremium',
  'Policy.LineOfBusiness',
  'Quote.QuoteNumber',
  'Quote.PolicyNumber',
  'Quote.EffectiveDate',
  'Quote.ExpirationDate',
  'Quote.PremiumAmount',
  'Quote.TaxesAndFees',
  'Quote.TotalPremium',
  'Quote.CoverageDescription',
  'Quote.Deductible',
  'Quote.Limit',
  'Quote.UninsuredMotoristLimit',
  'Quote.MedicalPaymentsLimit',
  'Quote.LineOfBusiness',
  'Submission.SubmissionNumber',
  'Insured.DisplayName',
  'Insured.Name',
  'Insured.CompanyName',
  'Insured.Dba',
  'Insured.FirstName',
  'Insured.LastName',
  'Insured.AddressLine1',
  'Insured.AddressLine2',
  'Insured.City',
  'Insured.State',
  'Insured.ZipCode',
  'Insured.FullAddress',
  'Insured.Email',
  'Insured.Phone',
  'Carrier.Name',
  'Carrier.Naic',
]
const FORMAT_OPTIONS = ['', 'currency', 'number', 'percent', 'MM/dd/yyyy']
const DEFAULT_TRIGGER_CONDITION = JSON.stringify({ path: 'Quote.PremiumAmount', greaterThan: 0 })
type TriggerField = typeof TRIGGER_FIELDS[number]
type TriggerFieldPath = TriggerField['path']
type TriggerOperator = 'equals' | 'notEquals' | 'greaterThan' | 'lessThan'
type TriggerConfig = {
  path: TriggerFieldPath
  operator: TriggerOperator
  value: string | number | boolean
}

const emptyTemplate = {
  formNumber: '',
  name: '',
  editionDate: '',
  documentType: 'PolicyForm' as const,
  fileName: '',
  contentType: '',
  storagePath: '',
  isFillable: false,
  isActive: true,
  notes: '',
}

const emptyPackage = {
  carrierId: '',
  lineOfBusiness: 'InlandMarine' as PolicyLineOfBusiness,
  state: '',
  name: '',
  isActive: true,
}

function getTriggerField(path: string | undefined) {
  return TRIGGER_FIELDS.find((field) => field.path === path) ?? TRIGGER_FIELDS[0]
}

function parseTriggerCondition(triggerConditionJson?: string): TriggerConfig {
  try {
    const parsed = triggerConditionJson ? JSON.parse(triggerConditionJson) : {}
    const field = getTriggerField(parsed.path)

    if (field.kind === 'boolean') {
      if (typeof parsed.notEquals === 'boolean') {
        return { path: field.path, operator: 'notEquals', value: parsed.notEquals }
      }
      return { path: field.path, operator: 'equals', value: typeof parsed.equals === 'boolean' ? parsed.equals : true }
    }

    if (field.kind === 'lob') {
      if (typeof parsed.notEquals === 'string') {
        return { path: field.path, operator: 'notEquals', value: parsed.notEquals }
      }
      return { path: field.path, operator: 'equals', value: typeof parsed.equals === 'string' ? parsed.equals : 'InlandMarine' }
    }

    if (typeof parsed.lessThan === 'number') {
      return { path: field.path, operator: 'lessThan', value: parsed.lessThan }
    }
    if (typeof parsed.equals === 'number') {
      return { path: field.path, operator: 'equals', value: parsed.equals }
    }
    return {
      path: field.path,
      operator: 'greaterThan',
      value: typeof parsed.greaterThan === 'number' ? parsed.greaterThan : 0,
    }
  } catch {
    return { path: 'Quote.PremiumAmount', operator: 'greaterThan', value: 0 }
  }
}

function buildTriggerCondition(config: TriggerConfig) {
  const field = getTriggerField(config.path)
  const value = field.kind === 'number' ? Number(config.value) || 0 : config.value
  return JSON.stringify({ path: config.path, [config.operator]: value })
}

function describeTriggerCondition(config: TriggerConfig) {
  const field = getTriggerField(config.path)
  const operatorLabels: Record<TriggerOperator, string> = {
    equals: 'is',
    notEquals: 'is not',
    greaterThan: 'is greater than',
    lessThan: 'is less than',
  }
  const value = typeof config.value === 'boolean'
    ? (config.value ? 'Yes' : 'No')
    : config.value
  return `${field.label} ${operatorLabels[config.operator]} ${value}`
}

export function PolicyFormsAdminPage() {
  const qc = useQueryClient()
  const [templateForm, setTemplateForm] = useState(emptyTemplate)
  const [packageForm, setPackageForm] = useState(emptyPackage)
  const [selectedPackageId, setSelectedPackageId] = useState<string | null>(null)
  const [packageRows, setPackageRows] = useState<PolicyPackageFormUpsert[]>([])
  const [selectedTemplateId, setSelectedTemplateId] = useState<string | null>(null)
  const [mappingRows, setMappingRows] = useState<PolicyFormFieldMappingUpsert[]>([])
  const [testPolicyId, setTestPolicyId] = useState('')

  const { data: templates = [], isLoading: loadingTemplates } = useQuery({
    queryKey: ['policy-form-templates'],
    queryFn: () => policyFormsApi.getTemplates(true),
  })

  const { data: packages = [], isLoading: loadingPackages } = useQuery({
    queryKey: ['policy-form-packages'],
    queryFn: () => policyFormsApi.getPackages({ includeInactive: true }),
  })

  const { data: carriers = [] } = useQuery({
    queryKey: ['carriers', 'active'],
    queryFn: () => carriersApi.getAll(true),
  })

  const { data: tags = [] } = useQuery({
    queryKey: ['policy-form-tags'],
    queryFn: policyFormsApi.getTags,
  })

  const { data: policyPage } = useQuery({
    queryKey: ['policies', 'policy-form-test-data'],
    queryFn: () => policiesApi.getAll({ page: 1, pageSize: 50, sortBy: 'createdAt', sortDir: 'desc' }),
  })

  const selectedPackage = packages.find((p) => p.id === selectedPackageId) ?? null
  const selectedTemplate = templates.find((t) => t.id === selectedTemplateId) ?? null
  const policyOptions = policyPage?.items ?? []

  const packageTemplates = useMemo(() => templates.filter((t) => t.isActive), [templates])
  const derivedPackageName = useMemo(() => {
    const carrierName = carriers.find((carrier) => carrier.id === packageForm.carrierId)?.name
    if (!carrierName || !packageForm.lineOfBusiness || !packageForm.state) return ''
    return `${carrierName} - ${LOB_LABELS[packageForm.lineOfBusiness]} - ${packageForm.state}`
  }, [carriers, packageForm.carrierId, packageForm.lineOfBusiness, packageForm.state])
  const mappingDataPaths = useMemo(() => {
    const nonRepeatingTags = tags.filter((t) => !t.isRepeatable).map((t) => t.tag)
    return nonRepeatingTags.length > 0 ? nonRepeatingTags : DATA_PATH_OPTIONS
  }, [tags])
  const tagCategories = useMemo(() => {
    return tags.reduce<Record<string, DocumentTag[]>>((acc, tag) => {
      acc[tag.category] = [...(acc[tag.category] ?? []), tag]
      return acc
    }, {})
  }, [tags])

  useEffect(() => {
    if (!testPolicyId && policyOptions.length > 0) {
      setTestPolicyId(policyOptions[0].id)
    }
  }, [policyOptions, testPolicyId])

  const createTemplate = useMutation({
    mutationFn: () => policyFormsApi.createTemplate({
      ...templateForm,
      editionDate: templateForm.editionDate || undefined,
      fileName: templateForm.fileName || undefined,
      contentType: templateForm.contentType || undefined,
      storagePath: templateForm.storagePath || undefined,
      notes: templateForm.notes || undefined,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policy-form-templates'] })
      setTemplateForm(emptyTemplate)
      toast.success('Policy form saved')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Policy form could not be saved'),
  })

  const uploadTemplateFile = useMutation({
    mutationFn: ({ templateId, file }: { templateId: string; file: File }) =>
      policyFormsApi.uploadTemplateFile(templateId, file),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policy-form-templates'] })
      toast.success('Policy form file uploaded')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Policy form file could not be uploaded'),
  })

  const testMergeTemplate = useMutation({
    mutationFn: (templateId: string) => policyFormsApi.testMergeTemplate(templateId, testPolicyId),
    onSuccess: (data) => {
      window.open(data.url, '_blank', 'noopener,noreferrer')
      toast.success('Test merge created')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Test merge could not be created'),
  })

  const openTemplateFile = async (templateId: string) => {
    try {
      const data = await policyFormsApi.getTemplateDownloadUrl(templateId)
      window.open(data.url, '_blank', 'noopener,noreferrer')
    } catch (e: any) {
      toast.error(e?.response?.data?.errorMessage ?? 'Policy form file could not be opened')
    }
  }

  const createPackage = useMutation({
    mutationFn: () => policyFormsApi.createPackage({
      ...packageForm,
      name: derivedPackageName,
      state: packageForm.state.toUpperCase(),
    }),
    onSuccess: (saved) => {
      qc.invalidateQueries({ queryKey: ['policy-form-packages'] })
      setPackageForm(emptyPackage)
      setSelectedPackageId(saved.id)
      setPackageRows([])
      toast.success('Package created')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Package could not be saved'),
  })

  const savePackageRows = useMutation({
    mutationFn: () => policyFormsApi.replacePackageForms(selectedPackageId!, packageRows.map((row, index) => ({
      ...row,
      sequenceOrder: Number(row.sequenceOrder) || index + 1,
      triggerConditionJson: row.formType === 'Conditional'
        ? buildTriggerCondition(parseTriggerCondition(row.triggerConditionJson))
        : undefined,
    }))),
    onSuccess: (saved) => {
      qc.invalidateQueries({ queryKey: ['policy-form-packages'] })
      setPackageRows(saved.forms.map((f) => ({
        policyFormTemplateId: f.policyFormTemplateId,
        sequenceOrder: f.sequenceOrder,
        formType: f.formType,
        triggerConditionJson: f.triggerConditionJson ?? undefined,
        notes: f.notes ?? undefined,
      })))
      toast.success('Package forms saved')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? e?.response?.data?.message ?? 'Package forms could not be saved'),
  })

  const saveMappings = useMutation({
    mutationFn: () => policyFormsApi.replaceMappings(selectedTemplateId!, mappingRows),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policy-form-templates'] })
      toast.success('Field mappings saved')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Field mappings could not be saved'),
  })

  const selectPackage = (pkg: PolicyPackageConfiguration) => {
    setSelectedPackageId(pkg.id)
    setPackageRows(pkg.forms.map((f) => ({
      policyFormTemplateId: f.policyFormTemplateId,
      sequenceOrder: f.sequenceOrder,
      formType: f.formType,
      triggerConditionJson: f.triggerConditionJson ?? undefined,
      notes: f.notes ?? undefined,
    })))
  }

  const selectTemplateMappings = (template: PolicyFormTemplate) => {
    setSelectedTemplateId(template.id)
    setMappingRows(template.fieldMappings.map((m) => ({
      pdfFieldName: m.pdfFieldName,
      dataPath: m.dataPath,
      format: m.format ?? undefined,
    })))
  }

  const addMappingRow = () => {
    setMappingRows((rows) => [
      ...rows,
      {
        pdfFieldName: '',
        dataPath: mappingDataPaths[0],
      },
    ])
  }

  const copyTag = async (tag: DocumentTag) => {
    const text = tag.isRepeatable && tag.repeatBlock
      ? `{{#${tag.repeatBlock}}}\n{{${tag.tag}}}\n{{/${tag.repeatBlock}}}`
      : `{{${tag.tag}}}`
    await navigator.clipboard.writeText(text)
    toast.success('Tag copied')
  }

  const addPackageRow = () => {
    const firstTemplate = packageTemplates[0]
    if (!firstTemplate) {
      toast.error('Create at least one active form first')
      return
    }
    setPackageRows((rows) => [
      ...rows,
      {
        policyFormTemplateId: firstTemplate.id,
        sequenceOrder: rows.length + 1,
        formType: 'Mandatory',
      },
    ])
  }

  const updatePackageRowType = (index: number, formType: PolicyFormType) => {
    setPackageRows((rows) => rows.map((row, i) => {
      if (i !== index) return row
      return {
        ...row,
        formType,
        triggerConditionJson: formType === 'Conditional' ? (row.triggerConditionJson ?? DEFAULT_TRIGGER_CONDITION) : undefined,
      }
    }))
  }

  if (loadingTemplates || loadingPackages) return <LoadingSpinner />

  return (
    <div className="p-6 space-y-6 max-w-7xl">
      <div>
        <h1 className="text-xl font-semibold text-slate-900">Policy Forms & Packages</h1>
        <p className="text-sm text-slate-500 mt-1">Set up carrier forms and the policy packets used during issuance.</p>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-3 gap-5">
        <section className="bg-white border rounded-lg">
          <div className="px-4 py-3 border-b flex items-center gap-2">
            <FileText className="h-4 w-4 text-slate-400" />
            <h2 className="text-sm font-semibold text-slate-800">Form Library</h2>
          </div>
          <div className="p-4 space-y-3 border-b bg-slate-50">
            <div className="grid grid-cols-2 gap-2">
              <input value={templateForm.formNumber} onChange={(e) => setTemplateForm((f) => ({ ...f, formNumber: e.target.value }))} placeholder="Form number" className="border rounded px-2 py-1.5 text-sm" />
              <input value={templateForm.editionDate} onChange={(e) => setTemplateForm((f) => ({ ...f, editionDate: e.target.value }))} placeholder="Edition" className="border rounded px-2 py-1.5 text-sm" />
            </div>
            <input value={templateForm.name} onChange={(e) => setTemplateForm((f) => ({ ...f, name: e.target.value }))} placeholder="Form name" className="w-full border rounded px-2 py-1.5 text-sm" />
            <input value={templateForm.storagePath} onChange={(e) => setTemplateForm((f) => ({ ...f, storagePath: e.target.value }))} placeholder="Storage path or document reference" className="w-full border rounded px-2 py-1.5 text-sm" />
            <label className="flex items-center gap-2 text-sm text-slate-600">
              <input type="checkbox" checked={templateForm.isFillable} onChange={(e) => setTemplateForm((f) => ({ ...f, isFillable: e.target.checked }))} />
              Fillable PDF
            </label>
            <button onClick={() => createTemplate.mutate()} disabled={createTemplate.isPending || !templateForm.formNumber || !templateForm.name} className="inline-flex items-center gap-1.5 px-3 py-2 bg-blue-600 text-white text-sm rounded disabled:opacity-50">
              <Plus className="h-4 w-4" /> Add form
            </button>
            <div className="pt-2 border-t">
              <label className="block text-xs font-medium text-slate-500 mb-1">Test data policy</label>
              <select value={testPolicyId} onChange={(e) => setTestPolicyId(e.target.value)} className="w-full border rounded px-2 py-1.5 text-sm">
                {policyOptions.length === 0 ? (
                  <option value="">No policies found</option>
                ) : (
                  policyOptions.map((policy) => (
                    <option key={policy.id} value={policy.id}>{formatPolicyOption(policy)}</option>
                  ))
                )}
              </select>
            </div>
          </div>
          <div className="divide-y max-h-[520px] overflow-auto">
            {templates.map((template) => (
              <TemplateRow
                key={template.id}
                template={template}
                uploading={uploadTemplateFile.isPending}
                onUpload={(file) => uploadTemplateFile.mutate({ templateId: template.id, file })}
                onOpen={() => openTemplateFile(template.id)}
                onMap={() => selectTemplateMappings(template)}
                onTest={() => testMergeTemplate.mutate(template.id)}
                canTest={Boolean(testPolicyId && template.storagePath)}
                testing={testMergeTemplate.isPending}
              />
            ))}
          </div>
          {selectedTemplate && (
            <div className="border-t p-4 space-y-3">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="text-sm font-semibold text-slate-800">PDF field mappings</p>
                  <p className="text-xs text-slate-500">{selectedTemplate.formNumber} - {selectedTemplate.name}</p>
                </div>
                <button onClick={addMappingRow} className="inline-flex items-center gap-1 px-2 py-1 text-xs border rounded hover:bg-slate-50">
                  <Plus className="h-3 w-3" /> Add
                </button>
              </div>
              <div className="space-y-2">
                {mappingRows.length === 0 && (
                  <p className="text-xs text-slate-400 border rounded p-3">No mapped fields yet.</p>
                )}
                {mappingRows.map((row, index) => (
                  <div key={index} className="border rounded p-2 space-y-2">
                    <input
                      value={row.pdfFieldName}
                      onChange={(e) => setMappingRows((rows) => rows.map((r, i) => i === index ? { ...r, pdfFieldName: e.target.value } : r))}
                      placeholder="PDF field name"
                      className="w-full border rounded px-2 py-1.5 text-sm"
                    />
                    <select
                      value={row.dataPath}
                      onChange={(e) => setMappingRows((rows) => rows.map((r, i) => i === index ? { ...r, dataPath: e.target.value } : r))}
                      className="w-full border rounded px-2 py-1.5 text-sm"
                    >
                      {mappingDataPaths.map((path) => <option key={path} value={path}>{path}</option>)}
                    </select>
                    <div className="grid grid-cols-[1fr_auto] gap-2">
                      <select
                        value={row.format ?? ''}
                        onChange={(e) => setMappingRows((rows) => rows.map((r, i) => i === index ? { ...r, format: e.target.value || undefined } : r))}
                        className="border rounded px-2 py-1.5 text-sm"
                      >
                        {FORMAT_OPTIONS.map((format) => <option key={format || 'plain'} value={format}>{format || 'plain text'}</option>)}
                      </select>
                      <button onClick={() => setMappingRows((rows) => rows.filter((_, i) => i !== index))} className="px-2 border rounded text-slate-500 hover:text-red-600">
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </div>
                ))}
              </div>
              <button onClick={() => saveMappings.mutate()} disabled={saveMappings.isPending || !selectedTemplateId} className="inline-flex items-center gap-1.5 px-3 py-2 bg-blue-600 text-white text-sm rounded disabled:opacity-50">
                <Check className="h-4 w-4" /> Save mappings
              </button>
            </div>
          )}
        </section>

        <section className="bg-white border rounded-lg">
          <div className="px-4 py-3 border-b flex items-center gap-2">
            <PackagePlus className="h-4 w-4 text-slate-400" />
            <h2 className="text-sm font-semibold text-slate-800">Packages</h2>
          </div>
          <div className="p-4 space-y-3 border-b bg-slate-50">
            <select value={packageForm.carrierId} onChange={(e) => setPackageForm((f) => ({ ...f, carrierId: e.target.value }))} className="w-full border rounded px-2 py-1.5 text-sm">
              <option value="">Select carrier</option>
              {carriers.map((carrier) => <option key={carrier.id} value={carrier.id}>{carrier.name}</option>)}
            </select>
            <div className="grid grid-cols-2 gap-2">
              <select value={packageForm.lineOfBusiness} onChange={(e) => setPackageForm((f) => ({ ...f, lineOfBusiness: e.target.value as PolicyLineOfBusiness }))} className="border rounded px-2 py-1.5 text-sm">
                {ACTIVE_LOBS.map((lob) => <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>)}
              </select>
              <select value={packageForm.state} onChange={(e) => setPackageForm((f) => ({ ...f, state: e.target.value }))} className="border rounded px-2 py-1.5 text-sm">
                <option value="">State</option>
                {US_STATES.map((state) => <option key={state} value={state}>{state}</option>)}
              </select>
            </div>
            <div className="border rounded px-2 py-1.5 text-sm bg-white text-slate-700 min-h-9">
              {derivedPackageName || 'Package name will be Carrier - LOB - State'}
            </div>
            <button onClick={() => createPackage.mutate()} disabled={createPackage.isPending || !derivedPackageName} className="inline-flex items-center gap-1.5 px-3 py-2 bg-blue-600 text-white text-sm rounded disabled:opacity-50">
              <Plus className="h-4 w-4" /> Create package
            </button>
          </div>
          <div className="divide-y max-h-[520px] overflow-auto">
            {packages.map((pkg) => (
              <button key={pkg.id} onClick={() => selectPackage(pkg)} className={`w-full text-left p-3 hover:bg-slate-50 ${selectedPackageId === pkg.id ? 'bg-blue-50' : ''}`}>
                <p className="text-sm font-medium text-slate-800">{pkg.name}</p>
                <p className="text-xs text-slate-500">{pkg.carrierName} · {LOB_LABELS[pkg.lineOfBusiness]} · {pkg.state} · {pkg.forms.length} forms</p>
              </button>
            ))}
          </div>
        </section>

        <section className="bg-white border rounded-lg xl:col-span-1">
          <div className="px-4 py-3 border-b flex items-center justify-between">
            <h2 className="text-sm font-semibold text-slate-800">Package Forms</h2>
            {selectedPackage && <button onClick={addPackageRow} className="inline-flex items-center gap-1 px-2 py-1 text-xs border rounded hover:bg-slate-50"><Plus className="h-3 w-3" /> Add</button>}
          </div>
          {!selectedPackage ? (
            <p className="p-4 text-sm text-slate-400">Select or create a package to configure the form sequence.</p>
          ) : (
            <div className="p-4 space-y-3">
              <div>
                <p className="text-sm font-semibold text-slate-800">{selectedPackage.name}</p>
                <p className="text-xs text-slate-500">{selectedPackage.carrierName} · {selectedPackage.state}</p>
              </div>
              <div className="space-y-2">
                {packageRows.map((row, index) => (
                  <div key={index} className="border rounded p-2 space-y-2">
                    <div className="grid grid-cols-[52px_1fr] gap-2">
                      <input type="number" value={row.sequenceOrder} onChange={(e) => setPackageRows((rows) => rows.map((r, i) => i === index ? { ...r, sequenceOrder: Number(e.target.value) || index + 1 } : r))} className="border rounded px-2 py-1.5 text-sm" />
                      <select value={row.policyFormTemplateId} onChange={(e) => setPackageRows((rows) => rows.map((r, i) => i === index ? { ...r, policyFormTemplateId: e.target.value } : r))} className="border rounded px-2 py-1.5 text-sm">
                        {packageTemplates.map((template) => <option key={template.id} value={template.id}>{template.formNumber} - {template.name}</option>)}
                      </select>
                    </div>
                    <div className="grid grid-cols-[1fr_auto] gap-2">
                      <select value={row.formType} onChange={(e) => updatePackageRowType(index, e.target.value as PolicyFormType)} className="border rounded px-2 py-1.5 text-sm">
                        {FORM_TYPES.map((type) => <option key={type} value={type}>{type}</option>)}
                      </select>
                      <button onClick={() => setPackageRows((rows) => rows.filter((_, i) => i !== index).map((r, i) => ({ ...r, sequenceOrder: i + 1 })))} className="px-2 border rounded text-slate-500 hover:text-red-600">
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                    {row.formType === 'Conditional' && (
                      <ConditionalTriggerBuilder
                        value={row.triggerConditionJson}
                        onChange={(triggerConditionJson) => setPackageRows((rows) => rows.map((r, i) => i === index ? { ...r, triggerConditionJson } : r))}
                      />
                    )}
                  </div>
                ))}
              </div>
              <button onClick={() => savePackageRows.mutate()} disabled={savePackageRows.isPending} className="inline-flex items-center gap-1.5 px-3 py-2 bg-blue-600 text-white text-sm rounded disabled:opacity-50">
                <Check className="h-4 w-4" /> Save package forms
              </button>
            </div>
          )}
        </section>

        <section className="bg-white border rounded-lg xl:col-span-3">
          <div className="px-4 py-3 border-b">
            <h2 className="text-sm font-semibold text-slate-800">Approved Tags</h2>
            <p className="text-xs text-slate-500 mt-1">Use these tags in Word, HTML, proposal, email, and application templates.</p>
          </div>
          <div className="p-4 grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
            {Object.entries(tagCategories).map(([category, categoryTags]) => (
              <div key={category} className="border rounded">
                <div className="px-3 py-2 border-b bg-slate-50">
                  <p className="text-xs font-semibold text-slate-700">{category}</p>
                </div>
                <div className="divide-y max-h-80 overflow-auto">
                  {categoryTags.map((tag) => (
                    <button key={`${tag.repeatBlock ?? 'root'}-${tag.tag}`} type="button" onClick={() => copyTag(tag)} className="w-full text-left px-3 py-2 hover:bg-slate-50">
                      <p className="text-xs font-medium text-slate-700">{tag.label}</p>
                      <p className="text-[11px] text-slate-500 font-mono">{tag.isRepeatable && tag.repeatBlock ? `{{#${tag.repeatBlock}}} {{${tag.tag}}} {{/${tag.repeatBlock}}}` : `{{${tag.tag}}}`}</p>
                      <p className="text-[11px] text-slate-400">{tag.dataType}{tag.defaultFormat ? ` / ${tag.defaultFormat}` : ''}</p>
                    </button>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </section>
      </div>
    </div>
  )
}

function ConditionalTriggerBuilder({
  value,
  onChange,
}: {
  value?: string
  onChange: (value: string) => void
}) {
  const config = parseTriggerCondition(value)
  const field = getTriggerField(config.path)

  const update = (patch: Partial<TriggerConfig>) => {
    const nextField = patch.path ? getTriggerField(patch.path) : field
    const next: TriggerConfig = {
      ...config,
      ...patch,
      path: nextField.path,
    }

    if (patch.path) {
      if (nextField.kind === 'boolean') {
        next.operator = 'equals'
        next.value = true
      } else if (nextField.kind === 'lob') {
        next.operator = 'equals'
        next.value = 'InlandMarine'
      } else {
        next.operator = 'greaterThan'
        next.value = 0
      }
    }

    onChange(buildTriggerCondition(next))
  }

  return (
    <div className="rounded border border-blue-100 bg-blue-50 p-2 space-y-2">
      <div className="grid grid-cols-1 gap-2">
        <select
          value={config.path}
          onChange={(e) => update({ path: e.target.value as TriggerFieldPath })}
          className="border rounded px-2 py-1.5 text-sm bg-white"
        >
          {TRIGGER_FIELDS.map((triggerField) => (
            <option key={triggerField.path} value={triggerField.path}>{triggerField.label}</option>
          ))}
        </select>

        {field.kind === 'boolean' ? (
          <div className="grid grid-cols-2 gap-2">
            <select value={config.operator} onChange={(e) => update({ operator: e.target.value as TriggerOperator })} className="border rounded px-2 py-1.5 text-sm bg-white">
              <option value="equals">is</option>
              <option value="notEquals">is not</option>
            </select>
            <select value={String(config.value)} onChange={(e) => update({ value: e.target.value === 'true' })} className="border rounded px-2 py-1.5 text-sm bg-white">
              <option value="true">Yes</option>
              <option value="false">No</option>
            </select>
          </div>
        ) : field.kind === 'lob' ? (
          <div className="grid grid-cols-2 gap-2">
            <select value={config.operator} onChange={(e) => update({ operator: e.target.value as TriggerOperator })} className="border rounded px-2 py-1.5 text-sm bg-white">
              <option value="equals">is</option>
              <option value="notEquals">is not</option>
            </select>
            <select value={String(config.value)} onChange={(e) => update({ value: e.target.value })} className="border rounded px-2 py-1.5 text-sm bg-white">
              {ACTIVE_LOBS.map((lob) => <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>)}
            </select>
          </div>
        ) : (
          <div className="grid grid-cols-2 gap-2">
            <select value={config.operator} onChange={(e) => update({ operator: e.target.value as TriggerOperator })} className="border rounded px-2 py-1.5 text-sm bg-white">
              <option value="greaterThan">greater than</option>
              <option value="lessThan">less than</option>
              <option value="equals">equals</option>
            </select>
            <input
              type="number"
              value={Number(config.value)}
              onChange={(e) => update({ value: Number(e.target.value) || 0 })}
              className="border rounded px-2 py-1.5 text-sm bg-white"
            />
          </div>
        )}
      </div>
      <p className="text-[11px] text-blue-700">{describeTriggerCondition(config)}</p>
    </div>
  )
}

function TemplateRow({
  template,
  uploading,
  onUpload,
  onOpen,
  onMap,
  onTest,
  canTest,
  testing,
}: {
  template: PolicyFormTemplate
  uploading: boolean
  onUpload: (file: File) => void
  onOpen: () => void
  onMap: () => void
  onTest: () => void
  canTest: boolean
  testing: boolean
}) {
  return (
    <div className="p-3">
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-slate-800">{template.formNumber}</p>
          <p className="text-xs text-slate-500">{template.name}</p>
        </div>
        <span className={`text-xs px-2 py-0.5 rounded ${template.isActive ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500'}`}>
          {template.isActive ? 'Active' : 'Inactive'}
        </span>
      </div>
      <p className="text-xs text-slate-400 mt-1">{template.editionDate || 'No edition'} / {template.isFillable ? 'Fillable' : 'Static'} / {template.fieldMappings.length} mapped</p>
      {template.fileName && (
        <p className="text-xs text-slate-500 mt-1 truncate">{template.fileName}</p>
      )}
      <div className="flex items-center gap-2 mt-2">
        <label className="inline-flex items-center gap-1 px-2 py-1 border rounded text-xs text-slate-600 hover:bg-slate-50 cursor-pointer">
          <Upload className="h-3 w-3" />
          {template.fileName ? 'Replace' : 'Upload'}
          <input
            type="file"
            accept=".pdf,.doc,.docx,.html,.htm"
            disabled={uploading}
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0]
              e.target.value = ''
              if (file) onUpload(file)
            }}
          />
        </label>
        {template.storagePath && (
          <button type="button" onClick={onOpen} className="inline-flex items-center gap-1 px-2 py-1 border rounded text-xs text-slate-600 hover:bg-slate-50">
            <Download className="h-3 w-3" />
            Open
          </button>
        )}
        <button type="button" onClick={onTest} disabled={!canTest || testing} className="inline-flex items-center gap-1 px-2 py-1 border rounded text-xs text-slate-600 hover:bg-slate-50 disabled:opacity-50">
          <Play className="h-3 w-3" />
          Test
        </button>
        {template.isFillable && (
          <button type="button" onClick={onMap} className="inline-flex items-center gap-1 px-2 py-1 border rounded text-xs text-slate-600 hover:bg-slate-50">
            <Settings className="h-3 w-3" />
            Map
          </button>
        )}
      </div>
    </div>
  )
}

function formatPolicyOption(policy: PolicyListItem) {
  return `${policy.policyNumber} - ${policy.insuredName}`
}
