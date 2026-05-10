import { useState, useEffect, useMemo } from 'react'
import { Link, useParams, useNavigate, useLocation } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft, Plus, Pencil, Trash2, CheckCircle, X, Check, FileText,
  RefreshCw, AlertTriangle, TrendingDown, Calculator, MoreHorizontal,
  ShieldCheck,
} from 'lucide-react'
import { toast } from 'sonner'
import { submissionsApi } from '@/api/submissions.api'
import { inboundEmailsApi } from '@/api/inboundEmails.api'
import { quotesApi } from '@/api/quotes.api'
import { carriersApi } from '@/api/carriers.api'
import { usersApi } from '@/api/users.api'
import { agentsApi } from '@/api/agents.api'
import { submissionDriversApi, submissionVehiclesApi, submissionPriorCarriersApi, submissionSupplementalApi, submissionGLApi, submissionIMApi, imLookupsApi } from '@/api/submissionLob.api'
import { submissionLossHistoryApi } from '@/api/submissionLossHistory.api'
import { insuredsApi } from '@/api/insureds.api'
import { VEHICLE_CLASS_LABELS, OPERATING_RADIUS_LABELS, IM_DEDUCTIBLE_TIERS, SETTLEMENT_BASIS_LABELS, APD_VEHICLE_CLASS_OPTIONS, APD_ROAD_TYPE_OPTIONS, APD_OPERATION_CODE_OPTIONS, APD_DRIVER_AGE_CODE_OPTIONS, APD_DRIVER_POINTS_CODE_OPTIONS, APD_DRIVER_EXP_MOD_OPTIONS, APD_COMP_DEDUCTIBLE_OPTIONS, APD_COLL_DEDUCTIBLE_OPTIONS, APD_SUPPORTED_STATES } from '@/types/submissionLob.types'
import type { SubmissionDriver, SubmissionDriverCreate, SubmissionVehicle, SubmissionVehicleCreate, SubmissionPriorCarrier, SubmissionPriorCarrierCreate, SubmissionSupplemental, SubmissionSupplementalUpsert, VehicleClass, OperatingRadius, SubmissionEquipmentCreate, SettlementBasis } from '@/types/submissionLob.types'
import { SUBMISSION_STATUS_LABELS, type SubmissionStatus, type SubmissionUpdate, type Submission } from '@/types/submission.types'
import { LOB_LABELS, ACTIVE_LOBS, QUOTE_STATUS_LABELS, type PolicyLineOfBusiness, type QuoteStatus, type QuoteCreate, type QuoteBind, type CommissionOverrideRequest } from '@/types/quote.types'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { QuoteRatingPanel } from '@/components/quotes/QuoteRatingPanel'
import { QuoteAutoSafetyPanel } from '@/components/quotes/QuoteAutoSafetyPanel'
import { formatCurrency } from '@/lib/utils'
import { DocumentsSection } from '@/components/documents/DocumentsSection'
import { GenerateDocumentModal } from '@/components/documents/GenerateDocumentModal'
import { usePermissions } from '@/hooks/usePermissions'

// ── Constants ──────────────────────────────────────────────────────────────────

const LOB_SHORT: Record<string, string> = {
  CommercialAuto: 'Auto',
  GeneralLiability: 'GL',
  WorkersComp: 'WC',
  InlandMarine: 'IM',
  Property: 'Prop',
  Cargo: 'Cargo',
}

const AUTO_SAFETY_LOBS = new Set<PolicyLineOfBusiness>(['AutoLiability', 'AutoPhysicalDamage', 'CommercialAuto'])

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
  carrierId: '', lineOfBusiness: '', effectiveDate: '', expirationDate: '',
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
  const diff = new Date(dateStr).getTime() - Date.now()
  return Math.ceil(diff / 86400000)
}

// ── Page Component ─────────────────────────────────────────────────────────────

type Tab = 'quotes' | 'exposures' | 'prior-carriers' | 'documents' | 'activity'

export function SubmissionDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const location = useLocation()
  const qc = useQueryClient()
  const { canUploadAttachments, canDeleteAttachments, canCreatePolicies } = usePermissions()

  const extractionState = location.state as { extractionStatus?: string; emailId?: string } | null
  const [showExtractionBanner, setShowExtractionBanner] = useState(
    extractionState?.extractionStatus === 'Failed' || extractionState?.extractionStatus === 'DetectionFailed'
  )
  const [reExtractLob, setReExtractLob] = useState('')

  const [activeTab, setActiveTab] = useState<Tab>('quotes')
  const [expLob, setExpLob] = useState<'auto' | 'gl' | 'im'>('auto')
  const [showGenerateModal, setShowGenerateModal] = useState(false)
  const [showQuoteForm, setShowQuoteForm] = useState(false)
  const [quoteForm, setQuoteForm] = useState<QuoteForm>(emptyQuoteForm())
  const [bindingQuoteId, setBindingQuoteId] = useState<string | null>(null)
  const [bindForm, setBindForm] = useState({ boundDate: '', effectiveDate: '', expirationDate: '' })

  const openLossHistory = () => {
    if (id) navigate(`/submissions/${id}/loss-history`)
  }
  const [overrideQuoteId, setOverrideQuoteId] = useState<string | null>(null)
  const [overrideMode, setOverrideMode] = useState<'dollar' | 'rate'>('dollar')
  const [overrideInput, setOverrideInput] = useState('')
  const [ratingOpenForQuoteId, setRatingOpenForQuoteId] = useState<string | null>(null)
  const [autoSafetyOpenForQuoteId, setAutoSafetyOpenForQuoteId] = useState<string | null>(null)
  const [supplementalOpen, setSupplementalOpen] = useState(false)
  const emptyDriverForm = (): SubmissionDriverCreate => ({ driverNumber: 1, name: '', dateOfBirth: undefined, licenseNumber: undefined, licenseState: undefined, dateHired: undefined })
  const [showDriverForm, setShowDriverForm] = useState(false)
  const [driverForm, setDriverForm] = useState<SubmissionDriverCreate>(emptyDriverForm())
  const [editingDriverId, setEditingDriverId] = useState<string | null>(null)
  const emptyVehicleForm = (): SubmissionVehicleCreate => ({ unitNumber: 1, vehicleClass: 'Unknown' })
  const [showVehicleForm, setShowVehicleForm] = useState(false)
  const [vehicleForm, setVehicleForm] = useState<SubmissionVehicleCreate>(emptyVehicleForm())
  const [editingVehicleId, setEditingVehicleId] = useState<string | null>(null)
  const emptyCarrierForm = (): SubmissionPriorCarrierCreate => ({ carrierName: '' })
  const [showCarrierForm, setShowCarrierForm] = useState(false)
  const [carrierForm, setCarrierForm] = useState<SubmissionPriorCarrierCreate>(emptyCarrierForm())
  const [editingCarrierId, setEditingCarrierId] = useState<string | null>(null)
  const emptyEquipmentForm = (): SubmissionEquipmentCreate => ({ itemNumber: 1 })
  const [showEquipmentForm, setShowEquipmentForm] = useState(false)
  const [equipmentForm, setEquipmentForm] = useState<SubmissionEquipmentCreate>(emptyEquipmentForm())
  const [editingEquipmentId, setEditingEquipmentId] = useState<string | null>(null)
  const emptySupplementalForm = (): SubmissionSupplementalUpsert => ({ commoditiesHauled: [], terminalLocations: [], safetyProgramInPlace: false, filingsRequired: [], ownerOperator: false })
  const [supplementalForm, setSupplementalForm] = useState<SubmissionSupplementalUpsert>(emptySupplementalForm())
  const [supplementalDirty, setSupplementalDirty] = useState(false)

  // ── Queries ────────────────────────────────────────────────────────────────

  const { data: submission, isLoading } = useQuery({
    queryKey: ['submissions', id],
    queryFn: () => submissionsApi.getById(id!),
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

  const { data: priorCarriers = [] } = useQuery({
    queryKey: ['submission-prior-carriers', id],
    queryFn: () => submissionPriorCarriersApi.getAll(id!),
    enabled: !!id && activeTab === 'prior-carriers',
  })

  const { data: supplemental } = useQuery({
    queryKey: ['submission-supplemental', id],
    queryFn: () => submissionSupplementalApi.get(id!),
    enabled: !!id && supplementalOpen,
  })

  const { data: glCoverages } = useQuery({
    queryKey: ['submission-gl-coverages', id],
    queryFn: () => submissionGLApi.getCoverages(id!),
    enabled: !!id,
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
    queryKey: ['insured', submission?.insuredId],
    queryFn: () => insuredsApi.getById(submission!.insuredId),
    enabled: !!submission?.insuredId,
  })

  const defaultTerritoryCode = useMemo(() => {
    const state = insured?.state?.trim().toUpperCase()
    if (!state || imTerritories.length === 0) return undefined
    const match = imTerritories.find((t) =>
      t.states.split(',').map((s) => s.trim().toUpperCase()).includes(state)
    )
    return match?.code
  }, [insured?.state, imTerritories])

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

  const selectedCarrier = carriers.find((c) => c.id === quoteForm.carrierId)
  const availableLobs = selectedCarrier
    ? selectedCarrier.linesOfBusiness.filter((l) => ACTIVE_LOBS.includes(l))
    : ACTIVE_LOBS

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
        qc.invalidateQueries({ queryKey: ['submission-prior-carriers', id] })
        qc.invalidateQueries({ queryKey: ['submission-supplemental', id] })
        qc.invalidateQueries({ queryKey: ['submission-gl-coverages', id] })
        qc.invalidateQueries({ queryKey: ['submission-im-coverages', id] })
      } else {
        toast.error('Extraction failed again — please fill in the fields manually below')
      }
    },
    onError: () => toast.error('Re-extraction request failed'),
  })

  const setLobsMutation = useMutation({
    mutationFn: (lobs: string[]) => submissionsApi.setLinesOfBusiness(id!, lobs),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['submissions', id] }),
    onError: () => toast.error('Failed to update lines of business'),
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
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to create quote'),
  })

  const deleteQuoteMutation = useMutation({
    mutationFn: (quoteId: string) => quotesApi.delete(quoteId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quotes', 'by-submission', id] })
      toast.success('Quote deleted')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to delete quote'),
  })

  const bindMutation = useMutation({
    mutationFn: ({ quoteId, data }: { quoteId: string; data: QuoteBind }) => quotesApi.bind(quoteId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quotes', 'by-submission', id] })
      qc.invalidateQueries({ queryKey: ['submissions', id] })
      setBindingQuoteId(null)
      toast.success('Quote bound — policy created')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to bind quote'),
  })

  const commissionOverrideMutation = useMutation({
    mutationFn: ({ quoteId, data }: { quoteId: string; data: CommissionOverrideRequest }) =>
      quotesApi.commissionOverride(quoteId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quotes', 'by-submission', id] })
      setOverrideQuoteId(null)
      setOverrideInput('')
      toast.success('Commission give-back applied — premium updated')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to apply commission override'),
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
    onError: () => toast.error('Failed to save driver'),
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
    onError: () => toast.error('Failed to save vehicle'),
  })
  const deleteVehicleMutation = useMutation({
    mutationFn: (vId: string) => submissionVehiclesApi.delete(id!, vId),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['submission-vehicles', id] }); toast.success('Vehicle removed') },
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
    onError: () => toast.error('Failed to save prior carrier'),
  })
  const deletePriorCarrierMutation = useMutation({
    mutationFn: (cId: string) => submissionPriorCarriersApi.delete(id!, cId),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['submission-prior-carriers', id] }); toast.success('Prior carrier removed') },
  })

  const saveEquipmentMutation = useMutation({
    mutationFn: (dto: SubmissionEquipmentCreate) =>
      editingEquipmentId
        ? submissionIMApi.updateEquipment(id!, editingEquipmentId, dto)
        : submissionIMApi.createEquipment(id!, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['submission-equipment', id] })
      setShowEquipmentForm(false); setEquipmentForm(emptyEquipmentForm()); setEditingEquipmentId(null)
      toast.success('Equipment saved')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to save equipment'),
  })
  const deleteEquipmentMutation = useMutation({
    mutationFn: (eId: string) => submissionIMApi.deleteEquipment(id!, eId),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['submission-equipment', id] }); toast.success('Equipment removed') },
  })

  const saveSupplementalMutation = useMutation({
    mutationFn: (dto: SubmissionSupplementalUpsert) => submissionSupplementalApi.upsert(id!, dto),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['submission-supplemental', id] })
      setSupplementalDirty(false)
      toast.success('Supplemental info saved')
    },
    onError: () => toast.error('Failed to save supplemental info'),
  })

  // ── Handlers ───────────────────────────────────────────────────────────────

  const handleCreateQuote = () => {
    if (!quoteForm.carrierId || !quoteForm.lineOfBusiness || !quoteForm.effectiveDate || !quoteForm.expirationDate) {
      toast.error('Carrier, line of business, and dates are required')
      return
    }
    createQuoteMutation.mutate({
      submissionId: id!,
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

  const handleBind = () => {
    if (!bindingQuoteId || !bindForm.boundDate || !bindForm.effectiveDate || !bindForm.expirationDate) {
      toast.error('All bind dates are required')
      return
    }
    bindMutation.mutate({ quoteId: bindingQuoteId, data: bindForm })
  }

  const setQF = (k: keyof QuoteForm) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    const val = e.target.value
    setQuoteForm((prev) => {
      const next = { ...prev, [k]: val }
      if (k === 'carrierId') next.lineOfBusiness = ''
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

  const hasGL = submission.linesOfBusiness.includes('GeneralLiability')
  const hasIM = submission.linesOfBusiness.includes('InlandMarine')
  const expLobList = [
    { k: 'auto' as const, label: 'Commercial Auto', count: drivers.length + vehicles.length },
    ...(hasGL ? [{ k: 'gl' as const, label: 'General Liability', count: null as null }] : []),
    ...(hasIM ? [{ k: 'im' as const, label: 'Inland Marine', count: equipment.length }] : []),
  ]
  const activeLob: 'auto' | 'gl' | 'im' =
    (expLob === 'gl' && !hasGL) ? 'auto' :
    (expLob === 'im' && !hasIM) ? 'auto' :
    expLob

  // ── Shared quote list renderer ─────────────────────────────────────────────

  const renderQuoteList = () => (
    <div>
      {/* LOB strip */}
      <div className="sd-lob-strip">
        <span style={{ fontSize: 10.5, letterSpacing: '.06em', textTransform: 'uppercase', color: 'var(--ink-4)', fontWeight: 600, marginRight: 2 }}>Lines:</span>
        {submission.linesOfBusiness.map((lob) => (
          <span key={lob} className="sd-lob-chip">
            {LOB_SHORT[lob] ?? lob}
            <button
              onClick={() => setLobsMutation.mutate(submission.linesOfBusiness.filter((l) => l !== lob))}
              disabled={setLobsMutation.isPending}
              style={{ cursor: 'pointer', opacity: .5, fontSize: 11, background: 'none', border: 0, color: 'inherit', lineHeight: 1, padding: 0 }}
            >×</button>
          </span>
        ))}
        {ACTIVE_LOBS.filter((l) => !submission.linesOfBusiness.includes(l)).length > 0 && (
          <select
            value=""
            onChange={(e) => { if (e.target.value) setLobsMutation.mutate([...submission.linesOfBusiness, e.target.value]) }}
            disabled={setLobsMutation.isPending}
            className="sd-lob-add"
          >
            <option value="">+ Add LOB</option>
            {ACTIVE_LOBS.filter((l) => !submission.linesOfBusiness.includes(l)).map((l) => (
              <option key={l} value={l}>{LOB_LABELS[l]}</option>
            ))}
          </select>
        )}
      </div>

      {/* Quote rows */}
      {quotes.length === 0 && !showQuoteForm ? (
        <div style={{ padding: '36px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>
          <div style={{ color: 'var(--ink-2)', fontWeight: 600, fontSize: 13.5, marginBottom: 4 }}>No quotes yet</div>
          <button className="sd-btn sm" style={{ marginTop: 12 }} onClick={() => { setShowQuoteForm(true); setQuoteForm(emptyQuoteForm()) }}>
            <Plus size={13} /> Add Quote
          </button>
        </div>
      ) : (
        <div>
          {quotes.map((q) => (
            <div key={q.id}>
              <div className="sd-quote">
                <div className="badge">{LOB_SHORT[q.lineOfBusiness] ?? q.lineOfBusiness}</div>
                <div className="body">
                  <div className="top">
                    <Link to={`/quotes/${q.id}`} className="carrier hover:underline">{q.carrierName}</Link>
                    <span className={`sd-pill ${QUOTE_STATUS_PILL[q.status]}`}>{QUOTE_STATUS_LABELS[q.status]}</span>
                  </div>
                  <div className="meta">
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--ink-4)' }}>{q.quoteNumber}</span>
                    <span className="sep">·</span>
                    <span>{new Date(q.effectiveDate).toLocaleDateString()} → {new Date(q.expirationDate).toLocaleDateString()}</span>
                    {q.hasCommissionOverride && <><span className="sep">·</span><span style={{ color: 'var(--warn-fg)' }}>commission override</span></>}
                  </div>
                </div>
                <div className="prem">
                  {q.totalPremium == null || q.totalPremium === 0
                    ? <div className="v" style={{ color: 'var(--ink-4)' }}>—</div>
                    : <div className="v">{fmtMoney(q.totalPremium)}</div>}
                </div>
                <div className="acts">
                  <button
                    onClick={(e) => { e.stopPropagation(); setRatingOpenForQuoteId((cur) => cur === q.id ? null : q.id) }}
                    className={`sd-btn sm ${ratingOpenForQuoteId === q.id ? 'primary' : 'outline'}`}
                    title="Rating engine"
                  >
                    <Calculator size={12} /> Rate
                  </button>
                  {AUTO_SAFETY_LOBS.has(q.lineOfBusiness) && (
                    <button
                      onClick={(e) => { e.stopPropagation(); setAutoSafetyOpenForQuoteId((cur) => cur === q.id ? null : q.id) }}
                      className={`sd-btn sm ${autoSafetyOpenForQuoteId === q.id ? 'primary' : 'outline'}`}
                      title="FMCSA auto safety"
                    >
                      <ShieldCheck size={12} /> Safety
                    </button>
                  )}
                  {q.status !== 'Bound' && q.status !== 'Cancelled' && q.status !== 'Expired' && canCreatePolicies && !q.hasCommissionOverride && (
                    <button
                      onClick={(e) => { e.stopPropagation(); setOverrideQuoteId(q.id); setOverrideMode('dollar'); setOverrideInput('') }}
                      className="sd-btn sm outline"
                      title="Reduce commission"
                    >
                      <TrendingDown size={12} /> Reduce
                    </button>
                  )}
                  {q.status !== 'Bound' && q.status !== 'Cancelled' && q.status !== 'Expired' && (
                    <button
                      onClick={(e) => { e.stopPropagation(); setBindingQuoteId(q.id); setBindForm({ boundDate: '', effectiveDate: q.effectiveDate, expirationDate: q.expirationDate }) }}
                      className="sd-btn sm success"
                    >
                      <CheckCircle size={12} /> Bind
                    </button>
                  )}
                  {q.status === 'Bound' && (
                    <Link to={`/policies/${q.id}`} className="sd-btn sm outline">View Policy</Link>
                  )}
                  <Link to={`/quotes/${q.id}/writeup`} className="sd-btn sm ghost" title="UW Writeup">
                    <FileText size={12} />
                  </Link>
                  {q.status !== 'Bound' && (
                    <button
                      onClick={(e) => { e.stopPropagation(); if (confirm('Delete this quote?')) deleteQuoteMutation.mutate(q.id) }}
                      className="sd-btn sm ghost"
                      style={{ color: 'var(--bad-fg)' }}
                    >
                      <Trash2 size={12} />
                    </button>
                  )}
                </div>
              </div>

              {/* Commission override panel */}
              {overrideQuoteId === q.id && (
                <div style={{ padding: '14px 16px', background: 'var(--warn-bg)', borderTop: '1px solid #f0dbb0' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
                    <TrendingDown size={14} style={{ color: 'var(--warn-fg)' }} />
                    <span style={{ fontSize: 12, fontWeight: 600, color: 'var(--warn-fg)' }}>Reduce Agent Commission — lower premium for competitive pricing</span>
                  </div>
                  <p style={{ fontSize: 11.5, color: 'var(--warn-fg)', marginBottom: 12, marginTop: 0 }}>
                    Carrier net and SMM commission are unchanged. Agent gives back part of their commission, reducing total premium.
                  </p>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
                    <div style={{ display: 'flex', borderRadius: 6, border: '1px solid #e8c97a', overflow: 'hidden', fontSize: 12 }}>
                      <button onClick={() => setOverrideMode('dollar')} style={{ padding: '6px 12px', background: overrideMode === 'dollar' ? '#c07d10' : '#fff', color: overrideMode === 'dollar' ? '#fff' : 'var(--warn-fg)', border: 0, cursor: 'pointer', fontFamily: 'inherit' }}>$ Give-Back</button>
                      <button onClick={() => setOverrideMode('rate')} style={{ padding: '6px 12px', background: overrideMode === 'rate' ? '#c07d10' : '#fff', color: overrideMode === 'rate' ? '#fff' : 'var(--warn-fg)', border: 0, cursor: 'pointer', fontFamily: 'inherit' }}>% New Rate</button>
                    </div>
                    <input
                      type="number" min="0"
                      step={overrideMode === 'dollar' ? '1' : '0.01'}
                      value={overrideInput}
                      onChange={(e) => setOverrideInput(e.target.value)}
                      placeholder={overrideMode === 'dollar' ? 'e.g. 500' : 'e.g. 8.5'}
                      style={{ width: 120, border: '1px solid #e8c97a', borderRadius: 6, padding: '6px 8px', fontSize: 13, fontFamily: 'inherit' }}
                    />
                    <button
                      onClick={() => {
                        const val = parseFloat(overrideInput)
                        if (isNaN(val) || val <= 0) { toast.error('Enter a valid amount'); return }
                        const data: CommissionOverrideRequest = overrideMode === 'dollar' ? { givebackAmount: val } : { newAgentRate: val / 100 }
                        commissionOverrideMutation.mutate({ quoteId: q.id, data })
                      }}
                      disabled={commissionOverrideMutation.isPending}
                      className="sd-btn sm"
                      style={{ background: '#c07d10', color: '#fff', border: 0 }}
                    >
                      <Check size={12} /> Apply
                    </button>
                    <button onClick={() => setOverrideQuoteId(null)} className="sd-btn sm outline"><X size={12} /> Cancel</button>
                  </div>
                </div>
              )}

              {/* Rating panel */}
              {ratingOpenForQuoteId === q.id && (
                <QuoteRatingPanel
                  quoteId={q.id}
                  submissionId={q.submissionId}
                  lineOfBusiness={q.lineOfBusiness}
                  isBound={q.status === 'Bound'}
                />
              )}

              {/* Auto safety panel */}
              {autoSafetyOpenForQuoteId === q.id && AUTO_SAFETY_LOBS.has(q.lineOfBusiness) && (
                <QuoteAutoSafetyPanel quoteId={q.id} />
              )}

              {/* Bind form */}
              {bindingQuoteId === q.id && (
                <div style={{ padding: '14px 16px', background: 'var(--good-bg)', borderTop: '1px solid #b8dfc4' }}>
                  <p style={{ fontSize: 12, fontWeight: 600, color: 'var(--good-fg)', marginBottom: 12, marginTop: 0 }}>Bind Quote — confirm policy dates</p>
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12, marginBottom: 12 }}>
                    {[
                      { label: 'Bound Date *', key: 'boundDate' },
                      { label: 'Effective Date *', key: 'effectiveDate' },
                      { label: 'Expiration Date *', key: 'expirationDate' },
                    ].map(({ label, key }) => (
                      <div key={key}>
                        <label style={{ display: 'block', fontSize: 10.5, fontWeight: 600, color: 'var(--ink-3)', marginBottom: 4, textTransform: 'uppercase', letterSpacing: '.04em' }}>{label}</label>
                        <input type="date" value={bindForm[key as keyof typeof bindForm]} onChange={(e) => setBindForm((b) => ({ ...b, [key]: e.target.value }))} style={{ width: '100%', border: '1px solid var(--line)', borderRadius: 6, padding: '6px 8px', fontSize: 13, fontFamily: 'inherit' }} />
                      </div>
                    ))}
                  </div>
                  <div style={{ display: 'flex', gap: 8 }}>
                    <button onClick={handleBind} disabled={bindMutation.isPending} className="sd-btn success sm"><CheckCircle size={12} /> Confirm Bind</button>
                    <button onClick={() => setBindingQuoteId(null)} className="sd-btn outline sm"><X size={12} /> Cancel</button>
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {/* Add quote form */}
      {showQuoteForm && (
        <div style={{ padding: '14px 16px', borderTop: '1px solid var(--line-2)', background: 'var(--surface-2)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
            <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)' }}>New Quote</span>
            <button onClick={() => setShowQuoteForm(false)} className="sd-btn ghost sm"><X size={13} /></button>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 10 }}>
            {[
              { label: 'Carrier *', node: <select value={quoteForm.carrierId} onChange={setQF('carrierId')} style={inputStyle}><option value="">— Select carrier —</option>{carriers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</select> },
              { label: 'Line of Business *', node: <select value={quoteForm.lineOfBusiness} onChange={setQF('lineOfBusiness')} disabled={!quoteForm.carrierId} style={inputStyle}><option value="">— Select LOB —</option>{availableLobs.map((l) => <option key={l} value={l}>{LOB_LABELS[l]}</option>)}</select> },
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
          <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
            <button onClick={handleCreateQuote} disabled={createQuoteMutation.isPending} className="sd-btn primary sm"><Check size={13} /> Save Quote</button>
            <button onClick={() => setShowQuoteForm(false)} className="sd-btn outline sm"><X size={13} /> Cancel</button>
          </div>
        </div>
      )}

      {!showQuoteForm && quotes.length > 0 && (
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
        <div style={{ marginBottom: 16, borderRadius: 8, border: '1px solid #f0d480', background: '#fdf8e1', padding: '12px 16px' }}>
          <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 16 }}>
            <div style={{ display: 'flex', alignItems: 'flex-start', gap: 8, fontSize: 13, color: '#7a5a0b' }}>
              <AlertTriangle size={15} style={{ marginTop: 1, flexShrink: 0 }} />
              <span>
                {extractionState?.extractionStatus === 'DetectionFailed'
                  ? <><strong>Line of business not detected</strong> — data extracted but LOB could not be identified. Select the LOB below and re-run, or set LOBs manually.</>
                  : <><strong>AI extraction failed</strong> — attachment could not be read automatically. Re-run or fill in fields manually.</>}
              </span>
            </div>
            <button onClick={() => setShowExtractionBanner(false)} style={{ background: 'none', border: 0, cursor: 'pointer', color: '#c07d10' }}><X size={15} /></button>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 10 }}>
            {extractionState?.extractionStatus === 'DetectionFailed' && (
              <select value={reExtractLob} onChange={(e) => setReExtractLob(e.target.value)} style={{ ...inputStyle, width: 'auto' }}>
                <option value="">— Select LOB hint —</option>
                {ACTIVE_LOBS.map((l) => <option key={l} value={l}>{LOB_LABELS[l]}</option>)}
              </select>
            )}
            <button onClick={() => reExtract.mutate()} disabled={reExtract.isPending} className="sd-btn outline sm" style={{ color: '#7a5a0b', borderColor: '#e8c97a' }}>
              <RefreshCw size={13} className={reExtract.isPending ? 'animate-spin' : ''} />
              {reExtract.isPending ? 'Re-extracting…' : 'Re-run Extraction'}
            </button>
          </div>
        </div>
      )}

      {/* Back link */}
      <Link to={`/insureds/${submission.insuredId}`} style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 12.5, color: 'var(--ink-3)', fontWeight: 500, marginBottom: 14, textDecoration: 'none' }}>
        <ArrowLeft size={13} /> {submission.insuredName}
      </Link>

      {/* Page header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 24, marginBottom: 0 }}>
        <div style={{ display: 'flex', gap: 14, alignItems: 'flex-start', minWidth: 0 }}>
          <div style={{ width: 36, height: 36, borderRadius: 8, background: 'var(--accent-soft)', color: 'var(--accent-ink)', display: 'grid', placeItems: 'center', flexShrink: 0, border: '1px solid #cfe0ef' }}>
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
          {canCreatePolicies && (
            <button onClick={() => setShowGenerateModal(true)} className="sd-btn">
              <FileText size={13} /> Generate doc
            </button>
          )}
          <button
            className="sd-btn primary"
            onClick={() => { setActiveTab('quotes'); setShowQuoteForm(true); setQuoteForm(emptyQuoteForm()) }}
          >
            <Plus size={13} /> Add quote
          </button>
        </div>
      </div>

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
          <div className="v" style={{ gap: 4 }}>
            {submission.linesOfBusiness.length === 0
              ? <span style={{ color: 'var(--ink-4)', fontStyle: 'italic' }}>None</span>
              : submission.linesOfBusiness.map((l) => <span key={l} className="sd-lob">{LOB_SHORT[l] ?? l}</span>)}
          </div>
        </div>
        <div>
          <div className="k">Effective</div>
          <div className="v" style={{ fontVariantNumeric: 'tabular-nums' }}>
            {submission.effectiveDate ? new Date(submission.effectiveDate).toLocaleDateString() : '—'}
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
          <div className="v" style={{ color: daysToEff != null && daysToEff < 14 ? '#b33a2a' : 'inherit' }}>
            {daysToEff != null ? `${daysToEff}d` : '—'}
          </div>
          <div className="s">{submission.effectiveDate ? new Date(submission.effectiveDate).toLocaleDateString() : '—'}</div>
        </div>
        <div className="sd-metric">
          <div className="k">Loss ratio</div>
          <div className="v" style={{ color: lossSummary?.lossRatio != null && lossSummary.lossRatio > 0.65 ? '#b33a2a' : 'inherit' }}>
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
                  {submission.effectiveDate ? new Date(submission.effectiveDate).toLocaleDateString() : '—'}
                  {' → '}
                  {submission.expirationDate ? new Date(submission.expirationDate).toLocaleDateString() : '—'}
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
            <div className="sd-card-body" style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', minHeight: 118, gap: 10 }}>
              <p style={{ margin: 0, fontSize: 12.5, color: 'var(--ink-4)', fontStyle: 'italic' }}>No loss history on file</p>
              <button type="button" onClick={openLossHistory} className="sd-btn sm"><Plus size={13} /> Add loss year</button>
            </div>
          )}
        </section>
        <section className="sd-card">
          <div className="sd-card-head"><h3>UW Notes</h3></div>
          <div className="sd-card-body" style={{ fontSize: 13, lineHeight: 1.55, color: 'var(--ink-2)' }}>
            {submission.descriptionOfOperations
              ? <p style={{ margin: 0 }}>{submission.descriptionOfOperations}</p>
              : <p style={{ margin: 0, color: 'var(--ink-4)', fontStyle: 'italic' }}>No notes on file.</p>}
            {submission.underwriterName && (
              <div style={{ marginTop: 10, paddingTop: 10, borderTop: '1px solid var(--line-2)', display: 'flex', alignItems: 'center', gap: 8, fontSize: 11.5, color: 'var(--ink-3)' }}>
                <div style={{ width: 22, height: 22, borderRadius: '50%', background: 'var(--accent-soft)', color: 'var(--accent-ink)', display: 'grid', placeItems: 'center', fontSize: 9.5, fontWeight: 700, flexShrink: 0 }}>
                  {submission.underwriterName.split(' ').map((n) => n[0]).join('').slice(0, 2)}
                </div>
                <span><b style={{ color: 'var(--ink-2)' }}>{submission.underwriterName}</b></span>
              </div>
            )}
          </div>
        </section>
      </div>

      {/* Tab strip */}
      <div className="sd-tabs" style={{ marginBottom: 14 }}>
        {([
          { key: 'quotes', label: 'Quotes', count: quotes.length },
          { key: 'exposures', label: 'Exposures', count: drivers.length + vehicles.length + equipment.length > 0 ? drivers.length + vehicles.length + equipment.length : undefined },
          { key: 'prior-carriers', label: 'Prior carriers', count: priorCarriers.length > 0 ? priorCarriers.length : undefined },
          { key: 'documents', label: 'Documents' },
          { key: 'activity', label: 'Activity' },
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
            <div style={{ display: 'flex', gap: 6 }}>
              <button className="sd-btn sm primary" onClick={() => { setShowQuoteForm(true); setQuoteForm(emptyQuoteForm()) }}>
                <Plus size={12} /> Add quote
              </button>
            </div>
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
                <div style={{ padding: '20px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>No drivers added yet</div>
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
                            <button onClick={() => { setDriverForm({ driverNumber: d.driverNumber, name: d.name, dateOfBirth: d.dateOfBirth ?? undefined, licenseNumber: d.licenseNumber ?? undefined, licenseState: d.licenseState ?? undefined, dateHired: d.dateHired ?? undefined }); setEditingDriverId(d.id); setShowDriverForm(true) }} className="sd-btn ghost sm"><Pencil size={12} /></button>
                            <button onClick={() => { if (confirm('Remove driver?')) deleteDriverMutation.mutate(d.id) }} className="sd-btn ghost sm" style={{ color: 'var(--bad-fg)' }}><Trash2 size={12} /></button>
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
                  <div style={{ padding: '20px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>No vehicles added yet</div>
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
                              <button onClick={() => { setVehicleForm({ unitNumber: v.unitNumber, year: v.year ?? undefined, make: v.make ?? undefined, model: v.model ?? undefined, vin: v.vin ?? undefined, gvw: v.gvw ?? undefined, vehicleClass: v.vehicleClass, garagingZip: v.garagingZip ?? undefined, radius: v.radius ?? undefined, apdVehicleClass: v.apdVehicleClass ?? undefined, apdRoadType: v.apdRoadType ?? undefined, apdAnnualMiles: v.apdAnnualMiles ?? undefined, apdOperationCode: v.apdOperationCode ?? undefined, apdState: v.apdState ?? undefined, apdStatedValue: v.apdStatedValue ?? undefined, apdCompDeductible: v.apdCompDeductible ?? undefined, apdCollDeductible: v.apdCollDeductible ?? undefined, apdDriverAgeCode: v.apdDriverAgeCode ?? undefined, apdDriverPointsCode: v.apdDriverPointsCode ?? undefined, apdDriverExpMod: v.apdDriverExpMod ?? undefined }); setEditingVehicleId(v.id); setShowVehicleForm(true) }} className="sd-btn ghost sm"><Pencil size={12} /></button>
                              <button onClick={() => { if (confirm('Remove vehicle?')) deleteVehicleMutation.mutate(v.id) }} className="sd-btn ghost sm" style={{ color: 'var(--bad-fg)' }}><Trash2 size={12} /></button>
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
                    <button onClick={() => saveEquipmentMutation.mutate(equipmentForm)} disabled={saveEquipmentMutation.isPending} className="sd-btn primary sm"><Check size={13} /> Save</button>
                    <button onClick={() => { setShowEquipmentForm(false); setEquipmentForm(emptyEquipmentForm()); setEditingEquipmentId(null) }} className="sd-btn outline sm"><X size={13} /> Cancel</button>
                  </div>
                </div>
              )}
              <div className="exp-h">
                <div className="exp-h-l">Equipment schedule <span className="c">{equipment.length}</span></div>
                <button className="sd-btn ghost sm" onClick={() => { setShowEquipmentForm(true); setEquipmentForm({ itemNumber: (equipment.at(-1)?.itemNumber ?? 0) + 1, territoryCode: defaultTerritoryCode ?? null }); setEditingEquipmentId(null) }}><Plus size={12} /> Add</button>
              </div>
              {equipment.length === 0 && !showEquipmentForm ? (
                <div style={{ padding: '20px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>No equipment scheduled yet</div>
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
                              <button onClick={() => { setEquipmentForm({ itemNumber: eq.itemNumber, year: eq.year ?? undefined, make: eq.make ?? undefined, model: eq.model ?? undefined, description: eq.description ?? undefined, serialNumber: eq.serialNumber ?? undefined, value: eq.value ?? undefined, equipmentTypeId: eq.equipmentTypeId, territoryCode: eq.territoryCode, deductible: eq.deductible, settlementBasis: eq.settlementBasis }); setEditingEquipmentId(eq.id); setShowEquipmentForm(true) }} className="sd-btn ghost sm"><Pencil size={12} /></button>
                              <button onClick={() => { if (confirm('Remove equipment item?')) deleteEquipmentMutation.mutate(eq.id) }} className="sd-btn ghost sm" style={{ color: 'var(--bad-fg)' }}><Trash2 size={12} /></button>
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
              <div className="sd-fields">
                {glCoverages ? (
                  <>
                    {glCoverages.eachOccurrence != null && <div className="sd-field"><span className="lbl">Each Occurrence</span><span className="val">{fmtMoney(glCoverages.eachOccurrence)}</span></div>}
                    {glCoverages.generalAggregate != null && <div className="sd-field"><span className="lbl">General Aggregate</span><span className="val">{fmtMoney(glCoverages.generalAggregate)}</span></div>}
                  </>
                ) : (
                  <p style={{ margin: 0, fontSize: 12.5, color: 'var(--ink-4)', fontStyle: 'italic' }}>No GL coverage data on file.</p>
                )}
              </div>
            </div>
          )}
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
              <div style={{ padding: '36px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>
                <div style={{ color: 'var(--ink-2)', fontWeight: 600, marginBottom: 4 }}>No prior carrier history</div>
                <button className="sd-btn outline sm" style={{ marginTop: 10 }} onClick={() => { setShowCarrierForm(true); setCarrierForm(emptyCarrierForm()); setEditingCarrierId(null) }}><Plus size={13} /> Add Prior Carrier</button>
              </div>
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
                            <button onClick={() => { setCarrierForm({ carrierName: p.carrierName, lineOfBusiness: p.lineOfBusiness ?? undefined, policyNumber: p.policyNumber ?? undefined, expirationDate: p.expirationDate ?? undefined, premium: p.premium ?? undefined }); setEditingCarrierId(p.id); setShowCarrierForm(true) }} className="sd-btn ghost sm"><Pencil size={12} /></button>
                            <button onClick={() => { if (confirm('Remove prior carrier?')) deletePriorCarrierMutation.mutate(p.id) }} className="sd-btn ghost sm" style={{ color: 'var(--bad-fg)' }}><Trash2 size={12} /></button>
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
        <section className="sd-card">
          <div className="sd-card-head"><h3>Documents</h3></div>
          <div style={{ padding: '14px 16px' }}>
            <DocumentsSection entityType="Submission" entityId={id!} canUpload={canUploadAttachments} canDelete={canDeleteAttachments} />
          </div>
        </section>
      )}

      {/* Activity tab */}
      {activeTab === 'activity' && (
        <section className="sd-card">
          <div className="sd-card-head"><h3>Activity</h3></div>
          <div style={{ padding: '36px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>
            <div style={{ color: 'var(--ink-2)', fontWeight: 600, fontSize: 13.5, marginBottom: 4 }}>No activity recorded yet</div>
            <div style={{ fontSize: 12 }}>Activity log coming soon.</div>
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
