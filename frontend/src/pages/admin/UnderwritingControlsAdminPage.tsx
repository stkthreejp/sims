import { useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Archive, Check, FileSearch, Loader2, Pencil, Plus, Rocket, Save, ShieldAlert, Trash2, X } from 'lucide-react'
import { toast } from 'sonner'
import { attachmentsApi } from '@/api/attachments.api'
import { carriersApi } from '@/api/carriers.api'
import { programConfigurationsApi } from '@/api/programConfigurations.api'
import { underwritingGuidelinesApi } from '@/api/underwritingGuidelines.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ErrorState } from '@/components/common/ErrorState'
import { getApiErrorMessage } from '@/lib/apiError'
import { PageHeader } from '@/components/common/PageHeader'
import { US_STATES } from '@/constants/usStates'
import type { Attachment } from '@/types/attachment.types'
import { ACTIVE_LOBS, LOB_LABELS, type PolicyLineOfBusiness } from '@/types/quote.types'
import type {
  CreateUnderwritingGuidelineControlRequest,
  CreateUnderwritingGuidelineDocumentRequest,
  UnderwritingControlItemType,
  UnderwritingControlSeverity,
  UnderwritingControlStage,
  UnderwritingControlStatus,
  UnderwritingGuidelineControl,
  UnderwritingGuidelineDocument,
  UpdateUnderwritingGuidelineControlRequest,
} from '@/types/underwritingGuidelines.types'

const STATE_OPTIONS = ['ALL', ...US_STATES]

const ITEM_TYPES: UnderwritingControlItemType[] = ['AppetiteRule', 'ReferralTrigger', 'AuthorityLimit', 'DocumentChecklistItem', 'AppetiteNote']
const STAGES: UnderwritingControlStage[] = ['Submission', 'Quote', 'Bind', 'Issue', 'PostBind', 'Renewal']
const SEVERITIES: UnderwritingControlSeverity[] = ['Informational', 'Warning', 'ReferralRequired', 'HardBlock']
const REVIEW_STATUSES: Array<UnderwritingControlStatus | 'All'> = ['All', 'AiSuggested', 'Draft', 'Approved', 'Published', 'Rejected', 'Retired']
const CONDITION_OPERATORS = ['>', '>=', '<', '<=', '==', '!=', 'contains', 'notContains'] as const
const NUMERIC_CONDITION_OPERATORS: ConditionOperator[] = ['>', '>=', '<', '<=', '==', '!=']
const CONDITION_FIELDS = [
  { key: 'largestSingleItemValue', label: 'Largest single item value', kind: 'currency' },
  { key: 'totalInsuredValue', label: 'Total insured value', kind: 'currency' },
  { key: 'premiumAmount', label: 'Premium amount', kind: 'currency' },
  { key: 'totalPremium', label: 'Total premium', kind: 'currency' },
  { key: 'lossRatio', label: 'Loss ratio', kind: 'percent' },
  { key: 'driverCount', label: 'Driver count', kind: 'number' },
  { key: 'vehicleCount', label: 'Vehicle count', kind: 'number' },
  { key: 'isFilingState', label: 'Filing state', kind: 'boolean' },
  { key: 'glGeneralAggregate', label: 'GL general aggregate', kind: 'currency' },
  { key: 'glProductsCompletedOps', label: 'GL products/completed ops aggregate', kind: 'currency' },
  { key: 'glEachOccurrence', label: 'GL each occurrence limit', kind: 'currency' },
  { key: 'glPersonalAndAdvertisingInjury', label: 'GL personal & advertising injury', kind: 'currency' },
  { key: 'glDamageToRentedPremises', label: 'GL damage to rented premises', kind: 'currency' },
  { key: 'glMedicalExpense', label: 'GL medical expense limit', kind: 'currency' },
  { key: 'glTotalSubcontractorCost', label: 'GL total subcontractor cost', kind: 'currency' },
  { key: 'glAdditionalInsuredCount', label: 'GL additional insured count', kind: 'number' },
  { key: 'glBlanketAdditionalInsured', label: 'GL blanket additional insured', kind: 'boolean' },
  { key: 'glWaiverOfSubrogationCount', label: 'GL waiver of subrogation count', kind: 'number' },
  { key: 'glBlanketWaiverOfSubrogation', label: 'GL blanket waiver of subrogation', kind: 'boolean' },
  { key: 'glPrimaryNonContributory', label: 'GL primary & non-contributory', kind: 'boolean' },
  { key: 'glIncludeTria', label: 'GL TRIA included', kind: 'boolean' },
  { key: 'glClassificationCount', label: 'GL classification count', kind: 'number' },
  { key: 'glTotalExposure', label: 'GL total exposure', kind: 'currency' },
  { key: 'glMaxClassExposure', label: 'GL largest class exposure', kind: 'currency' },
  { key: 'glHasUnsupportedClassCode', label: 'GL has unsupported class code', kind: 'boolean' },
  { key: 'glClassCodes', label: 'GL class codes', kind: 'text' },
  { key: 'glScheduleCreditPercent', label: 'GL schedule credit', kind: 'percent-whole' },
  { key: 'glLoggingRevenuePercent', label: 'GL logging revenue', kind: 'percent-whole' },
  { key: 'glManagementExperienceYears', label: 'GL management experience years', kind: 'number' },
  { key: 'glLargestSingleLossAmount', label: 'GL largest single loss', kind: 'currency' },
  { key: 'glFuelStorageOverMax', label: 'GL fuel storage over max allowable', kind: 'boolean' },
  { key: 'glLogRoadBuildingOverAllowed', label: 'GL log road building exceeds allowed percent', kind: 'boolean' },
  { key: 'glGradingExcavationOverAllowed', label: 'GL grading/excavation exceeds allowed percent', kind: 'boolean' },
  { key: 'glAircraftOrDroneOps', label: 'GL aircraft/drone operations', kind: 'boolean' },
  { key: 'glExplosivesUsed', label: 'GL explosives used', kind: 'boolean' },
  { key: 'glNonMechanizedLogging', label: 'GL non-mechanized logging', kind: 'boolean' },
  { key: 'glBankruptcyOrReceivership', label: 'GL bankruptcy or receivership', kind: 'boolean' },
  { key: 'glHerbicidePesticideApplication', label: 'GL herbicide/pesticide application', kind: 'boolean' },
  { key: 'glCraneUseOutsideAllowed', label: 'GL crane use outside allowed operations', kind: 'boolean' },
  { key: 'glEquipmentRentalToOthers', label: 'GL equipment rental/leasing to others', kind: 'boolean' },
  { key: 'glThirdPartyEquipmentRepair', label: 'GL third-party equipment repair/service', kind: 'boolean' },
  { key: 'glRightOfWayClearing', label: 'GL right-of-way clearing/maintenance', kind: 'boolean' },
] as const
const DEFAULT_CONDITION = JSON.stringify({ field: 'totalInsuredValue', operator: '>', value: 0 })

const STATUS_STYLES: Record<UnderwritingControlStatus, string> = {
  AiSuggested: 'bg-sky-50 text-sky-700',
  Draft: 'bg-slate-100 text-slate-700',
  Approved: 'bg-amber-50 text-amber-700',
  Published: 'bg-emerald-50 text-emerald-700',
  Rejected: 'bg-rose-50 text-rose-700',
  Retired: 'bg-zinc-100 text-zinc-600',
}

const inputCls = 'sims-input'
const textareaCls = 'sims-textarea'
const iconBtnCls = 'sd-btn outline sm'
type ConditionOperator = typeof CONDITION_OPERATORS[number]
type ConditionFieldKey = typeof CONDITION_FIELDS[number]['key']
type ConditionField = typeof CONDITION_FIELDS[number]
type ParsedCondition =
  | { mode: 'always' }
  | { mode: 'builder'; field: ConditionField; operator: ConditionOperator; value: string }
  | { mode: 'unsupported'; raw: string; reason: string }

const emptyDocument: CreateUnderwritingGuidelineDocumentRequest = {
  programId: null,
  programName: '',
  carrierId: null,
  lineOfBusiness: 'InlandMarine',
  stateCode: 'ALL',
  title: '',
  sourceFileName: '',
  sourceBlobName: '',
  notes: '',
}

const emptyControl: CreateUnderwritingGuidelineControlRequest = {
  itemType: 'DocumentChecklistItem',
  stage: 'Submission',
  severity: 'Warning',
  ruleKey: '',
  label: '',
  description: '',
  conditionJson: '',
  isBlocking: false,
  overrideAllowed: true,
  overridePermission: 'underwriting.clearance.override',
  sourceCitation: '',
  aiConfidence: null,
  sortOrder: 0,
}

export function UnderwritingControlsAdminPage() {
  const qc = useQueryClient()
  const [selectedDocumentId, setSelectedDocumentId] = useState<string | null>(null)
  const [documentForm, setDocumentForm] = useState<CreateUnderwritingGuidelineDocumentRequest>(emptyDocument)
  const [editingDocumentId, setEditingDocumentId] = useState<string | null>(null)
  const [controlForm, setControlForm] = useState<CreateUnderwritingGuidelineControlRequest>(emptyControl)
  const [editingControlId, setEditingControlId] = useState<string | null>(null)
  const [decisionNotes, setDecisionNotes] = useState<Record<string, string>>({})
  const [selectedAttachmentId, setSelectedAttachmentId] = useState('')
  const [aiJsonInput, setAiJsonInput] = useState('')
  const [controlStatusFilter, setControlStatusFilter] = useState<UnderwritingControlStatus | 'All'>('All')
  const [controlSeverityFilter, setControlSeverityFilter] = useState<UnderwritingControlSeverity | 'All'>('All')
  const [controlStageFilter, setControlStageFilter] = useState<UnderwritingControlStage | 'All'>('All')
  const [blockingOnly, setBlockingOnly] = useState(false)
  const [controlSearch, setControlSearch] = useState('')
  const [selectedControlIds, setSelectedControlIds] = useState<string[]>([])
  const aiJsonFileInputRef = useRef<HTMLInputElement | null>(null)
  const controlEditorRef = useRef<HTMLDivElement | null>(null)

  const { data: documents = [], isLoading: loadingDocuments, isError: documentsError, error: documentsErrorObj, refetch: refetchDocuments } = useQuery({
    queryKey: ['admin', 'underwriting-guidelines', 'documents'],
    queryFn: underwritingGuidelinesApi.getDocuments,
  })

  const { data: carriers = [] } = useQuery({
    queryKey: ['carriers', 'active'],
    queryFn: () => carriersApi.getAll(true),
  })

  const { data: programs = [] } = useQuery({
    queryKey: ['admin', 'program-configurations', 'active'],
    queryFn: () => programConfigurationsApi.getAll(false),
  })

  const { data: carrierAttachments = [] } = useQuery({
    queryKey: ['carrier', documentForm.carrierId, 'attachments'],
    queryFn: () => attachmentsApi.getAll('Carrier', documentForm.carrierId!),
    enabled: !!documentForm.carrierId,
  })

  const selectedDocument = documents.find((doc) => doc.id === selectedDocumentId) ?? documents[0] ?? null
  const activeDocumentId = selectedDocument?.id ?? null

  const { data: controls = [], isLoading: loadingControls } = useQuery({
    queryKey: ['admin', 'underwriting-guidelines', 'controls', activeDocumentId],
    queryFn: () => underwritingGuidelinesApi.getControls(activeDocumentId!),
    enabled: !!activeDocumentId,
  })

  const { data: auditLog = [] } = useQuery({
    queryKey: ['admin', 'underwriting-guidelines', 'audit-log', activeDocumentId],
    queryFn: () => underwritingGuidelinesApi.getAuditLog(activeDocumentId ? { documentId: activeDocumentId } : undefined),
    enabled: !!activeDocumentId,
  })

  const groupedCounts = useMemo(() => {
    return controls.reduce<Record<UnderwritingControlStatus, number>>((acc, control) => {
      acc[control.status] = (acc[control.status] ?? 0) + 1
      return acc
    }, {} as Record<UnderwritingControlStatus, number>)
  }, [controls])

  const visibleControls = useMemo(() => {
    const search = controlSearch.trim().toLowerCase()
    return controls.filter((control) => {
      if (controlStatusFilter !== 'All' && control.status !== controlStatusFilter) return false
      if (controlSeverityFilter !== 'All' && control.severity !== controlSeverityFilter) return false
      if (controlStageFilter !== 'All' && control.stage !== controlStageFilter) return false
      if (blockingOnly && !control.isBlocking) return false
      if (!search) return true
      return [
        control.ruleKey,
        control.label,
        control.description,
        control.sourceCitation,
      ].some((value) => value?.toLowerCase().includes(search))
    })
  }, [blockingOnly, controlSearch, controlSeverityFilter, controlStageFilter, controlStatusFilter, controls])

  const selectedControls = visibleControls.filter((control) => selectedControlIds.includes(control.id))
  const selectableVisibleControls = visibleControls.filter((control) => control.status !== 'Published' && control.status !== 'Retired')
  const parsedCondition = parseControlCondition(controlForm.conditionJson)

  const createDocument = useMutation({
    mutationFn: (payload: CreateUnderwritingGuidelineDocumentRequest) => editingDocumentId
      ? underwritingGuidelinesApi.updateDocument(editingDocumentId, payload)
      : underwritingGuidelinesApi.createDocument(payload),
    onSuccess: (doc) => {
      toast.success(editingDocumentId ? 'Guideline document updated' : 'Guideline document created')
      resetDocumentForm()
      setSelectedDocumentId(doc.id)
      qc.invalidateQueries({ queryKey: ['admin', 'underwriting-guidelines', 'documents'] })
      qc.invalidateQueries({ queryKey: ['admin', 'underwriting-guidelines', 'controls', doc.id] })
      qc.invalidateQueries({ queryKey: ['admin', 'underwriting-guidelines', 'audit-log', doc.id] })
    },
    onError: (err) => toast.error(getApiErrorMessage(err, editingDocumentId ? 'Guideline document could not be updated' : 'Guideline document could not be created')),
  })

  const deleteDocument = useMutation({
    mutationFn: underwritingGuidelinesApi.deleteDocument,
    onSuccess: (_result, documentId) => {
      toast.success('Guideline document deleted')
      if (selectedDocumentId === documentId) setSelectedDocumentId(null)
      if (editingDocumentId === documentId) resetDocumentForm()
      qc.invalidateQueries({ queryKey: ['admin', 'underwriting-guidelines', 'documents'] })
      qc.invalidateQueries({ queryKey: ['admin', 'underwriting-guidelines', 'controls', documentId] })
      qc.invalidateQueries({ queryKey: ['admin', 'underwriting-guidelines', 'audit-log', documentId] })
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Guideline document could not be deleted')),
  })

  const proposeFromAttachment = useMutation({
    mutationFn: () => underwritingGuidelinesApi.proposeFromAttachment({
      attachmentId: selectedAttachmentId,
      document: {
        ...documentForm,
        programId: documentForm.programId || null,
        carrierId: documentForm.carrierId || null,
        stateCode: documentForm.stateCode || 'ALL',
        sourceFileName: null,
        sourceBlobName: null,
      },
    }),
    onSuccess: (result) => {
      toast.success(`AI proposed ${result.controls.length} controls`)
      result.warnings.slice(1).forEach((warning) => toast.warning(warning))
      setSelectedAttachmentId('')
      resetDocumentForm()
      setSelectedDocumentId(result.document.id)
      invalidateControls(result.document.id)
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'AI guideline proposal failed')),
  })

  const saveControl = useMutation({
    mutationFn: (payload: CreateUnderwritingGuidelineControlRequest | UpdateUnderwritingGuidelineControlRequest) => {
      if (!activeDocumentId) throw new Error('Select a guideline document first')
      return editingControlId
        ? underwritingGuidelinesApi.updateControl(editingControlId, payload as UpdateUnderwritingGuidelineControlRequest).then((saved) => [saved])
        : underwritingGuidelinesApi.addProposedControls(activeDocumentId, { controls: [payload as CreateUnderwritingGuidelineControlRequest] })
    },
    onSuccess: () => {
      toast.success(editingControlId ? 'Control updated' : 'Proposed control added')
      setEditingControlId(null)
      setControlForm(emptyControl)
      invalidateControls(activeDocumentId)
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Control could not be saved')),
  })

  const createFromAiJson = useMutation({
    mutationFn: async (payload: { document: CreateUnderwritingGuidelineDocumentRequest; controls: CreateUnderwritingGuidelineControlRequest[] }) => {
      const doc = editingDocumentId
        ? await underwritingGuidelinesApi.updateDocument(editingDocumentId, payload.document)
        : await underwritingGuidelinesApi.createDocument(payload.document)
      const controls = await underwritingGuidelinesApi.addProposedControls(doc.id, { controls: payload.controls })
      return { doc, controls }
    },
    onSuccess: ({ doc, controls }) => {
      toast.success(`Guideline created with ${controls.length} proposed controls`)
      setAiJsonInput('')
      resetDocumentForm()
      setSelectedDocumentId(doc.id)
      invalidateControls(doc.id)
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'AI JSON controls could not be imported')),
  })

  const decideControl = useMutation({
    mutationFn: ({ control, action }: { control: UnderwritingGuidelineControl; action: 'approve' | 'reject' | 'publish' | 'retire' }) => {
      const notes = decisionNotes[control.id]
      if (action === 'approve') return underwritingGuidelinesApi.approveControl(control.id, notes)
      if (action === 'reject') return underwritingGuidelinesApi.rejectControl(control.id, notes)
      if (action === 'publish') return underwritingGuidelinesApi.publishControl(control.id, notes)
      return underwritingGuidelinesApi.retireControl(control.id, notes)
    },
    onSuccess: (_saved, vars) => {
      toast.success(`Control ${decisionPastTense(vars.action)}`)
      setDecisionNotes((prev) => ({ ...prev, [vars.control.id]: '' }))
      invalidateControls(activeDocumentId)
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Control action failed')),
  })

  const bulkDecideControls = useMutation({
    mutationFn: async ({ selected, action }: { selected: UnderwritingGuidelineControl[]; action: 'approve' | 'reject' }) => {
      for (const control of selected) {
        if (action === 'approve') await underwritingGuidelinesApi.approveControl(control.id, decisionNotes[control.id])
        else await underwritingGuidelinesApi.rejectControl(control.id, decisionNotes[control.id])
      }
      return { selected, action }
    },
    onSuccess: ({ selected, action }) => {
      toast.success(`${selected.length} controls ${action}d`)
      setSelectedControlIds([])
      invalidateControls(activeDocumentId)
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Bulk control action failed')),
  })

  function invalidateControls(documentId: string | null) {
    qc.invalidateQueries({ queryKey: ['admin', 'underwriting-guidelines', 'documents'] })
    qc.invalidateQueries({ queryKey: ['admin', 'underwriting-guidelines', 'controls', documentId] })
    qc.invalidateQueries({ queryKey: ['admin', 'underwriting-guidelines', 'audit-log', documentId] })
  }

  function submitDocument() {
    createDocument.mutate({
      ...documentForm,
      programId: documentForm.programId || null,
      carrierId: documentForm.carrierId || null,
      stateCode: documentForm.stateCode || 'ALL',
    })
  }

  function resetDocumentForm() {
    setDocumentForm(emptyDocument)
    setEditingDocumentId(null)
    setSelectedAttachmentId('')
  }

  function editDocument(doc: UnderwritingGuidelineDocument) {
    setEditingDocumentId(doc.id)
    setSelectedDocumentId(doc.id)
    setSelectedAttachmentId('')
    setDocumentForm({
      programId: doc.programId,
      programName: doc.programName,
      carrierId: doc.carrierId,
      lineOfBusiness: doc.lineOfBusiness,
      stateCode: doc.stateCode,
      title: doc.title,
      sourceFileName: doc.sourceFileName ?? '',
      sourceBlobName: doc.sourceBlobName ?? '',
      notes: doc.notes ?? '',
    })
  }

  function requestDeleteDocument(doc: UnderwritingGuidelineDocument) {
    if (!confirm(`Delete ${doc.title}? Draft, AI suggested, approved, and rejected controls in this guideline will be removed.`)) return
    deleteDocument.mutate(doc.id)
  }

  function requestPublishControl(control: UnderwritingGuidelineControl) {
    const impact = control.isBlocking
      ? `This is a ${severityLabel(control.severity).toLowerCase()} BLOCKING control — once live it can block submissions, quotes, or binds that match it.`
      : `This is a ${severityLabel(control.severity).toLowerCase()} control — once live it will apply to matching submissions, quotes, and binds.`
    if (!confirm(`Publish "${control.label}"?\n\n${impact}`)) return
    decideControl.mutate({ control, action: 'publish' })
  }

  function requestRetireControl(control: UnderwritingGuidelineControl) {
    if (!confirm(`Retire "${control.label}"?\n\nThis removes a live control — it will no longer apply to submissions, quotes, or binds.`)) return
    decideControl.mutate({ control, action: 'retire' })
  }

  function submitAttachmentProposal() {
    proposeFromAttachment.mutate()
  }

  function submitControl() {
    const payload = {
      ...controlForm,
      conditionJson: controlForm.conditionJson?.trim() ? controlForm.conditionJson : null,
      description: controlForm.description?.trim() ? controlForm.description : null,
      overridePermission: controlForm.overridePermission?.trim() ? controlForm.overridePermission : null,
      sourceCitation: controlForm.sourceCitation?.trim() ? controlForm.sourceCitation : null,
      aiConfidence: controlForm.aiConfidence,
      changeNotes: editingControlId ? 'Admin UI edit' : undefined,
    }
    saveControl.mutate(payload)
  }

  function submitAiJsonImport() {
    try {
      createFromAiJson.mutate({
        document: {
          ...documentForm,
          programId: documentForm.programId || null,
          carrierId: documentForm.carrierId || null,
          stateCode: documentForm.stateCode || 'ALL',
        },
        controls: parseAiControlJson(aiJsonInput),
      })
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'AI JSON could not be parsed')
    }
  }

  async function uploadAiJsonFile(file: File | null) {
    if (!file) return
    try {
      const text = await file.text()
      setAiJsonInput(text)
      toast.success(`${file.name} loaded`)
    } catch {
      toast.error('AI JSON file could not be read')
    } finally {
      if (aiJsonFileInputRef.current) aiJsonFileInputRef.current.value = ''
    }
  }

  function editControl(control: UnderwritingGuidelineControl) {
    setEditingControlId(control.id)
    setControlForm({
      itemType: control.itemType,
      stage: control.stage,
      severity: control.severity,
      ruleKey: control.ruleKey,
      label: control.label,
      description: control.description ?? '',
      conditionJson: control.conditionJson ?? '',
      isBlocking: control.isBlocking,
      overrideAllowed: control.overrideAllowed,
      overridePermission: control.overridePermission ?? '',
      sourceCitation: control.sourceCitation ?? '',
      aiConfidence: control.aiConfidence,
      sortOrder: control.sortOrder,
    })
    setTimeout(() => controlEditorRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }), 0)
  }

  function cancelControlEdit() {
    setEditingControlId(null)
    setControlForm(emptyControl)
  }

  function setAlwaysApplies() {
    setControlForm((f) => ({ ...f, conditionJson: '' }))
  }

  function setConditionalApplies() {
    setControlForm((f) => ({ ...f, conditionJson: parsedCondition.mode === 'builder' ? f.conditionJson : DEFAULT_CONDITION }))
  }

  function updateCondition(partial: Partial<{ field: ConditionFieldKey; operator: ConditionOperator; value: string }>) {
    const current = parsedCondition.mode === 'builder'
      ? parsedCondition
      : parseControlCondition(DEFAULT_CONDITION)
    if (current.mode !== 'builder') return
    const fieldKey = partial.field ?? current.field.key
    const field = getConditionField(fieldKey)
    const operator = field.kind === 'text'
      ? (partial.operator === 'notContains' ? 'notContains' : 'contains')
      : field.kind === 'boolean'
      ? (partial.operator === '!=' ? '!=' : (partial.operator ?? current.operator) === '!=' ? '!=' : '==')
      : partial.operator ?? current.operator
    const value = partial.field && field.kind === 'boolean'
      ? '1'
      : partial.field && field.kind === 'text'
        ? ''
      : partial.value ?? current.value
    setControlForm((f) => ({
      ...f,
      conditionJson: buildControlCondition(field, operator, value),
    }))
  }

  function toggleControlSelection(controlId: string) {
    setSelectedControlIds((prev) => prev.includes(controlId)
      ? prev.filter((id) => id !== controlId)
      : [...prev, controlId])
  }

  function toggleAllVisibleControls() {
    const visibleIds = selectableVisibleControls.map((control) => control.id)
    const allSelected = visibleIds.length > 0 && visibleIds.every((id) => selectedControlIds.includes(id))
    setSelectedControlIds((prev) => allSelected
      ? prev.filter((id) => !visibleIds.includes(id))
      : Array.from(new Set([...prev, ...visibleIds])))
  }

  function selectProgram(programId: string) {
    const program = programs.find((p) => p.id === programId)
    setSelectedAttachmentId('')
    setDocumentForm((f) => ({
      ...f,
      programId: programId || null,
      programName: program?.name ?? f.programName,
    }))
  }

  const guidelineAttachments = carrierAttachments.filter((a: Attachment) => a.documentType === 'UnderwritingGuidelines')
  const supportedGuidelineAttachments = guidelineAttachments.filter(isSupportedGuidelineAttachment)

  if (loadingDocuments) return <LoadingSpinner />
  if (documentsError) {
    return (
      <div className="p-6 space-y-5">
        <PageHeader
          title="Underwriting Controls"
          subtitle="Guideline-scoped rules, blockers, referrals, and document checklist controls"
        />
        <ErrorState error={documentsErrorObj} onRetry={refetchDocuments} />
      </div>
    )
  }

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="Underwriting Controls"
        subtitle="Guideline-scoped rules, blockers, referrals, and document checklist controls"
      />

      <section className="grid gap-5 xl:grid-cols-[360px_1fr]">
        <div className="space-y-5">
          <div className="admin-panel">
            <div className="admin-panel-head justify-start">
              <FileSearch className="h-4 w-4" style={{ color: 'var(--ink-3)' }} />
              <h2 className="admin-panel-title">Guideline Scope</h2>
              {editingDocumentId && (
                <button type="button" onClick={resetDocumentForm} className="ml-auto text-xs font-medium text-slate-500 hover:text-slate-700">
                  Cancel edit
                </button>
              )}
            </div>
            <div className="space-y-3 p-5">
              <select
                value={documentForm.programId ?? ''}
                onChange={(e) => selectProgram(e.target.value)}
                className={inputCls}
              >
                <option value="">Manual program scope</option>
                {programs.map((program) => (
                  <option key={program.id} value={program.id}>
                    {program.name}
                  </option>
                ))}
              </select>
              <input
                value={documentForm.programName}
                onChange={(e) => setDocumentForm((f) => ({ ...f, programName: e.target.value, programId: null }))}
                className={inputCls}
                placeholder="Program"
              />
              <select
                value={documentForm.carrierId ?? ''}
                onChange={(e) => {
                  setSelectedAttachmentId('')
                  setDocumentForm((f) => ({ ...f, carrierId: e.target.value || null }))
                }}
                className={inputCls}
              >
                <option value="">All companies</option>
                {carriers.map((carrier) => (
                  <option key={carrier.id} value={carrier.id}>{carrier.name}</option>
                ))}
              </select>
              <div className="grid grid-cols-2 gap-3">
                <select
                  value={documentForm.lineOfBusiness}
                  onChange={(e) => setDocumentForm((f) => ({ ...f, lineOfBusiness: e.target.value as PolicyLineOfBusiness }))}
                  className={inputCls}
                >
                  {ACTIVE_LOBS.map((lob) => (
                    <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>
                  ))}
                </select>
                <select
                  value={documentForm.stateCode}
                  onChange={(e) => setDocumentForm((f) => ({ ...f, stateCode: e.target.value }))}
                  className={inputCls}
                >
                  {STATE_OPTIONS.map((state) => (
                    <option key={state} value={state}>{state}</option>
                  ))}
                </select>
              </div>
              <input
                value={documentForm.title}
                onChange={(e) => setDocumentForm((f) => ({ ...f, title: e.target.value }))}
                className={inputCls}
                placeholder="Guideline title"
              />
              <input
                value={documentForm.sourceFileName ?? ''}
                onChange={(e) => setDocumentForm((f) => ({ ...f, sourceFileName: e.target.value }))}
                className={inputCls}
                placeholder="Source file name"
              />
              <input
                value={documentForm.sourceBlobName ?? ''}
                onChange={(e) => setDocumentForm((f) => ({ ...f, sourceBlobName: e.target.value }))}
                className={inputCls}
                placeholder="Source blob path"
              />
              <textarea
                value={documentForm.notes ?? ''}
                onChange={(e) => setDocumentForm((f) => ({ ...f, notes: e.target.value }))}
                className={textareaCls}
                rows={3}
                placeholder="Notes"
              />
              <button
                type="button"
                onClick={submitDocument}
                disabled={createDocument.isPending || createFromAiJson.isPending || !documentForm.programName.trim() || !documentForm.title.trim()}
                className="sd-btn primary w-full"
              >
                <Plus className="h-4 w-4" />
                {editingDocumentId ? 'Save Guideline' : 'Create Guideline'}
              </button>
              <div className="rounded-md border border-dashed border-slate-300 bg-white p-3">
                <input
                  ref={aiJsonFileInputRef}
                  type="file"
                  accept="application/json,.json"
                  className="hidden"
                  onChange={(e) => uploadAiJsonFile(e.target.files?.[0] ?? null)}
                />
                <textarea
                  value={aiJsonInput}
                  onChange={(e) => setAiJsonInput(e.target.value)}
                  className={textareaCls}
                  rows={5}
                  placeholder="Paste or upload AI controls JSON"
                />
                <div className="mt-3 grid gap-2">
                  <button
                    type="button"
                    onClick={() => aiJsonFileInputRef.current?.click()}
                    disabled={createFromAiJson.isPending}
                    className="sd-btn outline w-full"
                  >
                    <FileSearch className="h-4 w-4" />
                    Upload JSON
                  </button>
                  <button
                    type="button"
                    onClick={submitAiJsonImport}
                    disabled={
                      createFromAiJson.isPending ||
                      !documentForm.programName.trim() ||
                      !documentForm.title.trim() ||
                      !aiJsonInput.trim()
                    }
                    className="sd-btn accent w-full"
                  >
                    <FileSearch className="h-4 w-4" />
                    {createFromAiJson.isPending ? 'Creating...' : 'Create From AI JSON'}
                  </button>
                </div>
              </div>
              <div className="pt-3" style={{ borderTop: '1px solid var(--line-2)' }}>
                <div className="grid gap-3">
                  <select
                    value={selectedAttachmentId}
                    onChange={(e) => setSelectedAttachmentId(e.target.value)}
                    disabled={!documentForm.carrierId}
                    className={inputCls}
                  >
                    <option value="">{documentForm.carrierId ? 'Select guideline attachment' : 'Select company first'}</option>
                    {supportedGuidelineAttachments.map((attachment) => (
                      <option key={attachment.id} value={attachment.id}>{attachment.fileName}</option>
                    ))}
                  </select>
                  {documentForm.carrierId && guidelineAttachments.length > 0 && supportedGuidelineAttachments.length === 0 && (
                    <div className="text-xs text-amber-700">
                      Guideline import supports PDF and plain-text files.
                    </div>
                  )}
                  <button
                    type="button"
                    onClick={submitAttachmentProposal}
                    disabled={
                      proposeFromAttachment.isPending ||
                      !documentForm.programName.trim() ||
                      !documentForm.title.trim() ||
                      !selectedAttachmentId
                    }
                    className="sd-btn accent w-full"
                  >
                    {proposeFromAttachment.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <FileSearch className="h-4 w-4" />}
                    {proposeFromAttachment.isPending ? 'Reading guideline with AI...' : 'Propose From Attachment'}
                  </button>
                  {proposeFromAttachment.isPending && (
                    <div className="text-xs" style={{ color: 'var(--ink-3)' }}>
                      This can take a minute for larger PDFs.
                    </div>
                  )}
                </div>
              </div>
            </div>
          </div>

          <div className="admin-panel">
            <div className="admin-panel-head">
              <h2 className="admin-panel-title">Documents</h2>
            </div>
            <div className="divide-y">
              {documents.length === 0 ? (
                <div className="admin-empty m-4">No guideline documents yet.</div>
              ) : documents.map((doc) => (
                <button
                  key={doc.id}
                  type="button"
                  onClick={() => setSelectedDocumentId(doc.id)}
                  className="w-full px-5 py-4 text-left"
                  style={doc.id === activeDocumentId ? { background: 'var(--accent-soft)' } : undefined}
                >
                  <div className="flex items-center justify-between gap-3">
                    <div className="text-sm font-semibold" style={{ color: 'var(--ink)' }}>{doc.title}</div>
                    <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-600">v{doc.version}</span>
                  </div>
                  <div className="mt-1 text-xs" style={{ color: 'var(--ink-3)' }}>
                    {doc.programName} / {doc.carrierName ?? 'All companies'} / {LOB_LABELS[doc.lineOfBusiness]} / {doc.stateCode}
                  </div>
                  <div className="mt-2 text-xs" style={{ color: 'var(--ink-3)' }}>{doc.controlCount} controls</div>
                </button>
              ))}
            </div>
          </div>
        </div>

        <div className="space-y-5">
          <section className="admin-panel">
            <div className="admin-panel-head flex-wrap">
              <div>
                <h2 className="admin-panel-title">{selectedDocument?.title ?? 'Select a guideline'}</h2>
                {selectedDocument && (
                  <div className="mt-1 text-xs" style={{ color: 'var(--ink-3)' }}>
                    {selectedDocument.programName} / {selectedDocument.carrierName ?? 'All companies'} / {LOB_LABELS[selectedDocument.lineOfBusiness]} / {selectedDocument.stateCode}
                  </div>
                )}
              </div>
              <div className="flex flex-wrap gap-2">
                {selectedDocument && (
                  <>
                    <button type="button" onClick={() => editDocument(selectedDocument)} className={iconBtnCls}>
                      <Pencil className="h-3.5 w-3.5" />
                      Edit Guideline
                    </button>
                    <button
                      type="button"
                      onClick={() => requestDeleteDocument(selectedDocument)}
                      disabled={deleteDocument.isPending || controls.some((control) => control.status === 'Published')}
                      className={iconBtnCls}
                      title={controls.some((control) => control.status === 'Published') ? 'Retire published controls before deleting this guideline.' : 'Delete guideline'}
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                      Delete
                    </button>
                  </>
                )}
                {(['AiSuggested', 'Draft', 'Approved', 'Published'] as UnderwritingControlStatus[]).map((status) => (
                  <span key={status} className={`rounded-full px-2 py-1 text-xs font-medium ${STATUS_STYLES[status]}`}>
                    {statusLabel(status)} {groupedCounts[status] ?? 0}
                  </span>
                ))}
              </div>
            </div>

            {activeDocumentId && (
              <div ref={controlEditorRef} className="px-5 py-4" style={{ borderBottom: '1px solid var(--line-2)', background: 'var(--surface-2)' }}>
                {editingControlId && (
                  <div className="mb-3 flex items-center justify-between rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-800">
                    <span>Editing control</span>
                    <button type="button" onClick={cancelControlEdit} className="font-medium">
                      Cancel
                    </button>
                  </div>
                )}
                <div className="grid gap-3 lg:grid-cols-4">
                  <select
                    value={controlForm.itemType}
                    onChange={(e) => setControlForm((f) => ({ ...f, itemType: e.target.value as UnderwritingControlItemType }))}
                    className={inputCls}
                  >
                    {ITEM_TYPES.map((type) => <option key={type} value={type}>{itemTypeLabel(type)}</option>)}
                  </select>
                  <select
                    value={controlForm.stage}
                    onChange={(e) => setControlForm((f) => ({ ...f, stage: e.target.value as UnderwritingControlStage }))}
                    className={inputCls}
                  >
                    {STAGES.map((stage) => <option key={stage} value={stage}>{stageLabel(stage)}</option>)}
                  </select>
                  <select
                    value={controlForm.severity}
                    onChange={(e) => setControlForm((f) => ({ ...f, severity: e.target.value as UnderwritingControlSeverity }))}
                    className={inputCls}
                  >
                    {SEVERITIES.map((severity) => <option key={severity} value={severity}>{severityLabel(severity)}</option>)}
                  </select>
                  <input
                    value={controlForm.ruleKey}
                    onChange={(e) => setControlForm((f) => ({ ...f, ruleKey: e.target.value }))}
                    className={inputCls}
                    placeholder="Rule key"
                  />
                </div>
                <div className="mt-3 grid gap-3 lg:grid-cols-[1fr_1fr_auto]">
                  <input
                    value={controlForm.label}
                    onChange={(e) => setControlForm((f) => ({ ...f, label: e.target.value }))}
                    className={inputCls}
                    placeholder="Control label"
                  />
                  <input
                    value={controlForm.sourceCitation ?? ''}
                    onChange={(e) => setControlForm((f) => ({ ...f, sourceCitation: e.target.value }))}
                    className={inputCls}
                    placeholder="Source citation"
                  />
                  <div className="admin-muted-panel flex items-center gap-4 px-3 py-2 text-sm">
                    <label className="inline-flex items-center gap-2">
                      <input
                        type="checkbox"
                        checked={controlForm.isBlocking}
                        onChange={(e) => setControlForm((f) => ({ ...f, isBlocking: e.target.checked }))}
                      />
                      Blocking
                    </label>
                    <label className="inline-flex items-center gap-2">
                      <input
                        type="checkbox"
                        checked={controlForm.overrideAllowed}
                        onChange={(e) => setControlForm((f) => ({ ...f, overrideAllowed: e.target.checked }))}
                      />
                      Override
                    </label>
                  </div>
                </div>
                <textarea
                  value={controlForm.description ?? ''}
                  onChange={(e) => setControlForm((f) => ({ ...f, description: e.target.value }))}
                  className={`${textareaCls} mt-3`}
                  rows={2}
                  placeholder="Description"
                />
                <div className="mt-3 grid gap-3 lg:grid-cols-[1fr_220px_auto]">
                  <div className="rounded-md border border-slate-200 bg-white p-3">
                    <div className="flex flex-wrap items-center gap-4 text-sm text-slate-700">
                      <span className="font-medium text-slate-900">Applies when</span>
                      <label className="inline-flex items-center gap-2">
                        <input
                          type="radio"
                          checked={parsedCondition.mode === 'always'}
                          onChange={setAlwaysApplies}
                        />
                        Always
                      </label>
                      <label className="inline-flex items-center gap-2">
                        <input
                          type="radio"
                          checked={parsedCondition.mode !== 'always'}
                          onChange={setConditionalApplies}
                        />
                        Field matches value
                      </label>
                    </div>
                    {parsedCondition.mode === 'builder' && (
                      <>
                        <div className="mt-4 grid gap-3 xl:grid-cols-[minmax(280px,1fr)_180px_180px]">
                          <label className="block">
                            <span className="mb-1 block text-xs font-medium text-slate-600">Field</span>
                            <select
                              value={parsedCondition.field.key}
                              onChange={(e) => updateCondition({ field: e.target.value as ConditionFieldKey })}
                              className={inputCls}
                            >
                              {CONDITION_FIELDS.map((field) => (
                                <option key={field.key} value={field.key}>{field.label}</option>
                              ))}
                            </select>
                          </label>
                          <label className="block">
                            <span className="mb-1 block text-xs font-medium text-slate-600">Comparison</span>
                            <select
                              value={parsedCondition.operator}
                              onChange={(e) => updateCondition({ operator: e.target.value as ConditionOperator })}
                              className={inputCls}
                            >
                              {(parsedCondition.field.kind === 'text'
                                ? (['contains', 'notContains'] as ConditionOperator[])
                                : parsedCondition.field.kind === 'boolean'
                                  ? (['==', '!='] as ConditionOperator[])
                                  : NUMERIC_CONDITION_OPERATORS).map((operator) => (
                                <option key={operator} value={operator}>{operatorLabel(operator)}</option>
                              ))}
                            </select>
                          </label>
                          <label className="block">
                            <span className="mb-1 block text-xs font-medium text-slate-600">Value</span>
                            {parsedCondition.field.kind === 'boolean' ? (
                              <select
                                value={parsedCondition.value === '0' ? '0' : '1'}
                                onChange={(e) => updateCondition({ value: e.target.value })}
                                className={inputCls}
                              >
                                <option value="1">Yes</option>
                                <option value="0">No</option>
                              </select>
                            ) : parsedCondition.field.kind === 'text' ? (
                              <input
                                value={parsedCondition.value}
                                onChange={(e) => updateCondition({ value: e.target.value })}
                                className={inputCls}
                                placeholder="Class code"
                              />
                            ) : (
                              <input
                                type="number"
                                min="0"
                                step={parsedCondition.field.kind === 'percent' ? '0.01' : '1'}
                                value={parsedCondition.value}
                                onChange={(e) => updateCondition({ value: e.target.value })}
                                className={inputCls}
                                placeholder={parsedCondition.field.kind === 'percent' || parsedCondition.field.kind === 'percent-whole' ? 'Percent' : 'Value'}
                              />
                            )}
                          </label>
                        </div>
                        <div className="mt-3 rounded-md bg-slate-50 px-3 py-2 text-sm leading-6 text-slate-700">
                          {describeControlCondition(parsedCondition)}
                        </div>
                      </>
                    )}
                    {parsedCondition.mode === 'unsupported' && (
                      <div className="mt-3 rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-800">
                        {parsedCondition.reason}. This rule needs field mapping before it can be checked automatically.
                        <button
                          type="button"
                          onClick={() => setControlForm((f) => ({ ...f, conditionJson: DEFAULT_CONDITION }))}
                          className="ml-2 font-medium"
                        >
                          Use builder
                        </button>
                      </div>
                    )}
                  </div>
                  <input
                    value={controlForm.overridePermission ?? ''}
                    onChange={(e) => setControlForm((f) => ({ ...f, overridePermission: e.target.value }))}
                    className={inputCls}
                    placeholder="Override permission"
                  />
                  <button
                    type="button"
                    onClick={submitControl}
                    disabled={saveControl.isPending || !controlForm.ruleKey.trim() || !controlForm.label.trim()}
                    className="sd-btn primary"
                  >
                    <Save className="h-4 w-4" />
                    {editingControlId ? 'Save Control' : 'Add Proposed'}
                  </button>
                </div>
              </div>
            )}

            {loadingControls ? (
              <LoadingSpinner />
            ) : controls.length === 0 ? (
              <div className="admin-empty m-4">No controls for this guideline yet.</div>
            ) : (
              <>
                <div className="px-5 py-4" style={{ borderBottom: '1px solid var(--line-2)' }}>
                  <div className="grid gap-3 lg:grid-cols-[1fr_160px_160px_160px_auto]">
                    <input
                      value={controlSearch}
                      onChange={(e) => setControlSearch(e.target.value)}
                      className={inputCls}
                      placeholder="Search controls"
                    />
                    <select value={controlStatusFilter} onChange={(e) => setControlStatusFilter(e.target.value as UnderwritingControlStatus | 'All')} className={inputCls}>
                      {REVIEW_STATUSES.map((status) => <option key={status} value={status}>{status === 'All' ? 'All statuses' : statusLabel(status)}</option>)}
                    </select>
                    <select value={controlStageFilter} onChange={(e) => setControlStageFilter(e.target.value as UnderwritingControlStage | 'All')} className={inputCls}>
                      <option value="All">All stages</option>
                      {STAGES.map((stage) => <option key={stage} value={stage}>{stageLabel(stage)}</option>)}
                    </select>
                    <select value={controlSeverityFilter} onChange={(e) => setControlSeverityFilter(e.target.value as UnderwritingControlSeverity | 'All')} className={inputCls}>
                      <option value="All">All severities</option>
                      {SEVERITIES.map((severity) => <option key={severity} value={severity}>{severityLabel(severity)}</option>)}
                    </select>
                    <label className="admin-muted-panel flex items-center gap-2 px-3 text-sm">
                      <input type="checkbox" checked={blockingOnly} onChange={(e) => setBlockingOnly(e.target.checked)} />
                      Blocking
                    </label>
                  </div>
                  <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
                    <label className="inline-flex items-center gap-2 text-sm text-slate-600">
                      <input
                        type="checkbox"
                        checked={selectableVisibleControls.length > 0 && selectableVisibleControls.every((control) => selectedControlIds.includes(control.id))}
                        onChange={toggleAllVisibleControls}
                      />
                      Select visible editable
                    </label>
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-xs text-slate-500">{visibleControls.length} visible / {selectedControls.length} selected</span>
                      <button
                        type="button"
                        onClick={() => bulkDecideControls.mutate({ selected: selectedControls, action: 'approve' })}
                        disabled={bulkDecideControls.isPending || selectedControls.length === 0}
                        className="sd-btn outline sm"
                      >
                        <Check className="h-3.5 w-3.5" />
                        Approve Selected
                      </button>
                      <button
                        type="button"
                        onClick={() => bulkDecideControls.mutate({ selected: selectedControls, action: 'reject' })}
                        disabled={bulkDecideControls.isPending || selectedControls.length === 0}
                        className="sd-btn outline sm"
                      >
                        <X className="h-3.5 w-3.5" />
                        Reject Selected
                      </button>
                    </div>
                  </div>
                </div>
                {visibleControls.length === 0 ? (
                  <div className="admin-empty m-4">No controls match the current filters.</div>
                ) : (
                  <div className="divide-y">
                    {visibleControls.map((control) => (
                      <div key={control.id} className="px-5 py-4">
                        <div className="flex w-full flex-nowrap items-start justify-end gap-2 overflow-x-auto pb-1">
                          <button type="button" onClick={() => editControl(control)} disabled={control.status === 'Published' || control.status === 'Retired'} className={iconBtnCls}>
                            <Save className="h-3.5 w-3.5" />
                            Edit
                          </button>
                          <button type="button" onClick={() => decideControl.mutate({ control, action: 'approve' })} disabled={control.status === 'Approved' || control.status === 'Published' || control.status === 'Retired'} className={iconBtnCls}>
                            <Check className="h-3.5 w-3.5" />
                            Approve
                          </button>
                          <button type="button" onClick={() => decideControl.mutate({ control, action: 'reject' })} disabled={control.status === 'Published' || control.status === 'Retired'} className={iconBtnCls}>
                            <X className="h-3.5 w-3.5" />
                            Reject
                          </button>
                          <button type="button" onClick={() => requestPublishControl(control)} disabled={control.status !== 'Approved'} className={iconBtnCls}>
                            <Rocket className="h-3.5 w-3.5" />
                            Publish
                          </button>
                          <button type="button" onClick={() => requestRetireControl(control)} disabled={control.status !== 'Published'} className={iconBtnCls}>
                            <Archive className="h-3.5 w-3.5" />
                            Retire
                          </button>
                        </div>
                        <div className="mt-3">
                          <div className="grid min-w-0 grid-cols-[auto_minmax(0,1fr)] gap-3">
                            <input
                              type="checkbox"
                              className="mt-1"
                              checked={selectedControlIds.includes(control.id)}
                              disabled={control.status === 'Published' || control.status === 'Retired'}
                              onChange={() => toggleControlSelection(control.id)}
                            />
                            <div className="min-w-0 w-full">
                              <div className="flex flex-wrap items-center gap-2">
                                <h3 className="text-base font-semibold text-slate-900">{control.label}</h3>
                                <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_STYLES[control.status]}`}>{statusLabel(control.status)}</span>
                                {control.isBlocking && (
                                  <span className="inline-flex items-center gap-1 rounded-full bg-rose-50 px-2 py-0.5 text-xs font-medium text-rose-700">
                                    <ShieldAlert className="h-3 w-3" />
                                    Blocking
                                  </span>
                                )}
                              </div>
                              <div className="mt-1 text-sm text-slate-500">
                                {control.ruleKey} / {itemTypeLabel(control.itemType)} / {stageLabel(control.stage)} / {severityLabel(control.severity)}
                              </div>
                              {control.description && <p className="mt-3 text-sm leading-6 text-slate-700">{control.description}</p>}
                              {control.sourceCitation && <div className="mt-3 text-sm leading-6 text-slate-500">Source: {control.sourceCitation}</div>}
                            </div>
                          </div>
                        </div>
                        <input
                          value={decisionNotes[control.id] ?? ''}
                          onChange={(e) => setDecisionNotes((prev) => ({ ...prev, [control.id]: e.target.value }))}
                          className={`${inputCls} mt-4`}
                          placeholder="Decision notes"
                        />
                      </div>
                    ))}
                  </div>
                )}
              </>
            )}
          </section>

          <section className="admin-panel">
            <div className="admin-panel-head">
              <h2 className="admin-panel-title">Recent Activity</h2>
            </div>
            {auditLog.length === 0 ? (
              <div className="admin-empty m-4">No activity recorded for this guideline.</div>
            ) : (
              <table className="sd-table">
                <thead>
                  <tr>
                    <th>Action</th>
                    <th>Notes</th>
                    <th>When</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {auditLog.slice(0, 12).map((row) => (
                    <tr key={row.id}>
                      <td className="primary-cell">{row.action}</td>
                      <td>{row.notes ?? '-'}</td>
                      <td>{new Date(row.createdAt).toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>
        </div>
      </section>
    </div>
  )
}

function isSupportedGuidelineAttachment(attachment: Attachment) {
  const fileName = attachment.fileName.toLowerCase()
  const contentType = attachment.contentType.toLowerCase()
  return fileName.endsWith('.pdf') ||
    fileName.endsWith('.txt') ||
    contentType === 'application/pdf' ||
    contentType.startsWith('text/')
}

function parseAiControlJson(raw: string): CreateUnderwritingGuidelineControlRequest[] {
  const parsed = JSON.parse(raw)
  const parsedControls = Array.isArray(parsed) ? parsed : parsed?.controls
  if (!Array.isArray(parsedControls) || parsedControls.length === 0)
    throw new Error('Paste JSON with a non-empty controls array.')

  return parsedControls.map((control, index) => {
    if (!ITEM_TYPES.includes(control.itemType))
      throw new Error(`Control ${index + 1} has an invalid itemType.`)
    if (!STAGES.includes(control.stage))
      throw new Error(`Control ${index + 1} has an invalid stage.`)
    if (!SEVERITIES.includes(control.severity))
      throw new Error(`Control ${index + 1} has an invalid severity.`)
    if (typeof control.ruleKey !== 'string' || !control.ruleKey.trim())
      throw new Error(`Control ${index + 1} is missing ruleKey.`)
    if (typeof control.label !== 'string' || !control.label.trim())
      throw new Error(`Control ${index + 1} is missing label.`)

    return {
      itemType: control.itemType,
      stage: control.stage,
      severity: control.severity,
      ruleKey: control.ruleKey.trim(),
      label: control.label.trim(),
      description: stringOrNull(control.description),
      conditionJson: normalizeImportedCondition(control.conditionJson),
      isBlocking: Boolean(control.isBlocking),
      overrideAllowed: control.overrideAllowed !== false,
      overridePermission: stringOrNull(control.overridePermission) ?? 'underwriting.clearance.override',
      sourceCitation: stringOrNull(control.sourceCitation),
      aiConfidence: typeof control.aiConfidence === 'number' ? control.aiConfidence : null,
      sortOrder: typeof control.sortOrder === 'number' && control.sortOrder > 0 ? control.sortOrder : (index + 1) * 10,
    }
  })
}

function stringOrNull(value: unknown): string | null {
  return typeof value === 'string' && value.trim() ? value.trim() : null
}

function normalizeImportedCondition(value: unknown): string | null {
  if (value == null || value === '') return null
  if (typeof value === 'string') return value.trim() ? value.trim() : null
  return JSON.stringify(value)
}

function getConditionField(key: string): ConditionField {
  return CONDITION_FIELDS.find((field) => field.key === key) ?? CONDITION_FIELDS[1]
}

function parseControlCondition(conditionJson?: string | null): ParsedCondition {
  if (!conditionJson?.trim()) return { mode: 'always' }

  try {
    const parsed = JSON.parse(conditionJson)
    const fieldKey = typeof parsed.field === 'string' ? parsed.field : ''
    const field = CONDITION_FIELDS.find((option) => option.key === fieldKey)
    if (!field) return { mode: 'unsupported', raw: conditionJson, reason: fieldKey ? `Unsupported field "${fieldKey}"` : 'Missing field' }

    const operator = normalizeConditionOperator(parsed.operator)
    if (!operator) return { mode: 'unsupported', raw: conditionJson, reason: 'Unsupported comparison' }

    const value = displayConditionValue(field, parsed.value)
    return { mode: 'builder', field, operator, value }
  } catch {
    return { mode: 'unsupported', raw: conditionJson, reason: 'Invalid condition format' }
  }
}

function normalizeConditionOperator(value: unknown): ConditionOperator | null {
  if (typeof value !== 'string') return null
  const normalized = value.trim()
  if (CONDITION_OPERATORS.includes(normalized as ConditionOperator)) return normalized as ConditionOperator
  return {
    greaterThan: '>',
    greaterThanOrEqual: '>=',
    lessThan: '<',
    lessThanOrEqual: '<=',
    equals: '==',
    '=': '==',
    notEquals: '!=',
    contains: 'contains',
    notContains: 'notContains',
  }[normalized] as ConditionOperator | undefined ?? null
}

function displayConditionValue(field: ConditionField, value: unknown) {
  if (field.kind === 'text') return typeof value === 'string' ? value : ''
  const numericValue = typeof value === 'number'
    ? value
    : typeof value === 'string'
      ? Number(value)
      : 0

  if (field.kind === 'boolean') return numericValue === 0 ? '0' : '1'
  if (field.kind === 'percent') return Number.isFinite(numericValue) ? String(numericValue * 100) : '0'
  if (field.kind === 'percent-whole') return Number.isFinite(numericValue) ? String(numericValue) : '0'
  return Number.isFinite(numericValue) ? String(numericValue) : '0'
}

function buildControlCondition(field: ConditionField, operator: ConditionOperator, value: string) {
  if (field.kind === 'text') {
    return JSON.stringify({ field: field.key, operator, value })
  }
  const numericValue = Number(value) || 0
  const storedValue = field.kind === 'percent'
    ? numericValue / 100
    : field.kind === 'boolean'
      ? (value === '0' ? 0 : 1)
      : numericValue
  return JSON.stringify({ field: field.key, operator, value: storedValue })
}

function describeControlCondition(condition: Extract<ParsedCondition, { mode: 'builder' }>) {
  const rawValue = condition.field.kind === 'boolean'
    ? (condition.value === '0' ? 'No' : 'Yes')
    : condition.field.kind === 'text'
      ? condition.value || '-'
    : condition.field.kind === 'percent'
      ? `${condition.value || 0}%`
      : condition.field.kind === 'percent-whole'
        ? `${condition.value || 0}%`
      : condition.field.kind === 'currency'
        ? Number(condition.value || 0).toLocaleString(undefined, { style: 'currency', currency: 'USD', maximumFractionDigits: 0 })
        : condition.value || 0
  return `Applies when ${condition.field.label} ${operatorLabel(condition.operator).toLowerCase()} ${rawValue}.`
}

function operatorLabel(operator: ConditionOperator) {
  return {
    '>': '>',
    '>=': '>=',
    '<': '<',
    '<=': '<=',
    '==': 'is',
    '!=': 'is not',
    contains: 'contains',
    notContains: 'does not contain',
  }[operator]
}

function itemTypeLabel(value: UnderwritingControlItemType) {
  return {
    AppetiteRule: 'Appetite rule',
    ReferralTrigger: 'Referral trigger',
    AuthorityLimit: 'Authority limit',
    DocumentChecklistItem: 'Document checklist',
    AppetiteNote: 'Appetite note',
  }[value]
}

function stageLabel(value: UnderwritingControlStage) {
  return {
    Submission: 'Submission',
    Quote: 'Quote',
    Bind: 'Bind',
    Issue: 'Issue',
    PostBind: 'Post-bind',
    Renewal: 'Renewal',
  }[value]
}

function severityLabel(value: UnderwritingControlSeverity) {
  return {
    Informational: 'Informational',
    Warning: 'Warning',
    ReferralRequired: 'Referral required',
    HardBlock: 'Hard block',
  }[value]
}

function decisionPastTense(action: 'approve' | 'reject' | 'publish' | 'retire') {
  return {
    approve: 'approved',
    reject: 'rejected',
    publish: 'published',
    retire: 'retired',
  }[action]
}

function statusLabel(value: UnderwritingControlStatus) {
  return {
    AiSuggested: 'AI suggested',
    Draft: 'Draft',
    Approved: 'Approved',
    Published: 'Published',
    Rejected: 'Rejected',
    Retired: 'Retired',
  }[value]
}
