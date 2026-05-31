import { useEffect, useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft, Plus, Pencil, Trash2, Check, X, Phone, Mail,
  Star, UserCircle, Globe, MapPin, Percent, BanknoteIcon, ShieldCheck,
  FileSpreadsheet, AlertTriangle, CheckCircle2,
} from 'lucide-react'
import { toast } from 'sonner'
import { carriersApi } from '@/api/carriers.api'
import { programConfigurationsApi } from '@/api/programConfigurations.api'
import type { CarrierContact, CarrierContactInput, CarrierUpdate } from '@/types/carrier.types'
import type { PolicyLineOfBusiness } from '@/types/quote.types'
import { LOB_LABELS, ACTIVE_LOBS } from '@/types/quote.types'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { EmptyState } from '@/components/common/EmptyState'
import { AddressAutocomplete } from '@/components/common/AddressAutocomplete'
import { isValidEmail, isValidPhone, isValidZip, formatPhoneInput } from '@/lib/validators'
import { formatCurrency } from '@/lib/utils'
import { DocumentsSection } from '@/components/documents/DocumentsSection'
import { usePermissions } from '@/hooks/usePermissions'
import {
  getCarrierCommissions,
  createCarrierCommission,
  disableCarrierCommission,
} from '@/api/carrierCommissions.api'
import {
  getBordereauxProfiles,
  createBordereauxProfile,
  updateBordereauxProfile,
} from '@/api/bordereaux.api'
import type { CarrierCommission } from '@/types/carrierCommission.types'
import type { BordereauxProfile } from '@/types/bordereaux.types'
import {
  BordereauxProfileSetupPanel,
  bordereauxProfileToRequest,
} from '@/components/bordereaux/BordereauxProfileSetupPanel'
import { ratingApi } from '@/api/rating.api'
import type { CarrierRatingAssignment, RatingPlanVersionPicker } from '@/types/rating.types'
import { carrierAdditionalInterestRatesApi } from '@/api/carrierAdditionalInterestRates.api'
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

// ─── Contact form ──────────────────────────────────────────────────────────────

type ContactFormData = {
  firstName: string
  lastName: string
  title: string
  email: string
  phone: string
  isPrimary: boolean
}

const emptyContactForm = (): ContactFormData => ({
  firstName: '', lastName: '', title: '', email: '', phone: '', isPrimary: false,
})

function contactToForm(c: CarrierContact): ContactFormData {
  return { firstName: c.firstName, lastName: c.lastName ?? '', title: c.title ?? '', email: c.email ?? '', phone: c.phone ?? '', isPrimary: c.isPrimary }
}

function ContactForm({
  form, setForm, onSave, onCancel, isPending,
}: {
  form: ContactFormData
  setForm: (f: ContactFormData) => void
  onSave: () => void
  onCancel: () => void
  isPending: boolean
}) {
  const set = (k: keyof ContactFormData) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm({ ...form, [k]: e.target.type === 'checkbox' ? (e.target as HTMLInputElement).checked : e.target.value })

  const emailError = form.email && !isValidEmail(form.email)
  const phoneError = form.phone && !isValidPhone(form.phone)

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12, paddingTop: 4 }}>
      <div className="sims-fields" style={{ gridTemplateColumns: 'repeat(4, 1fr)' }}>
        <div>
          <label className="sims-field-label">First Name *</label>
          <input value={form.firstName} onChange={set('firstName')} placeholder="First name" className="sims-input" autoFocus />
        </div>
        <div>
          <label className="sims-field-label">Last Name</label>
          <input value={form.lastName} onChange={set('lastName')} placeholder="Last name" className="sims-input" />
        </div>
        <div>
          <label className="sims-field-label">Title / Role</label>
          <input value={form.title} onChange={set('title')} placeholder="e.g. Underwriter" className="sims-input" />
        </div>
        <div style={{ display: 'flex', alignItems: 'flex-end', paddingBottom: 2 }}>
          <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, color: 'var(--ink-2)', cursor: 'pointer' }}>
            <input type="checkbox" checked={form.isPrimary} onChange={set('isPrimary')} />
            Primary contact
          </label>
        </div>
      </div>
      <div className="sims-fields" style={{ gridTemplateColumns: '1fr 1fr' }}>
        <div>
          <label className="sims-field-label">Email</label>
          <input value={form.email} onChange={set('email')} type="text" placeholder="email@example.com" className="sims-input" />
          {emailError && <p style={{ fontSize: 11.5, color: 'var(--bad-fg)', marginTop: 2 }}>Enter a valid email address</p>}
        </div>
        <div>
          <label className="sims-field-label">Phone</label>
          <input
            value={form.phone}
            onChange={(e) => setForm({ ...form, phone: formatPhoneInput(e.target.value) })}
            type="text"
            placeholder="(555) 123-4567"
            className="sims-input"
          />
          {phoneError && <p style={{ fontSize: 11.5, color: 'var(--bad-fg)', marginTop: 2 }}>Enter a valid 10-digit number</p>}
        </div>
      </div>
      <div style={{ display: 'flex', gap: 8 }}>
        <button onClick={onSave} disabled={isPending || !form.firstName.trim()} className="sd-btn primary sm">
          <Check style={{ width: 12, height: 12 }} /> Save Contact
        </button>
        <button onClick={onCancel} className="sd-btn outline sm">
          <X style={{ width: 12, height: 12 }} /> Cancel
        </button>
      </div>
    </div>
  )
}

// ─── LOB checkboxes ────────────────────────────────────────────────────────────

function LobCheckboxes({ selected, onChange }: { selected: PolicyLineOfBusiness[]; onChange: (v: PolicyLineOfBusiness[]) => void }) {
  const toggle = (lob: PolicyLineOfBusiness) =>
    onChange(selected.includes(lob) ? selected.filter((l) => l !== lob) : [...selected, lob])
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 6 }}>
      {ACTIVE_LOBS.map((lob) => (
        <label key={lob} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, cursor: 'pointer', color: 'var(--ink-2)' }}>
          <input type="checkbox" checked={selected.includes(lob)} onChange={() => toggle(lob)} />
          {LOB_LABELS[lob]}
        </label>
      ))}
    </div>
  )
}

function bordereauxProfileLabel(profile: BordereauxProfile) {
  const scope = [
    profile.programName,
    profile.lineOfBusiness ? LOB_LABELS[profile.lineOfBusiness as PolicyLineOfBusiness] ?? profile.lineOfBusiness : null,
    profile.stateCode,
  ].filter(Boolean).join(' / ')
  return scope ? `${profile.name} (${scope})` : profile.name
}

// ─── Main page ─────────────────────────────────────────────────────────────────

type InfoFormData = {
  name: string
  naic: string
  amBestRating: string
  addressLine1: string
  addressLine2: string
  city: string
  state: string
  zipCode: string
  website: string
  defaultCurrencyCode: string
  isActive: boolean
  linesOfBusiness: PolicyLineOfBusiness[]
}

type AdditionalInterestRateForm = {
  lineOfBusiness: PolicyLineOfBusiness | ''
  coverageType: AdditionalInterestCoverageType
  chargeMethod: AdditionalInterestChargeMethod
  perInterestAmount: string
  blanketAmount: string
  minimumCharge: string
  maximumCharge: string
  state: string
  effectiveDate: string
  expirationDate: string
  isActive: boolean
}

type BordereauxProfileForm = {
  name: string
  programConfigurationId: string
  lineOfBusiness: PolicyLineOfBusiness | ''
  stateCode: string
  requiresAccountCurrent: boolean
}

const emptyAdditionalInterestRateForm = (): AdditionalInterestRateForm => ({
  lineOfBusiness: '',
  coverageType: 'AdditionalInsured',
  chargeMethod: 'PerInterest',
  perInterestAmount: '',
  blanketAmount: '',
  minimumCharge: '',
  maximumCharge: '',
  state: '',
  effectiveDate: '',
  expirationDate: '',
  isActive: true,
})

const emptyBordereauxProfileForm = (): BordereauxProfileForm => ({
  name: '',
  programConfigurationId: '',
  lineOfBusiness: '',
  stateCode: '',
  requiresAccountCurrent: true,
})

export function CarrierDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { canUploadAttachments, canDeleteAttachments } = usePermissions()

  const [editingInfo, setEditingInfo] = useState(false)
  const [infoForm, setInfoForm] = useState<InfoFormData>({
    name: '', naic: '', amBestRating: '', addressLine1: '', addressLine2: '',
    city: '', state: '', zipCode: '', website: '', defaultCurrencyCode: 'USD', isActive: true, linesOfBusiness: [],
  })

  const [showNewContact, setShowNewContact] = useState(false)
  const [newContactForm, setNewContactForm] = useState<ContactFormData>(emptyContactForm())
  const [editingContactId, setEditingContactId] = useState<string | null>(null)
  const [editContactForm, setEditContactForm] = useState<ContactFormData>(emptyContactForm())

  const [showAddCommission, setShowAddCommission] = useState(false)
  const [commissionForm, setCommissionForm] = useState({ programConfigurationId: '', lineOfBusiness: '' as string, commissionRate: '', smmRetentionRate: '', effectiveDate: new Date().toISOString().slice(0, 10) })
  const [expandedLobs, setExpandedLobs] = useState<Set<string>>(new Set())
  const [showAdditionalInterestRateForm, setShowAdditionalInterestRateForm] = useState(false)
  const [editingAdditionalInterestRateId, setEditingAdditionalInterestRateId] = useState<string | null>(null)
  const [additionalInterestRateForm, setAdditionalInterestRateForm] = useState<AdditionalInterestRateForm>(emptyAdditionalInterestRateForm())

  const [showRatingModal, setShowRatingModal] = useState(false)
  const [editingAssignmentId, setEditingAssignmentId] = useState<string | null>(null)
  const [ratingForm, setRatingForm] = useState<{ programConfigurationId: string; lineOfBusiness: PolicyLineOfBusiness | ''; ratingPlanVersionId: string }>({ programConfigurationId: '', lineOfBusiness: '', ratingPlanVersionId: '' })
  const [ratingPickerLob, setRatingPickerLob] = useState<PolicyLineOfBusiness | null>(null)
  const [selectedBordereauxProfileId, setSelectedBordereauxProfileId] = useState('')
  const [showBordereauxProfileForm, setShowBordereauxProfileForm] = useState(false)
  const [bordereauxProfileForm, setBordereauxProfileForm] = useState<BordereauxProfileForm>(emptyBordereauxProfileForm())

  const { data: carrier, isLoading } = useQuery({
    queryKey: ['carriers', id],
    queryFn: () => carriersApi.getById(id!),
    enabled: !!id,
  })

  const { data: commissions = [] } = useQuery({
    queryKey: ['carrier-commissions', id],
    queryFn: () => getCarrierCommissions(id!),
    enabled: !!id,
  })

  const { data: programs = [] } = useQuery({
    queryKey: ['admin', 'program-configurations', 'active'],
    queryFn: () => programConfigurationsApi.getAll(false),
  })

  const { data: bordereauxProfiles = [] } = useQuery({
    queryKey: ['bordereaux', 'profiles', 'carrier', id],
    queryFn: () => getBordereauxProfiles({ includeInactive: true, carrierId: id!, reportType: 'Premium' }),
    enabled: !!id,
  })

  const { data: additionalInterestRates = [] } = useQuery<CarrierAdditionalInterestRate[]>({
    queryKey: ['carrier-additional-interest-rates', id],
    queryFn: () => carrierAdditionalInterestRatesApi.getAll(id!),
    enabled: !!id,
  })

  useEffect(() => {
    if (bordereauxProfiles.length === 0) {
      if (selectedBordereauxProfileId) setSelectedBordereauxProfileId('')
      return
    }
    if (!selectedBordereauxProfileId || !bordereauxProfiles.some((profile) => profile.id === selectedBordereauxProfileId)) {
      setSelectedBordereauxProfileId(bordereauxProfiles[0].id)
    }
  }, [bordereauxProfiles, selectedBordereauxProfileId])

  const addCommissionMutation = useMutation({
    mutationFn: () => createCarrierCommission(id!, {
      programConfigurationId: commissionForm.programConfigurationId || null,
      lineOfBusiness: commissionForm.lineOfBusiness || null,
      commissionRate: parseFloat(commissionForm.commissionRate) / 100,
      smmRetentionRate: parseFloat(commissionForm.smmRetentionRate) / 100,
      effectiveDate: commissionForm.effectiveDate,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['carrier-commissions', id] })
      setShowAddCommission(false)
      setCommissionForm({ programConfigurationId: '', lineOfBusiness: '', commissionRate: '', smmRetentionRate: '', effectiveDate: new Date().toISOString().slice(0, 10) })
      toast.success('Commission rate added')
    },
    onError: (err: Error) => toast.error(err.message),
  })

  const disableCommissionMutation = useMutation({
    mutationFn: (commissionId: number) => disableCarrierCommission(id!, commissionId, { disabledDate: null }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['carrier-commissions', id] })
      toast.success('Commission rate disabled')
    },
    onError: (err: Error) => toast.error(err.message),
  })

  const toAdditionalInterestRateDto = (): CarrierAdditionalInterestRateCreate => ({
    lineOfBusiness: additionalInterestRateForm.lineOfBusiness,
    coverageType: additionalInterestRateForm.coverageType,
    chargeMethod: additionalInterestRateForm.chargeMethod,
    perInterestAmount: additionalInterestRateForm.perInterestAmount ? parseFloat(additionalInterestRateForm.perInterestAmount) : undefined,
    blanketAmount: additionalInterestRateForm.blanketAmount ? parseFloat(additionalInterestRateForm.blanketAmount) : undefined,
    minimumCharge: additionalInterestRateForm.minimumCharge ? parseFloat(additionalInterestRateForm.minimumCharge) : undefined,
    maximumCharge: additionalInterestRateForm.maximumCharge ? parseFloat(additionalInterestRateForm.maximumCharge) : undefined,
    state: additionalInterestRateForm.state || undefined,
    effectiveDate: additionalInterestRateForm.effectiveDate || undefined,
    expirationDate: additionalInterestRateForm.expirationDate || undefined,
    isActive: additionalInterestRateForm.isActive,
  })

  const saveAdditionalInterestRateMutation = useMutation({
    mutationFn: () => {
      const dto = toAdditionalInterestRateDto()
      return editingAdditionalInterestRateId
        ? carrierAdditionalInterestRatesApi.update(id!, editingAdditionalInterestRateId, dto)
        : carrierAdditionalInterestRatesApi.create(id!, dto)
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['carrier-additional-interest-rates', id] })
      setShowAdditionalInterestRateForm(false)
      setEditingAdditionalInterestRateId(null)
      setAdditionalInterestRateForm(emptyAdditionalInterestRateForm())
      toast.success('Additional interest rate saved')
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to save additional interest rate'),
  })

  const deleteAdditionalInterestRateMutation = useMutation({
    mutationFn: (rateId: string) => carrierAdditionalInterestRatesApi.delete(id!, rateId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['carrier-additional-interest-rates', id] })
      toast.success('Additional interest rate removed')
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to remove additional interest rate'),
  })

  const updateBordereauxProfileMutation = useMutation({
    mutationFn: (profile: BordereauxProfile) => updateBordereauxProfile(profile.id, bordereauxProfileToRequest(profile)),
    onSuccess: (updated) => {
      toast.success('BDX profile setup saved')
      qc.setQueryData(['bordereaux', 'profiles', 'carrier', id], (current: BordereauxProfile[] | undefined) =>
        current?.map((profile) => (profile.id === updated.id ? updated : profile)) ?? [updated])
      qc.invalidateQueries({ queryKey: ['bordereaux', 'profiles'] })
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Could not save BDX profile setup'),
  })

  const createBordereauxProfileMutation = useMutation({
    mutationFn: () => createBordereauxProfile({
      name: bordereauxProfileForm.name.trim(),
      programConfigurationId: bordereauxProfileForm.programConfigurationId,
      carrierId: id!,
      lineOfBusiness: bordereauxProfileForm.lineOfBusiness || null,
      stateCode: bordereauxProfileForm.stateCode.trim().toUpperCase() || null,
      reportType: 'Premium',
      frequency: 'Monthly',
      outputFormat: 'Xlsx',
      dateBasis: 'EffectiveOrBoundDateGreater',
      requiresAccountCurrent: bordereauxProfileForm.requiresAccountCurrent,
      isActive: true,
      requiredTabsJson: '[]',
      requiredColumnsJson: '[]',
      mappingRulesJson: '{}',
      staticValuesJson: '{}',
      validationRulesJson: '{}',
      includedTransactionTypesJson: '[]',
      notes: null,
    }),
    onSuccess: (created) => {
      toast.success('BDX profile created')
      qc.setQueryData(['bordereaux', 'profiles', 'carrier', id], (current: BordereauxProfile[] | undefined) =>
        current ? [...current, created] : [created])
      qc.invalidateQueries({ queryKey: ['bordereaux', 'profiles'] })
      setSelectedBordereauxProfileId(created.id)
      setShowBordereauxProfileForm(false)
      setBordereauxProfileForm(emptyBordereauxProfileForm())
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Could not create BDX profile'),
  })

  const { data: ratingAssignments = [] } = useQuery({
    queryKey: ['carrier-rating-assignments', id],
    queryFn: () => ratingApi.getAssignments(id!),
    enabled: !!id,
  })

  const { data: ratingVersionPicker = [] } = useQuery<RatingPlanVersionPicker[]>({
    queryKey: ['rating-plan-versions', ratingPickerLob],
    queryFn: () => ratingApi.getVersionsForLob(ratingPickerLob!),
    enabled: !!ratingPickerLob,
  })

  const createAssignmentMutation = useMutation({
    mutationFn: () => ratingApi.createAssignment({
      programConfigurationId: ratingForm.programConfigurationId || null,
      carrierId: id!,
      lineOfBusiness: ratingForm.lineOfBusiness as PolicyLineOfBusiness,
      ratingPlanVersionId: ratingForm.ratingPlanVersionId,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['carrier-rating-assignments', id] })
      setShowRatingModal(false)
      setRatingForm({ programConfigurationId: '', lineOfBusiness: '', ratingPlanVersionId: '' })
      setRatingPickerLob(null)
      toast.success('Rating plan assigned')
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to assign rating plan'),
  })

  const updateAssignmentMutation = useMutation({
    mutationFn: () => ratingApi.updateAssignment(editingAssignmentId!, { ratingPlanVersionId: ratingForm.ratingPlanVersionId }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['carrier-rating-assignments', id] })
      setShowRatingModal(false)
      setEditingAssignmentId(null)
      setRatingForm({ programConfigurationId: '', lineOfBusiness: '', ratingPlanVersionId: '' })
      setRatingPickerLob(null)
      toast.success('Rating plan updated')
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to update rating plan'),
  })

  const deleteAssignmentMutation = useMutation({
    mutationFn: (assignmentId: string) => ratingApi.deleteAssignment(assignmentId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['carrier-rating-assignments', id] })
      toast.success('Rating plan assignment removed')
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to remove assignment'),
  })

  const openAddRatingModal = () => {
    setEditingAssignmentId(null)
    setRatingForm({ programConfigurationId: '', lineOfBusiness: '', ratingPlanVersionId: '' })
    setRatingPickerLob(null)
    setShowRatingModal(true)
  }

  const openEditRatingModal = (assignment: CarrierRatingAssignment) => {
    setEditingAssignmentId(assignment.id)
    setRatingForm({
      programConfigurationId: assignment.programConfigurationId ?? '',
      lineOfBusiness: assignment.lineOfBusiness,
      ratingPlanVersionId: assignment.ratingPlanVersionId,
    })
    setRatingPickerLob(assignment.lineOfBusiness)
    setShowRatingModal(true)
  }

  const closeRatingModal = () => {
    setShowRatingModal(false)
    setEditingAssignmentId(null)
    setRatingForm({ programConfigurationId: '', lineOfBusiness: '', ratingPlanVersionId: '' })
    setRatingPickerLob(null)
  }

  const saveRatingAssignment = () => {
    if (!ratingForm.lineOfBusiness) { toast.error('Select a line of business'); return }
    if (!ratingForm.ratingPlanVersionId) { toast.error('Select a rating plan version'); return }
    if (editingAssignmentId) {
      updateAssignmentMutation.mutate()
    } else {
      createAssignmentMutation.mutate()
    }
  }

  const updateInfoMutation = useMutation({
    mutationFn: () => {
      const data: CarrierUpdate = {
        name: infoForm.name.trim(),
        naic: infoForm.naic || undefined,
        amBestRating: infoForm.amBestRating || undefined,
        addressLine1: infoForm.addressLine1 || undefined,
        addressLine2: infoForm.addressLine2 || undefined,
        city: infoForm.city || undefined,
        state: infoForm.state || undefined,
        zipCode: infoForm.zipCode || undefined,
        website: infoForm.website || undefined,
        defaultCurrencyCode: infoForm.defaultCurrencyCode || 'USD',
        isActive: infoForm.isActive,
        linesOfBusiness: infoForm.linesOfBusiness,
      }
      return carriersApi.update(id!, data)
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['carriers', id] })
      qc.invalidateQueries({ queryKey: ['carriers'] })
      setEditingInfo(false)
      toast.success('Carrier updated')
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to update carrier'),
  })

  const startEditInfo = () => {
    if (!carrier) return
    setInfoForm({
      name: carrier.name,
      naic: carrier.naic ?? '',
      amBestRating: carrier.amBestRating ?? '',
      addressLine1: carrier.addressLine1 ?? '',
      addressLine2: carrier.addressLine2 ?? '',
      city: carrier.city ?? '',
      state: carrier.state ?? '',
      zipCode: carrier.zipCode ?? '',
      website: carrier.website ?? '',
      defaultCurrencyCode: carrier.defaultCurrencyCode ?? 'USD',
      isActive: carrier.isActive,
      linesOfBusiness: [...carrier.linesOfBusiness],
    })
    setEditingInfo(true)
  }

  const addContactMutation = useMutation({
    mutationFn: (data: CarrierContactInput) => carriersApi.addContact(id!, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['carriers', id] })
      qc.invalidateQueries({ queryKey: ['carriers'] })
      setShowNewContact(false)
      setNewContactForm(emptyContactForm())
      toast.success('Contact added')
    },
    onError: () => toast.error('Failed to add contact'),
  })

  const updateContactMutation = useMutation({
    mutationFn: ({ contactId, data }: { contactId: string; data: CarrierContactInput }) =>
      carriersApi.updateContact(id!, contactId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['carriers', id] })
      setEditingContactId(null)
      toast.success('Contact updated')
    },
    onError: () => toast.error('Failed to update contact'),
  })

  const deleteContactMutation = useMutation({
    mutationFn: (contactId: string) => carriersApi.deleteContact(id!, contactId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['carriers', id] })
      qc.invalidateQueries({ queryKey: ['carriers'] })
      toast.success('Contact deleted')
    },
    onError: () => toast.error('Failed to delete contact'),
  })

  const formToContactInput = (f: ContactFormData): CarrierContactInput => ({
    firstName: f.firstName.trim(),
    lastName: f.lastName || undefined,
    title: f.title || undefined,
    email: f.email || undefined,
    phone: f.phone || undefined,
    isPrimary: f.isPrimary,
  })

  const infoSet = (k: keyof InfoFormData) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setInfoForm({ ...infoForm, [k]: e.target.type === 'checkbox' ? (e.target as HTMLInputElement).checked : e.target.value })

  const zipError = infoForm.zipCode && !isValidZip(infoForm.zipCode)

  if (isLoading) return <LoadingSpinner />
  if (!carrier) return <EmptyState icon={ShieldCheck} title="Carrier not found" description="The requested carrier record could not be loaded." />

  const address = [carrier.addressLine1, carrier.addressLine2, [carrier.city, carrier.state].filter(Boolean).join(', '), carrier.zipCode].filter(Boolean).join(' ')
  const ratingProgramOptions = programs.filter((program) =>
    program.id === ratingForm.programConfigurationId ||
    program.carriers.some((programCarrier) => programCarrier.carrierId === carrier.id && programCarrier.isActive)
  )
  const selectedRatingProgram = programs.find((program) => program.id === ratingForm.programConfigurationId)
  const selectedRatingProgramCarrier = selectedRatingProgram?.carriers.find((programCarrier) => programCarrier.carrierId === carrier.id && programCarrier.isActive)
  const ratingLobOptions = ratingForm.programConfigurationId
    ? Array.from(new Set((selectedRatingProgramCarrier?.linesOfBusiness ?? [])
        .filter((lob) => lob.isActive && carrier.linesOfBusiness.includes(lob.lineOfBusiness))
        .map((lob) => lob.lineOfBusiness)))
    : carrier.linesOfBusiness
  const commissionProgramOptions = programs.filter((program) =>
    program.id === commissionForm.programConfigurationId ||
    program.carriers.some((programCarrier) => programCarrier.carrierId === carrier.id && programCarrier.isActive)
  )
  const selectedCommissionProgram = programs.find((program) => program.id === commissionForm.programConfigurationId)
  const selectedCommissionProgramCarrier = selectedCommissionProgram?.carriers.find((programCarrier) => programCarrier.carrierId === carrier.id && programCarrier.isActive)
  const commissionLobOptions = commissionForm.programConfigurationId
    ? Array.from(new Set((selectedCommissionProgramCarrier?.linesOfBusiness ?? [])
        .filter((lob) => lob.isActive && carrier.linesOfBusiness.includes(lob.lineOfBusiness))
        .map((lob) => lob.lineOfBusiness)))
    : carrier.linesOfBusiness
  const bordereauxProgramOptions = programs.filter((program) =>
    program.carriers.some((programCarrier) => programCarrier.carrierId === carrier.id && programCarrier.isActive)
  )
  const selectedBordereauxProgram = programs.find((program) => program.id === bordereauxProfileForm.programConfigurationId)
  const selectedBordereauxProgramCarrier = selectedBordereauxProgram?.carriers.find((programCarrier) =>
    programCarrier.carrierId === carrier.id && programCarrier.isActive
  )
  const createBordereauxLobOptions = bordereauxProfileForm.programConfigurationId
    ? Array.from(new Set((selectedBordereauxProgramCarrier?.linesOfBusiness ?? [])
        .filter((lob) => lob.isActive && carrier.linesOfBusiness.includes(lob.lineOfBusiness))
        .map((lob) => lob.lineOfBusiness)))
    : carrier.linesOfBusiness
  const bordereauxLobOptions = Array.from(new Set([
    ...carrier.linesOfBusiness,
    ...bordereauxProfiles.map((profile) => profile.lineOfBusiness).filter((lob): lob is PolicyLineOfBusiness => Boolean(lob)),
  ])).map((lob) => ({ value: lob, label: LOB_LABELS[lob] ?? lob }))
  const selectedBordereauxProfile = bordereauxProfiles.find((profile) => profile.id === selectedBordereauxProfileId)

  const openBordereauxProfileForm = () => {
    const programId = bordereauxProgramOptions[0]?.id ?? ''
    const programCarrier = bordereauxProgramOptions[0]?.carriers.find((programCarrier) =>
      programCarrier.carrierId === carrier.id && programCarrier.isActive
    )
    const firstLob = programCarrier?.linesOfBusiness.find((lob) =>
      lob.isActive && carrier.linesOfBusiness.includes(lob.lineOfBusiness)
    )?.lineOfBusiness ?? carrier.linesOfBusiness[0] ?? ''

    setBordereauxProfileForm({
      ...emptyBordereauxProfileForm(),
      name: `${carrier.name} Premium BDX`,
      programConfigurationId: programId,
      lineOfBusiness: firstLob,
    })
    setShowBordereauxProfileForm(true)
  }

  // ─── form panel style ────────────────────────────────────────────────────────
  const formPanelStyle: React.CSSProperties = {
    border: '1px solid var(--line)',
    background: 'var(--surface-2)',
    borderRadius: 'var(--r)',
    padding: 14,
    display: 'flex',
    flexDirection: 'column',
    gap: 12,
  }

  return (
    <div className="subs-wrap">
      {/* Breadcrumb */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8, fontSize: 13, color: 'var(--ink-3)' }}>
        <Link to="/carriers" className="sd-btn ghost sm">
          <ArrowLeft style={{ width: 13, height: 13 }} /> Carriers
        </Link>
        <span>/</span>
        <span style={{ color: 'var(--ink)', fontWeight: 600 }}>{carrier.name}</span>
      </div>

      {/* Carrier info panel */}
      <div className="sd-card">
        <div className="sd-card-head" style={{ justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <h3 style={{ fontWeight: 600, fontSize: 16, color: 'var(--ink)' }}>{carrier.name}</h3>
            <span className={`sd-pill ${carrier.isActive ? 'good' : 'withdrawn'}`}>
              {carrier.isActive ? 'Active' : 'Inactive'}
            </span>
          </div>
          {!editingInfo && (
            <button onClick={startEditInfo} className="sd-btn outline sm">
              <Pencil style={{ width: 12, height: 12 }} /> Edit
            </button>
          )}
        </div>

        <div className="sd-card-body">
          {editingInfo ? (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              <div className="sims-fields" style={{ gridTemplateColumns: 'repeat(3, 1fr)' }}>
                <div>
                  <label className="sims-field-label">Carrier Name *</label>
                  <input value={infoForm.name} onChange={infoSet('name')} className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">NAIC #</label>
                  <input value={infoForm.naic} onChange={infoSet('naic')} placeholder="14788" className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">AM Best Rating</label>
                  <input value={infoForm.amBestRating} onChange={infoSet('amBestRating')} placeholder="A+" className="sims-input" />
                </div>
              </div>

              <div className="sims-fields" style={{ gridTemplateColumns: '1fr 1fr' }}>
                <div>
                  <label className="sims-field-label">Street Address</label>
                  <AddressAutocomplete
                    value={infoForm.addressLine1}
                    onChange={(val) => setInfoForm({ ...infoForm, addressLine1: val })}
                    onSelect={(c) => setInfoForm({ ...infoForm, addressLine1: c.addressLine1, city: c.city, state: c.state, zipCode: c.zipCode })}
                    placeholder="Start typing an address…"
                  />
                </div>
                <div>
                  <label className="sims-field-label">Suite / Unit</label>
                  <input value={infoForm.addressLine2} onChange={infoSet('addressLine2')} placeholder="Apt, Suite, Unit…" className="sims-input" />
                </div>
              </div>

              <div className="sims-fields" style={{ gridTemplateColumns: 'repeat(3, 1fr)' }}>
                <div>
                  <label className="sims-field-label">City</label>
                  <input value={infoForm.city} onChange={infoSet('city')} placeholder="City" className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">State</label>
                  <input value={infoForm.state} onChange={infoSet('state')} maxLength={2} placeholder="TX" className="sims-input" style={{ textTransform: 'uppercase' }} />
                </div>
                <div>
                  <label className="sims-field-label">ZIP</label>
                  <input value={infoForm.zipCode} onChange={infoSet('zipCode')} placeholder="78701" className="sims-input" />
                  {zipError && <p style={{ fontSize: 11.5, color: 'var(--bad-fg)', marginTop: 2 }}>Invalid ZIP code</p>}
                </div>
              </div>

              <div className="sims-fields" style={{ gridTemplateColumns: '1fr 1fr' }}>
                <div>
                  <label className="sims-field-label">Website</label>
                  <input value={infoForm.website} onChange={infoSet('website')} placeholder="https://example.com" className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">Currency</label>
                  <input value={infoForm.defaultCurrencyCode} onChange={infoSet('defaultCurrencyCode')} maxLength={3} placeholder="USD" className="sims-input" style={{ textTransform: 'uppercase' }} />
                </div>
              </div>

              <div>
                <label className="sims-field-label" style={{ marginBottom: 8 }}>Lines of Business</label>
                <LobCheckboxes
                  selected={infoForm.linesOfBusiness}
                  onChange={(lobs) => setInfoForm({ ...infoForm, linesOfBusiness: lobs })}
                />
              </div>

              <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, color: 'var(--ink-2)', cursor: 'pointer' }}>
                <input type="checkbox" id="carrier-active" checked={infoForm.isActive} onChange={infoSet('isActive')} />
                Active
              </label>

              <div style={{ display: 'flex', gap: 8 }}>
                <button
                  onClick={() => {
                    if (!infoForm.name.trim()) { toast.error('Name is required'); return }
                    if (infoForm.linesOfBusiness.length === 0) { toast.error('Select at least one line of business'); return }
                    if (zipError) { toast.error('Enter a valid ZIP code'); return }
                    updateInfoMutation.mutate()
                  }}
                  disabled={updateInfoMutation.isPending}
                  className="sd-btn primary sm"
                >
                  <Check style={{ width: 12, height: 12 }} /> Save
                </button>
                <button onClick={() => setEditingInfo(false)} className="sd-btn outline sm">
                  <X style={{ width: 12, height: 12 }} /> Cancel
                </button>
              </div>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {(carrier.naic || carrier.amBestRating) && (
                <div style={{ display: 'flex', gap: 16, fontSize: 13, color: 'var(--ink-3)' }}>
                  {carrier.naic && <span>NAIC {carrier.naic}</span>}
                  {carrier.amBestRating && <span>AM Best: {carrier.amBestRating}</span>}
                </div>
              )}
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                {carrier.linesOfBusiness.map((lob) => (
                  <span key={lob} className="sd-lob">{LOB_LABELS[lob]}</span>
                ))}
                {carrier.linesOfBusiness.length === 0 && (
                  <span style={{ fontSize: 12, color: 'var(--ink-4)' }}>No lines of business configured</span>
                )}
              </div>
              {(address || carrier.website) && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                  {address && (
                    <span style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, color: 'var(--ink-2)' }}>
                      <MapPin style={{ width: 13, height: 13, color: 'var(--ink-4)', flexShrink: 0 }} /> {address}
                    </span>
                  )}
                  {carrier.website && (
                    <a
                      href={carrier.website.startsWith('http') ? carrier.website : `https://${carrier.website}`}
                      target="_blank"
                      rel="noopener noreferrer"
                      style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, color: 'var(--accent-ink)', textDecoration: 'none' }}
                    >
                      <Globe style={{ width: 13, height: 13, flexShrink: 0 }} /> {carrier.website}
                    </a>
                  )}
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Documents */}
      <DocumentsSection entityType="Carrier" entityId={id!} canUpload={canUploadAttachments} canDelete={canDeleteAttachments} />

      {/* Bordereaux Profiles */}
      <div className="sd-card">
        <div className="sd-card-head" style={{ justifyContent: 'space-between' }}>
          <h3>
            <FileSpreadsheet style={{ width: 13, height: 13, marginRight: 6, display: 'inline', verticalAlign: 'text-bottom' }} />
            Bordereaux Profiles
            <span className="cnt">{bordereauxProfiles.length}</span>
          </h3>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            {bordereauxProfiles.length > 0 && (
              <span style={{ fontSize: 12, color: 'var(--ink-4)' }}>
                {bordereauxProfiles.filter((p) => !p.setupStatus.isReadyForExport).length} need setup
              </span>
            )}
            {!showBordereauxProfileForm && (
              <button
                type="button"
                onClick={openBordereauxProfileForm}
                disabled={bordereauxProgramOptions.length === 0}
                className="sd-btn primary sm"
              >
                <Plus style={{ width: 12, height: 12 }} /> New Profile
              </button>
            )}
          </div>
        </div>

        <div className="sd-card-body">
          {showBordereauxProfileForm && (
            <div style={{ ...formPanelStyle, marginBottom: 16 }}>
              <div className="sims-fields" style={{ gridTemplateColumns: '2fr 1fr 1fr 1fr auto' }}>
                <div>
                  <label className="sims-field-label">Profile Name *</label>
                  <input
                    value={bordereauxProfileForm.name}
                    onChange={(e) => setBordereauxProfileForm((f) => ({ ...f, name: e.target.value }))}
                    className="sims-input"
                  />
                </div>
                <div>
                  <label className="sims-field-label">Program *</label>
                  <select
                    value={bordereauxProfileForm.programConfigurationId}
                    onChange={(e) => setBordereauxProfileForm((f) => ({ ...f, programConfigurationId: e.target.value, lineOfBusiness: '' }))}
                    className="sims-select"
                  >
                    <option value="">Select...</option>
                    {bordereauxProgramOptions.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
                  </select>
                </div>
                <div>
                  <label className="sims-field-label">Line of Business</label>
                  <select
                    value={bordereauxProfileForm.lineOfBusiness}
                    onChange={(e) => setBordereauxProfileForm((f) => ({ ...f, lineOfBusiness: e.target.value as PolicyLineOfBusiness | '' }))}
                    className="sims-select"
                  >
                    <option value="">All lines</option>
                    {createBordereauxLobOptions.map((lob) => <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>)}
                  </select>
                </div>
                <div>
                  <label className="sims-field-label">State</label>
                  <input
                    value={bordereauxProfileForm.stateCode}
                    maxLength={2}
                    onChange={(e) => setBordereauxProfileForm((f) => ({ ...f, stateCode: e.target.value.toUpperCase() }))}
                    placeholder="All"
                    className="sims-input"
                    style={{ textTransform: 'uppercase' }}
                  />
                </div>
                <div style={{ display: 'flex', alignItems: 'flex-end', paddingBottom: 2 }}>
                  <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12.5, color: 'var(--ink-2)', cursor: 'pointer', whiteSpace: 'nowrap' }}>
                    <input
                      type="checkbox"
                      checked={bordereauxProfileForm.requiresAccountCurrent}
                      onChange={(e) => setBordereauxProfileForm((f) => ({ ...f, requiresAccountCurrent: e.target.checked }))}
                    />
                    Account Current
                  </label>
                </div>
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                <button
                  type="button"
                  onClick={() => {
                    if (!bordereauxProfileForm.name.trim()) { toast.error('Profile name is required'); return }
                    if (!bordereauxProfileForm.programConfigurationId) { toast.error('Select a program'); return }
                    if (bordereauxProfileForm.stateCode.trim() && bordereauxProfileForm.stateCode.trim().length !== 2) {
                      toast.error('State must be two characters')
                      return
                    }
                    createBordereauxProfileMutation.mutate()
                  }}
                  disabled={createBordereauxProfileMutation.isPending}
                  className="sd-btn primary sm"
                >
                  <Check style={{ width: 12, height: 12 }} /> Save Profile
                </button>
                <button
                  type="button"
                  onClick={() => { setShowBordereauxProfileForm(false); setBordereauxProfileForm(emptyBordereauxProfileForm()) }}
                  className="sd-btn outline sm"
                >
                  <X style={{ width: 12, height: 12 }} /> Cancel
                </button>
              </div>
            </div>
          )}

          {bordereauxProfiles.length === 0 && !showBordereauxProfileForm ? (
            <EmptyState
              icon={FileSpreadsheet}
              title="No BDX profiles for this carrier"
              description={bordereauxProgramOptions.length === 0
                ? 'Add this carrier to an active program before creating a BDX profile.'
                : 'Create the first carrier BDX profile here, choosing the program and LOB before setup.'}
              action={bordereauxProgramOptions.length > 0
                ? <button type="button" onClick={openBordereauxProfileForm} className="sd-btn outline sm">Create first profile</button>
                : undefined}
            />
          ) : bordereauxProfiles.length > 0 ? (
            <div style={{ display: 'grid', gridTemplateColumns: 'minmax(240px, 320px) 1fr', gap: 16 }}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                {bordereauxProfiles.map((profile) => {
                  const selected = profile.id === selectedBordereauxProfileId
                  const ready = profile.setupStatus.isReadyForExport
                  return (
                    <button
                      key={profile.id}
                      type="button"
                      onClick={() => setSelectedBordereauxProfileId(profile.id)}
                      style={{
                        border: `1px solid ${selected ? 'var(--accent-light)' : 'var(--line)'}`,
                        background: selected ? 'var(--accent-soft)' : 'var(--surface)',
                        borderRadius: 'var(--r)',
                        padding: '10px 12px',
                        textAlign: 'left',
                        cursor: 'pointer',
                        transition: 'border-color .15s, background .15s',
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 8 }}>
                        <div style={{ minWidth: 0 }}>
                          <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{profile.name}</div>
                          <div style={{ marginTop: 2, fontSize: 11.5, color: 'var(--ink-3)' }}>
                            {[profile.programName, profile.lineOfBusiness ? LOB_LABELS[profile.lineOfBusiness as PolicyLineOfBusiness] ?? profile.lineOfBusiness : null, profile.stateCode]
                              .filter(Boolean).join(' / ')}
                          </div>
                        </div>
                        {ready
                          ? <CheckCircle2 style={{ width: 15, height: 15, color: 'var(--good-fg)', flexShrink: 0 }} />
                          : <AlertTriangle style={{ width: 15, height: 15, color: 'var(--warn-fg)', flexShrink: 0 }} />}
                      </div>
                      <div style={{ marginTop: 6, fontSize: 11.5, fontWeight: 600, color: ready ? 'var(--good-fg)' : 'var(--warn-fg)' }}>
                        {ready ? 'Ready for export' : `${profile.setupStatus.missingItems} missing setup item${profile.setupStatus.missingItems === 1 ? '' : 's'}`}
                      </div>
                    </button>
                  )
                })}
              </div>

              <div style={{ border: '1px solid var(--line)', borderRadius: 'var(--r)', background: 'var(--surface)', padding: 16 }}>
                {selectedBordereauxProfile && (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                    <div>
                      <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)' }}>{bordereauxProfileLabel(selectedBordereauxProfile)}</div>
                      <div style={{ marginTop: 3, fontSize: 12, color: 'var(--ink-3)' }}>
                        {selectedBordereauxProfile.requiresAccountCurrent ? 'London BDX and Account Current' : 'London BDX'}
                      </div>
                    </div>
                    <BordereauxProfileSetupPanel
                      profile={selectedBordereauxProfile}
                      isSaving={updateBordereauxProfileMutation.isPending}
                      lineOfBusinessOptions={bordereauxLobOptions}
                      onSave={(profile) => updateBordereauxProfileMutation.mutate(profile)}
                    />
                  </div>
                )}
              </div>
            </div>
          ) : null}
        </div>
      </div>

      {/* Commission Schedules */}
      <div className="sd-card">
        <div className="sd-card-head" style={{ justifyContent: 'space-between' }}>
          <h3>
            <Percent style={{ width: 12, height: 12, marginRight: 6, display: 'inline', verticalAlign: 'text-bottom' }} />
            Commission Schedules
          </h3>
          {!showAddCommission && (
            <button onClick={() => setShowAddCommission(true)} className="sd-btn primary sm">
              <Plus style={{ width: 12, height: 12 }} /> Add Rate
            </button>
          )}
        </div>

        <div className="sd-card-body">
          {showAddCommission && (
            <div style={{ ...formPanelStyle, marginBottom: 16 }}>
              <p style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink-2)' }}>New Commission Rate</p>
              <div className="sims-fields" style={{ gridTemplateColumns: 'repeat(5, 1fr)' }}>
                <div>
                  <label className="sims-field-label">Program</label>
                  <select
                    value={commissionForm.programConfigurationId}
                    onChange={(e) => setCommissionForm({ ...commissionForm, programConfigurationId: e.target.value, lineOfBusiness: '' })}
                    className="sims-select"
                  >
                    <option value="">Any program</option>
                    {commissionProgramOptions.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
                  </select>
                </div>
                <div>
                  <label className="sims-field-label">Line of Business</label>
                  <select
                    value={commissionForm.lineOfBusiness}
                    onChange={(e) => setCommissionForm({ ...commissionForm, lineOfBusiness: e.target.value })}
                    className="sims-select"
                  >
                    <option value="">All Lines (default)</option>
                    {commissionLobOptions.map((lob) => <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>)}
                  </select>
                  {commissionForm.programConfigurationId && commissionLobOptions.length === 0 && (
                    <p style={{ marginTop: 3, fontSize: 11.5, color: 'var(--warn-fg)' }}>No active lines under this program.</p>
                  )}
                </div>
                <div>
                  <label className="sims-field-label">Total Commission %</label>
                  <div style={{ position: 'relative' }}>
                    <input
                      type="number" min="0" max="100" step="0.01"
                      value={commissionForm.commissionRate}
                      onChange={(e) => setCommissionForm({ ...commissionForm, commissionRate: e.target.value })}
                      placeholder="15"
                      className="sims-input"
                      style={{ paddingRight: 24 }}
                    />
                    <span style={{ position: 'absolute', right: 8, top: '50%', transform: 'translateY(-50%)', color: 'var(--ink-4)', fontSize: 13 }}>%</span>
                  </div>
                  <p style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>What carrier pays SMM total</p>
                </div>
                <div>
                  <label className="sims-field-label">SMM Retention %</label>
                  <div style={{ position: 'relative' }}>
                    <input
                      type="number" min="0" max={commissionForm.commissionRate || '100'} step="0.01"
                      value={commissionForm.smmRetentionRate}
                      onChange={(e) => setCommissionForm({ ...commissionForm, smmRetentionRate: e.target.value })}
                      placeholder="5"
                      className="sims-input"
                      style={{ paddingRight: 24 }}
                    />
                    <span style={{ position: 'absolute', right: 8, top: '50%', transform: 'translateY(-50%)', color: 'var(--ink-4)', fontSize: 13 }}>%</span>
                  </div>
                  <p style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>Portion SMM keeps</p>
                </div>
                <div>
                  <label className="sims-field-label">Effective Date</label>
                  <input
                    type="date"
                    value={commissionForm.effectiveDate}
                    onChange={(e) => setCommissionForm({ ...commissionForm, effectiveDate: e.target.value })}
                    className="sims-input"
                  />
                </div>
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                <button
                  onClick={() => {
                    const rate = parseFloat(commissionForm.commissionRate)
                    const smmRate = parseFloat(commissionForm.smmRetentionRate)
                    if (isNaN(rate) || rate < 0 || rate > 100) { toast.error('Enter a valid total commission rate between 0 and 100'); return }
                    if (isNaN(smmRate) || smmRate < 0 || smmRate > rate) { toast.error('SMM retention must be between 0 and the total commission rate'); return }
                    if (!commissionForm.effectiveDate) { toast.error('Effective date is required'); return }
                    addCommissionMutation.mutate()
                  }}
                  disabled={addCommissionMutation.isPending}
                  className="sd-btn primary sm"
                >
                  <Check style={{ width: 12, height: 12 }} /> Save
                </button>
                <button onClick={() => setShowAddCommission(false)} className="sd-btn outline sm">
                  <X style={{ width: 12, height: 12 }} /> Cancel
                </button>
              </div>
            </div>
          )}

          {commissions.length === 0 && !showAddCommission ? (
            <EmptyState
              icon={BanknoteIcon}
              title="No commission rates configured"
              action={<button onClick={() => setShowAddCommission(true)} className="sd-btn outline sm">Add the first rate</button>}
            />
          ) : (
            (() => {
              const grouped = commissions.reduce<Record<string, CarrierCommission[]>>((acc, c) => {
                const key = c.lineOfBusiness ?? '__all__'
                if (!acc[key]) acc[key] = []
                acc[key].push(c)
                return acc
              }, {})

              const sortedKeys = Object.keys(grouped).sort((a, b) => {
                if (a === '__all__') return 1
                if (b === '__all__') return -1
                return (LOB_LABELS[a as PolicyLineOfBusiness] ?? a).localeCompare(LOB_LABELS[b as PolicyLineOfBusiness] ?? b)
              })

              return (
                <div style={{ border: '1px solid var(--line)', borderRadius: 'var(--r)', overflow: 'hidden' }}>
                  {sortedKeys.map((key, i) => {
                    const rows = grouped[key]
                    const activeRow = rows.find((r) => r.isActive)
                    const lobLabel = key === '__all__' ? 'All Lines (default)' : (LOB_LABELS[key as PolicyLineOfBusiness] ?? key)
                    const isExpanded = expandedLobs.has(key)

                    return (
                      <div key={key} style={{ borderTop: i > 0 ? '1px solid var(--line)' : undefined }}>
                        <div
                          style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '10px 14px', cursor: 'pointer', background: 'var(--surface)' }}
                          onClick={() => setExpandedLobs((prev) => { const next = new Set(prev); isExpanded ? next.delete(key) : next.add(key); return next })}
                        >
                          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                            <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)' }}>{lobLabel}</span>
                            {activeRow ? (
                              <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--good-fg)', background: 'var(--good-bg)', borderRadius: 4, padding: '1px 7px' }}>
                                {(activeRow.commissionRate * 100).toFixed(2)}% total · {(activeRow.smmRetentionRate * 100).toFixed(2)}% SMM
                              </span>
                            ) : (
                              <span style={{ fontSize: 11.5, color: 'var(--ink-4)', fontStyle: 'italic' }}>no active rate</span>
                            )}
                            {activeRow && <span style={{ fontSize: 11.5, color: 'var(--ink-4)' }}>eff. {activeRow.effectiveDate}</span>}
                            {activeRow?.programName && <span style={{ fontSize: 11.5, color: 'var(--accent-ink)' }}>{activeRow.programName}</span>}
                          </div>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                            <span style={{ fontSize: 11.5, color: 'var(--ink-4)' }}>{rows.length} version{rows.length !== 1 ? 's' : ''}</span>
                            <span style={{ fontSize: 11, color: 'var(--ink-4)' }}>{isExpanded ? '▲' : '▼'}</span>
                          </div>
                        </div>

                        {isExpanded && (
                          <div style={{ borderTop: '1px solid var(--line)', background: 'var(--surface-2)' }}>
                            <table className="subs-table">
                              <thead>
                                <tr>
                                  <th className="subs-th">Total Rate</th>
                                  <th className="subs-th">Program</th>
                                  <th className="subs-th">SMM Retention</th>
                                  <th className="subs-th">Effective</th>
                                  <th className="subs-th">Disabled</th>
                                  <th className="subs-th">Status</th>
                                  <th className="subs-th" />
                                </tr>
                              </thead>
                              <tbody>
                                {rows.map((r) => (
                                  <tr key={r.id} className="subs-row">
                                    <td style={{ fontWeight: 600 }}>{(r.commissionRate * 100).toFixed(2)}%</td>
                                    <td style={{ color: 'var(--ink-2)' }}>{r.programName ?? 'Any program'}</td>
                                    <td>{(r.smmRetentionRate * 100).toFixed(2)}%</td>
                                    <td style={{ color: 'var(--ink-2)' }}>{r.effectiveDate}</td>
                                    <td style={{ color: 'var(--ink-3)' }}>{r.disabledDate ?? '—'}</td>
                                    <td>
                                      <span className={`sd-pill ${r.isActive ? 'bound' : 'withdrawn'}`}>
                                        {r.isActive ? 'Active' : 'Disabled'}
                                      </span>
                                    </td>
                                    <td style={{ textAlign: 'right' }}>
                                      {r.isActive && (
                                        <button
                                          onClick={(e) => { e.stopPropagation(); if (confirm('Disable this commission rate?')) disableCommissionMutation.mutate(r.id) }}
                                          className="sims-icon-btn"
                                          style={{ fontSize: 11.5, color: 'var(--ink-3)' }}
                                          title="Disable commission rate"
                                        >
                                          Disable
                                        </button>
                                      )}
                                    </td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                        )}
                      </div>
                    )
                  })}
                </div>
              )
            })()
          )}
        </div>
      </div>

      {/* Rating Plans */}
      <div className="sd-card">
        <div className="sd-card-head" style={{ justifyContent: 'space-between' }}>
          <h3>
            <ShieldCheck style={{ width: 13, height: 13, marginRight: 6, display: 'inline', verticalAlign: 'text-bottom' }} />
            Rating Plans
          </h3>
          <button onClick={openAddRatingModal} className="sd-btn primary sm">
            <Plus style={{ width: 12, height: 12 }} /> Assign Rating Plan
          </button>
        </div>

        <div className="sd-card-body">
          {ratingAssignments.length === 0 ? (
            <EmptyState
              icon={ShieldCheck}
              title="No rating plans assigned"
              description="Quotes for this carrier will not rate until a plan is assigned."
              action={<button onClick={openAddRatingModal} className="sd-btn outline sm">Assign the first plan</button>}
            />
          ) : (
            <div className="subs-table-card" style={{ margin: 0 }}>
              <table className="subs-table">
                <thead>
                  <tr>
                    <th className="subs-th">Line of Business</th>
                    <th className="subs-th">Program</th>
                    <th className="subs-th">Plan</th>
                    <th className="subs-th">Version</th>
                    <th className="subs-th">Effective Date</th>
                    <th className="subs-th" />
                  </tr>
                </thead>
                <tbody>
                  {ratingAssignments.map((a) => (
                    <tr key={a.id} className="subs-row">
                      <td style={{ color: 'var(--ink-2)' }}>{a.lineOfBusinessLabel}</td>
                      <td style={{ color: 'var(--ink-3)' }}>{a.programName ?? 'Any program'}</td>
                      <td style={{ fontWeight: 600 }}>{a.planName}</td>
                      <td style={{ color: 'var(--ink-2)', fontFamily: 'var(--font-mono)', fontSize: 12 }}>v{a.versionNumber}</td>
                      <td style={{ color: 'var(--ink-2)' }}>{a.effectiveDate}</td>
                      <td style={{ textAlign: 'right' }}>
                        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 4 }}>
                          <button onClick={() => openEditRatingModal(a)} className="sims-icon-btn" title="Edit assignment">
                            <Pencil style={{ width: 13, height: 13 }} />
                          </button>
                          <button
                            onClick={() => { if (confirm(`Remove the ${a.lineOfBusinessLabel} rating plan assignment?`)) deleteAssignmentMutation.mutate(a.id) }}
                            className="sims-icon-btn"
                            title="Remove assignment"
                          >
                            <Trash2 style={{ width: 13, height: 13 }} />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* Additional Interest Rates */}
      <div className="sd-card">
        <div className="sd-card-head" style={{ justifyContent: 'space-between' }}>
          <h3>
            <BanknoteIcon style={{ width: 13, height: 13, marginRight: 6, display: 'inline', verticalAlign: 'text-bottom' }} />
            Additional Interest Rates
          </h3>
          <button
            onClick={() => {
              setEditingAdditionalInterestRateId(null)
              setAdditionalInterestRateForm({ ...emptyAdditionalInterestRateForm(), lineOfBusiness: carrier.linesOfBusiness[0] ?? '' })
              setShowAdditionalInterestRateForm(true)
            }}
            className="sd-btn primary sm"
          >
            <Plus style={{ width: 12, height: 12 }} /> Add Rate
          </button>
        </div>

        <div className="sd-card-body">
          {showAdditionalInterestRateForm && (
            <div style={{ ...formPanelStyle, marginBottom: 16 }}>
              <div className="sims-fields" style={{ gridTemplateColumns: 'repeat(4, 1fr)' }}>
                <div>
                  <label className="sims-field-label">Line of Business *</label>
                  <select value={additionalInterestRateForm.lineOfBusiness} onChange={(e) => setAdditionalInterestRateForm((f) => ({ ...f, lineOfBusiness: e.target.value as PolicyLineOfBusiness }))} className="sims-select">
                    <option value="">Select...</option>
                    {carrier.linesOfBusiness.map((lob) => <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>)}
                  </select>
                </div>
                <div>
                  <label className="sims-field-label">Interest Type</label>
                  <select value={additionalInterestRateForm.coverageType} onChange={(e) => setAdditionalInterestRateForm((f) => ({ ...f, coverageType: e.target.value as AdditionalInterestCoverageType }))} className="sims-select">
                    {(Object.keys(ADDITIONAL_INTEREST_COVERAGE_LABELS) as AdditionalInterestCoverageType[]).map((k) => <option key={k} value={k}>{ADDITIONAL_INTEREST_COVERAGE_LABELS[k]}</option>)}
                  </select>
                </div>
                <div>
                  <label className="sims-field-label">Charge Method</label>
                  <select value={additionalInterestRateForm.chargeMethod} onChange={(e) => setAdditionalInterestRateForm((f) => ({ ...f, chargeMethod: e.target.value as AdditionalInterestChargeMethod }))} className="sims-select">
                    {(Object.keys(ADDITIONAL_INTEREST_CHARGE_METHOD_LABELS) as AdditionalInterestChargeMethod[]).map((k) => <option key={k} value={k}>{ADDITIONAL_INTEREST_CHARGE_METHOD_LABELS[k]}</option>)}
                  </select>
                </div>
                <div>
                  <label className="sims-field-label">State</label>
                  <input value={additionalInterestRateForm.state} maxLength={2} onChange={(e) => setAdditionalInterestRateForm((f) => ({ ...f, state: e.target.value.toUpperCase() }))} placeholder="Optional" className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">Per Interest Amount</label>
                  <input type="number" value={additionalInterestRateForm.perInterestAmount} onChange={(e) => setAdditionalInterestRateForm((f) => ({ ...f, perInterestAmount: e.target.value }))} className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">Blanket Amount</label>
                  <input type="number" value={additionalInterestRateForm.blanketAmount} onChange={(e) => setAdditionalInterestRateForm((f) => ({ ...f, blanketAmount: e.target.value }))} className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">Minimum</label>
                  <input type="number" value={additionalInterestRateForm.minimumCharge} onChange={(e) => setAdditionalInterestRateForm((f) => ({ ...f, minimumCharge: e.target.value }))} className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">Maximum</label>
                  <input type="number" value={additionalInterestRateForm.maximumCharge} onChange={(e) => setAdditionalInterestRateForm((f) => ({ ...f, maximumCharge: e.target.value }))} className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">Effective Date</label>
                  <input type="date" value={additionalInterestRateForm.effectiveDate} onChange={(e) => setAdditionalInterestRateForm((f) => ({ ...f, effectiveDate: e.target.value }))} className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">Expiration Date</label>
                  <input type="date" value={additionalInterestRateForm.expirationDate} onChange={(e) => setAdditionalInterestRateForm((f) => ({ ...f, expirationDate: e.target.value }))} className="sims-input" />
                </div>
                <div style={{ display: 'flex', alignItems: 'flex-end', paddingBottom: 2 }}>
                  <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, color: 'var(--ink-2)', cursor: 'pointer' }}>
                    <input type="checkbox" checked={additionalInterestRateForm.isActive} onChange={(e) => setAdditionalInterestRateForm((f) => ({ ...f, isActive: e.target.checked }))} />
                    Active
                  </label>
                </div>
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                <button
                  onClick={() => {
                    if (!additionalInterestRateForm.lineOfBusiness) { toast.error('Select a line of business'); return }
                    saveAdditionalInterestRateMutation.mutate()
                  }}
                  disabled={saveAdditionalInterestRateMutation.isPending}
                  className="sd-btn primary sm"
                >
                  <Check style={{ width: 12, height: 12 }} /> Save Rate
                </button>
                <button onClick={() => { setShowAdditionalInterestRateForm(false); setEditingAdditionalInterestRateId(null); setAdditionalInterestRateForm(emptyAdditionalInterestRateForm()) }} className="sd-btn outline sm">
                  <X style={{ width: 12, height: 12 }} /> Cancel
                </button>
              </div>
            </div>
          )}

          {additionalInterestRates.length === 0 && !showAdditionalInterestRateForm ? (
            <EmptyState
              icon={BanknoteIcon}
              title="No additional interest rates configured"
              description="Rating will use these rules after the premium calculation is wired in."
            />
          ) : (
            <div className="subs-table-card" style={{ margin: 0 }}>
              <table className="subs-table">
                <thead>
                  <tr>
                    <th className="subs-th">LOB</th>
                    <th className="subs-th">Type</th>
                    <th className="subs-th">Method</th>
                    <th className="subs-th">Amount</th>
                    <th className="subs-th">State</th>
                    <th className="subs-th">Effective</th>
                    <th className="subs-th">Status</th>
                    <th className="subs-th" />
                  </tr>
                </thead>
                <tbody>
                  {additionalInterestRates.map((r) => (
                    <tr key={r.id} className="subs-row">
                      <td style={{ color: 'var(--ink-2)' }}>{LOB_LABELS[r.lineOfBusiness as PolicyLineOfBusiness] ?? r.lineOfBusiness}</td>
                      <td style={{ color: 'var(--ink-2)' }}>{ADDITIONAL_INTEREST_COVERAGE_LABELS[r.coverageType]}</td>
                      <td style={{ color: 'var(--ink-3)' }}>{ADDITIONAL_INTEREST_CHARGE_METHOD_LABELS[r.chargeMethod]}</td>
                      <td style={{ fontFamily: 'var(--font-mono)', fontSize: 12 }}>
                        {r.chargeMethod === 'PerInterest' ? formatCurrency(r.perInterestAmount ?? 0) : r.chargeMethod === 'BlanketFlat' ? formatCurrency(r.blanketAmount ?? 0) : '—'}
                      </td>
                      <td style={{ color: 'var(--ink-3)' }}>{r.state ?? '—'}</td>
                      <td style={{ color: 'var(--ink-3)' }}>{r.effectiveDate ?? '—'}</td>
                      <td>
                        <span className={`sd-pill ${r.isActive ? 'bound' : 'withdrawn'}`}>
                          {r.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 4 }}>
                          <button
                            onClick={() => {
                              setEditingAdditionalInterestRateId(r.id)
                              setAdditionalInterestRateForm({
                                lineOfBusiness: r.lineOfBusiness as PolicyLineOfBusiness,
                                coverageType: r.coverageType,
                                chargeMethod: r.chargeMethod,
                                perInterestAmount: r.perInterestAmount?.toString() ?? '',
                                blanketAmount: r.blanketAmount?.toString() ?? '',
                                minimumCharge: r.minimumCharge?.toString() ?? '',
                                maximumCharge: r.maximumCharge?.toString() ?? '',
                                state: r.state ?? '',
                                effectiveDate: r.effectiveDate ?? '',
                                expirationDate: r.expirationDate ?? '',
                                isActive: r.isActive,
                              })
                              setShowAdditionalInterestRateForm(true)
                            }}
                            className="sims-icon-btn"
                            title="Edit rate"
                          >
                            <Pencil style={{ width: 13, height: 13 }} />
                          </button>
                          <button
                            onClick={() => { if (confirm('Remove this additional interest rate?')) deleteAdditionalInterestRateMutation.mutate(r.id) }}
                            className="sims-icon-btn"
                            title="Remove rate"
                          >
                            <Trash2 style={{ width: 13, height: 13 }} />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* Rating plan assign/edit modal */}
      {showRatingModal && (
        <div className="sims-modal-backdrop" onClick={closeRatingModal}>
          <div className="sims-modal" style={{ maxWidth: 440 }} onClick={(e) => e.stopPropagation()}>
            <div className="sims-modal-head">
              <span>{editingAssignmentId ? 'Edit Rating Plan' : 'Assign Rating Plan'}</span>
              <button className="sims-icon-btn" onClick={closeRatingModal}><X style={{ width: 14, height: 14 }} /></button>
            </div>
            <div className="sims-modal-body">
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                <div>
                  <label className="sims-field-label">Program</label>
                  <select
                    value={ratingForm.programConfigurationId}
                    onChange={(e) => {
                      setRatingForm({ ...ratingForm, programConfigurationId: e.target.value, lineOfBusiness: '', ratingPlanVersionId: '' })
                      setRatingPickerLob(null)
                    }}
                    disabled={!!editingAssignmentId}
                    className="sims-select"
                  >
                    <option value="">Any program</option>
                    {ratingProgramOptions.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
                  </select>
                </div>
                <div>
                  <label className="sims-field-label">Line of Business</label>
                  <select
                    value={ratingForm.lineOfBusiness}
                    onChange={(e) => {
                      const lob = e.target.value as PolicyLineOfBusiness
                      setRatingForm({ ...ratingForm, lineOfBusiness: lob, ratingPlanVersionId: '' })
                      setRatingPickerLob(lob || null)
                    }}
                    disabled={!!editingAssignmentId}
                    className="sims-select"
                  >
                    <option value="">Select a line of business…</option>
                    {ratingLobOptions.map((lob) => <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>)}
                  </select>
                  {ratingForm.programConfigurationId && ratingLobOptions.length === 0 && (
                    <p style={{ marginTop: 3, fontSize: 11.5, color: 'var(--warn-fg)' }}>This carrier has no active lines configured under the selected program.</p>
                  )}
                </div>
                <div>
                  <label className="sims-field-label">Rating Plan Version</label>
                  <select
                    value={ratingForm.ratingPlanVersionId}
                    onChange={(e) => setRatingForm({ ...ratingForm, ratingPlanVersionId: e.target.value })}
                    disabled={!ratingPickerLob}
                    className="sims-select"
                  >
                    <option value="">
                      {ratingPickerLob
                        ? ratingVersionPicker.length === 0 ? 'No active versions available' : 'Select a version…'
                        : 'Select a line of business first'}
                    </option>
                    {ratingVersionPicker.map((v) => (
                      <option key={v.id} value={v.id}>
                        {v.planName} — v{v.versionNumber} (eff. {v.effectiveDate})
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            </div>
            <div className="sims-modal-foot">
              <button onClick={closeRatingModal} className="sd-btn outline sm">
                <X style={{ width: 12, height: 12 }} /> Cancel
              </button>
              <button
                onClick={saveRatingAssignment}
                disabled={createAssignmentMutation.isPending || updateAssignmentMutation.isPending}
                className="sd-btn primary sm"
              >
                <Check style={{ width: 12, height: 12 }} /> Save
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Contacts */}
      <div className="sd-card">
        <div className="sd-card-head" style={{ justifyContent: 'space-between' }}>
          <h3>
            Contacts
            <span className="cnt">{carrier.contacts.length}</span>
          </h3>
          {!showNewContact && (
            <button
              onClick={() => { setShowNewContact(true); setNewContactForm(emptyContactForm()); setEditingContactId(null) }}
              className="sd-btn primary sm"
            >
              <Plus style={{ width: 12, height: 12 }} /> Add Contact
            </button>
          )}
        </div>

        <div className="sd-card-body">
          {showNewContact && (
            <div style={{ ...formPanelStyle, marginBottom: 12 }}>
              <ContactForm
                form={newContactForm}
                setForm={setNewContactForm}
                onSave={() => addContactMutation.mutate(formToContactInput(newContactForm))}
                onCancel={() => { setShowNewContact(false); setNewContactForm(emptyContactForm()) }}
                isPending={addContactMutation.isPending}
              />
            </div>
          )}

          {carrier.contacts.length === 0 && !showNewContact ? (
            <EmptyState
              icon={UserCircle}
              title="No contacts yet"
              action={<button onClick={() => setShowNewContact(true)} className="sd-btn outline sm">Add the first contact</button>}
            />
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              {carrier.contacts.map((contact) => {
                const fullName = [contact.firstName, contact.lastName].filter(Boolean).join(' ')
                const isEditingThis = editingContactId === contact.id

                return (
                  <div key={contact.id}>
                    {isEditingThis ? (
                      <div style={formPanelStyle}>
                        <ContactForm
                          form={editContactForm}
                          setForm={setEditContactForm}
                          onSave={() => updateContactMutation.mutate({ contactId: contact.id, data: formToContactInput(editContactForm) })}
                          onCancel={() => setEditingContactId(null)}
                          isPending={updateContactMutation.isPending}
                        />
                      </div>
                    ) : (
                      <div className="subs-row" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '8px 4px' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                          <UserCircle style={{ width: 30, height: 30, color: 'var(--line)', flexShrink: 0 }} />
                          <div>
                            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                              <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)' }}>{fullName}</span>
                              {contact.isPrimary && (
                                <span style={{ display: 'flex', alignItems: 'center', gap: 3, fontSize: 11.5, color: 'var(--warn-fg)' }}>
                                  <Star style={{ width: 11, height: 11 }} /> Primary
                                </span>
                              )}
                              {contact.title && (
                                <span style={{ fontSize: 12, color: 'var(--ink-4)' }}>· {contact.title}</span>
                              )}
                            </div>
                            <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginTop: 2 }}>
                              {contact.email && (
                                <a
                                  href={`mailto:${contact.email}`}
                                  onClick={(e) => e.stopPropagation()}
                                  style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 12, color: 'var(--accent-ink)', textDecoration: 'none' }}
                                >
                                  <Mail style={{ width: 11, height: 11 }} /> {contact.email}
                                </a>
                              )}
                              {contact.phone && (
                                <span style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 12, color: 'var(--ink-3)' }}>
                                  <Phone style={{ width: 11, height: 11 }} /> {contact.phone}
                                </span>
                              )}
                            </div>
                          </div>
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                          <button
                            onClick={() => { setEditingContactId(contact.id); setEditContactForm(contactToForm(contact)); setShowNewContact(false) }}
                            className="sims-icon-btn"
                            title="Edit contact"
                          >
                            <Pencil style={{ width: 13, height: 13 }} />
                          </button>
                          <button
                            onClick={() => { if (confirm(`Delete contact ${fullName}?`)) deleteContactMutation.mutate(contact.id) }}
                            className="sims-icon-btn"
                            title="Delete contact"
                          >
                            <Trash2 style={{ width: 13, height: 13 }} />
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
