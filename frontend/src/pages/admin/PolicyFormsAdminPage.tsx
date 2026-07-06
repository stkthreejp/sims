import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Download, FileText, PackagePlus, Pencil, Play, Plus, Settings, Trash2, Upload } from 'lucide-react'
import { toast } from 'sonner'
import { policyFormsApi } from '@/api/policyForms.api'
import { carriersApi } from '@/api/carriers.api'
import { programConfigurationsApi } from '@/api/programConfigurations.api'
import { policiesApi } from '@/api/policies.api'
import { documentTemplatesApi } from '@/api/documentTemplates.api'
import { proposalDocumentsApi } from '@/api/proposalDocuments.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { getApiErrorMessage } from '@/lib/apiError'
import { ACTIVE_LOBS, LOB_LABELS, type PolicyLineOfBusiness } from '@/types/quote.types'
import type { PolicyListItem } from '@/types/policy.types'
import type { DocumentTag, PolicyFormFieldMappingUpsert, PolicyFormTemplate, PolicyFormType, PolicyPackageConfiguration, PolicyPackageConfigurationUpsert, PolicyPackageFormUpsert } from '@/types/policyForm.types'
import type { ProposalDocumentConfiguration, ProposalDocumentConfigurationUpsert, ProposalDocumentRole } from '@/types/proposalDocument.types'

const FORM_TYPES: PolicyFormType[] = ['Mandatory', 'Conditional', 'AdHoc']
const PROPOSAL_DOCUMENT_ROLE_LABELS: Record<ProposalDocumentRole, string> = {
  Proposal: 'Proposal',
  StateNotice: 'State notice',
}
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
  documentTemplateId: '',
}

const emptyPackage = {
  programConfigurationId: '',
  carrierId: '',
  lineOfBusiness: 'InlandMarine' as PolicyLineOfBusiness,
  state: '',
  name: '',
  isActive: true,
}

const emptyProposalDocument = (): ProposalDocumentConfigurationUpsert => ({
  programConfigurationId: '',
  carrierId: '',
  lineOfBusiness: 'InlandMarine',
  state: '',
  role: 'Proposal',
  documentTemplateId: '',
  sequenceOrder: 1,
  isActive: true,
  effectiveDate: null,
  expirationDate: null,
  notes: '',
})

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
  const [editingTemplateId, setEditingTemplateId] = useState<string | null>(null)
  const [packageForm, setPackageForm] = useState(emptyPackage)
  const [selectedPackageId, setSelectedPackageId] = useState<string | null>(null)
  const [packageRows, setPackageRows] = useState<PolicyPackageFormUpsert[]>([])
  const [selectedTemplateId, setSelectedTemplateId] = useState<string | null>(null)
  const [mappingRows, setMappingRows] = useState<PolicyFormFieldMappingUpsert[]>([])
  const [testPolicyId, setTestPolicyId] = useState('')
  const [proposalDocumentForm, setProposalDocumentForm] = useState<ProposalDocumentConfigurationUpsert>(emptyProposalDocument())
  const [editingProposalDocumentId, setEditingProposalDocumentId] = useState<string | null>(null)

  const { data: templates = [], isLoading: loadingTemplates } = useQuery({
    queryKey: ['policy-form-templates'],
    queryFn: () => policyFormsApi.getTemplates(true),
  })

  const { data: packages = [], isLoading: loadingPackages } = useQuery({
    queryKey: ['policy-form-packages'],
    queryFn: () => policyFormsApi.getPackages({ includeInactive: true }),
  })

  const { data: proposalDocuments = [], isLoading: loadingProposalDocuments } = useQuery({
    queryKey: ['proposal-document-configurations'],
    queryFn: () => proposalDocumentsApi.getAll(true),
  })

  const { data: proposalTemplates = [] } = useQuery({
    queryKey: ['document-templates', 'quote', 'proposal-documents'],
    queryFn: () => documentTemplatesApi.getAll('Quote', false),
  })

  // F16: Policy-scoped Doc Library templates a policy form can be authored from.
  const { data: policyDocTemplates = [] } = useQuery({
    queryKey: ['document-templates', 'policy', 'form-authoring'],
    queryFn: () => documentTemplatesApi.getAll('Policy', false),
  })

  const { data: carriers = [] } = useQuery({
    queryKey: ['carriers', 'active'],
    queryFn: () => carriersApi.getAll(true),
  })

  const { data: programs = [] } = useQuery({
    queryKey: ['program-configurations', 'options', 'active'],
    queryFn: () => programConfigurationsApi.getOptions(false),
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
  const policyOptions = useMemo(() => policyPage?.items ?? [], [policyPage])

  const packageTemplates = useMemo(() => templates.filter((t) => t.isActive), [templates])
  const activeProposalTemplates = useMemo(() => proposalTemplates.filter((template) => template.kind !== 'Email'), [proposalTemplates])
  const derivedPackageName = useMemo(() => {
    const programName = programs.find((program) => program.id === packageForm.programConfigurationId)?.name
    const carrierName = carriers.find((carrier) => carrier.id === packageForm.carrierId)?.name
    if (!carrierName || !packageForm.lineOfBusiness) return ''
    const stateLabel = packageForm.state || 'All States'
    return [programName, carrierName, LOB_LABELS[packageForm.lineOfBusiness], stateLabel].filter(Boolean).join(' - ')
  }, [carriers, packageForm.carrierId, packageForm.lineOfBusiness, packageForm.programConfigurationId, packageForm.state, programs])
  const selectedPackageProgram = programs.find((program) => program.id === packageForm.programConfigurationId)
  const packageCarrierOptions = useMemo(() => (
    selectedPackageProgram
      ? selectedPackageProgram.carriers
          .filter((programCarrier) => programCarrier.isActive)
          .map((programCarrier) => ({ id: programCarrier.carrierId, name: programCarrier.carrierName }))
      : carriers
  ), [carriers, selectedPackageProgram])
  const selectedPackageProgramCarrier = selectedPackageProgram?.carriers.find((programCarrier) =>
    programCarrier.carrierId === packageForm.carrierId
  )
  const packageLobOptions = useMemo(() => (
    selectedPackageProgramCarrier
      ? selectedPackageProgramCarrier.linesOfBusiness
          .filter((lob) => lob.isActive)
          .map((lob) => lob.lineOfBusiness)
      : ACTIVE_LOBS
  ), [selectedPackageProgramCarrier])
  const selectedPackageLob = selectedPackageProgramCarrier?.linesOfBusiness.find((lob) =>
    lob.lineOfBusiness === packageForm.lineOfBusiness
  )
  const packageStateOptions = useMemo(() => (
    selectedPackageLob
      ? selectedPackageLob.states
          .filter((state) => state.isActive)
          .map((state) => state.stateCode)
      : US_STATES
  ), [selectedPackageLob])
  const selectedProposalProgram = programs.find((program) => program.id === proposalDocumentForm.programConfigurationId)
  const proposalCarrierOptions = useMemo(() => (
    selectedProposalProgram
      ? selectedProposalProgram.carriers
          .filter((programCarrier) => programCarrier.isActive)
          .map((programCarrier) => ({ id: programCarrier.carrierId, name: programCarrier.carrierName }))
      : carriers
  ), [carriers, selectedProposalProgram])
  const selectedProposalProgramCarrier = selectedProposalProgram?.carriers.find((programCarrier) =>
    programCarrier.carrierId === proposalDocumentForm.carrierId
  )
  const proposalLobOptions = useMemo(() => (
    selectedProposalProgramCarrier
      ? selectedProposalProgramCarrier.linesOfBusiness
          .filter((lob) => lob.isActive)
          .map((lob) => lob.lineOfBusiness)
      : ACTIVE_LOBS
  ), [selectedProposalProgramCarrier])
  const selectedProposalLob = selectedProposalProgramCarrier?.linesOfBusiness.find((lob) =>
    lob.lineOfBusiness === proposalDocumentForm.lineOfBusiness
  )
  const proposalStateOptions = useMemo(() => (
    selectedProposalLob
      ? selectedProposalLob.states
          .filter((state) => state.isActive)
          .map((state) => state.stateCode)
      : US_STATES
  ), [selectedProposalLob])
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

  useEffect(() => {
    if (!selectedProposalProgram) return
    setProposalDocumentForm((form) => {
      const carrierId = proposalCarrierOptions.some((carrier) => carrier.id === form.carrierId)
        ? form.carrierId
        : proposalCarrierOptions[0]?.id ?? ''
      const carrierSetup = selectedProposalProgram.carriers.find((programCarrier) => programCarrier.carrierId === carrierId)
      const lobOptions = carrierSetup
        ? carrierSetup.linesOfBusiness.filter((lob) => lob.isActive).map((lob) => lob.lineOfBusiness)
        : []
      const lineOfBusiness = lobOptions.includes(form.lineOfBusiness)
        ? form.lineOfBusiness
        : lobOptions[0] ?? form.lineOfBusiness
      const lobSetup = carrierSetup?.linesOfBusiness.find((lob) => lob.lineOfBusiness === lineOfBusiness)
      const stateOptions = lobSetup
        ? lobSetup.states.filter((state) => state.isActive).map((state) => state.stateCode)
        : []
      const state = form.state && stateOptions.includes(form.state) ? form.state : ''

      if (carrierId === form.carrierId && lineOfBusiness === form.lineOfBusiness && state === form.state) {
        return form
      }

      return { ...form, carrierId, lineOfBusiness, state }
    })
  }, [proposalCarrierOptions, proposalDocumentForm.carrierId, proposalDocumentForm.lineOfBusiness, proposalDocumentForm.state, selectedProposalProgram])

  useEffect(() => {
    if (!selectedPackageProgram) return
    setPackageForm((form) => {
      const carrierId = packageCarrierOptions.some((carrier) => carrier.id === form.carrierId)
        ? form.carrierId
        : packageCarrierOptions[0]?.id ?? ''
      const carrierSetup = selectedPackageProgram.carriers.find((programCarrier) => programCarrier.carrierId === carrierId)
      const lobOptions = carrierSetup
        ? carrierSetup.linesOfBusiness.filter((lob) => lob.isActive).map((lob) => lob.lineOfBusiness)
        : []
      const lineOfBusiness = lobOptions.includes(form.lineOfBusiness)
        ? form.lineOfBusiness
        : lobOptions[0] ?? form.lineOfBusiness
      const lobSetup = carrierSetup?.linesOfBusiness.find((lob) => lob.lineOfBusiness === lineOfBusiness)
      const stateOptions = lobSetup
        ? lobSetup.states.filter((state) => state.isActive).map((state) => state.stateCode)
        : []
      const state = form.state && stateOptions.includes(form.state) ? form.state : ''

      if (carrierId === form.carrierId && lineOfBusiness === form.lineOfBusiness && state === form.state) {
        return form
      }

      return { ...form, carrierId, lineOfBusiness, state }
    })
  }, [packageCarrierOptions, packageForm.carrierId, packageForm.lineOfBusiness, packageForm.state, selectedPackageProgram])

  const createTemplate = useMutation({
    mutationFn: () => {
      const payload = {
        ...templateForm,
        editionDate: templateForm.editionDate || undefined,
        fileName: templateForm.fileName || undefined,
        contentType: templateForm.contentType || undefined,
        storagePath: templateForm.storagePath || undefined,
        notes: templateForm.notes || undefined,
        documentTemplateId: templateForm.documentTemplateId || null,
      }
      return editingTemplateId
        ? policyFormsApi.updateTemplate(editingTemplateId, payload)
        : policyFormsApi.createTemplate(payload)
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policy-form-templates'] })
      setTemplateForm(emptyTemplate)
      setEditingTemplateId(null)
      toast.success(editingTemplateId ? 'Policy form updated' : 'Policy form saved')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Policy form could not be saved')),
  })

  const toggleTemplateActive = useMutation({
    mutationFn: (template: PolicyFormTemplate) => policyFormsApi.updateTemplate(template.id, {
      formNumber: template.formNumber,
      name: template.name,
      editionDate: template.editionDate ?? undefined,
      documentType: template.documentType,
      fileName: template.fileName ?? undefined,
      contentType: template.contentType ?? undefined,
      storagePath: template.storagePath ?? undefined,
      isFillable: template.isFillable,
      isActive: !template.isActive,
      notes: template.notes ?? undefined,
      documentTemplateId: template.documentTemplateId,
    }),
    onSuccess: (saved) => {
      qc.invalidateQueries({ queryKey: ['policy-form-templates'] })
      toast.success(saved.isActive ? 'Policy form activated' : 'Policy form deactivated')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Policy form status could not be changed')),
  })

  const uploadTemplateFile = useMutation({
    mutationFn: ({ templateId, file }: { templateId: string; file: File }) =>
      policyFormsApi.uploadTemplateFile(templateId, file),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policy-form-templates'] })
      toast.success('Policy form file uploaded')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Policy form file could not be uploaded')),
  })

  const testMergeTemplate = useMutation({
    mutationFn: (templateId: string) => policyFormsApi.testMergeTemplate(templateId, testPolicyId),
    onSuccess: (data) => {
      window.open(data.url, '_blank', 'noopener,noreferrer')
      toast.success('Test merge created')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Test merge could not be created')),
  })

  const openTemplateFile = async (templateId: string) => {
    try {
      const data = await policyFormsApi.getTemplateDownloadUrl(templateId)
      window.open(data.url, '_blank', 'noopener,noreferrer')
    } catch (e: any) {
      toast.error(getApiErrorMessage(e, 'Policy form file could not be opened'))
    }
  }

  const createPackage = useMutation({
    mutationFn: () => policyFormsApi.createPackage({
      ...packageForm,
      programConfigurationId: packageForm.programConfigurationId || null,
      name: derivedPackageName,
      state: packageForm.state ? packageForm.state.toUpperCase() : null,
    }),
    onSuccess: (saved) => {
      qc.invalidateQueries({ queryKey: ['policy-form-packages'] })
      setPackageForm(emptyPackage)
      setSelectedPackageId(saved.id)
      setPackageRows([])
      toast.success('Package created')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Package could not be saved')),
  })

  const updatePackage = useMutation({
    mutationFn: ({ pkg, changes }: { pkg: PolicyPackageConfiguration; changes: Partial<PolicyPackageConfigurationUpsert> }) =>
      policyFormsApi.updatePackage(pkg.id, {
        programConfigurationId: pkg.programConfigurationId,
        carrierId: pkg.carrierId,
        lineOfBusiness: pkg.lineOfBusiness,
        state: pkg.state,
        name: pkg.name,
        isActive: pkg.isActive,
        ...changes,
      }),
    onSuccess: (saved) => {
      qc.invalidateQueries({ queryKey: ['policy-form-packages'] })
      toast.success(saved.isActive ? 'Package updated' : 'Package deactivated')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Package could not be updated')),
  })

  const savePackageRows = useMutation({
    mutationFn: () => {
      if (!selectedPackageId) throw new Error('Select a package before saving forms.')
      if (packageRows.some((row) => !row.policyFormTemplateId)) throw new Error('Each package form needs a selected form.')

      return policyFormsApi.replacePackageForms(selectedPackageId, packageRows.map((row, index) => ({
        ...row,
        sequenceOrder: Number(row.sequenceOrder) || index + 1,
        triggerConditionJson: row.formType === 'Conditional'
          ? buildTriggerCondition(parseTriggerCondition(row.triggerConditionJson))
          : undefined,
      })))
    },
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
    onError: (e: any) => toast.error(getApiErrorMessage(e, e?.message ?? 'Package forms could not be saved')),
  })

  const saveMappings = useMutation({
    mutationFn: () => policyFormsApi.replaceMappings(selectedTemplateId!, mappingRows),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policy-form-templates'] })
      toast.success('Field mappings saved')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Field mappings could not be saved')),
  })

  const saveProposalDocument = useMutation({
    mutationFn: () => {
      const payload = cleanProposalDocument(proposalDocumentForm)
      return editingProposalDocumentId
        ? proposalDocumentsApi.update(editingProposalDocumentId, payload)
        : proposalDocumentsApi.create(payload)
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['proposal-document-configurations'] })
      setProposalDocumentForm(emptyProposalDocument())
      setEditingProposalDocumentId(null)
      toast.success('Proposal document setup saved')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Proposal document setup could not be saved')),
  })

  const deleteProposalDocument = useMutation({
    mutationFn: proposalDocumentsApi.delete,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['proposal-document-configurations'] })
      toast.success('Proposal document setup removed')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Proposal document setup could not be removed')),
  })

  const renamePackage = (pkg: PolicyPackageConfiguration) => {
    const name = prompt('Package name', pkg.name)?.trim()
    if (!name || name === pkg.name) return
    updatePackage.mutate({ pkg, changes: { name } })
  }

  const togglePackageActive = (pkg: PolicyPackageConfiguration) => {
    updatePackage.mutate({ pkg, changes: { isActive: !pkg.isActive } })
  }

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

  const editTemplate = (template: PolicyFormTemplate) => {
    setEditingTemplateId(template.id)
    setTemplateForm({
      formNumber: template.formNumber,
      name: template.name,
      editionDate: template.editionDate ?? '',
      documentType: template.documentType as typeof emptyTemplate.documentType,
      fileName: template.fileName ?? '',
      contentType: template.contentType ?? '',
      storagePath: template.storagePath ?? '',
      isFillable: template.isFillable,
      isActive: template.isActive,
      notes: template.notes ?? '',
      documentTemplateId: template.documentTemplateId ?? '',
    })
  }

  const cancelTemplateEdit = () => {
    setEditingTemplateId(null)
    setTemplateForm(emptyTemplate)
  }

  const selectTemplateMappings = (template: PolicyFormTemplate) => {
    setSelectedTemplateId(template.id)
    setMappingRows(template.fieldMappings.map((m) => ({
      pdfFieldName: m.pdfFieldName,
      dataPath: m.dataPath,
      format: m.format ?? undefined,
    })))
  }

  const editProposalDocument = (configuration: ProposalDocumentConfiguration) => {
    setEditingProposalDocumentId(configuration.id)
    setProposalDocumentForm({
      programConfigurationId: configuration.programConfigurationId ?? '',
      carrierId: configuration.carrierId,
      lineOfBusiness: configuration.lineOfBusiness,
      state: configuration.state ?? '',
      role: configuration.role,
      documentTemplateId: configuration.documentTemplateId,
      sequenceOrder: configuration.sequenceOrder,
      isActive: configuration.isActive,
      effectiveDate: configuration.effectiveDate,
      expirationDate: configuration.expirationDate,
      notes: configuration.notes ?? '',
    })
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

  if (loadingTemplates || loadingPackages || loadingProposalDocuments) return <LoadingSpinner />

  return (
    <div className="p-6 space-y-6 max-w-7xl">
      <div>
        <h1 className="text-xl font-semibold text-slate-900">Policy Forms & Packages</h1>
        <p className="text-sm text-slate-500 mt-1">Set up Program-aware carrier forms, proposal notices, and policy packets.</p>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-3 gap-5">
        <section className="bg-white border rounded-lg">
          <div className="px-4 py-3 border-b flex items-center gap-2">
            <FileText className="h-4 w-4 text-slate-400" />
            <h2 className="text-sm font-semibold text-slate-800">Form Library</h2>
            {editingTemplateId && (
              <button type="button" onClick={cancelTemplateEdit} className="ml-auto text-xs text-slate-500 hover:text-slate-700">
                Cancel edit
              </button>
            )}
          </div>
          <div className="p-4 space-y-3 border-b bg-slate-50">
            <div className="grid grid-cols-2 gap-2">
              <input value={templateForm.formNumber} onChange={(e) => setTemplateForm((f) => ({ ...f, formNumber: e.target.value }))} placeholder="Form number" className="border rounded px-2 py-1.5 text-sm" />
              <input value={templateForm.editionDate} onChange={(e) => setTemplateForm((f) => ({ ...f, editionDate: e.target.value }))} placeholder="Edition" className="border rounded px-2 py-1.5 text-sm" />
            </div>
            <input value={templateForm.name} onChange={(e) => setTemplateForm((f) => ({ ...f, name: e.target.value }))} placeholder="Form name" className="w-full border rounded px-2 py-1.5 text-sm" />
            <select value={templateForm.documentTemplateId} onChange={(e) => setTemplateForm((f) => ({ ...f, documentTemplateId: e.target.value }))} className="w-full border rounded px-2 py-1.5 text-sm">
              <option value="">Uploaded file (PDF / DOCX / HTML)</option>
              {policyDocTemplates.map((t) => <option key={t.id} value={t.id}>Authored template: {t.name}</option>)}
            </select>
            {templateForm.documentTemplateId ? (
              <p className="text-xs text-slate-400">Authored in the Document Library (visual builder — tags + repeat blocks). Rendered into the packet automatically; no file upload needed.</p>
            ) : (
              <>
                <input value={templateForm.storagePath} onChange={(e) => setTemplateForm((f) => ({ ...f, storagePath: e.target.value }))} placeholder="Storage path or document reference" className="w-full border rounded px-2 py-1.5 text-sm" />
                <label className="flex items-center gap-2 text-sm text-slate-600">
                  <input type="checkbox" checked={templateForm.isFillable} onChange={(e) => setTemplateForm((f) => ({ ...f, isFillable: e.target.checked }))} />
                  Fillable PDF
                </label>
              </>
            )}
            <button onClick={() => createTemplate.mutate()} disabled={createTemplate.isPending || !templateForm.formNumber || !templateForm.name} className="inline-flex items-center gap-1.5 px-3 py-2 bg-blue-600 text-white text-sm rounded disabled:opacity-50">
              {editingTemplateId ? <><Check className="h-4 w-4" /> Save form</> : <><Plus className="h-4 w-4" /> Add form</>}
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
                onEdit={() => editTemplate(template)}
                onToggleActive={() => toggleTemplateActive.mutate(template)}
                togglingActive={toggleTemplateActive.isPending}
                canTest={Boolean(testPolicyId && (template.storagePath || template.documentTemplateId))}
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
            <select value={packageForm.programConfigurationId} onChange={(e) => setPackageForm((f) => ({ ...f, programConfigurationId: e.target.value }))} className="w-full border rounded px-2 py-1.5 text-sm">
              <option value="">Any program</option>
              {programs.map((program) => <option key={program.id} value={program.id}>{program.name}</option>)}
            </select>
            <select value={packageForm.carrierId} onChange={(e) => setPackageForm((f) => ({ ...f, carrierId: e.target.value }))} className="w-full border rounded px-2 py-1.5 text-sm">
              <option value="">Select carrier</option>
              {packageCarrierOptions.map((carrier) => <option key={carrier.id} value={carrier.id}>{carrier.name}</option>)}
            </select>
            <div className="grid grid-cols-2 gap-2">
              <select value={packageForm.lineOfBusiness} onChange={(e) => setPackageForm((f) => ({ ...f, lineOfBusiness: e.target.value as PolicyLineOfBusiness }))} className="border rounded px-2 py-1.5 text-sm">
                {packageLobOptions.map((lob) => <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>)}
              </select>
              <select value={packageForm.state} onChange={(e) => setPackageForm((f) => ({ ...f, state: e.target.value }))} className="border rounded px-2 py-1.5 text-sm">
                <option value="">Any state</option>
                {packageStateOptions.map((state) => <option key={state} value={state}>{state}</option>)}
              </select>
            </div>
            <div className="border rounded px-2 py-1.5 text-sm bg-white text-slate-700 min-h-9">
              {derivedPackageName || 'Package name will be Program - Carrier - LOB - State/All States'}
            </div>
            <button onClick={() => createPackage.mutate()} disabled={createPackage.isPending || !derivedPackageName} className="inline-flex items-center gap-1.5 px-3 py-2 bg-blue-600 text-white text-sm rounded disabled:opacity-50">
              <Plus className="h-4 w-4" /> Create package
            </button>
          </div>
          <div className="divide-y max-h-[520px] overflow-auto">
            {packages.map((pkg) => (
              <div key={pkg.id} className={`p-3 ${selectedPackageId === pkg.id ? 'bg-blue-50' : ''}`}>
                <button onClick={() => selectPackage(pkg)} className="w-full text-left">
                  <div className="flex items-center justify-between gap-2">
                    <p className="text-sm font-medium text-slate-800">{pkg.name}</p>
                    <span className={`text-xs px-2 py-0.5 rounded shrink-0 ${pkg.isActive ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500'}`}>
                      {pkg.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </div>
                  <p className="text-xs text-slate-500">{pkg.carrierName} · {LOB_LABELS[pkg.lineOfBusiness]} · {pkg.state ?? 'All States'} · {pkg.forms.length} forms</p>
                </button>
                <div className="flex items-center gap-2 mt-2">
                  <button type="button" onClick={() => renamePackage(pkg)} className="inline-flex items-center gap-1 px-2 py-1 border rounded text-xs text-slate-600 hover:bg-slate-50">
                    <Pencil className="h-3 w-3" />
                    Rename
                  </button>
                  <button type="button" onClick={() => togglePackageActive(pkg)} disabled={updatePackage.isPending} className="inline-flex items-center gap-1 px-2 py-1 border rounded text-xs text-slate-600 hover:bg-slate-50 disabled:opacity-50">
                    <Check className="h-3 w-3" />
                    {pkg.isActive ? 'Deactivate' : 'Activate'}
                  </button>
                </div>
              </div>
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
                <p className="text-xs text-slate-500">{selectedPackage.carrierName} · {selectedPackage.state ?? 'All States'}</p>
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
          <div className="px-4 py-3 border-b flex items-center gap-2">
            <FileText className="h-4 w-4 text-slate-400" />
            <h2 className="text-sm font-semibold text-slate-800">Proposal Documents</h2>
          </div>
          <div className="p-4 grid grid-cols-1 lg:grid-cols-[360px_1fr] gap-4">
            <div className="border rounded-lg p-3 space-y-3 bg-slate-50">
              <div className="flex items-center justify-between gap-2">
                <p className="text-sm font-semibold text-slate-800">{editingProposalDocumentId ? 'Edit setup' : 'New setup'}</p>
                {editingProposalDocumentId && (
                  <button
                    type="button"
                    onClick={() => { setEditingProposalDocumentId(null); setProposalDocumentForm(emptyProposalDocument()) }}
                    className="text-xs text-slate-500 hover:text-slate-700"
                  >
                    Cancel
                  </button>
                )}
              </div>
              <select value={proposalDocumentForm.programConfigurationId ?? ''} onChange={(e) => setProposalDocumentForm((f) => ({ ...f, programConfigurationId: e.target.value }))} className="w-full border rounded px-2 py-1.5 text-sm">
                <option value="">Any program</option>
                {programs.map((program) => <option key={program.id} value={program.id}>{program.name}</option>)}
              </select>
              <select value={proposalDocumentForm.carrierId} onChange={(e) => setProposalDocumentForm((f) => ({ ...f, carrierId: e.target.value }))} className="w-full border rounded px-2 py-1.5 text-sm">
                <option value="">Select carrier</option>
                {proposalCarrierOptions.map((carrier) => <option key={carrier.id} value={carrier.id}>{carrier.name}</option>)}
              </select>
              <div className="grid grid-cols-2 gap-2">
                <select value={proposalDocumentForm.lineOfBusiness} onChange={(e) => setProposalDocumentForm((f) => ({ ...f, lineOfBusiness: e.target.value as PolicyLineOfBusiness }))} className="border rounded px-2 py-1.5 text-sm">
                  {proposalLobOptions.map((lob) => <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>)}
                </select>
                <select value={proposalDocumentForm.state ?? ''} onChange={(e) => setProposalDocumentForm((f) => ({ ...f, state: e.target.value }))} className="border rounded px-2 py-1.5 text-sm">
                  <option value="">{proposalDocumentForm.role === 'StateNotice' ? 'Select state' : 'Any state'}</option>
                  {proposalStateOptions.map((state) => <option key={state} value={state}>{state}</option>)}
                </select>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <select value={proposalDocumentForm.role} onChange={(e) => setProposalDocumentForm((f) => ({ ...f, role: e.target.value as ProposalDocumentRole }))} className="border rounded px-2 py-1.5 text-sm">
                  {Object.entries(PROPOSAL_DOCUMENT_ROLE_LABELS).map(([role, label]) => <option key={role} value={role}>{label}</option>)}
                </select>
                <input type="number" min={1} value={proposalDocumentForm.sequenceOrder} onChange={(e) => setProposalDocumentForm((f) => ({ ...f, sequenceOrder: Number(e.target.value) || 1 }))} className="border rounded px-2 py-1.5 text-sm" />
              </div>
              <select value={proposalDocumentForm.documentTemplateId} onChange={(e) => setProposalDocumentForm((f) => ({ ...f, documentTemplateId: e.target.value }))} className="w-full border rounded px-2 py-1.5 text-sm">
                <option value="">Select quote template</option>
                {activeProposalTemplates.map((template) => <option key={template.id} value={template.id}>{template.name}</option>)}
              </select>
              <div className="grid grid-cols-2 gap-2">
                <input type="date" value={proposalDocumentForm.effectiveDate ?? ''} onChange={(e) => setProposalDocumentForm((f) => ({ ...f, effectiveDate: e.target.value || null }))} className="border rounded px-2 py-1.5 text-sm" />
                <input type="date" value={proposalDocumentForm.expirationDate ?? ''} onChange={(e) => setProposalDocumentForm((f) => ({ ...f, expirationDate: e.target.value || null }))} className="border rounded px-2 py-1.5 text-sm" />
              </div>
              <textarea value={proposalDocumentForm.notes ?? ''} onChange={(e) => setProposalDocumentForm((f) => ({ ...f, notes: e.target.value }))} placeholder="Notes" rows={2} className="w-full border rounded px-2 py-1.5 text-sm" />
              <label className="flex items-center gap-2 text-sm text-slate-600">
                <input type="checkbox" checked={proposalDocumentForm.isActive} onChange={(e) => setProposalDocumentForm((f) => ({ ...f, isActive: e.target.checked }))} />
                Active
              </label>
              <button
                onClick={() => saveProposalDocument.mutate()}
                disabled={saveProposalDocument.isPending || !proposalDocumentForm.carrierId || !proposalDocumentForm.documentTemplateId || (proposalDocumentForm.role === 'StateNotice' && !proposalDocumentForm.state)}
                className="inline-flex items-center gap-1.5 px-3 py-2 bg-blue-600 text-white text-sm rounded disabled:opacity-50"
              >
                <Check className="h-4 w-4" /> Save setup
              </button>
            </div>

            <div className="border rounded-lg overflow-hidden">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-xs text-slate-500 border-b bg-slate-50">
                    <th className="px-4 py-2 font-medium">Scope</th>
                    <th className="px-4 py-2 font-medium">Role</th>
                    <th className="px-4 py-2 font-medium">Template</th>
                    <th className="px-4 py-2 font-medium">Dates</th>
                    <th className="px-4 py-2 font-medium">Status</th>
                    <th className="px-4 py-2" />
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {proposalDocuments.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="px-4 py-6 text-sm text-slate-400">No proposal document setup yet.</td>
                    </tr>
                  ) : (
                    proposalDocuments.map((configuration) => (
                      <tr key={configuration.id} className="hover:bg-slate-50">
                        <td className="px-4 py-3">
                          <p className="font-medium text-slate-800">{configuration.programName ?? 'Any program'}</p>
                          <p className="text-xs text-slate-500">{configuration.carrierName} / {configuration.lineOfBusinessLabel} / {configuration.state ?? 'Any state'}</p>
                        </td>
                        <td className="px-4 py-3 text-slate-700">
                          <p>{PROPOSAL_DOCUMENT_ROLE_LABELS[configuration.role]}</p>
                          <p className="text-xs text-slate-400">#{configuration.sequenceOrder}</p>
                        </td>
                        <td className="px-4 py-3 text-slate-700">{configuration.documentTemplateName}</td>
                        <td className="px-4 py-3 text-slate-600">{dateRange(configuration.effectiveDate, configuration.expirationDate)}</td>
                        <td className="px-4 py-3">
                          {configuration.isActive ? <span className="px-1.5 py-0.5 rounded bg-emerald-100 text-emerald-700 font-medium">Active</span> : <span className="px-1.5 py-0.5 rounded bg-slate-100 text-slate-500">Inactive</span>}
                        </td>
                        <td className="px-4 py-3 text-right">
                          <div className="flex items-center justify-end gap-2">
                            <button onClick={() => editProposalDocument(configuration)} className="sims-icon-btn hover:text-sky-600" title="Edit setup">
                              <Pencil className="h-3.5 w-3.5" />
                            </button>
                            <button
                              onClick={() => { if (confirm('Remove this proposal document setup?')) deleteProposalDocument.mutate(configuration.id) }}
                              className="sims-icon-btn hover:text-red-600"
                              title="Remove setup"
                            >
                              <Trash2 className="h-3.5 w-3.5" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
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
  onEdit,
  onToggleActive,
  togglingActive,
  canTest,
  testing,
}: {
  template: PolicyFormTemplate
  uploading: boolean
  onUpload: (file: File) => void
  onOpen: () => void
  onMap: () => void
  onTest: () => void
  onEdit: () => void
  onToggleActive: () => void
  togglingActive: boolean
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
      <div className="flex flex-wrap items-center gap-2 mt-2">
        <button type="button" onClick={onEdit} className="inline-flex items-center gap-1 px-2 py-1 border rounded text-xs text-slate-600 hover:bg-slate-50">
          <Pencil className="h-3 w-3" />
          Edit
        </button>
        <button type="button" onClick={onToggleActive} disabled={togglingActive} className="inline-flex items-center gap-1 px-2 py-1 border rounded text-xs text-slate-600 hover:bg-slate-50 disabled:opacity-50">
          <Check className="h-3 w-3" />
          {template.isActive ? 'Deactivate' : 'Activate'}
        </button>
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

function dateRange(effectiveDate?: string | null, expirationDate?: string | null) {
  if (!effectiveDate && !expirationDate) return 'Any date'
  return `${effectiveDate ?? 'Any'} - ${expirationDate ?? 'Open'}`
}

function cleanProposalDocument(form: ProposalDocumentConfigurationUpsert): ProposalDocumentConfigurationUpsert {
  return {
    ...form,
    programConfigurationId: form.programConfigurationId || null,
    state: form.state || null,
    sequenceOrder: Number(form.sequenceOrder) || 1,
    effectiveDate: form.effectiveDate || null,
    expirationDate: form.expirationDate || null,
    notes: form.notes?.trim() ? form.notes.trim() : null,
  }
}
