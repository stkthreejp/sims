import { useState, useEffect, useMemo, useRef } from 'react'
import { Link, useParams, useNavigate, useLocation } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft, Plus, Pencil, Trash2, X, Check, FileText,
  RefreshCw, AlertTriangle, ExternalLink, Send,
} from 'lucide-react'
import { toast } from 'sonner'
import { submissionsApi } from '@/api/submissions.api'
import { inboundEmailsApi } from '@/api/inboundEmails.api'
import { quotesApi } from '@/api/quotes.api'
import { carriersApi } from '@/api/carriers.api'
import { programConfigurationsApi } from '@/api/programConfigurations.api'
import { usersApi } from '@/api/users.api'
import { agentsApi } from '@/api/agents.api'
import { submissionDriversApi, submissionVehiclesApi, submissionLocationsApi, submissionPriorCarriersApi, submissionAdditionalInterestsApi, submissionSupplementalApi, submissionGLApi, submissionIMApi, imLookupsApi } from '@/api/submissionLob.api'
import { submissionLossHistoryApi } from '@/api/submissionLossHistory.api'
import { insuredsApi } from '@/api/insureds.api'
import { outboundCommunicationsApi } from '@/api/outboundCommunications.api'
import { VEHICLE_CLASS_LABELS, OPERATING_RADIUS_LABELS, IM_DEDUCTIBLE_TIERS, SETTLEMENT_BASIS_LABELS, APD_VEHICLE_CLASS_OPTIONS, APD_ROAD_TYPE_OPTIONS, APD_OPERATION_CODE_OPTIONS, APD_DRIVER_AGE_CODE_OPTIONS, APD_DRIVER_POINTS_CODE_OPTIONS, APD_DRIVER_EXP_MOD_OPTIONS, APD_COMP_DEDUCTIBLE_OPTIONS, APD_COLL_DEDUCTIBLE_OPTIONS, APD_SUPPORTED_STATES, ADDITIONAL_INTEREST_APPLIES_TO_LABELS, GL_CLASS_CODE_OPTIONS } from '@/types/submissionLob.types'
import type { SubmissionDriver, SubmissionDriverCreate, SubmissionVehicle, SubmissionVehicleCreate, SubmissionLocation, SubmissionLocationCreate, SubmissionPriorCarrier, SubmissionPriorCarrierCreate, SubmissionAdditionalInterestCreate, SubmissionAdditionalInterestBlanketUpsert, SubmissionSupplemental, SubmissionSupplementalUpsert, SubmissionGLCoveragesUpsert, SubmissionGLClassificationCreate, VehicleClass, OperatingRadius, SubmissionEquipment, SubmissionEquipmentCreate, SettlementBasis, AdditionalInterestAppliesToType } from '@/types/submissionLob.types'
import { GL_OCC_LIMIT_OPTIONS, GL_PCO_LIMIT_OPTIONS, GL_MED_LIMIT_OPTIONS, GL_LL_LIMIT_OPTIONS } from '@/types/submissionLob.types'
import { SUBMISSION_STATUS_LABELS, type SubmissionStatus, type SubmissionUpdate, type Submission, type UnderwritingClearanceEvaluation, type UnderwritingClearanceStatus, type UnderwritingClearanceCheckType, type UnderwritingReferralSummary, type UnderwritingReferralStatus } from '@/types/submission.types'
import { LOB_LABELS, ACTIVE_LOBS, QUOTE_STATUS_LABELS, type PolicyLineOfBusiness, type QuoteStatus, type QuoteCreate } from '@/types/quote.types'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { EmptyState } from '@/components/common/EmptyState'
import { formatCurrency, parseDateOnly } from '@/lib/utils'
import { DocumentsSection } from '@/components/documents/DocumentsSection'
import { GenerateDocumentModal } from '@/components/documents/GenerateDocumentModal'
import { usePermissions } from '@/hooks/usePermissions'
import { getApiErrorMessage } from '@/lib/apiError'

// ── Constants ──────────────────────────────────────────────────────────────────

const LOB_SHORT: Record<string, string> = {
  AutoLiability: 'AL',
  AutoPhysicalDamage: 'APD',
  CommercialAuto: 'CA',
  GeneralLiability: 'GL',
  InlandMarine: 'IM',
  WorkersCompensation: 'WC',
  Property: 'Prop',
  BusinessOwners: 'BOP',
  ProfessionalLiability: 'PL',
  Umbrella: 'UMB',
  Cyber: 'CYB',
  ExcessLiability: 'XS',
}

const getLobLabel = (lob: string) => LOB_LABELS[lob as PolicyLineOfBusiness] ?? lob

const STATUS_PILL: Record<SubmissionStatus, string> = {
  New: 'new',
  InProgress: 'inprogress',
  Quoted: 'quoted',
  Bound: 'bound',
  Declined: 'declined',
  Withdrawn: 'withdrawn',
}

const QUOTE_STATUS_PILL: Record<QuoteStatus, string> = {
  Draft: 'draft',
  Submitted: 'submitted',
  Quoted: 'quoted',
  Bound: 'bound',
  Declined: 'declined',
  Cancelled: 'cancelled',
  Expired: 'expired',
}

const STAGES = ['New', 'In Progress', 'Released', 'Quoted', 'Bound']
const STATUS_TO_STAGE: Record<SubmissionStatus, { idx: number; label: string }> = {
  New:        { idx: 0, label: 'New' },
  InProgress: { idx: 1, label: 'In Progress' },
  Quoted:     { idx: 3, label: 'Awaiting Decision' },
  Bound:      { idx: 4, label: 'Bound' },
  Declined:   { idx: 1, label: 'Declined' },
  Withdrawn:  { idx: 1, label: 'Withdrawn' },
}

type QuoteForm = {
  programId: string
  carrierId: string
  lineOfBusiness: PolicyLineOfBusiness | ''
  effectiveDate: string
  expirationDate: string
  premiumAmount: string
  taxesAndFees: string
  coverageDescription: string
  deductible: string
  limit: string
  uninsuredMotoristLimit: string
  medicalPaymentsLimit: string
  companyId: string
  producerId: string
  isFilingState: boolean
}

const emptyQuoteForm = (): QuoteForm => ({
  programId: '', carrierId: '', lineOfBusiness: '', effectiveDate: '', expirationDate: '',
  premiumAmount: '', taxesAndFees: '0',
  coverageDescription: '', deductible: '', limit: '',
  uninsuredMotoristLimit: '', medicalPaymentsLimit: '',
  companyId: '', producerId: '', isFilingState: false,
})

// ── Helpers ────────────────────────────────────────────────────────────────────

function fmtMoney(n: number | null | undefined) {
  if (n == null) return '—'
  return '$' + n.toLocaleString('en-US', { maximumFractionDigits: 0 })
}

function fmtMoneyK(n: number | null | undefined) {
  if (n == null) return '—'
  if (Math.abs(n) >= 1e6) return '$' + (n / 1e6).toFixed(2).replace(/\.?0+$/, '') + 'M'
  if (Math.abs(n) >= 1e3) return '$' + Math.round(n / 1e3) + 'K'
  return '$' + n.toLocaleString()
}

function fmtPct(n: number | null | undefined) {
  if (n == null) return '—'
  return `${(n * 100).toFixed(1)}%`
}

function daysUntil(dateStr: string | null | undefined): number | null {
  if (!dateStr) return null
  const diff = parseDateOnly(dateStr).getTime() - Date.now()
  return Math.ceil(diff / 86400000)
}

// ── Page Component ─────────────────────────────────────────────────────────────

type Tab = 'quotes' | 'exposures' | 'additional-interests' | 'prior-carriers' | 'documents' | 'activity'
type ExposureLob = 'auto' | 'gl' | 'im'

function hasAutoExposureLine(linesOfBusiness: string[]) {
  return linesOfBusiness.some((lob) =>
    lob === 'CommercialAuto' || lob === 'AutoLiability' || lob === 'AutoPhysicalDamage'
  )
}

const CLEARANCE_STATUS_LABELS: Record<UnderwritingClearanceStatus, string> = {
  Clear: 'Clear',
  Warning: 'Warning',
  Blocked: 'Blocked',
}

const CLEARANCE_STATUS_PILL: Record<UnderwritingClearanceStatus, string> = {
  Clear: 'bound',
  Warning: 'quoted',
  Blocked: 'declined',
}

const CLEARANCE_CHECK_LABELS: Record<UnderwritingClearanceCheckType, string> = {
  DuplicateSubmission: 'Duplicate submission',
  ActivePolicyOverlap: 'Active policy overlap',
}

const REFERRAL_STATUS_PILL: Record<UnderwritingReferralStatus, string> = {
  Open: 'declined',
  Approved: 'bound',
  Declined: 'declined',
  Waived: 'quoted',
}

const REFERRAL_DECISION_LABELS: Record<Exclude<UnderwritingReferralStatus, 'Open'>, string> = {
  Approved: 'Approve',
  Declined: 'Decline',
  Waived: 'Waive',
}

export function SubmissionDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const location = useLocation()
  const qc = useQueryClient()
  const { canUploadAttachments, canDeleteAttachments, canCreatePolicies, canDeletePolicies, canManageUnderwriting, canOverrideClearance } = usePermissions()

  const extractionState = location.state as { extractionStatus?: string; emailId?: string } | null
  const [showExtractionBanner, setShowExtractionBanner] = useState(
    extractionState?.extractionStatus === 'Failed' || extractionState?.extractionStatus === 'DetectionFailed'
  )
  const [reExtractLob, setReExtractLob] = useState('')

  const [activeTab, setActiveTab] = useState<Tab>('quotes')
  const [expLob, setExpLob] = useState<ExposureLob>('auto')
  const [showLobEditor, setShowLobEditor] = useState(false)
  const [showSubmissionEditor, setShowSubmissionEditor] = useState(false)
  const [submissionForm, setSubmissionForm] = useState<SubmissionUpdate | null>(null)
  const [showGenerateModal, setShowGenerateModal] = useState(false)
  const [showQuoteForm, setShowQuoteForm] = useState(false)
  const [quoteForm, setQuoteForm] = useState<QuoteForm>(emptyQuoteForm())
  const [showClearanceOverride, setShowClearanceOverride] = useState(false)
  const [clearanceOverrideReason, setClearanceOverrideReason] = useState('')
  const [referralDecision, setReferralDecision] = useState<{ referralId: string; status: Exclude<UnderwritingReferralStatus, 'Open'> } | null>(null)
  const [referralDecisionNotes, setReferralDecisionNotes] = useState('')

  const openLossHistory = () => {
    if (id) navigate(`/submissions/${id}/loss-history`)
  }
  const [supplementalOpen, setSupplementalOpen] = useState(false)
  const emptyDriverForm = (): SubmissionDriverCreate => ({ driverNumber: 1, name: '', dateOfBirth: undefined, licenseNumber: undefined, licenseState: undefined, dateHired: undefined })
  const [showDriverForm, setShowDriverForm] = useState(false)
  const [driverForm, setDriverForm] = useState<SubmissionDriverCreate>(emptyDriverForm())
  const [editingDriverId, setEditingDriverId] = useState<string | null>(null)
  const emptyVehicleForm = (): SubmissionVehicleCreate => ({ unitNumber: 1, vehicleClass: 'Unknown' })
  const [showVehicleForm, setShowVehicleForm] = useState(false)
  const [vehicleForm, setVehicleForm] = useState<SubmissionVehicleCreate>(emptyVehicleForm())
  const [editingVehicleId, setEditingVehicleId] = useState<string | null>(null)
  const emptyLocationForm = (): SubmissionLocationCreate => ({ locationNumber: 1, address: '', country: 'USA', isPrimary: false })
  const [showLocationForm, setShowLocationForm] = useState(false)
  const [locationForm, setLocationForm] = useState<SubmissionLocationCreate>(emptyLocationForm())
  const [editingLocationId, setEditingLocationId] = useState<string | null>(null)
  const emptyCarrierForm = (): SubmissionPriorCarrierCreate => ({ carrierName: '' })
  const [showCarrierForm, setShowCarrierForm] = useState(false)
  const [carrierForm, setCarrierForm] = useState<SubmissionPriorCarrierCreate>(emptyCarrierForm())
  const [editingCarrierId, setEditingCarrierId] = useState<string | null>(null)
  const emptyAdditionalInterestForm = (): SubmissionAdditionalInterestCreate => ({
    lineOfBusiness: ACTIVE_LOBS[0],
    name: '',
    appliesToType: 'Blanket',
    additionalInsured: false,
    lossPayee: false,
    waiverOfSubrogation: false,
    primaryNonContributory: false,
  })
  const [showAdditionalInterestForm, setShowAdditionalInterestForm] = useState(false)
  const [additionalInterestForm, setAdditionalInterestForm] = useState<SubmissionAdditionalInterestCreate>(emptyAdditionalInterestForm())
  const [editingAdditionalInterestId, setEditingAdditionalInterestId] = useState<string | null>(null)
  const emptyEquipmentForm = (): SubmissionEquipmentCreate => ({ itemNumber: 1 })
  const [showEquipmentForm, setShowEquipmentForm] = useState(false)
  const [equipmentForm, setEquipmentForm] = useState<SubmissionEquipmentCreate>(emptyEquipmentForm())
  const [editingEquipmentId, setEditingEquipmentId] = useState<string | null>(null)
  const savingEquipmentRef = useRef(false)
  const emptySupplementalForm = (): SubmissionSupplementalUpsert => ({ commoditiesHauled: [], terminalLocations: [], safetyProgramInPlace: false, filingsRequired: [], ownerOperator: false })
  const [supplementalForm, setSupplementalForm] = useState<SubmissionSupplementalUpsert>(emptySupplementalForm())
  const [supplementalDirty, setSupplementalDirty] = useState(false)
  const emptyGlCovForm = (): SubmissionGLCoveragesUpsert => ({ aiIndividualCount: 0, aiBlanket: false, wosIndividualCount: 0, wosBlanket: false, primaryNonContributory: false, includeTria: false })
  const [glCovForm, setGlCovForm] = useState<SubmissionGLCoveragesUpsert>(emptyGlCovForm())
  const [glCovDirty, setGlCovDirty] = useState(false)
  const emptyGlClassForm = (): SubmissionGLClassificationCreate => ({ locationNumber: 1 })
  const [showGlClassForm, setShowGlClassForm] = useState(false)
  const [glClassForm, setGlClassForm] = useState<SubmissionGLClassificationCreate>(emptyGlClassForm())
  const [editingGlClassId, setEditingGlClassId] = useState<string | null>(null)

  // ── Queries ────────────────────────────────────────────────────────────────

  const { data: submission, isLoading } = useQuery({
    queryKey: ['submissions', id],
    queryFn: () => submissionsApi.getById(id!),
  })

  const { data: clearance } = useQuery({
    queryKey: ['submissions', id, 'clearance'],
    queryFn: () => submissionsApi.getClearance(id!),
    enabled: !!id && canManageUnderwriting,
    retry: false,
  })

  const { data: referralSummary } = useQuery({
    queryKey: ['submissions', id, 'underwriting-referrals'],
    queryFn: () => submissionsApi.getUnderwritingReferrals(id!),
    enabled: !!id && canManageUnderwriting,
  })

  const { data: quotes = [] } = useQuery({
    queryKey: ['quotes', 'by-submission', id],
    queryFn: () => quotesApi.getBySubmission(id!),
    enabled: !!id,
  })

  const { data: carriers = [] } = useQuery({
    queryKey: ['carriers', 'active'],
    queryFn: () => carriersApi.getAll(true),
  })

  const { data: programs = [] } = useQuery({
    queryKey: ['program-configurations', 'options', 'active'],
    queryFn: () => programConfigurationsApi.getOptions(false),
  })

  const { data: usersData } = useQuery({
    queryKey: ['users', { pageSize: 100 }],
    queryFn: () => usersApi.getAll({ pageSize: 100 }),
  })
  const users = usersData?.items ?? []

  const { data: agents = [] } = useQuery({
    queryKey: ['agents', 'active'],
    queryFn: () => agentsApi.getAll(true),
  })

  const { data: drivers = [] } = useQuery({
    queryKey: ['submission-drivers', id],
    queryFn: () => submissionDriversApi.getAll(id!),
    enabled: !!id && activeTab === 'exposures',
  })

  const { data: vehicles = [] } = useQuery({
    queryKey: ['submission-vehicles', id],
    queryFn: () => submissionVehiclesApi.getAll(id!),
    enabled: !!id && activeTab === 'exposures',
  })

  const { data: locations = [] } = useQuery({
    queryKey: ['submission-locations', id],
    queryFn: () => submissionLocationsApi.getAll(id!),
    enabled: !!id && activeTab === 'exposures',
  })

  const { data: priorCarriers = [] } = useQuery({
    queryKey: ['submission-prior-carriers', id],
    queryFn: () => submissionPriorCarriersApi.getAll(id!),
    enabled: !!id && activeTab === 'prior-carriers',
  })

  const { data: additionalInterests = [] } = useQuery({
    queryKey: ['submission-additional-interests', id],
    queryFn: () => submissionAdditionalInterestsApi.getAll(id!),
    enabled: !!id && activeTab === 'additional-interests',
  })

  const { data: additionalInterestBlankets = [] } = useQuery({
    queryKey: ['submission-additional-interest-blankets', id],
    queryFn: () => submissionAdditionalInterestsApi.getBlankets(id!),
    enabled: !!id && activeTab === 'additional-interests',
  })

  const { data: supplemental } = useQuery({
    queryKey: ['submission-supplemental', id],
    queryFn: () => submissionSupplementalApi.get(id!),
    enabled: !!id && supplementalOpen,
    // Seeds a dirty-tracked form; a focus refetch would clobber unsaved edits.
    refetchOnWindowFocus: false,
  })

  const { data: glCoverages } = useQuery({
    queryKey: ['submission-gl-coverages', id],
    queryFn: () => submissionGLApi.getCoverages(id!),
    enabled: !!id,
    // Seeds a dirty-tracked form; a focus refetch would clobber unsaved edits.
    refetchOnWindowFocus: false,
  })

  const { data: glClassifications = [] } = useQuery({
    queryKey: ['submission-gl-classifications', id],
    queryFn: () => submissionGLApi.getClassifications(id!),
    enabled: !!id && activeTab === 'exposures',
  })

  const { data: imCoverages } = useQuery({
    queryKey: ['submission-im-coverages', id],
    queryFn: () => submissionIMApi.getCoverages(id!),
    enabled: !!id,
  })

  const { data: equipment = [] } = useQuery({
    queryKey: ['submission-equipment', id],
    queryFn: () => submissionIMApi.getEquipment(id!),
    enabled: !!id && activeTab === 'exposures',
  })

  const { data: lossSummary } = useQuery({
    queryKey: ['submission-loss-history-summary', id],
    queryFn: () => submissionLossHistoryApi.getSummary(id!),
    enabled: !!id,
  })

  const { data: imEquipmentTypes = [] } = useQuery({
    queryKey: ['im-equipment-types'],
    queryFn: () => imLookupsApi.getEquipmentTypes(),
    enabled: activeTab === 'exposures',
    staleTime: 5 * 60 * 1000,
  })

  const { data: imTerritories = [] } = useQuery({
    queryKey: ['im-territories'],
    queryFn: () => imLookupsApi.getTerritories(),
    enabled: activeTab === 'exposures',
    staleTime: 5 * 60 * 1000,
  })

  const { data: insured } = useQuery({
    // Align with the canonical ['insureds', id] key so an insured edit (which
    // invalidates ['insureds']) refreshes this header instead of drifting (audit X7).
    queryKey: ['insureds', submission?.insuredId],
    queryFn: () => insuredsApi.getById(submission!.insuredId),
    enabled: !!submission?.insuredId,
  })

  const { data: outboundCommunications = [] } = useQuery({
    queryKey: ['submission-outbound-communications', id],
    queryFn: () => outboundCommunicationsApi.getForEntity('Submission', id!),
    enabled: !!id && activeTab === 'activity',
  })

  const sendCommunicationMutation = useMutation({
    mutationFn: (communicationId: string) => outboundCommunicationsApi.send(communicationId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['submission-outbound-communications'] })
      toast.success('Email sent')
    },
    onError: (e: any) => {
      qc.invalidateQueries({ queryKey: ['submission-outbound-communications'] })
      toast.error(getApiErrorMessage(e, 'Failed to send email'))
    },
  })

  const defaultTerritoryCode = useMemo(() => {
    const state = insured?.state?.trim().toUpperCase()
    if (!state || imTerritories.length === 0) return undefined
    const match = imTerritories.find((t) =>
      t.states.split(',').map((s) => s.trim().toUpperCase()).includes(state)
    )
    return match?.code
  }, [insured?.state, imTerritories])

  const prepareLocationPayload = (form: SubmissionLocationCreate): SubmissionLocationCreate => ({
    locationNumber: form.locationNumber || 1,
    address: form.address.trim(),
    city: form.city?.trim() || undefined,
    state: form.state?.trim().toUpperCase() || undefined,
    county: form.county?.trim() || undefined,
    zipCode: form.zipCode?.trim() || undefined,
    country: form.country?.trim().toUpperCase() || undefined,
    isPrimary: form.isPrimary,
  })

  const openNewLocationForm = () => {
    const lastLocationNumber = locations.reduce((max, item) => Math.max(max, item.locationNumber), 0)
    setLocationForm({
      locationNumber: lastLocationNumber + 1,
      address: insured?.addressLine1 ?? '',
      city: insured?.city || undefined,
      state: insured?.state || undefined,
      county: insured?.county || undefined,
      zipCode: insured?.zipCode || undefined,
      country: 'USA',
      isPrimary: locations.length === 0,
    })
    setEditingLocationId(null)
    setShowLocationForm(true)
  }

  const openEditLocationForm = (riskLocation: SubmissionLocation) => {
    setLocationForm({
      locationNumber: riskLocation.locationNumber,
      address: riskLocation.address,
      city: riskLocation.city ?? undefined,
      state: riskLocation.state ?? undefined,
      county: riskLocation.county ?? undefined,
      zipCode: riskLocation.zipCode ?? undefined,
      country: riskLocation.country ?? undefined,
      isPrimary: riskLocation.isPrimary,
    })
    setEditingLocationId(riskLocation.id)
    setShowLocationForm(true)
  }

  const prepareEquipmentPayload = (form: SubmissionEquipmentCreate): SubmissionEquipmentCreate => ({
    itemNumber: form.itemNumber || 1,
    year: form.year,
    make: form.make?.trim() || undefined,
    model: form.model?.trim() || undefined,
    description: form.description?.trim() || undefined,
    serialNumber: form.serialNumber?.trim() || undefined,
    value: form.value,
    equipmentTypeId: form.equipmentTypeId || undefined,
    territoryCode: form.territoryCode || (!editingEquipmentId ? defaultTerritoryCode : undefined),
    deductible: form.deductible,
    settlementBasis: form.settlementBasis || undefined,
  })

  const openNewEquipmentForm = () => {
    const lastItemNumber = equipment.reduce((max, item) => Math.max(max, item.itemNumber), 0)
    setEquipmentForm({ itemNumber: lastItemNumber + 1, territoryCode: defaultTerritoryCode })
    setEditingEquipmentId(null)
    setShowEquipmentForm(true)
  }

  const saveEquipment = () => {
    if (savingEquipmentRef.current || saveEquipmentMutation.isPending) return
    savingEquipmentRef.current = true
    saveEquipmentMutation.mutate(prepareEquipmentPayload(equipmentForm), {
      onSettled: () => { savingEquipmentRef.current = false },
    })
  }

  useEffect(() => {
    if (!showEquipmentForm || editingEquipmentId || equipmentForm.territoryCode || !defaultTerritoryCode) return
    setEquipmentForm((form) => ({ ...form, territoryCode: defaultTerritoryCode }))
  }, [defaultTerritoryCode, editingEquipmentId, equipmentForm.territoryCode, showEquipmentForm])

  useEffect(() => {
    if (supplemental) {
      setSupplementalForm({
        commoditiesHauled: supplemental.commoditiesHauled,
        terminalLocations: supplemental.terminalLocations,
        filingsRequired: supplemental.filingsRequired,
        safetyProgramInPlace: supplemental.safetyProgramInPlace,
        ownerOperator: supplemental.ownerOperator,
      })
    }
  }, [supplemental])

  useEffect(() => {
    if (glCoverages) {
      setGlCovForm({
        generalAggregate: glCoverages.generalAggregate ?? undefined,
        productsCompletedOps: glCoverages.productsCompletedOps ?? undefined,
        eachOccurrence: glCoverages.eachOccurrence ?? undefined,
        personalAndAdvInjury: glCoverages.personalAndAdvInjury ?? undefined,
        damageToRentedPremises: glCoverages.damageToRentedPremises ?? undefined,
        medicalExpense: glCoverages.medicalExpense ?? undefined,
        totalSubcontractorCost: glCoverages.totalSubcontractorCost ?? undefined,
        aiIndividualCount: glCoverages.aiIndividualCount ?? 0,
        aiBlanket: glCoverages.aiBlanket ?? false,
        wosIndividualCount: glCoverages.wosIndividualCount ?? 0,
        wosBlanket: glCoverages.wosBlanket ?? false,
        primaryNonContributory: glCoverages.primaryNonContributory ?? false,
        includeTria: glCoverages.includeTria ?? false,
        loggingLumberingLimit: glCoverages.loggingLumberingLimit ?? undefined,
      })
    }
  }, [glCoverages])

  useEffect(() => {
    if (!submission) return

    const hasAuto = hasAutoExposureLine(submission.linesOfBusiness)
    const hasGL = submission.linesOfBusiness.includes('GeneralLiability')
    const hasIM = submission.linesOfBusiness.includes('InlandMarine')
    const preferredLob: ExposureLob = hasAuto ? 'auto' : hasGL ? 'gl' : hasIM ? 'im' : 'auto'

    if ((expLob === 'auto' && !hasAuto) || (expLob === 'gl' && !hasGL) || (expLob === 'im' && !hasIM)) {
      setExpLob(preferredLob)
    }
  }, [submission, expLob])

  const selectedProgram = programs.find((p) => p.id === quoteForm.programId)
  const insuredStateCode = insured?.state?.trim().toUpperCase()
  const configuredProgramCarriers = selectedProgram
    ? selectedProgram.carriers.filter((programCarrier) =>
        programCarrier.isActive &&
        programCarrier.linesOfBusiness.some((lob) =>
          lob.isActive &&
          (!insuredStateCode || lob.states.some((state) => state.isActive && state.stateCode === insuredStateCode))
        )
      )
    : []
  const quoteCarrierOptions = selectedProgram
    ? configuredProgramCarriers.map((programCarrier) => ({ id: programCarrier.carrierId, name: programCarrier.carrierName }))
    : carriers
  const selectedCarrier = carriers.find((c) => c.id === quoteForm.carrierId)
  const selectedProgramCarrier = configuredProgramCarriers.find((programCarrier) => programCarrier.carrierId === quoteForm.carrierId)
  const submissionLobOptions = submission?.linesOfBusiness.length ? submission.linesOfBusiness : ACTIVE_LOBS
  const availableLobs = selectedProgram && selectedProgramCarrier
    ? selectedProgramCarrier.linesOfBusiness
        .filter((lob) =>
          lob.isActive &&
          submissionLobOptions.includes(lob.lineOfBusiness) &&
          (!insuredStateCode || lob.states.some((state) => state.isActive && state.stateCode === insuredStateCode))
        )
        .map((lob) => lob.lineOfBusiness)
    : selectedCarrier
      ? selectedCarrier.linesOfBusiness.filter((l) => submissionLobOptions.includes(l))
      : submissionLobOptions

  // ── Mutations ──────────────────────────────────────────────────────────────

  const reExtract = useMutation({
    mutationFn: () => inboundEmailsApi.reExtract(extractionState!.emailId!, reExtractLob || undefined),
    onSuccess: (result) => {
      if (result.extractionStatus === 'Completed' || result.extractionStatus === 'DetectionFailed') {
        toast.success('Data extracted successfully — refreshing page data')
        setShowExtractionBanner(false)
        qc.invalidateQueries({ queryKey: ['submissions', id] })
        qc.invalidateQueries({ queryKey: ['submission-drivers', id] })
        qc.invalidateQueries({ queryKey: ['submission-vehicles', id] })
        qc.invalidateQueries({ queryKey: ['submission-locations', id] })
        qc.invalidateQueries({ queryKey: ['submission-prior-carriers', id] })
        qc.invalidateQueries({ queryKey: ['submission-additional-interests', id] })
        qc.invalidateQueries({ queryKey: ['submission-supplemental', id] })
        qc.invalidateQueries({ queryKey: ['submission-gl-coverages', id] })
        qc.invalidateQueries({ queryKey: ['submission-gl-classifications', id] })
        qc.invalidateQueries({ queryKey: ['submission-im-coverages', id] })
      } else {
        toast.error('Extraction failed again — please fill in the fields manually below')
      }
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Re-extraction request failed')),
  })

  const setLobsMutation = useMutation({
    mutationFn: (lobs: string[]) => submissionsApi.setLinesOfBusiness(id!, lobs),
    onSuccess: () => {
      // LOB set shows on the submissions list — invalidate the root, not just the detail (audit).
      qc.invalidateQueries({ queryKey: ['submissions'] })
      setShowLobEditor(false)
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed to update lines of business')),
  })

  const updateSubmissionMutation = useMutation({
    mutationFn: (dto: SubmissionUpdate) => submissionsApi.update(id!, dto),
    onSuccess: () => {
      // Status/dates/insured show on the submissions list + dashboard — invalidate the root (audit).
      qc.invalidateQueries({ queryKey: ['submissions'] })
      setShowSubmissionEditor(false)
      setSubmissionForm(null)
      toast.success('Submission updated')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Failed to update submission')),
  })

  const evaluateClearanceMutation = useMutation({
    mutationFn: () => submissionsApi.evaluateClearance(id!),
    onSuccess: (result) => {
      qc.setQueryData(['submissions', id, 'clearance'], result)
      setShowClearanceOverride(false)
      toast.success(result.overallStatus === 'Blocked' ? 'Clearance blocked' : 'Clearance evaluated')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Failed to evaluate clearance')),
  })

  const overrideClearanceMutation = useMutation({
    mutationFn: (reason: string) => submissionsApi.overrideClearance(id!, reason),
    onSuccess: (result) => {
      qc.setQueryData(['submissions', id, 'clearance'], result)
      setShowClearanceOverride(false)
      setClearanceOverrideReason('')
      toast.success('Clearance override recorded')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Failed to override clearance')),
  })

  const decideReferralMutation = useMutation({
    mutationFn: ({ referralId, status, notes }: { referralId: string; status: Exclude<UnderwritingReferralStatus, 'Open'>; notes?: string }) =>
      submissionsApi.decideUnderwritingReferral(referralId, status, notes),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['submissions', id, 'underwriting-referrals'] })
      setReferralDecision(null)
      setReferralDecisionNotes('')
      toast.success('Referral decision recorded')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Failed to record referral decision')),
  })

  const createQuoteMutation = useMutation({
    mutationFn: (data: QuoteCreate) => quotesApi.create(data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quotes', 'by-submission', id] })
      qc.invalidateQueries({ queryKey: ['submissions', id] })
      setShowQuoteForm(false)
      setQuoteForm(emptyQuoteForm())
      toast.success('Quote created')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Failed to create quote')),
  })

  const deleteQuoteMutation = useMutation({
    mutationFn: (quoteId: string) => quotesApi.delete(quoteId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quotes', 'by-submission', id] })
      toast.success('Quote deleted')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Failed to delete quote')),
  })

  const saveDriverMutation = useMutation({
    mutationFn: (dto: SubmissionDriverCreate) =>
      editingDriverId
        ? submissionDriversApi.update(id!, editingDriverId, dto)
        : submissionDriversApi.create(id!, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['submission-drivers', id] })
      setShowDriverForm(false); setDriverForm(emptyDriverForm()); setEditingDriverId(null)
      toast.success('Driver saved')
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed to save driver')),
  })
  const deleteDriverMutation = useMutation({
    mutationFn: (dId: string) => submissionDriversApi.delete(id!, dId),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['submission-drivers', id] }); toast.success('Driver removed') },
  })

  const saveVehicleMutation = useMutation({
    mutationFn: (dto: SubmissionVehicleCreate) =>
      editingVehicleId
        ? submissionVehiclesApi.update(id!, editingVehicleId, dto)
        : submissionVehiclesApi.create(id!, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['submission-vehicles', id] })
      setShowVehicleForm(false); setVehicleForm(emptyVehicleForm()); setEditingVehicleId(null)
      toast.success('Vehicle saved')
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed to save vehicle')),
  })
  const deleteVehicleMutation = useMutation({
    mutationFn: (vId: string) => submissionVehiclesApi.delete(id!, vId),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['submission-vehicles', id] }); toast.success('Vehicle removed') },
  })

  const saveLocationMutation = useMutation({
    mutationFn: (dto: SubmissionLocationCreate) =>
      editingLocationId
        ? submissionLocationsApi.update(id!, editingLocationId, dto)
        : submissionLocationsApi.create(id!, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['submission-locations', id] })
      setShowLocationForm(false); setLocationForm(emptyLocationForm()); setEditingLocationId(null)
      toast.success('Risk location saved')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Failed to save risk location')),
  })
  const deleteLocationMutation = useMutation({
    mutationFn: (locationId: string) => submissionLocationsApi.delete(id!, locationId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['submission-locations', id] })
      toast.success('Risk location removed')
    },
  })

  const savePriorCarrierMutation = useMutation({
    mutationFn: (dto: SubmissionPriorCarrierCreate) =>
      editingCarrierId
        ? submissionPriorCarriersApi.update(id!, editingCarrierId, dto)
        : submissionPriorCarriersApi.create(id!, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['submission-prior-carriers', id] })
      setShowCarrierForm(false); setCarrierForm(emptyCarrierForm()); setEditingCarrierId(null)
      toast.success('Prior carrier saved')
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed to save prior carrier')),
  })
  const deletePriorCarrierMutation = useMutation({
    mutationFn: (cId: string) => submissionPriorCarriersApi.delete(id!, cId),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['submission-prior-carriers', id] }); toast.success('Prior carrier removed') },
  })

  const saveAdditionalInterestMutation = useMutation({
    mutationFn: (dto: SubmissionAdditionalInterestCreate) =>
      editingAdditionalInterestId
        ? submissionAdditionalInterestsApi.update(id!, editingAdditionalInterestId, dto)
        : submissionAdditionalInterestsApi.create(id!, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['submission-additional-interests', id] })
      setShowAdditionalInterestForm(false); setAdditionalInterestForm(emptyAdditionalInterestForm()); setEditingAdditionalInterestId(null)
      toast.success('Additional interest saved')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Failed to save additional interest')),
  })
  const deleteAdditionalInterestMutation = useMutation({
    mutationFn: (aiId: string) => submissionAdditionalInterestsApi.delete(id!, aiId),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['submission-additional-interests', id] }); toast.success('Additional interest removed') },
  })

  const saveAdditionalInterestBlanketMutation = useMutation({
    mutationFn: ({ lineOfBusiness, dto }: { lineOfBusiness: string; dto: SubmissionAdditionalInterestBlanketUpsert }) =>
      submissionAdditionalInterestsApi.upsertBlanket(id!, lineOfBusiness, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['submission-additional-interest-blankets', id] })
      toast.success('Blanket request saved')
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed to save blanket request')),
  })

  const saveEquipmentMutation = useMutation({
    mutationFn: (dto: SubmissionEquipmentCreate) =>
      editingEquipmentId
        ? submissionIMApi.updateEquipment(id!, editingEquipmentId, dto)
        : submissionIMApi.createEquipment(id!, dto),
    onSuccess: (saved) => {
      qc.setQueryData<SubmissionEquipment[]>(['submission-equipment', id], (current = []) => {
        const existingIndex = current.findIndex((item) => item.id === saved.id)
        if (existingIndex >= 0) {
          return current.map((item) => item.id === saved.id ? saved : item)
        }
        return [...current, saved].sort((a, b) => a.itemNumber - b.itemNumber)
      })
      qc.invalidateQueries({ queryKey: ['submission-equipment', id] })
      qc.invalidateQueries({ queryKey: ['submission-im-coverages', id] })
      setShowEquipmentForm(false); setEquipmentForm(emptyEquipmentForm()); setEditingEquipmentId(null)
      toast.success('Equipment saved')
    },
    onError: (e: any) => toast.error(getApiErrorMessage(e, 'Failed to save equipment')),
  })
  const deleteEquipmentMutation = useMutation({
    mutationFn: (eId: string) => submissionIMApi.deleteEquipment(id!, eId),
    onSuccess: (_, eId) => {
      qc.setQueryData<SubmissionEquipment[]>(['submission-equipment', id], (current = []) => current.filter((item) => item.id !== eId))
      qc.invalidateQueries({ queryKey: ['submission-equipment', id] })
      qc.invalidateQueries({ queryKey: ['submission-im-coverages', id] })
      toast.success('Equipment removed')
    },
  })

  const saveSupplementalMutation = useMutation({
    mutationFn: (dto: SubmissionSupplementalUpsert) => submissionSupplementalApi.upsert(id!, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['submission-supplemental', id] })
      setSupplementalDirty(false)
      toast.success('Supplemental info saved')
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed to save supplemental info')),
  })

  const saveGlCovMutation = useMutation({
    mutationFn: (dto: SubmissionGLCoveragesUpsert) => submissionGLApi.upsertCoverages(id!, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['submission-gl-coverages', id] })
      setGlCovDirty(false)
      toast.success('GL coverages saved')
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed to save GL coverages')),
  })

  const saveGlClassMutation = useMutation({
    mutationFn: (dto: SubmissionGLClassificationCreate) =>
      editingGlClassId
        ? submissionGLApi.updateClassification(id!, editingGlClassId, dto)
        : submissionGLApi.createClassification(id!, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['submission-gl-classifications', id] })
      setShowGlClassForm(false); setGlClassForm(emptyGlClassForm()); setEditingGlClassId(null)
      toast.success('GL exposure saved')
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed to save GL exposure')),
  })
  const deleteGlClassMutation = useMutation({
    mutationFn: (classId: string) => submissionGLApi.deleteClassification(id!, classId),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['submission-gl-classifications', id] }); toast.success('GL exposure removed') },
  })

  // ── Handlers ───────────────────────────────────────────────────────────────

  const handleCreateQuote = () => {
    if (!quoteForm.programId || !quoteForm.carrierId || !quoteForm.lineOfBusiness || !quoteForm.effectiveDate || !quoteForm.expirationDate) {
      toast.error('Program, carrier, line of business, and dates are required')
      return
    }
    createQuoteMutation.mutate({
      submissionId: id!,
      programId: quoteForm.programId,
      carrierId: quoteForm.carrierId,
      lineOfBusiness: quoteForm.lineOfBusiness as PolicyLineOfBusiness,
      effectiveDate: quoteForm.effectiveDate,
      expirationDate: quoteForm.expirationDate,
      premiumAmount: parseFloat(quoteForm.premiumAmount) || 0,
      taxesAndFees: parseFloat(quoteForm.taxesAndFees) || 0,
      coverageDescription: quoteForm.coverageDescription || undefined,
      deductible: quoteForm.deductible ? parseFloat(quoteForm.deductible) : undefined,
      limit: quoteForm.limit ? parseFloat(quoteForm.limit) : undefined,
      uninsuredMotoristLimit: quoteForm.uninsuredMotoristLimit ? parseFloat(quoteForm.uninsuredMotoristLimit) : undefined,
      medicalPaymentsLimit: quoteForm.medicalPaymentsLimit ? parseFloat(quoteForm.medicalPaymentsLimit) : undefined,
      companyId: quoteForm.companyId ? parseInt(quoteForm.companyId) : undefined,
      producerId: quoteForm.producerId ? parseInt(quoteForm.producerId) : undefined,
      isFilingState: quoteForm.isFilingState,
    })
  }

  const setQF = (k: keyof QuoteForm) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    const val = e.target.value
    setQuoteForm((prev) => {
      const next = { ...prev, [k]: val }
      if (k === 'programId') {
        next.programId = val
        next.carrierId = ''
        next.lineOfBusiness = ''
      }
      if (k === 'carrierId') {
        next.lineOfBusiness = ''
      }
      if (k === 'lineOfBusiness') {
        if (val === 'GeneralLiability' && glCoverages?.eachOccurrence)
          next.limit = String(glCoverages.eachOccurrence)
        else if (val === 'InlandMarine') {
          if (imCoverages?.scheduledEquipmentTotalLimit)
            next.limit = String(imCoverages.scheduledEquipmentTotalLimit)
          if (imCoverages?.deductible)
            next.deductible = String(imCoverages.deductible)
        }
        if (!prev.effectiveDate && submission?.effectiveDate)
          next.effectiveDate = submission.effectiveDate
        if (!prev.expirationDate && submission?.expirationDate)
          next.expirationDate = submission.expirationDate
      }
      return next
    })
  }

  // ── Derived metrics ────────────────────────────────────────────────────────

  const activeQuotes = quotes.filter((q) => q.status === 'Quoted')
  const totalQuotedPremium = activeQuotes.reduce((a, b) => a + (b.totalPremium ?? 0), 0)
  const declinedCount = quotes.filter((q) => q.status === 'Declined').length
  const daysToEff = submission?.effectiveDate ? daysUntil(submission.effectiveDate) : null
  const stage = submission ? STATUS_TO_STAGE[submission.status] : null

  // ── Early returns ──────────────────────────────────────────────────────────

  if (isLoading) return <LoadingSpinner />
  if (!submission) return <p style={{ padding: 24, color: 'var(--ink-3)' }}>Submission not found.</p>

  // ── Derived exposure LOB state ─────────────────────────────────────────────

  const hasAuto = hasAutoExposureLine(submission.linesOfBusiness)
  const hasGL = submission.linesOfBusiness.includes('GeneralLiability')
  const hasIM = submission.linesOfBusiness.includes('InlandMarine')
  const defaultExposureLob: ExposureLob = hasAuto ? 'auto' : hasGL ? 'gl' : hasIM ? 'im' : 'auto'
  const expLobList = [
    ...(hasAuto ? [{ k: 'auto' as const, label: 'Commercial Auto', count: drivers.length + vehicles.length }] : []),
    ...(hasGL ? [{ k: 'gl' as const, label: 'General Liability', count: glClassifications.length }] : []),
    ...(hasIM ? [{ k: 'im' as const, label: 'Inland Marine', count: equipment.length }] : []),
  ]
  const activeLob: ExposureLob =
    (expLob === 'auto' && !hasAuto) ? defaultExposureLob :
    (expLob === 'gl' && !hasGL) ? defaultExposureLob :
    (expLob === 'im' && !hasIM) ? defaultExposureLob :
    expLob

  const additionalInterestLobs = submission.linesOfBusiness.length ? submission.linesOfBusiness : ACTIVE_LOBS
  const saveBlanketRequest = (lineOfBusiness: string, patch: Partial<SubmissionAdditionalInterestBlanketUpsert>) => {
    const current = additionalInterestBlankets.find((b) => b.lineOfBusiness === lineOfBusiness)
    saveAdditionalInterestBlanketMutation.mutate({
      lineOfBusiness,
      dto: {
        additionalInsured: current?.additionalInsured ?? false,
        waiverOfSubrogation: current?.waiverOfSubrogation ?? false,
        primaryNonContributory: current?.primaryNonContributory ?? false,
        ...patch,
      },
    })
  }

  const openSubmissionEditor = () => {
    setSubmissionForm({
      insuredId: submission.insuredId,
      agentId: submission.agentId ?? undefined,
      underwriterId: submission.underwriterId,
      assistantUWId: submission.assistantUWId ?? undefined,
      effectiveDate: submission.effectiveDate ?? undefined,
      expirationDate: submission.expirationDate ?? undefined,
      descriptionOfOperations: submission.descriptionOfOperations ?? undefined,
      linesOfBusiness: submission.linesOfBusiness,
      status: submission.status,
    })
    setShowSubmissionEditor(true)
  }

  const setSubmissionField = (field: keyof SubmissionUpdate, value: string | string[]) => {
    setSubmissionForm((current) => current ? { ...current, [field]: value } : current)
  }

  const toggleSubmissionLob = (lob: PolicyLineOfBusiness, checked: boolean) => {
    setSubmissionForm((current) => {
      if (!current) return current
      return {
        ...current,
        linesOfBusiness: checked
          ? [...current.linesOfBusiness, lob]
          : current.linesOfBusiness.filter((value) => value !== lob),
      }
    })
  }

  const saveSubmissionEdit = () => {
    if (!submissionForm) return
    if (!submissionForm.underwriterId || submissionForm.linesOfBusiness.length === 0) {
      toast.error('Underwriter and at least one line of business are required')
      return
    }
    updateSubmissionMutation.mutate({
      ...submissionForm,
      agentId: submissionForm.agentId || undefined,
      assistantUWId: submissionForm.assistantUWId || undefined,
      effectiveDate: submissionForm.effectiveDate || undefined,
      expirationDate: submissionForm.expirationDate || undefined,
      descriptionOfOperations: submissionForm.descriptionOfOperations || undefined,
    })
  }

  const submitClearanceOverride = () => {
    const reason = clearanceOverrideReason.trim()
    if (!reason) {
      toast.error('Override reason is required')
      return
    }
    overrideClearanceMutation.mutate(reason)
  }

  const submitReferralDecision = () => {
    if (!referralDecision) return
    decideReferralMutation.mutate({
      referralId: referralDecision.referralId,
      status: referralDecision.status,
      notes: referralDecisionNotes.trim() || undefined,
    })
  }

  const renderClearancePanel = (value: UnderwritingClearanceEvaluation | undefined) => {
    if (!canManageUnderwriting) return null

    const status = value?.overallStatus
    const results = value?.results ?? []
    const hasBlockedResult = results.some((result) => result.status === 'Blocked' && !result.isOverridden)

    return (
      <section className="sd-card" style={{ marginBottom: 14 }}>
        <div className="sd-card-head">
          <h3>Underwriting Clearance</h3>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            {status && <span className={`sd-pill ${CLEARANCE_STATUS_PILL[status]}`}>{CLEARANCE_STATUS_LABELS[status]}</span>}
            {hasBlockedResult && canOverrideClearance && (
              <button
                type="button"
                className="sd-btn outline sm"
                onClick={() => setShowClearanceOverride((value) => !value)}
                disabled={overrideClearanceMutation.isPending}
              >
                <Check size={13} />
                Override
              </button>
            )}
            <button
              type="button"
              className="sd-btn outline sm"
              onClick={() => evaluateClearanceMutation.mutate()}
              disabled={evaluateClearanceMutation.isPending}
            >
              <RefreshCw size={13} className={evaluateClearanceMutation.isPending ? 'animate-spin' : ''} />
              {evaluateClearanceMutation.isPending ? 'Checking' : 'Evaluate'}
            </button>
          </div>
        </div>
        {results.length === 0 ? (
          <div className="sd-card-body" style={{ fontSize: 13, color: 'var(--ink-3)' }}>
            {status === 'Clear' ? 'No clearance conflicts found.' : 'No clearance review has been recorded.'}
          </div>
        ) : (
          <div className="sd-card-body tight">
            <div style={{ display: 'grid', gap: 1, background: 'var(--line-2)' }}>
              {results.map((result) => (
                <div key={`${result.checkType}-${result.matchedRecordId ?? result.matchedRecordLabel ?? 'none'}`} style={{ background: 'var(--surface)', padding: '10px 12px', display: 'grid', gridTemplateColumns: '180px 90px 1fr', gap: 12, alignItems: 'center' }}>
                  <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--ink)' }}>
                    {CLEARANCE_CHECK_LABELS[result.checkType] ?? result.checkType}
                  </div>
                  <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                    <span className={`sd-pill ${CLEARANCE_STATUS_PILL[result.status]}`}>{CLEARANCE_STATUS_LABELS[result.status]}</span>
                    {result.isOverridden && <span className="sd-pill quoted">Overridden</span>}
                  </div>
                  <div style={{ minWidth: 0 }}>
                    <div style={{ fontSize: 12.5, color: 'var(--ink-2)' }}>{result.explanation}</div>
                    {result.matchedRecordLabel && (
                      <div style={{ marginTop: 3, fontSize: 11.5, color: 'var(--ink-4)', fontFamily: 'var(--font-mono)' }}>{result.matchedRecordLabel}</div>
                    )}
                    {result.isOverridden && (
                      <div style={{ marginTop: 6, fontSize: 11.5, color: 'var(--ink-3)' }}>
                        Override: {result.overrideReason}
                        {result.overriddenAt ? ` · ${new Date(result.overriddenAt).toLocaleString()}` : ''}
                      </div>
                    )}
                  </div>
                </div>
              ))}
            </div>
            {showClearanceOverride && hasBlockedResult && canOverrideClearance && (
              <div style={{ marginTop: 10, padding: 12, border: '1px solid var(--line-2)', borderRadius: 8, background: 'var(--surface-2)' }}>
                <label style={labelStyle}>Override reason</label>
                <textarea
                  value={clearanceOverrideReason}
                  onChange={(e) => setClearanceOverrideReason(e.target.value)}
                  rows={3}
                  style={inputStyle}
                  placeholder="Document why this blocked clearance can proceed."
                />
                <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
                  <button type="button" className="sd-btn primary sm" onClick={submitClearanceOverride} disabled={overrideClearanceMutation.isPending}>
                    <Check size={13} />
                    {overrideClearanceMutation.isPending ? 'Recording' : 'Record Override'}
                  </button>
                  <button type="button" className="sd-btn outline sm" onClick={() => setShowClearanceOverride(false)} disabled={overrideClearanceMutation.isPending}>
                    <X size={13} />
                    Cancel
                  </button>
                </div>
              </div>
            )}
          </div>
        )}
      </section>
    )
  }

  const renderReferralPanel = (summary: UnderwritingReferralSummary | undefined) => {
    if (!canManageUnderwriting) return null

    const referrals = summary?.referrals ?? []
    const appetiteResults = summary?.appetiteResults ?? []
    const openRequiredCount = referrals.filter((referral) => referral.required && referral.status === 'Open').length

    return (
      <section className="sd-card" style={{ marginBottom: 14 }}>
        <div className="sd-card-head">
          <h3>Underwriting Referrals</h3>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', justifyContent: 'flex-end' }}>
            {summary?.hasOpenRequiredReferrals && <span className="sd-pill declined">{openRequiredCount} open required</span>}
            {!summary?.hasOpenRequiredReferrals && referrals.length > 0 && <span className="sd-pill bound">Resolved</span>}
          </div>
        </div>
        <div className="sd-card-body tight">
          {referrals.length === 0 && appetiteResults.length === 0 ? (
            <div style={{ fontSize: 13, color: 'var(--ink-3)', padding: '2px 0' }}>No appetite referrals recorded.</div>
          ) : (
            <div style={{ display: 'grid', gap: 12 }}>
              {referrals.length > 0 && (
                <div style={{ display: 'grid', gap: 1, background: 'var(--line-2)' }}>
                  {referrals.map((referral) => {
                    const activeDecision = referralDecision?.referralId === referral.id ? referralDecision : null
                    return (
                      <div key={referral.id} style={{ background: 'var(--surface)', padding: '10px 12px', display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 12, alignItems: 'center' }}>
                        <div style={{ minWidth: 0 }}>
                          <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--ink)' }}>{referral.referralType}</div>
                          <div style={{ marginTop: 3, fontSize: 11.5, color: 'var(--ink-4)', fontFamily: 'var(--font-mono)' }}>{referral.quoteNumber ?? 'Submission'}</div>
                        </div>
                        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                          <span className={`sd-pill ${REFERRAL_STATUS_PILL[referral.status]}`}>{referral.status}</span>
                          {referral.required && <span className="sd-pill quoted">Required</span>}
                        </div>
                        <div style={{ minWidth: 0 }}>
                          <div style={{ fontSize: 12.5, color: 'var(--ink-2)' }}>{referral.reason}</div>
                          <div style={{ marginTop: 5, fontSize: 11.5, color: 'var(--ink-4)' }}>
                            Requested by {referral.requestedByName || 'Unknown'}
                            {referral.requestedAt ? ` · ${new Date(referral.requestedAt).toLocaleString()}` : ''}
                          </div>
                          {referral.decisionAt && (
                            <div style={{ marginTop: 5, fontSize: 11.5, color: 'var(--ink-3)' }}>
                              Decision by {referral.decisionByName || 'Unknown'} · {new Date(referral.decisionAt).toLocaleString()}
                              {referral.decisionNotes ? ` · ${referral.decisionNotes}` : ''}
                            </div>
                          )}
                          {activeDecision && (
                            <div style={{ marginTop: 9, padding: 10, border: '1px solid var(--line-2)', borderRadius: 8, background: 'var(--surface-2)' }}>
                              <label style={labelStyle}>{REFERRAL_DECISION_LABELS[activeDecision.status]} notes</label>
                              <textarea
                                value={referralDecisionNotes}
                                onChange={(e) => setReferralDecisionNotes(e.target.value)}
                                rows={2}
                                style={inputStyle}
                              />
                              <div style={{ display: 'flex', gap: 8, marginTop: 9, flexWrap: 'wrap' }}>
                                <button type="button" className="sd-btn primary sm" onClick={submitReferralDecision} disabled={decideReferralMutation.isPending}>
                                  <Check size={13} />
                                  {decideReferralMutation.isPending ? 'Saving' : REFERRAL_DECISION_LABELS[activeDecision.status]}
                                </button>
                                <button type="button" className="sd-btn outline sm" onClick={() => { setReferralDecision(null); setReferralDecisionNotes('') }} disabled={decideReferralMutation.isPending}>
                                  <X size={13} />
                                  Cancel
                                </button>
                              </div>
                            </div>
                          )}
                        </div>
                        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 6, flexWrap: 'wrap' }}>
                          {referral.status === 'Open' && (
                            <>
                              <button type="button" className="sd-btn outline sm" onClick={() => { setReferralDecision({ referralId: referral.id, status: 'Approved' }); setReferralDecisionNotes('') }}>Approve</button>
                              <button type="button" className="sd-btn outline sm" onClick={() => { setReferralDecision({ referralId: referral.id, status: 'Declined' }); setReferralDecisionNotes('') }}>Decline</button>
                              <button type="button" className="sd-btn outline sm" onClick={() => { setReferralDecision({ referralId: referral.id, status: 'Waived' }); setReferralDecisionNotes('') }}>Waive</button>
                            </>
                          )}
                        </div>
                      </div>
                    )
                  })}
                </div>
              )}
              {appetiteResults.length > 0 && (
                <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                  {appetiteResults.map((result) => (
                    <span key={result.id} className="sd-lob-chip" title={result.explanation}>
                      {result.quoteNumber ?? 'Submission'} · {result.ruleName}
                    </span>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      </section>
    )
  }

  // ── Shared quote list renderer ─────────────────────────────────────────────

  const renderQuoteList = () => (
    <div>
      {/* LOB strip */}
      <div className="sd-lob-strip">
        <span style={{ fontSize: 10.5, letterSpacing: '.06em', textTransform: 'uppercase', color: 'var(--ink-4)', fontWeight: 600, marginRight: 2 }}>Lines:</span>
        {submission.linesOfBusiness.map((lob) => (
          <span key={lob} className="sd-lob-chip">
            {LOB_SHORT[lob] ?? lob}
          </span>
        ))}
      </div>

      {/* Quote rows */}
      {quotes.length === 0 && !showQuoteForm ? (
        <EmptyState
          icon={FileText}
          title="No quotes yet"
          description="Add the first carrier quote for this submission."
          action={canCreatePolicies && (
            <button className="sd-btn outline sm" onClick={() => { setShowQuoteForm(true); setQuoteForm(emptyQuoteForm()) }}>
              <Plus size={13} /> Add Quote
            </button>
          )}
        />
      ) : (
        <div>
          {quotes.map((q) => (
            <div key={q.id}>
              <div
                className="sd-quote"
                role="link"
                tabIndex={0}
                onClick={() => navigate(`/quotes/${q.id}`)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault()
                    navigate(`/quotes/${q.id}`)
                  }
                }}
              >
                <div className="badge">{LOB_SHORT[q.lineOfBusiness] ?? q.lineOfBusiness}</div>
                <div className="body">
                  <div className="top">
                    <span className="carrier">{q.carrierName}</span>
                    <span className={`sd-pill ${QUOTE_STATUS_PILL[q.status]}`}>{QUOTE_STATUS_LABELS[q.status]}</span>
                    <div className="prem" style={{ marginLeft: 'auto' }}>
                      <div className="s">Quoted premium</div>
                      {q.totalPremium == null || q.totalPremium === 0
                        ? <div className="v" style={{ color: 'var(--ink-4)' }}>—</div>
                        : <div className="v">{fmtMoney(q.totalPremium)}</div>}
                    </div>
                  </div>
                  <div className="meta">
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--ink-4)' }}>{q.quoteNumber}</span>
                    <span className="sep">·</span>
                    <span>{parseDateOnly(q.effectiveDate).toLocaleDateString()} → {parseDateOnly(q.expirationDate).toLocaleDateString()}</span>
                    {q.hasCommissionOverride && <><span className="sep">·</span><span style={{ color: 'var(--warn-fg)' }}>commission override</span></>}
                  </div>
                </div>
                <div className="acts">
                  {q.status === 'Bound' && q.boundPolicyId && (
                    <Link to={`/policies/${q.boundPolicyId}`} className="sd-btn sm outline" onClick={(e) => e.stopPropagation()}>View Policy</Link>
                  )}
                  {canDeletePolicies && q.status === 'Draft' && (
                    <button
                      onClick={(e) => { e.stopPropagation(); if (confirm('Delete this quote?')) deleteQuoteMutation.mutate(q.id) }}
                      className="sims-icon-btn hover:text-red-500"
                      title="Delete quote"
                    >
                      <Trash2 size={12} />
                    </button>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Add quote form */}
      {showQuoteForm && canCreatePolicies && (
        <div style={{ padding: '14px 16px', borderTop: '1px solid var(--line-2)', background: 'var(--surface-2)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
            <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)' }}>New Quote</span>
            <button onClick={() => setShowQuoteForm(false)} className="sd-btn ghost sm"><X size={13} /></button>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 10 }}>
            {[
              { label: 'Program', node: <select value={quoteForm.programId} onChange={setQF('programId')} style={inputStyle}><option value="" disabled>Select a program…</option>{programs.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}</select> },
              { label: 'Carrier *', node: <select value={quoteForm.carrierId} onChange={setQF('carrierId')} style={inputStyle}><option value="">— Select carrier —</option>{quoteCarrierOptions.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</select> },
              { label: 'Line of Business *', node: <select value={quoteForm.lineOfBusiness} onChange={setQF('lineOfBusiness')} disabled={!quoteForm.carrierId} style={inputStyle}><option value="">— Select LOB —</option>{availableLobs.map((l) => <option key={l} value={l}>{getLobLabel(l)}</option>)}</select> },
              { label: 'Effective Date *', node: <input type="date" value={quoteForm.effectiveDate} onChange={setQF('effectiveDate')} style={inputStyle} /> },
              { label: 'Expiration Date *', node: <input type="date" value={quoteForm.expirationDate} onChange={setQF('expirationDate')} style={inputStyle} /> },
              { label: 'Premium', node: <input type="number" value={quoteForm.premiumAmount} onChange={setQF('premiumAmount')} placeholder="0.00" style={inputStyle} /> },
              { label: 'Taxes & Fees', node: <input type="number" value={quoteForm.taxesAndFees} onChange={setQF('taxesAndFees')} placeholder="0.00" style={inputStyle} /> },
              { label: 'Limit', node: <input type="number" value={quoteForm.limit} onChange={setQF('limit')} placeholder="Optional" style={inputStyle} /> },
              { label: 'Deductible', node: <input type="number" value={quoteForm.deductible} onChange={setQF('deductible')} placeholder="Optional" style={inputStyle} /> },
            ].map(({ label, node }) => (
              <div key={label}>
                <label style={labelStyle}>{label}</label>
                {node}
              </div>
            ))}
            {quoteForm.lineOfBusiness === 'CommercialAuto' && (
              <>
                <div><label style={labelStyle}>UM/UIM Limit</label><input type="number" value={quoteForm.uninsuredMotoristLimit} onChange={setQF('uninsuredMotoristLimit')} placeholder="Optional" style={inputStyle} /></div>
                <div><label style={labelStyle}>Med Pay Limit</label><input type="number" value={quoteForm.medicalPaymentsLimit} onChange={setQF('medicalPaymentsLimit')} placeholder="Optional" style={inputStyle} /></div>
              </>
            )}
            <div><label style={labelStyle}>Company ID</label><input type="number" value={quoteForm.companyId} onChange={setQF('companyId')} placeholder="Optional" style={inputStyle} /></div>
            <div><label style={labelStyle}>Producer ID</label><input type="number" value={quoteForm.producerId} onChange={setQF('producerId')} placeholder="Optional" style={inputStyle} /></div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, paddingTop: 20 }}>
              <input type="checkbox" id="isFilingState" checked={quoteForm.isFilingState} onChange={(e) => setQuoteForm((prev) => ({ ...prev, isFilingState: e.target.checked }))} style={{ width: 14, height: 14 }} />
              <label htmlFor="isFilingState" style={{ fontSize: 12.5, color: 'var(--ink-2)', cursor: 'pointer' }}>Filing State</label>
            </div>
          </div>
          {quoteForm.lineOfBusiness === 'AutoLiability' && (
            <div style={{ marginTop: 12, padding: '8px 12px', borderRadius: 6, fontSize: 12.5, background: 'var(--warn-bg)', color: 'var(--warn-fg)' }}>
              Auto Liability is not currently bindable — you can quote it for record-keeping, but it cannot be bound.
            </div>
          )}
          <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
            <button onClick={handleCreateQuote} disabled={createQuoteMutation.isPending} className="sd-btn primary sm"><Check size={13} /> Save Quote</button>
            <button onClick={() => setShowQuoteForm(false)} className="sd-btn outline sm"><X size={13} /> Cancel</button>
          </div>
        </div>
      )}

      {!showQuoteForm && quotes.length > 0 && canCreatePolicies && (
        <div style={{ padding: '10px 16px', borderTop: '1px solid var(--line-2)' }}>
          <button className="sd-btn ghost sm" onClick={() => { setShowQuoteForm(true); setQuoteForm(emptyQuoteForm()) }}>
            <Plus size={13} /> Add Quote
          </button>
        </div>
      )}
    </div>
  )

  // ── Render ─────────────────────────────────────────────────────────────────

  return (
    <div style={{ background: 'var(--bg)' }}>

      {/* Extraction banner */}
      {showExtractionBanner && (
        <div style={{ marginBottom: 16, borderRadius: 'var(--r-lg)', border: '1px solid var(--warn-border)', background: 'var(--warn-bg)', padding: '12px 16px' }}>
          <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 16 }}>
            <div style={{ display: 'flex', alignItems: 'flex-start', gap: 8, fontSize: 13, color: 'var(--warn-fg)' }}>
              <AlertTriangle size={15} style={{ marginTop: 1, flexShrink: 0 }} />
              <span>
                {extractionState?.extractionStatus === 'DetectionFailed'
                  ? <><strong>Line of business not detected</strong> — data extracted but LOB could not be identified. Select the LOB below and re-run, or set LOBs manually.</>
                  : <><strong>AI extraction failed</strong> — attachment could not be read automatically. Re-run or fill in fields manually.</>}
              </span>
            </div>
            <button onClick={() => setShowExtractionBanner(false)} className="sims-icon-btn" title="Dismiss extraction alert"><X size={15} /></button>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 10 }}>
            {extractionState?.extractionStatus === 'DetectionFailed' && (
              <select value={reExtractLob} onChange={(e) => setReExtractLob(e.target.value)} className="sims-select" style={{ width: 'auto' }}>
                <option value="">— Select LOB hint —</option>
                {ACTIVE_LOBS.map((l) => <option key={l} value={l}>{LOB_LABELS[l]}</option>)}
              </select>
            )}
            <button onClick={() => reExtract.mutate()} disabled={reExtract.isPending} className="sd-btn outline sm">
              <RefreshCw size={13} className={reExtract.isPending ? 'animate-spin' : ''} />
              {reExtract.isPending ? 'Re-extracting…' : 'Re-run Extraction'}
            </button>
          </div>
        </div>
      )}

      {/* Back link */}
      <Link to={`/insureds/${submission.insuredId}`} className="sd-btn ghost sm mb-3">
        <ArrowLeft size={13} /> {submission.insuredName}
      </Link>

      {/* Page header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 24, marginBottom: 0 }}>
        <div style={{ display: 'flex', gap: 14, alignItems: 'flex-start', minWidth: 0 }}>
          <div style={{ width: 36, height: 36, borderRadius: 8, background: 'var(--accent-soft)', color: 'var(--accent-ink)', display: 'grid', placeItems: 'center', flexShrink: 0, border: '1px solid var(--accent-border)' }}>
            <FileText size={16} />
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6, minWidth: 0 }}>
            <h1 style={{ margin: 0, fontSize: 24, fontWeight: 600, letterSpacing: '-.015em', lineHeight: 1.15, display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 22 }}>{submission.submissionNumber}</span>
              <span className={`sd-pill ${STATUS_PILL[submission.status]}`}>{SUBMISSION_STATUS_LABELS[submission.status]}</span>
              {stage && <span style={{ fontSize: 13, fontWeight: 500, color: 'var(--ink-3)' }}>· {stage.label}</span>}
            </h1>
          </div>
        </div>
        <div style={{ display: 'flex', gap: 8, flexShrink: 0 }}>
          <button onClick={openSubmissionEditor} className="sd-btn outline">
            <Pencil size={13} /> Edit submission
          </button>
          {canCreatePolicies && (
            <button onClick={() => setShowGenerateModal(true)} className="sd-btn outline">
              <FileText size={13} /> Generate doc
            </button>
          )}
          {canCreatePolicies && (
            <button
              className="sd-btn primary"
              onClick={() => { setActiveTab('quotes'); setShowQuoteForm(true); setQuoteForm(emptyQuoteForm()) }}
            >
              <Plus size={13} /> Add quote
            </button>
          )}
        </div>
      </div>

      {showSubmissionEditor && submissionForm && (
        <section className="sd-card" style={{ marginTop: 14, marginBottom: 14 }}>
          <div className="sd-card-head">
            <h3>Edit submission</h3>
            <button onClick={() => { setShowSubmissionEditor(false); setSubmissionForm(null) }} className="sims-icon-btn" title="Close editor"><X size={13} /></button>
          </div>
          <div className="sd-card-body">
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 10 }}>
              <div>
                <label style={labelStyle}>Status</label>
                <select value={submissionForm.status} onChange={(e) => setSubmissionField('status', e.target.value)} style={inputStyle}>
                  {(Object.keys(SUBMISSION_STATUS_LABELS) as SubmissionStatus[]).map((status) => (
                    <option key={status} value={status}>{SUBMISSION_STATUS_LABELS[status]}</option>
                  ))}
                </select>
              </div>
              <div>
                <label style={labelStyle}>Underwriter *</label>
                <select value={submissionForm.underwriterId} onChange={(e) => setSubmissionField('underwriterId', e.target.value)} style={inputStyle}>
                  <option value="">— Select underwriter —</option>
                  {users.map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
                </select>
              </div>
              <div>
                <label style={labelStyle}>Assistant UW</label>
                <select value={submissionForm.assistantUWId ?? ''} onChange={(e) => setSubmissionField('assistantUWId', e.target.value)} style={inputStyle}>
                  <option value="">— None —</option>
                  {users.map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
                </select>
              </div>
              <div>
                <label style={labelStyle}>Agent</label>
                <select value={submissionForm.agentId ?? ''} onChange={(e) => setSubmissionField('agentId', e.target.value)} style={inputStyle}>
                  <option value="">— None —</option>
                  {agents.map((a) => <option key={a.id} value={a.id}>{a.name}{a.agencyName ? ` (${a.agencyName})` : ''}</option>)}
                </select>
              </div>
              <div>
                <label style={labelStyle}>Target Effective Date</label>
                <input type="date" value={submissionForm.effectiveDate ?? ''} onChange={(e) => setSubmissionField('effectiveDate', e.target.value)} style={inputStyle} />
              </div>
              <div>
                <label style={labelStyle}>Target Expiration Date</label>
                <input type="date" value={submissionForm.expirationDate ?? ''} onChange={(e) => setSubmissionField('expirationDate', e.target.value)} style={inputStyle} />
              </div>
              <div style={{ gridColumn: 'span 3' }}>
                <label style={labelStyle}>Lines of Business *</label>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 8 }}>
                  {ACTIVE_LOBS.map((lob) => {
                    const checked = submissionForm.linesOfBusiness.includes(lob)
                    return (
                      <label key={lob} style={{ display: 'flex', alignItems: 'center', gap: 7, border: '1px solid var(--line-2)', borderRadius: 6, padding: '8px 10px', fontSize: 12.5, color: 'var(--ink-2)' }}>
                        <input
                          type="checkbox"
                          checked={checked}
                          disabled={checked && submissionForm.linesOfBusiness.length === 1}
                          onChange={(e) => toggleSubmissionLob(lob, e.target.checked)}
                        />
                        {LOB_LABELS[lob]}
                      </label>
                    )
                  })}
                </div>
              </div>
              <div style={{ gridColumn: 'span 3' }}>
                <label style={labelStyle}>UW Notes</label>
                <textarea value={submissionForm.descriptionOfOperations ?? ''} onChange={(e) => setSubmissionField('descriptionOfOperations', e.target.value)} rows={3} style={inputStyle} />
              </div>
            </div>
            <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
              <button onClick={saveSubmissionEdit} disabled={updateSubmissionMutation.isPending} className="sd-btn primary sm"><Check size={13} /> Save Submission</button>
              <button onClick={() => { setShowSubmissionEditor(false); setSubmissionForm(null) }} className="sd-btn outline sm"><X size={13} /> Cancel</button>
            </div>
          </div>
        </section>
      )}

      {/* Metadata strip */}
      <div className="sd-ph-strip">
        <div>
          <div className="k">Insured</div>
          <div className="v"><Link to={`/insureds/${submission.insuredId}`} style={{ color: 'var(--accent-ink)', fontWeight: 600 }}>{submission.insuredName}</Link></div>
        </div>
        <div>
          <div className="k">Status · State</div>
          <div className="v">
            {insured?.state && <span style={{ fontSize: 10.5, padding: '2px 6px', borderRadius: 'var(--r-xs)', background: 'var(--surface-2)', color: 'var(--ink-2)', fontFamily: 'var(--font-mono)', fontWeight: 600 }}>{insured.state}</span>}
            {insured?.entityType && insured.entityType !== 'Unknown' && <span style={{ color: 'var(--ink-2)' }}>{insured.entityType}</span>}
          </div>
        </div>
        <div>
          <div className="k">Lines</div>
          <div className="v" style={{ gap: 4, alignItems: 'center', flexWrap: 'wrap' }}>
            {submission.linesOfBusiness.length === 0
              ? <span style={{ color: 'var(--ink-4)', fontStyle: 'italic' }}>None</span>
              : submission.linesOfBusiness.map((l) => <span key={l} className="sd-lob">{LOB_SHORT[l] ?? l}</span>)}
            <button type="button" onClick={() => setShowLobEditor((v) => !v)} className="sd-btn outline sm" style={{ height: 22, padding: '0 8px' }}>
              Edit
            </button>
          </div>
          {showLobEditor && (
            <div style={{ marginTop: 8, padding: 10, border: '1px solid var(--line-2)', borderRadius: 8, background: 'var(--surface)', boxShadow: '0 8px 24px rgba(15,23,42,.08)' }}>
              <div style={{ display: 'grid', gap: 6 }}>
                {ACTIVE_LOBS.map((lob) => {
                  const checked = submission.linesOfBusiness.includes(lob)
                  const next = checked
                    ? submission.linesOfBusiness.filter((value) => value !== lob)
                    : [...submission.linesOfBusiness, lob]
                  return (
                    <label key={lob} style={{ display: 'flex', alignItems: 'center', gap: 7, fontSize: 12.5, color: 'var(--ink-2)' }}>
                      <input
                        type="checkbox"
                        checked={checked}
                        disabled={setLobsMutation.isPending || (checked && submission.linesOfBusiness.length === 1)}
                        onChange={() => setLobsMutation.mutate(next)}
                      />
                      {LOB_LABELS[lob]}
                    </label>
                  )
                })}
              </div>
            </div>
          )}
        </div>
        <div>
          <div className="k">Effective</div>
          <div className="v" style={{ fontVariantNumeric: 'tabular-nums' }}>
            {submission.effectiveDate ? parseDateOnly(submission.effectiveDate).toLocaleDateString() : '—'}
          </div>
        </div>
        <div>
          <div className="k">Underwriter</div>
          <div className="v">{submission.underwriterName}{submission.assistantUWName && <span style={{ color: 'var(--ink-3)' }}> · {submission.assistantUWName}</span>}</div>
        </div>
        <div>
          <div className="k">Agent</div>
          <div className="v">{submission.agentName ?? <span style={{ color: 'var(--ink-4)' }}>—</span>}{submission.agencyName && <span style={{ color: 'var(--ink-3)', fontSize: 11.5 }}> · {submission.agencyName}</span>}</div>
        </div>
      </div>

      {renderClearancePanel(clearance)}
      {renderReferralPanel(referralSummary)}

      {/* Metrics */}
      <div className="sd-metrics">
        <div className="sd-metric accent">
          <div className="k">Total quoted premium</div>
          <div className="v">{fmtMoneyK(totalQuotedPremium)}</div>
          <div className="s">{activeQuotes.length} active {activeQuotes.length === 1 ? 'quote' : 'quotes'}</div>
        </div>
        <div className="sd-metric">
          <div className="k">Carriers responded</div>
          <div className="v">
            {quotes.filter(q => q.status !== 'Draft').length}
            <span style={{ fontSize: 12, fontWeight: 500, color: 'var(--ink-3)' }}> / {quotes.length}</span>
          </div>
          <div className="s">{declinedCount} declined · {quotes.filter(q => q.status === 'Draft' || q.status === 'Submitted').length} pending</div>
        </div>
        <div className="sd-metric">
          <div className="k">Days to effective</div>
          <div className="v" style={{ color: daysToEff != null && daysToEff < 14 ? 'var(--error-border)' : 'inherit' }}>
            {daysToEff != null ? `${daysToEff}d` : '—'}
          </div>
          <div className="s">{submission.effectiveDate ? parseDateOnly(submission.effectiveDate).toLocaleDateString() : '—'}</div>
        </div>
        <div className="sd-metric">
          <div className="k">Loss ratio</div>
          <div className="v" style={{ color: lossSummary?.lossRatio != null && lossSummary.lossRatio > 0.65 ? 'var(--error-border)' : 'inherit' }}>
            {fmtPct(lossSummary?.lossRatio)}
          </div>
          <div className="s">
            {lossSummary?.yearCount ? `${lossSummary.yearCount} yrs · ${fmtMoneyK(lossSummary.totalIncurred)} incurred` : 'No loss history on file'}
          </div>
        </div>
      </div>

      {/* Info cards row */}
      <div className="sd-info-row">
        {/* Submission card */}
        <section className="sd-card">
          <div className="sd-card-head"><h3>Submission</h3></div>
          <div className="sd-card-body">
            <div className="sd-fields">
              <div className="sd-field">
                <span className="lbl">Received</span>
                <span className="val">{new Date(submission.createdAt).toLocaleDateString()}</span>
              </div>
              <div className="sd-field">
                <span className="lbl">Term</span>
                <span className="val" style={{ fontVariantNumeric: 'tabular-nums' }}>
                  {submission.effectiveDate ? parseDateOnly(submission.effectiveDate).toLocaleDateString() : '—'}
                  {' → '}
                  {submission.expirationDate ? parseDateOnly(submission.expirationDate).toLocaleDateString() : '—'}
                </span>
              </div>
              <div className="sd-field">
                <span className="lbl">Status</span>
                <span className="val"><span className={`sd-pill ${STATUS_PILL[submission.status]}`}>{SUBMISSION_STATUS_LABELS[submission.status]}</span></span>
              </div>
              <div className="sd-field">
                <span className="lbl">Quotes</span>
                <span className="val">{quotes.length} total · {activeQuotes.length} quoted</span>
              </div>
            </div>
          </div>
        </section>

        {/* Insured card */}
        <section className="sd-card">
          <div className="sd-card-head">
            <h3>Insured</h3>
            <Link to={`/insureds/${submission.insuredId}`} className="sd-btn ghost sm" title="Open insured">↗</Link>
          </div>
          <div className="sd-card-body">
            <div className="sd-fields">
              <div className="sd-field">
                <span className="lbl">Name</span>
                <span className="val"><Link to={`/insureds/${submission.insuredId}`}>{submission.insuredName}</Link></span>
              </div>
              {insured && (
                <>
                  <div className="sd-field">
                    <span className="lbl">Location</span>
                    <span className="val">{[insured.city, insured.state].filter(Boolean).join(', ') || '—'}</span>
                  </div>
                  {insured.entityType && insured.entityType !== 'Unknown' && (
                    <div className="sd-field">
                      <span className="lbl">Entity</span>
                      <span className="val">{insured.entityType}</span>
                    </div>
                  )}
                  {insured.yearsInBusiness != null && (
                    <div className="sd-field">
                      <span className="lbl">Years in Business</span>
                      <span className="val">{insured.yearsInBusiness}</span>
                    </div>
                  )}
                </>
              )}
            </div>
          </div>
        </section>

        {/* Agency card */}
        <section className="sd-card">
          <div className="sd-card-head"><h3>Agency / Producer</h3></div>
          <div className="sd-card-body">
            <div className="sd-fields">
              {submission.agencyName && (
                <div className="sd-field">
                  <span className="lbl">Agency</span>
                  <span className="val">{submission.agencyName}</span>
                </div>
              )}
              {submission.agentName && (
                <div className="sd-field">
                  <span className="lbl">Producer</span>
                  <span className="val">{submission.agentName}</span>
                </div>
              )}
              <div className="sd-field">
                <span className="lbl">Underwriter</span>
                <span className="val">{submission.underwriterName}</span>
              </div>
              {submission.assistantUWName && (
                <div className="sd-field">
                  <span className="lbl">Asst. Underwriter</span>
                  <span className="val">{submission.assistantUWName}</span>
                </div>
              )}
            </div>
          </div>
        </section>
      </div>

      {/* Always-visible: Loss history + UW Notes */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14, marginBottom: 14 }}>
        <section className="sd-card">
          <div className="sd-card-head">
            <h3>Loss history (5 yrs)</h3>
            <button type="button" onClick={openLossHistory} className="sd-btn ghost sm">Open analysis</button>
          </div>
          {lossSummary?.yearCount ? (
            <div className="sd-card-body tight">
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 1, borderBottom: '1px solid var(--line-2)', background: 'var(--line-2)' }}>
                {[
                  ['Loss ratio', fmtPct(lossSummary.lossRatio)],
                  ['Incurred', fmtMoneyK(lossSummary.totalIncurred)],
                  ['Premium', fmtMoneyK(lossSummary.totalPremium)],
                  ['Claims', String(lossSummary.claimCount)],
                ].map(([label, value]) => (
                  <div key={label} style={{ background: 'var(--surface)', padding: '10px 12px' }}>
                    <div style={{ fontSize: 10, letterSpacing: '.05em', textTransform: 'uppercase', color: 'var(--ink-4)', fontWeight: 600 }}>{label}</div>
                    <div style={{ fontSize: 16, fontWeight: 600, fontVariantNumeric: 'tabular-nums', marginTop: 3 }}>{value}</div>
                  </div>
                ))}
              </div>
              <table className="sd-table">
                <thead><tr><th>Year</th><th className="num">Premium</th><th className="num">Incurred</th><th className="num">LR</th></tr></thead>
                <tbody>
                  {lossSummary.years.slice(0, 5).map((y) => (
                    <tr key={y.id}>
                      <td className="id">{y.policyYear}</td>
                      <td className="num">{fmtMoneyK(y.premiumAmount)}</td>
                      <td className="num">{fmtMoneyK(y.incurred)}</td>
                      <td className="num">{fmtPct(y.lossRatio)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <EmptyState
              icon={FileText}
              title="No loss history on file"
              description="Add annual loss history when it is available."
              action={<button type="button" onClick={openLossHistory} className="sd-btn outline sm"><Plus size={13} /> Add loss year</button>}
            />
          )}
        </section>
        <section className="sd-card">
          <div className="sd-card-head"><h3>UW Notes</h3></div>
          {submission.descriptionOfOperations ? (
            <div className="sd-card-body" style={{ fontSize: 13, lineHeight: 1.55, color: 'var(--ink-2)' }}>
              <p style={{ margin: 0 }}>{submission.descriptionOfOperations}</p>
              {submission.underwriterName && (
                <div style={{ marginTop: 10, paddingTop: 10, borderTop: '1px solid var(--line-2)', display: 'flex', alignItems: 'center', gap: 8, fontSize: 11.5, color: 'var(--ink-3)' }}>
                  <div style={{ width: 22, height: 22, borderRadius: '50%', background: 'var(--accent-soft)', color: 'var(--accent-ink)', display: 'grid', placeItems: 'center', fontSize: 9.5, fontWeight: 700, flexShrink: 0 }}>
                    {submission.underwriterName.split(' ').map((n) => n[0]).join('').slice(0, 2)}
                  </div>
                  <span><b style={{ color: 'var(--ink-2)' }}>{submission.underwriterName}</b></span>
                </div>
              )}
            </div>
          ) : (
            <EmptyState icon={FileText} title="No UW notes on file" description="Underwriting notes added to this submission will appear here." />
          )}
        </section>
      </div>

      {/* Tab strip */}
      <div className="sd-tabs" style={{ marginBottom: 14 }}>
        {([
          { key: 'quotes', label: 'Quotes', count: quotes.length },
          { key: 'exposures', label: 'Exposures', count: drivers.length + vehicles.length + locations.length + equipment.length + glClassifications.length > 0 ? drivers.length + vehicles.length + locations.length + equipment.length + glClassifications.length : undefined },
          { key: 'additional-interests', label: 'Additional interests', count: additionalInterests.length > 0 ? additionalInterests.length : undefined },
          { key: 'prior-carriers', label: 'Prior carriers', count: priorCarriers.length > 0 ? priorCarriers.length : undefined },
          { key: 'documents', label: 'Documents' },
          { key: 'activity', label: 'Activity', count: outboundCommunications.length > 0 ? outboundCommunications.length : undefined },
        ] as { key: Tab; label: string; count?: number }[]).map(({ key, label, count }) => (
          <button
            key={key}
            className={`sd-tab${activeTab === key ? ' active' : ''}`}
            onClick={() => setActiveTab(key)}
          >
            {label}
            {count != null && <span className="cnt">{count}</span>}
          </button>
        ))}
      </div>

      {/* Quotes tab */}
      {activeTab === 'quotes' && (
        <section className="sd-card">
          <div className="sd-card-head">
            <h3>All quotes <span className="cnt">{quotes.length}</span></h3>
            {canCreatePolicies && (
              <div style={{ display: 'flex', gap: 6 }}>
                <button className="sd-btn sm primary" onClick={() => { setShowQuoteForm(true); setQuoteForm(emptyQuoteForm()) }}>
                  <Plus size={12} /> Add quote
                </button>
              </div>
            )}
          </div>
          <div className="sd-card-body tight">
            {renderQuoteList()}
          </div>
        </section>
      )}

      {/* Exposures tab */}
      {activeTab === 'exposures' && (
        <section className="sd-card">
          {expLobList.length > 1 && (
            <div className="sd-card-head" style={{ flexWrap: 'wrap', gap: 10 }}>
              <div className="exp-lob-switch">
                {expLobList.map((l) => (
                  <button key={l.k} className={`exp-lob${activeLob === l.k ? ' active' : ''}`} onClick={() => setExpLob(l.k)}>
                    {l.label}
                    {l.count != null && <span className="exp-lob-c">{l.count}</span>}
                  </button>
                ))}
              </div>
            </div>
          )}

          <div style={{ borderTop: expLobList.length > 1 ? '1px solid var(--line)' : 0 }}>
            {showLocationForm && (
              <div style={{ padding: '14px 16px', borderBottom: '1px solid var(--line-2)', background: 'var(--surface-2)' }}>
                <div style={{ fontWeight: 600, fontSize: 13, marginBottom: 10 }}>{editingLocationId ? 'Edit Risk Location' : 'Add Risk Location'}</div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: 10 }}>
                  <div><label style={labelStyle}>Location #</label><input type="number" value={locationForm.locationNumber} onChange={(e) => setLocationForm((f) => ({ ...f, locationNumber: parseInt(e.target.value) || 1 }))} style={inputStyle} /></div>
                  <div><label style={labelStyle}>Address *</label><input value={locationForm.address} onChange={(e) => setLocationForm((f) => ({ ...f, address: e.target.value }))} style={inputStyle} /></div>
                  <div><label style={labelStyle}>City</label><input value={locationForm.city ?? ''} onChange={(e) => setLocationForm((f) => ({ ...f, city: e.target.value || undefined }))} style={inputStyle} /></div>
                  <div><label style={labelStyle}>State</label><input maxLength={2} value={locationForm.state ?? ''} onChange={(e) => setLocationForm((f) => ({ ...f, state: e.target.value.toUpperCase() || undefined }))} style={inputStyle} /></div>
                  <div><label style={labelStyle}>County</label><input value={locationForm.county ?? ''} onChange={(e) => setLocationForm((f) => ({ ...f, county: e.target.value || undefined }))} style={inputStyle} /></div>
                  <div><label style={labelStyle}>ZIP</label><input value={locationForm.zipCode ?? ''} onChange={(e) => setLocationForm((f) => ({ ...f, zipCode: e.target.value || undefined }))} style={inputStyle} /></div>
                  <div><label style={labelStyle}>Country</label><input value={locationForm.country ?? ''} onChange={(e) => setLocationForm((f) => ({ ...f, country: e.target.value.toUpperCase() || undefined }))} style={inputStyle} /></div>
                </div>
                <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', fontSize: 13, marginTop: 10 }}>
                  <input type="checkbox" checked={locationForm.isPrimary} onChange={(e) => setLocationForm((f) => ({ ...f, isPrimary: e.target.checked }))} />
                  Primary risk location
                </label>
                <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
                  <button onClick={() => saveLocationMutation.mutate(prepareLocationPayload(locationForm))} disabled={!locationForm.address.trim() || saveLocationMutation.isPending} className="sd-btn primary sm"><Check size={13} /> Save</button>
                  <button onClick={() => { setShowLocationForm(false); setLocationForm(emptyLocationForm()); setEditingLocationId(null) }} className="sd-btn outline sm"><X size={13} /> Cancel</button>
                </div>
              </div>
            )}
            <div className="exp-h">
              <div className="exp-h-l">Risk locations <span className="c">{locations.length}</span></div>
              <button type="button" className="sd-btn ghost sm" onClick={openNewLocationForm}><Plus size={12} /> Add</button>
            </div>
            {locations.length === 0 && !showLocationForm ? (
              <EmptyState icon={FileText} title="No risk locations added yet" description="Add the primary location used for bordereaux and rating setup." />
            ) : (
              <div style={{ overflowX: 'auto' }}>
                <table className="sd-table">
                  <thead><tr><th>Primary</th><th>Loc.</th><th>Address</th><th>City / State / ZIP</th><th>County</th><th>Country</th><th /></tr></thead>
                  <tbody>
                    {locations.map((riskLocation) => (
                      <tr key={riskLocation.id}>
                        <td>{riskLocation.isPrimary ? <span className="sd-lob">Primary</span> : '-'}</td>
                        <td className="id">{riskLocation.locationNumber}</td>
                        <td className="primary-cell">{riskLocation.address}</td>
                        <td>{[riskLocation.city, riskLocation.state, riskLocation.zipCode].filter(Boolean).join(', ') || '-'}</td>
                        <td>{riskLocation.county ?? '-'}</td>
                        <td>{riskLocation.country ?? '-'}</td>
                        <td style={{ padding: '8px 14px' }}>
                          <div style={{ display: 'flex', gap: 4 }}>
                            <button type="button" onClick={() => openEditLocationForm(riskLocation)} className="sims-icon-btn hover:text-sky-600" title="Edit risk location"><Pencil size={12} /></button>
                            <button type="button" onClick={() => { if (confirm('Remove risk location?')) deleteLocationMutation.mutate(riskLocation.id) }} disabled={deleteLocationMutation.isPending} className="sims-icon-btn hover:text-red-500" title="Remove risk location"><Trash2 size={12} /></button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {activeLob === 'auto' && (
            <>
              {/* Driver form */}
              {showDriverForm && (
                <div style={{ padding: '14px 16px', borderBottom: '1px solid var(--line-2)', background: 'var(--surface-2)' }}>
                  <div style={{ fontWeight: 600, fontSize: 13, marginBottom: 10 }}>{editingDriverId ? 'Edit Driver' : 'Add Driver'}</div>
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 10 }}>
                    <div><label style={labelStyle}>Driver #</label><input type="number" value={driverForm.driverNumber} onChange={(e) => setDriverForm((f) => ({ ...f, driverNumber: parseInt(e.target.value) || 1 }))} style={inputStyle} /></div>
                    <div><label style={labelStyle}>Name *</label><input value={driverForm.name} onChange={(e) => setDriverForm((f) => ({ ...f, name: e.target.value }))} style={inputStyle} /></div>
                    <div><label style={labelStyle}>Date of Birth</label><input type="date" value={driverForm.dateOfBirth ?? ''} onChange={(e) => setDriverForm((f) => ({ ...f, dateOfBirth: e.target.value || undefined }))} style={inputStyle} /></div>
                    <div><label style={labelStyle}>License #</label><input value={driverForm.licenseNumber ?? ''} onChange={(e) => setDriverForm((f) => ({ ...f, licenseNumber: e.target.value || undefined }))} style={inputStyle} /></div>
                    <div><label style={labelStyle}>License State</label><input maxLength={2} value={driverForm.licenseState ?? ''} onChange={(e) => setDriverForm((f) => ({ ...f, licenseState: e.target.value.toUpperCase() || undefined }))} style={inputStyle} /></div>
                    <div><label style={labelStyle}>Date Hired</label><input type="date" value={driverForm.dateHired ?? ''} onChange={(e) => setDriverForm((f) => ({ ...f, dateHired: e.target.value || undefined }))} style={inputStyle} /></div>
                  </div>
                  <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
                    <button onClick={() => saveDriverMutation.mutate(driverForm)} disabled={!driverForm.name || saveDriverMutation.isPending} className="sd-btn primary sm"><Check size={13} /> Save</button>
                    <button onClick={() => { setShowDriverForm(false); setDriverForm(emptyDriverForm()); setEditingDriverId(null) }} className="sd-btn outline sm"><X size={13} /> Cancel</button>
                  </div>
                </div>
              )}
              {/* Drivers section */}
              <div className="exp-h">
                <div className="exp-h-l">Drivers <span className="c">{drivers.length}</span></div>
                <button className="sd-btn ghost sm" onClick={() => { setShowDriverForm(true); setDriverForm(emptyDriverForm()); setEditingDriverId(null) }}><Plus size={12} /> Add</button>
              </div>
              {drivers.length === 0 && !showDriverForm ? (
                <EmptyState icon={FileText} title="No drivers added yet" description="Add scheduled drivers for auto submissions." />
              ) : (
                <table className="sd-table">
                  <thead><tr><th>#</th><th>Name</th><th>DOB</th><th>License</th><th>State</th><th>Hired</th><th /></tr></thead>
                  <tbody>
                    {drivers.map((d) => (
                      <tr key={d.id}>
                        <td className="id">{d.driverNumber}</td>
                        <td className="primary-cell">{d.name}</td>
                        <td>{d.dateOfBirth ?? '—'}</td>
                        <td className="id">{d.licenseNumber ?? '—'}</td>
                        <td>{d.licenseState ?? '—'}</td>
                        <td>{d.dateHired ?? '—'}</td>
                        <td style={{ padding: '8px 14px' }}>
                          <div style={{ display: 'flex', gap: 4 }}>
                            <button onClick={() => { setDriverForm({ driverNumber: d.driverNumber, name: d.name, dateOfBirth: d.dateOfBirth ?? undefined, licenseNumber: d.licenseNumber ?? undefined, licenseState: d.licenseState ?? undefined, dateHired: d.dateHired ?? undefined }); setEditingDriverId(d.id); setShowDriverForm(true) }} className="sims-icon-btn hover:text-sky-600" title="Edit driver"><Pencil size={12} /></button>
                            <button onClick={() => { if (confirm('Remove driver?')) deleteDriverMutation.mutate(d.id) }} className="sims-icon-btn hover:text-red-500" title="Remove driver"><Trash2 size={12} /></button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}

              {/* Vehicle form */}
              <div style={{ borderTop: '1px solid var(--line)' }}>
                {showVehicleForm && (
                  <div style={{ padding: '14px 16px', borderBottom: '1px solid var(--line-2)', background: 'var(--surface-2)' }}>
                    <div style={{ fontWeight: 600, fontSize: 13, marginBottom: 10 }}>{editingVehicleId ? 'Edit Vehicle' : 'Add Vehicle'}</div>
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10 }}>
                      <div><label style={labelStyle}>Unit #</label><input type="number" value={vehicleForm.unitNumber} onChange={(e) => setVehicleForm((f) => ({ ...f, unitNumber: parseInt(e.target.value) || 1 }))} style={inputStyle} /></div>
                      <div><label style={labelStyle}>Year</label><input type="number" value={vehicleForm.year ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, year: parseInt(e.target.value) || undefined }))} style={inputStyle} /></div>
                      <div><label style={labelStyle}>Make</label><input value={vehicleForm.make ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, make: e.target.value || undefined }))} style={inputStyle} /></div>
                      <div><label style={labelStyle}>Model</label><input value={vehicleForm.model ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, model: e.target.value || undefined }))} style={inputStyle} /></div>
                      <div><label style={labelStyle}>VIN</label><input value={vehicleForm.vin ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, vin: e.target.value || undefined }))} style={inputStyle} /></div>
                      <div><label style={labelStyle}>GVW (lbs)</label><input type="number" value={vehicleForm.gvw ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, gvw: parseInt(e.target.value) || undefined }))} style={inputStyle} /></div>
                      <div><label style={labelStyle}>Class *</label><select value={vehicleForm.vehicleClass} onChange={(e) => setVehicleForm((f) => ({ ...f, vehicleClass: e.target.value as VehicleClass }))} style={inputStyle}>{(Object.keys(VEHICLE_CLASS_LABELS) as VehicleClass[]).map((k) => <option key={k} value={k}>{VEHICLE_CLASS_LABELS[k]}</option>)}</select></div>
                      <div><label style={labelStyle}>Garaging ZIP</label><input value={vehicleForm.garagingZip ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, garagingZip: e.target.value || undefined }))} style={inputStyle} /></div>
                      <div><label style={labelStyle}>Radius</label><select value={vehicleForm.radius ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, radius: (e.target.value as OperatingRadius) || undefined }))} style={inputStyle}><option value="">— Select —</option>{(Object.keys(OPERATING_RADIUS_LABELS) as OperatingRadius[]).map((k) => <option key={k} value={k}>{OPERATING_RADIUS_LABELS[k]}</option>)}</select></div>
                    </div>
                    {/* APD rating inputs */}
                    <div style={{ marginTop: 14, paddingTop: 12, borderTop: '1px solid var(--line-2)' }}>
                      <div style={{ fontWeight: 600, fontSize: 12, color: 'var(--ink-3)', marginBottom: 8, textTransform: 'uppercase', letterSpacing: '0.05em' }}>APD Rating Inputs</div>
                      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10 }}>
                        <div><label style={labelStyle}>APD Vehicle Class</label><select value={vehicleForm.apdVehicleClass ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, apdVehicleClass: e.target.value ? parseInt(e.target.value) : undefined }))} style={inputStyle}><option value="">— Select —</option>{APD_VEHICLE_CLASS_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select></div>
                        <div><label style={labelStyle}>Road Type</label><select value={vehicleForm.apdRoadType ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, apdRoadType: e.target.value ? parseInt(e.target.value) : undefined }))} style={inputStyle}><option value="">— Select —</option>{APD_ROAD_TYPE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select></div>
                        <div><label style={labelStyle}>Annual Miles</label><input type="number" value={vehicleForm.apdAnnualMiles ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, apdAnnualMiles: parseInt(e.target.value) || undefined }))} style={inputStyle} /></div>
                        <div><label style={labelStyle}>Operation Code</label><select value={vehicleForm.apdOperationCode ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, apdOperationCode: e.target.value ? parseInt(e.target.value) : undefined }))} style={inputStyle}><option value="">— Select —</option>{APD_OPERATION_CODE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select></div>
                        <div><label style={labelStyle}>State</label><select value={vehicleForm.apdState ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, apdState: e.target.value || undefined }))} style={inputStyle}><option value="">— Select —</option>{APD_SUPPORTED_STATES.map((s) => <option key={s} value={s}>{s}</option>)}</select></div>
                        <div><label style={labelStyle}>Stated Value ($)</label><input type="number" value={vehicleForm.apdStatedValue ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, apdStatedValue: parseFloat(e.target.value) || undefined }))} style={inputStyle} /></div>
                        <div><label style={labelStyle}>Comp Deductible</label><select value={vehicleForm.apdCompDeductible ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, apdCompDeductible: e.target.value ? parseInt(e.target.value) : undefined }))} style={inputStyle}><option value="">— Select —</option>{APD_COMP_DEDUCTIBLE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select></div>
                        <div><label style={labelStyle}>Coll Deductible</label><select value={vehicleForm.apdCollDeductible ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, apdCollDeductible: e.target.value ? parseInt(e.target.value) : undefined }))} style={inputStyle}><option value="">— Select —</option>{APD_COLL_DEDUCTIBLE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select></div>
                        <div><label style={labelStyle}>Driver Age Code</label><select value={vehicleForm.apdDriverAgeCode ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, apdDriverAgeCode: e.target.value !== '' ? parseInt(e.target.value) : undefined }))} style={inputStyle}><option value="">— Select —</option>{APD_DRIVER_AGE_CODE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select></div>
                        <div><label style={labelStyle}>Driver Points Code</label><select value={vehicleForm.apdDriverPointsCode ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, apdDriverPointsCode: e.target.value !== '' ? parseInt(e.target.value) : undefined }))} style={inputStyle}><option value="">— Select —</option>{APD_DRIVER_POINTS_CODE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select></div>
                        <div><label style={labelStyle}>Driver Exp Mod</label><select value={vehicleForm.apdDriverExpMod ?? ''} onChange={(e) => setVehicleForm((f) => ({ ...f, apdDriverExpMod: e.target.value ? parseFloat(e.target.value) : undefined }))} style={inputStyle}><option value="">— Select —</option>{APD_DRIVER_EXP_MOD_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select></div>
                      </div>
                    </div>
                    <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
                      <button onClick={() => saveVehicleMutation.mutate(vehicleForm)} disabled={saveVehicleMutation.isPending} className="sd-btn primary sm"><Check size={13} /> Save</button>
                      <button onClick={() => { setShowVehicleForm(false); setVehicleForm(emptyVehicleForm()); setEditingVehicleId(null) }} className="sd-btn outline sm"><X size={13} /> Cancel</button>
                    </div>
                  </div>
                )}
                {/* Vehicles section */}
                <div className="exp-h">
                  <div className="exp-h-l">Vehicles <span className="c">{vehicles.length}</span></div>
                  <button className="sd-btn ghost sm" onClick={() => { setShowVehicleForm(true); setVehicleForm(emptyVehicleForm()); setEditingVehicleId(null) }}><Plus size={12} /> Add</button>
                </div>
                {vehicles.length === 0 && !showVehicleForm ? (
                  <EmptyState icon={FileText} title="No vehicles added yet" description="Add scheduled vehicles for auto submissions." />
                ) : (
                  <table className="sd-table">
                    <thead><tr><th>Unit</th><th>Year / Make / Model</th><th>VIN</th><th>Class</th><th>GVW</th><th>Radius</th><th>Stated Value</th><th /></tr></thead>
                    <tbody>
                      {vehicles.map((v) => (
                        <tr key={v.id}>
                          <td className="id">{v.unitNumber}</td>
                          <td className="primary-cell">{[v.year, v.make, v.model].filter(Boolean).join(' ') || '—'}</td>
                          <td className="id">{v.vin ?? '—'}</td>
                          <td><span className="sd-lob">{VEHICLE_CLASS_LABELS[v.vehicleClass]}</span></td>
                          <td>{v.gvw ? v.gvw.toLocaleString() : '—'}</td>
                          <td>{v.radius ? OPERATING_RADIUS_LABELS[v.radius] : '—'}</td>
                          <td>{v.apdStatedValue ? formatCurrency(v.apdStatedValue) : '—'}</td>
                          <td style={{ padding: '8px 14px' }}>
                            <div style={{ display: 'flex', gap: 4 }}>
                              <button onClick={() => { setVehicleForm({ unitNumber: v.unitNumber, year: v.year ?? undefined, make: v.make ?? undefined, model: v.model ?? undefined, vin: v.vin ?? undefined, gvw: v.gvw ?? undefined, vehicleClass: v.vehicleClass, garagingZip: v.garagingZip ?? undefined, radius: v.radius ?? undefined, apdVehicleClass: v.apdVehicleClass ?? undefined, apdRoadType: v.apdRoadType ?? undefined, apdAnnualMiles: v.apdAnnualMiles ?? undefined, apdOperationCode: v.apdOperationCode ?? undefined, apdState: v.apdState ?? undefined, apdStatedValue: v.apdStatedValue ?? undefined, apdCompDeductible: v.apdCompDeductible ?? undefined, apdCollDeductible: v.apdCollDeductible ?? undefined, apdDriverAgeCode: v.apdDriverAgeCode ?? undefined, apdDriverPointsCode: v.apdDriverPointsCode ?? undefined, apdDriverExpMod: v.apdDriverExpMod ?? undefined }); setEditingVehicleId(v.id); setShowVehicleForm(true) }} className="sims-icon-btn hover:text-sky-600" title="Edit vehicle"><Pencil size={12} /></button>
                              <button onClick={() => { if (confirm('Remove vehicle?')) deleteVehicleMutation.mutate(v.id) }} className="sims-icon-btn hover:text-red-500" title="Remove vehicle"><Trash2 size={12} /></button>
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>

              {/* Supplemental collapsible */}
              <div style={{ borderTop: '1px solid var(--line)' }}>
                <button
                  className="sd-card-head"
                  style={{ width: '100%', background: 'none', border: 0, cursor: 'pointer', textAlign: 'left', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}
                  onClick={() => setSupplementalOpen((o) => !o)}
                >
                  <h3 style={{ margin: 0, fontSize: 13, fontWeight: 600 }}>Supplemental Info</h3>
                  <span style={{ fontSize: 12, color: 'var(--ink-3)' }}>{supplementalOpen ? '▲' : '▼'}</span>
                </button>
                {supplementalOpen && (
                  <div className="sd-card-body">
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                      <div>
                        <label style={labelStyle}>Commodities Hauled (comma-separated)</label>
                        <input value={supplementalForm.commoditiesHauled.join(', ')} onChange={(e) => { setSupplementalForm((f) => ({ ...f, commoditiesHauled: e.target.value.split(',').map((s) => s.trim()).filter(Boolean) })); setSupplementalDirty(true) }} placeholder="e.g. Lumber, Steel" style={inputStyle} />
                      </div>
                      <div>
                        <label style={labelStyle}>Terminal Locations (comma-separated)</label>
                        <input value={supplementalForm.terminalLocations.join(', ')} onChange={(e) => { setSupplementalForm((f) => ({ ...f, terminalLocations: e.target.value.split(',').map((s) => s.trim()).filter(Boolean) })); setSupplementalDirty(true) }} placeholder="e.g. Dallas TX" style={inputStyle} />
                      </div>
                      <div>
                        <label style={labelStyle}>Filings Required (comma-separated)</label>
                        <input value={supplementalForm.filingsRequired.join(', ')} onChange={(e) => { setSupplementalForm((f) => ({ ...f, filingsRequired: e.target.value.split(',').map((s) => s.trim()).filter(Boolean) })); setSupplementalDirty(true) }} placeholder="e.g. MCS-90" style={inputStyle} />
                      </div>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 10, justifyContent: 'center' }}>
                        <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', fontSize: 13 }}>
                          <input type="checkbox" checked={supplementalForm.safetyProgramInPlace} onChange={(e) => { setSupplementalForm((f) => ({ ...f, safetyProgramInPlace: e.target.checked })); setSupplementalDirty(true) }} />
                          Safety program in place
                        </label>
                        <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', fontSize: 13 }}>
                          <input type="checkbox" checked={supplementalForm.ownerOperator} onChange={(e) => { setSupplementalForm((f) => ({ ...f, ownerOperator: e.target.checked })); setSupplementalDirty(true) }} />
                          Owner-operator
                        </label>
                      </div>
                    </div>
                    {supplementalDirty && (
                      <div style={{ marginTop: 12 }}>
                        <button onClick={() => saveSupplementalMutation.mutate(supplementalForm)} disabled={saveSupplementalMutation.isPending} className="sd-btn primary sm">
                          <Check size={13} /> Save Supplemental Info
                        </button>
                      </div>
                    )}
                  </div>
                )}
              </div>
            </>
          )}

          {activeLob === 'im' && (
            <>
              {showEquipmentForm && (
                <div style={{ padding: '14px 16px', borderBottom: '1px solid var(--line-2)', background: 'var(--surface-2)' }}>
                  <div style={{ fontWeight: 600, fontSize: 13, marginBottom: 10 }}>{editingEquipmentId ? 'Edit Equipment' : 'Add Equipment'}</div>
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10 }}>
                    <div><label style={labelStyle}>Item #</label><input type="number" value={equipmentForm.itemNumber} onChange={(e) => setEquipmentForm((f) => ({ ...f, itemNumber: parseInt(e.target.value) || 1 }))} style={inputStyle} /></div>
                    <div><label style={labelStyle}>Year</label><input type="number" value={equipmentForm.year ?? ''} onChange={(e) => setEquipmentForm((f) => ({ ...f, year: parseInt(e.target.value) || undefined }))} style={inputStyle} /></div>
                    <div><label style={labelStyle}>Make</label><input value={equipmentForm.make ?? ''} onChange={(e) => setEquipmentForm((f) => ({ ...f, make: e.target.value || undefined }))} style={inputStyle} /></div>
                    <div><label style={labelStyle}>Model</label><input value={equipmentForm.model ?? ''} onChange={(e) => setEquipmentForm((f) => ({ ...f, model: e.target.value || undefined }))} style={inputStyle} /></div>
                    <div className="col-span-2"><label style={labelStyle}>Description</label><input value={equipmentForm.description ?? ''} onChange={(e) => setEquipmentForm((f) => ({ ...f, description: e.target.value || undefined }))} style={inputStyle} /></div>
                    <div><label style={labelStyle}>Serial #</label><input value={equipmentForm.serialNumber ?? ''} onChange={(e) => setEquipmentForm((f) => ({ ...f, serialNumber: e.target.value || undefined }))} style={inputStyle} /></div>
                    <div><label style={labelStyle}>Stated Value</label><input type="number" value={equipmentForm.value ?? ''} onChange={(e) => setEquipmentForm((f) => ({ ...f, value: parseFloat(e.target.value) || undefined }))} style={inputStyle} /></div>
                    <div><label style={labelStyle}>Equipment Type</label><select value={equipmentForm.equipmentTypeId ?? ''} onChange={(e) => setEquipmentForm((f) => ({ ...f, equipmentTypeId: e.target.value || null }))} style={inputStyle}><option value="">— Select —</option>{imEquipmentTypes.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}</select></div>
                    <div>
                      <label style={labelStyle}>Deductible</label>
                      <select value={equipmentForm.deductible === null ? '10ACV' : equipmentForm.deductible !== undefined ? String(equipmentForm.deductible) : ''} onChange={(e) => { const v = e.target.value; setEquipmentForm((f) => ({ ...f, deductible: v === '' ? undefined : v === '10ACV' ? null : Number(v) })) }} style={inputStyle}>
                        <option value="">— Select —</option>
                        {IM_DEDUCTIBLE_TIERS.map((t) => <option key={t.label} value={t.value === null ? '10ACV' : String(t.value)}>{t.label}</option>)}
                      </select>
                    </div>
                    <div><label style={labelStyle}>Settlement Basis</label><select value={equipmentForm.settlementBasis ?? ''} onChange={(e) => setEquipmentForm((f) => ({ ...f, settlementBasis: (e.target.value as SettlementBasis) || null }))} style={inputStyle}><option value="">— Select —</option>{(Object.keys(SETTLEMENT_BASIS_LABELS) as SettlementBasis[]).map((k) => <option key={k} value={k}>{SETTLEMENT_BASIS_LABELS[k]}</option>)}</select></div>
                    <div>
                      <label style={labelStyle}>Territory</label>
                      <select value={equipmentForm.territoryCode ?? ''} onChange={(e) => setEquipmentForm((f) => ({ ...f, territoryCode: e.target.value || null }))} style={inputStyle}>
                        <option value="">— Select —</option>
                        {imTerritories.map((t) => <option key={t.id} value={t.code}>Terr {t.code} ({t.states})</option>)}
                      </select>
                      {!editingEquipmentId && defaultTerritoryCode && !equipmentForm.territoryCode && (
                        <p style={{ fontSize: 11, color: 'var(--ink-3)', marginTop: 4, marginBottom: 0 }}>Default: Terr {defaultTerritoryCode}</p>
                      )}
                    </div>
                  </div>
                  <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
                    <button type="button" onClick={saveEquipment} disabled={saveEquipmentMutation.isPending} className="sd-btn primary sm"><Check size={13} /> Save</button>
                    <button type="button" onClick={() => { setShowEquipmentForm(false); setEquipmentForm(emptyEquipmentForm()); setEditingEquipmentId(null) }} className="sd-btn outline sm"><X size={13} /> Cancel</button>
                  </div>
                </div>
              )}
              <div className="exp-h">
                <div className="exp-h-l">Equipment schedule <span className="c">{equipment.length}</span></div>
                <button type="button" className="sd-btn ghost sm" onClick={openNewEquipmentForm}><Plus size={12} /> Add</button>
              </div>
              {equipment.length === 0 && !showEquipmentForm ? (
                <EmptyState icon={FileText} title="No equipment scheduled yet" description="Add scheduled equipment for Inland Marine submissions." />
              ) : (
                <table className="sd-table">
                  <thead><tr><th>Item</th><th>Year / Make / Model</th><th>Type</th><th className="num">Value</th><th>Ded.</th><th>Sett.</th><th>Terr.</th><th /></tr></thead>
                  <tbody>
                    {equipment.map((eq) => {
                      const type = imEquipmentTypes.find((t) => t.id === eq.equipmentTypeId)
                      const dedLabel = eq.deductible === null ? '10% ACV' : eq.deductible != null ? `$${eq.deductible.toLocaleString()}` : '—'
                      return (
                        <tr key={eq.id}>
                          <td className="id">{eq.itemNumber}</td>
                          <td className="primary-cell">{[eq.year, eq.make, eq.model].filter(Boolean).join(' ') || '—'}</td>
                          <td>{type?.name ?? '—'}</td>
                          <td className="num">{eq.value != null ? `$${eq.value.toLocaleString()}` : '—'}</td>
                          <td>{dedLabel}</td>
                          <td>{eq.settlementBasis ?? '—'}</td>
                          <td>{eq.territoryCode ?? '—'}</td>
                          <td style={{ padding: '8px 14px' }}>
                            <div style={{ display: 'flex', gap: 4 }}>
                              <button type="button" onClick={() => { setEquipmentForm({ itemNumber: eq.itemNumber, year: eq.year ?? undefined, make: eq.make ?? undefined, model: eq.model ?? undefined, description: eq.description ?? undefined, serialNumber: eq.serialNumber ?? undefined, value: eq.value ?? undefined, equipmentTypeId: eq.equipmentTypeId, territoryCode: eq.territoryCode, deductible: eq.deductible, settlementBasis: eq.settlementBasis }); setEditingEquipmentId(eq.id); setShowEquipmentForm(true) }} className="sims-icon-btn hover:text-sky-600" title="Edit equipment"><Pencil size={12} /></button>
                              <button type="button" onClick={() => { if (confirm('Remove equipment item?')) deleteEquipmentMutation.mutate(eq.id) }} disabled={deleteEquipmentMutation.isPending} className="sims-icon-btn hover:text-red-500" title="Remove equipment"><Trash2 size={12} /></button>
                            </div>
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              )}
            </>
          )}

          {activeLob === 'gl' && (
            <div className="sd-card-body">
              {/* Coverage limits */}
              <div style={{ marginBottom: 14 }}>
                <div style={{ fontWeight: 600, fontSize: 12, color: 'var(--ink-3)', marginBottom: 8, textTransform: 'uppercase', letterSpacing: '0.05em' }}>Coverage Limits</div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10 }}>
                  <div><label style={labelStyle}>Each Occurrence *</label><select value={glCovForm.eachOccurrence ?? ''} onChange={(e) => { setGlCovForm((f) => ({ ...f, eachOccurrence: e.target.value ? parseInt(e.target.value) : undefined, generalAggregate: e.target.value ? parseInt(e.target.value) * 2 : undefined, personalAndAdvInjury: e.target.value ? parseInt(e.target.value) : undefined })); setGlCovDirty(true) }} style={inputStyle}><option value="">— Select —</option>{GL_OCC_LIMIT_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select></div>
                  <div><label style={labelStyle}>General Aggregate</label><input type="number" readOnly value={glCovForm.generalAggregate ?? ''} style={{ ...inputStyle, background: 'var(--surface-2)', color: 'var(--ink-3)' }} /></div>
                  <div><label style={labelStyle}>Prod/Completed Ops Agg</label><select value={glCovForm.productsCompletedOps ?? ''} onChange={(e) => { setGlCovForm((f) => ({ ...f, productsCompletedOps: e.target.value ? parseInt(e.target.value) : undefined })); setGlCovDirty(true) }} style={inputStyle}><option value="">— Select —</option>{GL_PCO_LIMIT_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select></div>
                  <div><label style={labelStyle}>Medical Expense *</label><select value={glCovForm.medicalExpense ?? ''} onChange={(e) => { setGlCovForm((f) => ({ ...f, medicalExpense: e.target.value ? parseInt(e.target.value) : undefined })); setGlCovDirty(true) }} style={inputStyle}><option value="">— Select —</option>{GL_MED_LIMIT_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select></div>
                  <div><label style={labelStyle}>Damage to Rented Premises</label><input type="number" value={glCovForm.damageToRentedPremises ?? ''} onChange={(e) => { setGlCovForm((f) => ({ ...f, damageToRentedPremises: parseFloat(e.target.value) || undefined })); setGlCovDirty(true) }} style={inputStyle} placeholder="e.g. 100000" /></div>
                  <div><label style={labelStyle}>Total Subcontractor Cost</label><input type="number" value={glCovForm.totalSubcontractorCost ?? ''} onChange={(e) => { setGlCovForm((f) => ({ ...f, totalSubcontractorCost: parseFloat(e.target.value) || undefined })); setGlCovDirty(true) }} style={inputStyle} placeholder="For class 91581" /></div>
                </div>
              </div>
              {/* Endorsements */}
              <div style={{ paddingTop: 12, borderTop: '1px solid var(--line-2)', marginBottom: 14 }}>
                <div style={{ fontWeight: 600, fontSize: 12, color: 'var(--ink-3)', marginBottom: 8, textTransform: 'uppercase', letterSpacing: '0.05em' }}>Endorsements</div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10 }}>
                  <div><label style={labelStyle}># Additional Insureds (indiv.)</label><input type="number" min={0} value={glCovForm.aiIndividualCount || ''} onChange={(e) => { setGlCovForm((f) => ({ ...f, aiIndividualCount: parseInt(e.target.value) || 0 })); setGlCovDirty(true) }} style={inputStyle} placeholder="0" /></div>
                  <div><label style={labelStyle}># Waivers of Subrog. (indiv.)</label><input type="number" min={0} value={glCovForm.wosIndividualCount || ''} onChange={(e) => { setGlCovForm((f) => ({ ...f, wosIndividualCount: parseInt(e.target.value) || 0 })); setGlCovDirty(true) }} style={inputStyle} placeholder="0" /></div>
                  <div><label style={labelStyle}>Logging &amp; Lumbering (97111)</label><select value={glCovForm.loggingLumberingLimit ?? ''} onChange={(e) => { setGlCovForm((f) => ({ ...f, loggingLumberingLimit: e.target.value ? parseInt(e.target.value) : undefined })); setGlCovDirty(true) }} style={inputStyle}><option value="">— None —</option>{GL_LL_LIMIT_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select></div>
                </div>
                <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 6 }}>Enter counts to quote; add named additional-interest records before issuance. Blanket AI/WOS &amp; PNC are set in the Additional Interests section.</div>
              </div>
              {/* TRIA */}
              <div style={{ paddingTop: 12, borderTop: '1px solid var(--line-2)' }}>
                <div style={{ fontWeight: 600, fontSize: 12, color: 'var(--ink-3)', marginBottom: 8, textTransform: 'uppercase', letterSpacing: '0.05em' }}>Terrorism</div>
                <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', fontSize: 13 }}><input type="checkbox" checked={glCovForm.includeTria} onChange={(e) => { setGlCovForm((f) => ({ ...f, includeTria: e.target.checked })); setGlCovDirty(true) }} /> Include TRIA (2.5%)</label>
                {glCovDirty && (
                  <div style={{ marginTop: 12 }}>
                    <button onClick={() => saveGlCovMutation.mutate(glCovForm)} disabled={saveGlCovMutation.isPending} className="sd-btn primary sm"><Check size={13} /> Save GL Coverages</button>
                  </div>
                )}
              </div>

              <div style={{ marginTop: 16, paddingTop: 12, borderTop: '1px solid var(--line-2)' }}>
                <div className="exp-h" style={{ marginLeft: -16, marginRight: -16, borderTop: 0 }}>
                  <div className="exp-h-l">GL classification schedule <span className="c">{glClassifications.length}</span></div>
                  <button
                    className="sd-btn ghost sm"
                    onClick={() => {
                      setShowGlClassForm(true)
                      setGlClassForm({ locationNumber: (glClassifications.at(-1)?.locationNumber ?? 0) + 1 })
                      setEditingGlClassId(null)
                    }}
                  >
                    <Plus size={12} /> Add
                  </button>
                </div>

                {showGlClassForm && (
                  <div style={{ padding: '14px 0', borderBottom: '1px solid var(--line-2)' }}>
                    <div style={{ fontWeight: 600, fontSize: 13, marginBottom: 10 }}>{editingGlClassId ? 'Edit GL Exposure' : 'Add GL Exposure'}</div>
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10 }}>
                      <div><label style={labelStyle}>Location #</label><input type="number" value={glClassForm.locationNumber} onChange={(e) => setGlClassForm((f) => ({ ...f, locationNumber: parseInt(e.target.value) || 1 }))} style={inputStyle} /></div>
                      <div>
                        <label style={labelStyle}>Class Code *</label>
                        <select
                          value={glClassForm.classCode ?? ''}
                          onChange={(e) => {
                            const option = GL_CLASS_CODE_OPTIONS.find((o) => o.value === e.target.value)
                            setGlClassForm((f) => ({
                              ...f,
                              classCode: e.target.value || undefined,
                              description: option?.label.split(' - ')[1] ?? f.description,
                            }))
                          }}
                          style={inputStyle}
                        >
                          <option value="">- Select -</option>
                          {GL_CLASS_CODE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                        </select>
                      </div>
                      <div><label style={labelStyle}>Premium Basis</label><input value={glClassForm.premiumBasis ?? ''} onChange={(e) => setGlClassForm((f) => ({ ...f, premiumBasis: e.target.value || undefined }))} placeholder="Payroll, sales, cost, area" style={inputStyle} /></div>
                      <div><label style={labelStyle}>Exposure *</label><input type="number" value={glClassForm.exposure ?? ''} onChange={(e) => setGlClassForm((f) => ({ ...f, exposure: parseFloat(e.target.value) || undefined }))} placeholder="0" style={inputStyle} /></div>
                      <div style={{ gridColumn: 'span 4' }}><label style={labelStyle}>Description</label><input value={glClassForm.description ?? ''} onChange={(e) => setGlClassForm((f) => ({ ...f, description: e.target.value || undefined }))} style={inputStyle} /></div>
                    </div>
                    <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
                      <button onClick={() => saveGlClassMutation.mutate(glClassForm)} disabled={!glClassForm.classCode || !glClassForm.exposure || saveGlClassMutation.isPending} className="sd-btn primary sm"><Check size={13} /> Save GL Exposure</button>
                      <button onClick={() => { setShowGlClassForm(false); setGlClassForm(emptyGlClassForm()); setEditingGlClassId(null) }} className="sd-btn outline sm"><X size={13} /> Cancel</button>
                    </div>
                  </div>
                )}

                {glClassifications.length === 0 && !showGlClassForm ? (
                  <EmptyState icon={FileText} title="No GL classifications entered yet" description="Add class codes and exposures for General Liability submissions." />
                ) : (
                  <table className="sd-table">
                    <thead><tr><th>Loc.</th><th>Class Code</th><th>Description</th><th>Premium Basis</th><th className="num">Exposure</th><th /></tr></thead>
                    <tbody>
                      {glClassifications.map((classification) => (
                        <tr key={classification.id}>
                          <td className="id">{classification.locationNumber}</td>
                          <td><span className="sd-lob">{classification.classCode ?? '-'}</span></td>
                          <td className="primary-cell">{classification.description ?? '-'}</td>
                          <td>{classification.premiumBasis ?? '-'}</td>
                          <td className="num">{classification.exposure != null ? classification.exposure.toLocaleString() : '-'}</td>
                          <td style={{ padding: '8px 14px' }}>
                            <div style={{ display: 'flex', gap: 4 }}>
                              <button
                                onClick={() => {
                                  setGlClassForm({
                                    locationNumber: classification.locationNumber,
                                    classCode: classification.classCode ?? undefined,
                                    description: classification.description ?? undefined,
                                    premiumBasis: classification.premiumBasis ?? undefined,
                                    exposure: classification.exposure ?? undefined,
                                  })
                                  setEditingGlClassId(classification.id)
                                  setShowGlClassForm(true)
                                }}
                                className="sims-icon-btn hover:text-sky-600"
                                title="Edit GL exposure"
                              >
                                <Pencil size={12} />
                              </button>
                              <button onClick={() => { if (confirm('Remove GL exposure?')) deleteGlClassMutation.mutate(classification.id) }} className="sims-icon-btn hover:text-red-500" title="Remove GL exposure"><Trash2 size={12} /></button>
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </div>
          )}
        </section>
      )}

      {/* Additional Interests tab */}
      {activeTab === 'additional-interests' && (
        <section className="sd-card">
          <div className="sd-card-head">
            <h3>Additional interests <span className="cnt">{additionalInterests.length}</span></h3>
            <button
              className="sd-btn sm primary"
              onClick={() => {
                setShowAdditionalInterestForm(true)
                setAdditionalInterestForm({ ...emptyAdditionalInterestForm(), lineOfBusiness: submission?.linesOfBusiness[0] ?? ACTIVE_LOBS[0] })
                setEditingAdditionalInterestId(null)
              }}
            >
              <Plus size={12} /> Add interest
            </button>
          </div>
          <div className="sd-card-body tight">
            <div style={{ padding: '14px 16px', borderBottom: '1px solid var(--line-2)' }}>
              <div style={{ fontWeight: 600, fontSize: 13, marginBottom: 10 }}>Blanket requests</div>
              <table className="sd-table">
                <thead><tr><th>LOB</th><th>Blanket AI</th><th>Blanket WOS</th><th>Blanket PNC</th></tr></thead>
                <tbody>
                  {additionalInterestLobs.map((lob) => {
                    const blanket = additionalInterestBlankets.find((b) => b.lineOfBusiness === lob)
                    return (
                      <tr key={lob}>
                        <td><span className="sd-lob">{LOB_SHORT[lob] ?? lob}</span></td>
                        <td><input type="checkbox" checked={blanket?.additionalInsured ?? false} disabled={saveAdditionalInterestBlanketMutation.isPending} onChange={(e) => saveBlanketRequest(lob, { additionalInsured: e.target.checked })} /></td>
                        <td><input type="checkbox" checked={blanket?.waiverOfSubrogation ?? false} disabled={saveAdditionalInterestBlanketMutation.isPending} onChange={(e) => saveBlanketRequest(lob, { waiverOfSubrogation: e.target.checked })} /></td>
                        <td><input type="checkbox" checked={blanket?.primaryNonContributory ?? false} disabled={saveAdditionalInterestBlanketMutation.isPending} onChange={(e) => saveBlanketRequest(lob, { primaryNonContributory: e.target.checked })} /></td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
            {showAdditionalInterestForm && (
              <div style={{ padding: '14px 16px', borderBottom: '1px solid var(--line-2)', background: 'var(--surface-2)' }}>
                <div style={{ fontWeight: 600, fontSize: 13, marginBottom: 10 }}>{editingAdditionalInterestId ? 'Edit Additional Interest' : 'Add Additional Interest'}</div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10 }}>
                  <div style={{ gridColumn: 'span 2' }}><label style={labelStyle}>Name *</label><input value={additionalInterestForm.name} onChange={(e) => setAdditionalInterestForm((f) => ({ ...f, name: e.target.value }))} style={inputStyle} /></div>
                  <div><label style={labelStyle}>Line of Business</label><select value={additionalInterestForm.lineOfBusiness} onChange={(e) => setAdditionalInterestForm((f) => ({ ...f, lineOfBusiness: e.target.value }))} style={inputStyle}>{(submission?.linesOfBusiness.length ? submission.linesOfBusiness : ACTIVE_LOBS).map((l) => <option key={l} value={l}>{getLobLabel(l)}</option>)}</select></div>
                  <div><label style={labelStyle}>Applies To</label><select value={additionalInterestForm.appliesToType} onChange={(e) => setAdditionalInterestForm((f) => ({ ...f, appliesToType: e.target.value as AdditionalInterestAppliesToType }))} style={inputStyle}>{(Object.keys(ADDITIONAL_INTEREST_APPLIES_TO_LABELS) as AdditionalInterestAppliesToType[]).map((k) => <option key={k} value={k}>{ADDITIONAL_INTEREST_APPLIES_TO_LABELS[k]}</option>)}</select></div>
                  <div style={{ gridColumn: 'span 2' }}><label style={labelStyle}>Address Line 1</label><input value={additionalInterestForm.addressLine1 ?? ''} onChange={(e) => setAdditionalInterestForm((f) => ({ ...f, addressLine1: e.target.value || undefined }))} style={inputStyle} /></div>
                  <div style={{ gridColumn: 'span 2' }}><label style={labelStyle}>Address Line 2</label><input value={additionalInterestForm.addressLine2 ?? ''} onChange={(e) => setAdditionalInterestForm((f) => ({ ...f, addressLine2: e.target.value || undefined }))} style={inputStyle} /></div>
                  <div><label style={labelStyle}>City</label><input value={additionalInterestForm.city ?? ''} onChange={(e) => setAdditionalInterestForm((f) => ({ ...f, city: e.target.value || undefined }))} style={inputStyle} /></div>
                  <div><label style={labelStyle}>State</label><input maxLength={2} value={additionalInterestForm.state ?? ''} onChange={(e) => setAdditionalInterestForm((f) => ({ ...f, state: e.target.value.toUpperCase() || undefined }))} style={inputStyle} /></div>
                  <div><label style={labelStyle}>ZIP</label><input value={additionalInterestForm.zipCode ?? ''} onChange={(e) => setAdditionalInterestForm((f) => ({ ...f, zipCode: e.target.value || undefined }))} style={inputStyle} /></div>
                  <div><label style={labelStyle}>Scheduled Items</label><input value={additionalInterestForm.scheduledItemNumbers ?? ''} onChange={(e) => setAdditionalInterestForm((f) => ({ ...f, scheduledItemNumbers: e.target.value || undefined }))} placeholder="e.g. 1, 3, 7" style={inputStyle} /></div>
                  <div><label style={labelStyle}>Email</label><input value={additionalInterestForm.email ?? ''} onChange={(e) => setAdditionalInterestForm((f) => ({ ...f, email: e.target.value || undefined }))} style={inputStyle} /></div>
                  <div><label style={labelStyle}>Phone</label><input value={additionalInterestForm.phone ?? ''} onChange={(e) => setAdditionalInterestForm((f) => ({ ...f, phone: e.target.value || undefined }))} style={inputStyle} /></div>
                  <div style={{ gridColumn: 'span 2' }}><label style={labelStyle}>Notes</label><input value={additionalInterestForm.notes ?? ''} onChange={(e) => setAdditionalInterestForm((f) => ({ ...f, notes: e.target.value || undefined }))} style={inputStyle} /></div>
                </div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 14, marginTop: 12 }}>
                  <label style={{ display: 'flex', alignItems: 'center', gap: 7, fontSize: 13 }}><input type="checkbox" checked={additionalInterestForm.additionalInsured} onChange={(e) => setAdditionalInterestForm((f) => ({ ...f, additionalInsured: e.target.checked }))} /> Additional Insured</label>
                  <label style={{ display: 'flex', alignItems: 'center', gap: 7, fontSize: 13 }}><input type="checkbox" checked={additionalInterestForm.lossPayee} onChange={(e) => setAdditionalInterestForm((f) => ({ ...f, lossPayee: e.target.checked }))} /> Loss Payee</label>
                  <label style={{ display: 'flex', alignItems: 'center', gap: 7, fontSize: 13 }}><input type="checkbox" checked={additionalInterestForm.waiverOfSubrogation} onChange={(e) => setAdditionalInterestForm((f) => ({ ...f, waiverOfSubrogation: e.target.checked }))} /> Waiver of Subrogation</label>
                  <label style={{ display: 'flex', alignItems: 'center', gap: 7, fontSize: 13 }}><input type="checkbox" checked={additionalInterestForm.primaryNonContributory} onChange={(e) => setAdditionalInterestForm((f) => ({ ...f, primaryNonContributory: e.target.checked }))} /> Primary & Non-Contributory</label>
                </div>
                <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
                  <button onClick={() => saveAdditionalInterestMutation.mutate(additionalInterestForm)} disabled={!additionalInterestForm.name || saveAdditionalInterestMutation.isPending} className="sd-btn primary sm"><Check size={13} /> Save</button>
                  <button onClick={() => { setShowAdditionalInterestForm(false); setAdditionalInterestForm(emptyAdditionalInterestForm()); setEditingAdditionalInterestId(null) }} className="sd-btn outline sm"><X size={13} /> Cancel</button>
                </div>
              </div>
            )}
            {additionalInterests.length === 0 && !showAdditionalInterestForm ? (
              <EmptyState
                icon={FileText}
                title="No additional interests entered"
                description="Add additional insureds, loss payees, or waiver requests as needed."
                action={
                  <button className="sd-btn outline sm" onClick={() => { setShowAdditionalInterestForm(true); setAdditionalInterestForm({ ...emptyAdditionalInterestForm(), lineOfBusiness: submission?.linesOfBusiness[0] ?? ACTIVE_LOBS[0] }); setEditingAdditionalInterestId(null) }}><Plus size={13} /> Add Interest</button>
                }
              />
            ) : (
              <table className="sd-table">
                <thead><tr><th>Name</th><th>LOB</th><th>Requested</th><th>Applies To</th><th>Address</th><th /></tr></thead>
                <tbody>
                  {additionalInterests.map((a) => {
                    const requested = [
                      a.additionalInsured ? 'AI' : null,
                      a.lossPayee ? 'LP' : null,
                      a.waiverOfSubrogation ? 'WOS' : null,
                      a.primaryNonContributory ? 'PNC' : null,
                    ].filter((v): v is string => Boolean(v))
                    const address = [a.addressLine1, a.city, a.state, a.zipCode].filter(Boolean).join(', ')
                    return (
                      <tr key={a.id}>
                        <td className="primary-cell">{a.name}</td>
                        <td><span className="sd-lob">{LOB_SHORT[a.lineOfBusiness] ?? a.lineOfBusiness}</span></td>
                        <td>{requested.length > 0 ? requested.map((r) => <span key={r} className="sd-lob" style={{ marginRight: 4 }}>{r}</span>) : '-'}</td>
                        <td>{ADDITIONAL_INTEREST_APPLIES_TO_LABELS[a.appliesToType]}{a.scheduledItemNumbers ? `: ${a.scheduledItemNumbers}` : ''}</td>
                        <td>{address || '-'}</td>
                        <td style={{ padding: '8px 14px' }}>
                          <div style={{ display: 'flex', gap: 4 }}>
                            <button onClick={() => { setAdditionalInterestForm({ lineOfBusiness: a.lineOfBusiness, name: a.name, addressLine1: a.addressLine1 ?? undefined, addressLine2: a.addressLine2 ?? undefined, city: a.city ?? undefined, state: a.state ?? undefined, zipCode: a.zipCode ?? undefined, email: a.email ?? undefined, phone: a.phone ?? undefined, appliesToType: a.appliesToType, scheduledItemNumbers: a.scheduledItemNumbers ?? undefined, additionalInsured: a.additionalInsured, lossPayee: a.lossPayee, waiverOfSubrogation: a.waiverOfSubrogation, primaryNonContributory: a.primaryNonContributory, notes: a.notes ?? undefined }); setEditingAdditionalInterestId(a.id); setShowAdditionalInterestForm(true) }} className="sims-icon-btn hover:text-sky-600" title="Edit additional interest"><Pencil size={12} /></button>
                            <button onClick={() => { if (confirm('Remove additional interest?')) deleteAdditionalInterestMutation.mutate(a.id) }} className="sims-icon-btn hover:text-red-500" title="Remove additional interest"><Trash2 size={12} /></button>
                          </div>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            )}
          </div>
        </section>
      )}

      {/* Prior Carriers tab */}
      {activeTab === 'prior-carriers' && (
        <section className="sd-card">
          <div className="sd-card-head">
            <h3>Prior carriers <span className="cnt">{priorCarriers.length}</span></h3>
            <button className="sd-btn sm primary" onClick={() => { setShowCarrierForm(true); setCarrierForm(emptyCarrierForm()); setEditingCarrierId(null) }}><Plus size={12} /> Add prior carrier</button>
          </div>
          <div className="sd-card-body tight">
            {showCarrierForm && (
              <div style={{ padding: '14px 16px', borderBottom: '1px solid var(--line-2)', background: 'var(--surface-2)' }}>
                <div style={{ fontWeight: 600, fontSize: 13, marginBottom: 10 }}>{editingCarrierId ? 'Edit Prior Carrier' : 'Add Prior Carrier'}</div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10 }}>
                  <div style={{ gridColumn: 'span 2' }}><label style={labelStyle}>Carrier Name *</label><input value={carrierForm.carrierName} onChange={(e) => setCarrierForm((f) => ({ ...f, carrierName: e.target.value }))} style={inputStyle} /></div>
                  <div><label style={labelStyle}>Line of Business</label><input value={carrierForm.lineOfBusiness ?? ''} onChange={(e) => setCarrierForm((f) => ({ ...f, lineOfBusiness: e.target.value || undefined }))} placeholder="e.g. CommercialAuto" style={inputStyle} /></div>
                  <div><label style={labelStyle}>Policy Number</label><input value={carrierForm.policyNumber ?? ''} onChange={(e) => setCarrierForm((f) => ({ ...f, policyNumber: e.target.value || undefined }))} style={inputStyle} /></div>
                  <div><label style={labelStyle}>Expiration Date</label><input type="date" value={carrierForm.expirationDate ?? ''} onChange={(e) => setCarrierForm((f) => ({ ...f, expirationDate: e.target.value || undefined }))} style={inputStyle} /></div>
                  <div><label style={labelStyle}>Premium</label><input type="number" value={carrierForm.premium ?? ''} onChange={(e) => setCarrierForm((f) => ({ ...f, premium: parseFloat(e.target.value) || undefined }))} placeholder="Optional" style={inputStyle} /></div>
                </div>
                <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
                  <button onClick={() => savePriorCarrierMutation.mutate(carrierForm)} disabled={!carrierForm.carrierName || savePriorCarrierMutation.isPending} className="sd-btn primary sm"><Check size={13} /> Save</button>
                  <button onClick={() => { setShowCarrierForm(false); setCarrierForm(emptyCarrierForm()); setEditingCarrierId(null) }} className="sd-btn outline sm"><X size={13} /> Cancel</button>
                </div>
              </div>
            )}
            {priorCarriers.length === 0 && !showCarrierForm ? (
              <EmptyState
                icon={FileText}
                title="No prior carrier history"
                description="Add prior carrier details when they are available."
                action={
                  <button className="sd-btn outline sm" onClick={() => { setShowCarrierForm(true); setCarrierForm(emptyCarrierForm()); setEditingCarrierId(null) }}><Plus size={13} /> Add Prior Carrier</button>
                }
              />
            ) : (
              <>
                <table className="sd-table">
                  <thead><tr><th>Carrier</th><th>LOB</th><th>Policy #</th><th>Expiration</th><th className="num">Premium</th><th /></tr></thead>
                  <tbody>
                    {priorCarriers.map((p) => (
                      <tr key={p.id}>
                        <td className="primary-cell">{p.carrierName}</td>
                        <td>{p.lineOfBusiness ? <span className="sd-lob">{LOB_SHORT[p.lineOfBusiness] ?? p.lineOfBusiness}</span> : '—'}</td>
                        <td className="id">{p.policyNumber ?? '—'}</td>
                        <td>{p.expirationDate ?? '—'}</td>
                        <td className="num">{p.premium != null ? fmtMoney(p.premium) : '—'}</td>
                        <td style={{ padding: '8px 14px' }}>
                          <div style={{ display: 'flex', gap: 4 }}>
                            <button onClick={() => { setCarrierForm({ carrierName: p.carrierName, lineOfBusiness: p.lineOfBusiness ?? undefined, policyNumber: p.policyNumber ?? undefined, expirationDate: p.expirationDate ?? undefined, premium: p.premium ?? undefined }); setEditingCarrierId(p.id); setShowCarrierForm(true) }} className="sims-icon-btn hover:text-sky-600" title="Edit prior carrier"><Pencil size={12} /></button>
                            <button onClick={() => { if (confirm('Remove prior carrier?')) deletePriorCarrierMutation.mutate(p.id) }} className="sims-icon-btn hover:text-red-500" title="Remove prior carrier"><Trash2 size={12} /></button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                {!showCarrierForm && (
                  <div style={{ padding: '10px 14px', borderTop: '1px solid var(--line-2)' }}>
                    <button onClick={() => { setShowCarrierForm(true); setCarrierForm(emptyCarrierForm()); setEditingCarrierId(null) }} className="sd-btn ghost sm"><Plus size={13} /> Add Prior Carrier</button>
                  </div>
                )}
              </>
            )}
          </div>
        </section>
      )}

      {/* Documents tab */}
      {activeTab === 'documents' && (
        <DocumentsSection entityType="Submission" entityId={id!} canUpload={canUploadAttachments} canDelete={canDeleteAttachments} />
      )}

      {/* Activity tab */}
      {activeTab === 'activity' && (
        <section className="sd-card">
          <div className="sd-card-head"><h3>Activity <span className="cnt">{outboundCommunications.length}</span></h3></div>
          <div className="sd-card-body tight">
            {outboundCommunications.length === 0 ? (
              <EmptyState
                icon={Send}
                title="No communication activity recorded yet"
                description="Generated and sent emails for this submission will appear here."
              />
            ) : (
              <table className="sd-table">
                <thead><tr><th>Subject</th><th>To</th><th>From</th><th>Status</th><th>Attachments</th><th>Created</th><th /></tr></thead>
                <tbody>
                  {outboundCommunications.map((c) => (
                    <tr key={c.id}>
                      <td className="primary-cell">{c.subject}</td>
                      <td>{c.toName ? `${c.toName} <${c.toAddress}>` : c.toAddress}</td>
                      <td>{c.fromAddress}</td>
                      <td><span className="sd-lob">{c.status}</span></td>
                      <td>{c.attachmentCount}</td>
                      <td>{new Date(c.createdAt).toLocaleString()}</td>
                      <td>
                        {(c.status === 'Draft' || c.status === 'Failed') && (
                          <button
                            type="button"
                            className="sims-icon-btn hover:text-sky-600"
                            title="Send email"
                            disabled={sendCommunicationMutation.isPending}
                            onClick={() => sendCommunicationMutation.mutate(c.id)}
                          >
                            <Send className="h-3.5 w-3.5" />
                          </button>
                        )}
                        {c.status === 'Sent' && c.graphMessageWebLink && (
                          <button
                            type="button"
                            className="sims-icon-btn hover:text-sky-600"
                            title="Open in Outlook"
                            onClick={() => window.open(c.graphMessageWebLink!, '_blank', 'noopener,noreferrer')}
                          >
                            <ExternalLink className="h-3.5 w-3.5" />
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </section>
      )}

      {showGenerateModal && (
        <GenerateDocumentModal
          entityType="Submission"
          entityId={id!}
          onClose={() => setShowGenerateModal(false)}
        />
      )}
    </div>
  )
}

// ── Shared style objects ───────────────────────────────────────────────────────

const inputStyle: React.CSSProperties = {
  width: '100%',
  border: '1px solid var(--line)',
  borderRadius: 'var(--r-md)',
  padding: '6px 8px',
  fontSize: 13,
  fontFamily: 'inherit',
  background: 'var(--surface)',
  color: 'var(--ink)',
}

const labelStyle: React.CSSProperties = {
  display: 'block',
  fontSize: 10.5,
  fontWeight: 600,
  textTransform: 'uppercase',
  letterSpacing: '.04em',
  color: 'var(--ink-3)',
  marginBottom: 4,
}
