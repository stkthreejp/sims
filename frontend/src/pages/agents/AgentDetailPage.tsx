import { useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft, Plus, Pencil, Trash2, Check, X, MapPin, Phone, Mail,
  Star, Building2, ChevronDown, ChevronUp, UserCircle, Percent, BanknoteIcon,
} from 'lucide-react'
import { toast } from 'sonner'
import { agentsApi } from '@/api/agents.api'
import { programConfigurationsApi } from '@/api/programConfigurations.api'
import type { AgentLocation, AgentContact, AgentLocationInput, AgentContactInput } from '@/types/agent.types'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { EmptyState } from '@/components/common/EmptyState'
import { AddressAutocomplete } from '@/components/common/AddressAutocomplete'
import { isValidEmail, isValidPhone, isValidZip, formatPhoneInput } from '@/lib/validators'
import { DocumentsSection } from '@/components/documents/DocumentsSection'
import { usePermissions } from '@/hooks/usePermissions'
import { getAgentCommissions, createAgentCommission, disableAgentCommission } from '@/api/agentCommissions.api'
import type { AgentCommission } from '@/types/agentCommission.types'

// ─── Commission constants ──────────────────────────────────────────────────────

const LOB_OPTIONS = [
  { value: '', label: 'All Lines (default fallback)' },
  { value: 'GeneralLiability', label: 'General Liability' },
  { value: 'Property', label: 'Property' },
  { value: 'CommercialAuto', label: 'Commercial Auto' },
  { value: 'BusinessOwners', label: 'Business Owners' },
  { value: 'WorkersCompensation', label: 'Workers Compensation' },
  { value: 'ProfessionalLiability', label: 'Professional Liability' },
  { value: 'Umbrella', label: 'Umbrella' },
  { value: 'Cyber', label: 'Cyber' },
  { value: 'ExcessLiability', label: 'Excess Liability' },
  { value: 'Other', label: 'Other' },
]

// ─── Location form state ───────────────────────────────────────────────────────

type LocationFormData = {
  name: string
  addressLine1: string
  addressLine2: string
  city: string
  state: string
  zipCode: string
  phone: string
  isPrimary: boolean
}

const emptyLocationForm = (): LocationFormData => ({
  name: '', addressLine1: '', addressLine2: '', city: '', state: '', zipCode: '', phone: '', isPrimary: false,
})

function locationToForm(loc: AgentLocation): LocationFormData {
  return {
    name: loc.name ?? '',
    addressLine1: loc.addressLine1 ?? '',
    addressLine2: loc.addressLine2 ?? '',
    city: loc.city ?? '',
    state: loc.state ?? '',
    zipCode: loc.zipCode ?? '',
    phone: loc.phone ?? '',
    isPrimary: loc.isPrimary,
  }
}

// ─── Contact form state ────────────────────────────────────────────────────────

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

function contactToForm(c: AgentContact): ContactFormData {
  return {
    firstName: c.firstName,
    lastName: c.lastName ?? '',
    title: c.title ?? '',
    email: c.email ?? '',
    phone: c.phone ?? '',
    isPrimary: c.isPrimary,
  }
}

// ─── Location form component ───────────────────────────────────────────────────

function LocationForm({
  form,
  setForm,
  onSave,
  onCancel,
  isPending,
}: {
  form: LocationFormData
  setForm: (f: LocationFormData) => void
  onSave: () => void
  onCancel: () => void
  isPending: boolean
}) {
  const set = (k: keyof LocationFormData) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm({ ...form, [k]: e.target.type === 'checkbox' ? (e.target as HTMLInputElement).checked : e.target.value })

  const phoneError = form.phone && !isValidPhone(form.phone)
  const zipError = form.zipCode && !isValidZip(form.zipCode)

  return (
    <div className="space-y-3 pt-2">
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="sims-field-label">Office Label</label>
          <input
            value={form.name}
            onChange={set('name')}
            placeholder="e.g. Main Office, Downtown Branch"
            className="sims-input"
          />
        </div>
        <div>
          <label className="sims-field-label">Office Phone</label>
          <input
            value={form.phone}
            onChange={(e) => setForm({ ...form, phone: formatPhoneInput(e.target.value) })}
            type="text"
            placeholder="(555) 123-4567"
            className="sims-input"
            aria-invalid={!!phoneError}
          />
          {phoneError && <p className="text-xs text-red-600 mt-0.5">Enter a valid 10-digit number</p>}
        </div>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="sims-field-label">Street Address</label>
          <AddressAutocomplete
            value={form.addressLine1}
            onChange={(val) => setForm({ ...form, addressLine1: val })}
            onSelect={(c) => setForm({ ...form, addressLine1: c.addressLine1, city: c.city, state: c.state, zipCode: c.zipCode })}
            placeholder="Start typing an address…"
            className="px-2 py-1.5 text-sm"
          />
        </div>
        <div>
          <label className="sims-field-label">Suite / Unit</label>
          <input
            value={form.addressLine2}
            onChange={set('addressLine2')}
            placeholder="Apt, Suite, Unit…"
            className="sims-input"
          />
        </div>
      </div>

      <div className="grid grid-cols-3 gap-3">
        <div>
          <label className="sims-field-label">City</label>
          <input value={form.city} onChange={set('city')} placeholder="City" className="sims-input" />
        </div>
        <div>
          <label className="sims-field-label">State</label>
          <input value={form.state} onChange={set('state')} maxLength={2} placeholder="TX" className="sims-input uppercase" />
        </div>
        <div>
          <label className="sims-field-label">ZIP</label>
          <input
            value={form.zipCode}
            onChange={set('zipCode')}
            placeholder="78701"
            className="sims-input"
            aria-invalid={!!zipError}
          />
          {zipError && <p className="text-xs text-red-600 mt-0.5">Invalid ZIP code</p>}
        </div>
      </div>

      <div className="flex items-center gap-2">
        <input type="checkbox" id="loc-primary" checked={form.isPrimary} onChange={set('isPrimary')} className="rounded" />
        <label htmlFor="loc-primary" className="text-sm text-slate-600">Mark as primary office</label>
      </div>

      <div className="flex gap-2">
        <button
          onClick={onSave}
          disabled={isPending}
          className="sd-btn primary sm"
        >
          <Check className="h-3.5 w-3.5" /> Save Office
        </button>
        <button
          onClick={onCancel}
          className="sd-btn outline sm"
        >
          <X className="h-3.5 w-3.5" /> Cancel
        </button>
      </div>
    </div>
  )
}

// ─── Contact form component ────────────────────────────────────────────────────

function ContactForm({
  form,
  setForm,
  onSave,
  onCancel,
  isPending,
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
    <div className="space-y-3 pt-2">
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <div>
          <label className="sims-field-label">First Name *</label>
          <input
            value={form.firstName}
            onChange={set('firstName')}
            placeholder="First name"
            className="sims-input"
          />
        </div>
        <div>
          <label className="sims-field-label">Last Name</label>
          <input value={form.lastName} onChange={set('lastName')} placeholder="Last name" className="sims-input" />
        </div>
        <div>
          <label className="sims-field-label">Title / Role</label>
          <input value={form.title} onChange={set('title')} placeholder="e.g. Producer, Account Mgr" className="sims-input" />
        </div>
        <div className="flex items-end pb-0">
          <div className="flex items-center gap-2 mb-2">
            <input type="checkbox" id="contact-primary" checked={form.isPrimary} onChange={set('isPrimary')} className="rounded" />
            <label htmlFor="contact-primary" className="text-sm text-slate-600">Primary contact</label>
          </div>
        </div>
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="sims-field-label">Email</label>
          <input
            value={form.email}
            onChange={set('email')}
            type="text"
            placeholder="email@example.com"
            className="sims-input"
            aria-invalid={!!emailError}
          />
          {emailError && <p className="text-xs text-red-600 mt-0.5">Enter a valid email address</p>}
        </div>
        <div>
          <label className="sims-field-label">Direct Phone</label>
          <input
            value={form.phone}
            onChange={(e) => setForm({ ...form, phone: formatPhoneInput(e.target.value) })}
            type="text"
            placeholder="(555) 123-4567"
            className="sims-input"
            aria-invalid={!!phoneError}
          />
          {phoneError && <p className="text-xs text-red-600 mt-0.5">Enter a valid 10-digit number</p>}
        </div>
      </div>
      <div className="flex gap-2">
        <button
          onClick={onSave}
          disabled={isPending || !form.firstName.trim()}
          className="sd-btn primary sm"
        >
          <Check className="h-3.5 w-3.5" /> Save Contact
        </button>
        <button onClick={onCancel} className="sd-btn outline sm">
          <X className="h-3.5 w-3.5" /> Cancel
        </button>
      </div>
    </div>
  )
}

// ─── Main page ─────────────────────────────────────────────────────────────────

export function AgentDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { canUploadAttachments, canDeleteAttachments } = usePermissions()

  // Agent info edit
  const [editingInfo, setEditingInfo] = useState(false)
  const [infoForm, setInfoForm] = useState({ name: '', agencyName: '', licenseNumber: '', email: '', phone: '', isActive: true })

  // Location state
  const [showNewLocation, setShowNewLocation] = useState(false)
  const [newLocForm, setNewLocForm] = useState<LocationFormData>(emptyLocationForm())
  const [editingLocationId, setEditingLocationId] = useState<string | null>(null)
  const [editLocForm, setEditLocForm] = useState<LocationFormData>(emptyLocationForm())
  const [collapsedLocations, setCollapsedLocations] = useState<Set<string>>(new Set())

  // Contact state
  const [showNewContact, setShowNewContact] = useState<string | null>(null)  // locationId
  const [newContactForm, setNewContactForm] = useState<ContactFormData>(emptyContactForm())
  const [editingContact, setEditingContact] = useState<{ locationId: string; contactId: string } | null>(null)
  const [editContactForm, setEditContactForm] = useState<ContactFormData>(emptyContactForm())

  // Commission state
  const [showAddCommission, setShowAddCommission] = useState(false)
  const [commissionForm, setCommissionForm] = useState({ programConfigurationId: '', lineOfBusiness: '', rate: '', effectiveDate: '' })
  const [expandedLobs, setExpandedLobs] = useState<Set<string>>(new Set())

  const { data: agent, isLoading } = useQuery({
    queryKey: ['agents', id],
    queryFn: () => agentsApi.getById(id!),
    enabled: !!id,
  })

  const { data: commissions = [] } = useQuery({
    queryKey: ['agent-commissions', id],
    queryFn: () => getAgentCommissions(id!),
    enabled: !!id,
  })

  const { data: programs = [] } = useQuery({
    queryKey: ['admin', 'program-configurations', 'active'],
    queryFn: () => programConfigurationsApi.getAll(false),
  })

  // ─── Info mutations ──────────────────────────────────────────────────────────

  const updateInfoMutation = useMutation({
    mutationFn: () => agentsApi.update(id!, {
      name: infoForm.name.trim(),
      agencyName: infoForm.agencyName || undefined,
      licenseNumber: infoForm.licenseNumber || undefined,
      email: infoForm.email || undefined,
      phone: infoForm.phone || undefined,
      isActive: infoForm.isActive,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['agents', id] })
      qc.invalidateQueries({ queryKey: ['agents'] })
      setEditingInfo(false)
      toast.success('Agent updated')
    },
    onError: () => toast.error('Failed to update agent'),
  })

  const startEditInfo = () => {
    if (!agent) return
    setInfoForm({
      name: agent.name,
      agencyName: agent.agencyName ?? '',
      licenseNumber: agent.licenseNumber ?? '',
      email: agent.email ?? '',
      phone: agent.phone ?? '',
      isActive: agent.isActive,
    })
    setEditingInfo(true)
  }

  // ─── Location mutations ──────────────────────────────────────────────────────

  const addLocationMutation = useMutation({
    mutationFn: (data: AgentLocationInput) => agentsApi.addLocation(id!, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['agents', id] })
      qc.invalidateQueries({ queryKey: ['agents'] })
      setShowNewLocation(false)
      setNewLocForm(emptyLocationForm())
      toast.success('Office added')
    },
    onError: () => toast.error('Failed to add office'),
  })

  const updateLocationMutation = useMutation({
    mutationFn: ({ locationId, data }: { locationId: string; data: AgentLocationInput }) =>
      agentsApi.updateLocation(id!, locationId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['agents', id] })
      qc.invalidateQueries({ queryKey: ['agents'] })
      setEditingLocationId(null)
      toast.success('Office updated')
    },
    onError: () => toast.error('Failed to update office'),
  })

  const deleteLocationMutation = useMutation({
    mutationFn: (locationId: string) => agentsApi.deleteLocation(id!, locationId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['agents', id] })
      qc.invalidateQueries({ queryKey: ['agents'] })
      toast.success('Office deleted')
    },
    onError: () => toast.error('Failed to delete office'),
  })

  // ─── Contact mutations ───────────────────────────────────────────────────────

  const addContactMutation = useMutation({
    mutationFn: ({ locationId, data }: { locationId: string; data: AgentContactInput }) =>
      agentsApi.addContact(id!, locationId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['agents', id] })
      qc.invalidateQueries({ queryKey: ['agents'] })
      setShowNewContact(null)
      setNewContactForm(emptyContactForm())
      toast.success('Contact added')
    },
    onError: () => toast.error('Failed to add contact'),
  })

  const updateContactMutation = useMutation({
    mutationFn: ({ locationId, contactId, data }: { locationId: string; contactId: string; data: AgentContactInput }) =>
      agentsApi.updateContact(id!, locationId, contactId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['agents', id] })
      setEditingContact(null)
      toast.success('Contact updated')
    },
    onError: () => toast.error('Failed to update contact'),
  })

  const deleteContactMutation = useMutation({
    mutationFn: ({ locationId, contactId }: { locationId: string; contactId: string }) =>
      agentsApi.deleteContact(id!, locationId, contactId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['agents', id] })
      qc.invalidateQueries({ queryKey: ['agents'] })
      toast.success('Contact deleted')
    },
    onError: () => toast.error('Failed to delete contact'),
  })

  // ─── Commission mutations ────────────────────────────────────────────────────

  const addCommissionMutation = useMutation({
    mutationFn: () => createAgentCommission(id!, {
      programConfigurationId: commissionForm.programConfigurationId || null,
      lineOfBusiness: commissionForm.lineOfBusiness || null,
      commissionRate: parseFloat(commissionForm.rate) / 100,
      effectiveDate: commissionForm.effectiveDate,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['agent-commissions', id] })
      setShowAddCommission(false)
      setCommissionForm({ programConfigurationId: '', lineOfBusiness: '', rate: '', effectiveDate: '' })
      toast.success('Commission rate added')
    },
    onError: (e: Error) => toast.error(e.message),
  })

  const disableCommissionMutation = useMutation({
    mutationFn: (commId: number) => disableAgentCommission(id!, commId, { disabledDate: null }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['agent-commissions', id] })
      toast.success('Commission rate disabled')
    },
    onError: (e: Error) => toast.error(e.message),
  })

  const toggleLob = (key: string) => {
    setExpandedLobs((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  // ─── Helpers ────────────────────────────────────────────────────────────────

  const formToLocationInput = (f: LocationFormData): AgentLocationInput => ({
    name: f.name || undefined,
    addressLine1: f.addressLine1 || undefined,
    addressLine2: f.addressLine2 || undefined,
    city: f.city || undefined,
    state: f.state || undefined,
    zipCode: f.zipCode || undefined,
    phone: f.phone || undefined,
    isPrimary: f.isPrimary,
    contacts: [],
  })

  const formToContactInput = (f: ContactFormData): AgentContactInput => ({
    firstName: f.firstName.trim(),
    lastName: f.lastName || undefined,
    title: f.title || undefined,
    email: f.email || undefined,
    phone: f.phone || undefined,
    isPrimary: f.isPrimary,
  })

  const toggleCollapse = (locationId: string) => {
    setCollapsedLocations((prev) => {
      const next = new Set(prev)
      if (next.has(locationId)) next.delete(locationId)
      else next.add(locationId)
      return next
    })
  }

  const infoEmailError = infoForm.email && !isValidEmail(infoForm.email)
  const infoPhoneError = infoForm.phone && !isValidPhone(infoForm.phone)

  if (isLoading) return <LoadingSpinner />
  if (!agent) return <EmptyState icon={UserCircle} title="Agent not found" description="The requested agent record could not be loaded." />

  const primaryLocation = agent.locations.find((l) => l.isPrimary) ?? agent.locations[0]

  return (
    <div className="space-y-5">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-slate-500">
        <Link to="/agents" className="sd-btn ghost sm">
          <ArrowLeft className="h-4 w-4" /> Agents
        </Link>
        <span>/</span>
        <span className="text-slate-800 font-medium">{agent.name}</span>
      </div>

      {/* Agent info panel */}
      <div className="sd-card p-5">
        <div className="flex items-start justify-between mb-4">
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-xl font-semibold text-slate-900">{agent.name}</h1>
              <span className={`sd-pill ${agent.isActive ? 'bound' : 'draft'}`}>
                {agent.isActive ? 'Active' : 'Inactive'}
              </span>
            </div>
            {agent.agencyName && <p className="text-sm text-slate-500 mt-0.5">{agent.agencyName}</p>}
          </div>
          {!editingInfo && (
            <button
              onClick={startEditInfo}
              className="sd-btn outline sm"
            >
              <Pencil className="h-3.5 w-3.5" /> Edit
            </button>
          )}
        </div>

        {editingInfo ? (
          <div className="space-y-3">
            <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Name *</label>
                <input
                  value={infoForm.name}
                  onChange={(e) => setInfoForm({ ...infoForm, name: e.target.value })}
                  className="sims-input"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Agency</label>
                <input
                  value={infoForm.agencyName}
                  onChange={(e) => setInfoForm({ ...infoForm, agencyName: e.target.value })}
                  className="sims-input"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">License #</label>
                <input
                  value={infoForm.licenseNumber}
                  onChange={(e) => setInfoForm({ ...infoForm, licenseNumber: e.target.value })}
                  className="sims-input"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Email</label>
                <input
                  value={infoForm.email}
                  onChange={(e) => setInfoForm({ ...infoForm, email: e.target.value })}
                  type="text"
                  className="sims-input"
                  aria-invalid={!!infoEmailError}
                />
                {infoEmailError && <p className="text-xs text-red-600 mt-0.5">Enter a valid email</p>}
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Phone</label>
                <input
                  value={infoForm.phone}
                  onChange={(e) => setInfoForm({ ...infoForm, phone: formatPhoneInput(e.target.value) })}
                  type="text"
                  className="sims-input"
                  aria-invalid={!!infoPhoneError}
                />
                {infoPhoneError && <p className="text-xs text-red-600 mt-0.5">Enter a valid 10-digit number</p>}
              </div>
              <div className="flex items-center gap-2 mt-4">
                <input
                  type="checkbox"
                  id="info-active"
                  checked={infoForm.isActive}
                  onChange={(e) => setInfoForm({ ...infoForm, isActive: e.target.checked })}
                  className="rounded"
                />
                <label htmlFor="info-active" className="text-sm text-slate-600">Active</label>
              </div>
            </div>
            <div className="flex gap-2">
              <button
                onClick={() => {
                  if (!infoForm.name.trim()) { toast.error('Name is required'); return }
                  if (infoEmailError) { toast.error('Enter a valid email'); return }
                  if (infoPhoneError) { toast.error('Enter a valid phone number'); return }
                  updateInfoMutation.mutate()
                }}
                disabled={updateInfoMutation.isPending}
                className="sd-btn primary sm"
              >
                <Check className="h-3.5 w-3.5" /> Save
              </button>
              <button
                onClick={() => setEditingInfo(false)}
                className="sd-btn outline sm"
              >
                <X className="h-3.5 w-3.5" /> Cancel
              </button>
            </div>
          </div>
        ) : (
          <div className="grid grid-cols-2 md:grid-cols-3 gap-4 text-sm">
            {agent.licenseNumber && (
              <div>
                <span className="text-xs font-medium text-slate-500 uppercase tracking-wide">License #</span>
                <p className="text-slate-800 mt-0.5">{agent.licenseNumber}</p>
              </div>
            )}
            {agent.email && (
              <div>
                <span className="text-xs font-medium text-slate-500 uppercase tracking-wide">Email</span>
                <p className="text-slate-800 mt-0.5 flex items-center gap-1">
                  <Mail className="h-3.5 w-3.5 text-slate-400" />
                  <a href={`mailto:${agent.email}`} className="hover:text-sky-700">{agent.email}</a>
                </p>
              </div>
            )}
            {agent.phone && (
              <div>
                <span className="text-xs font-medium text-slate-500 uppercase tracking-wide">Phone</span>
                <p className="text-slate-800 mt-0.5 flex items-center gap-1">
                  <Phone className="h-3.5 w-3.5 text-slate-400" />
                  {agent.phone}
                </p>
              </div>
            )}
            {primaryLocation && (primaryLocation.city || primaryLocation.state) && (
              <div>
                <span className="text-xs font-medium text-slate-500 uppercase tracking-wide">Primary Location</span>
                <p className="text-slate-800 mt-0.5 flex items-center gap-1">
                  <MapPin className="h-3.5 w-3.5 text-slate-400" />
                  {[primaryLocation.city, primaryLocation.state].filter(Boolean).join(', ')}
                </p>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Documents */}
      <DocumentsSection entityType="Agent" entityId={id!} canUpload={canUploadAttachments} canDelete={canDeleteAttachments} />

      {/* Commission Schedules */}
      <div className="sd-card p-5">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-base font-semibold text-slate-800 flex items-center gap-2">
            <Percent className="h-4 w-4 text-slate-400" />
            Commission Schedules
          </h2>
          {!showAddCommission && (
            <button
              onClick={() => setShowAddCommission(true)}
              className="sd-btn primary sm"
            >
              <Plus className="h-3.5 w-3.5" /> Add Rate
            </button>
          )}
        </div>

        {/* Add commission form */}
        {showAddCommission && (
          <div className="mb-4 rounded-lg p-3" style={{ border: '1px solid var(--line)', background: 'var(--surface-2)' }}>
            <p className="text-xs font-medium text-slate-600 mb-2">New Commission Rate</p>
            <div className="grid grid-cols-4 gap-3">
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Program</label>
                <select
                  value={commissionForm.programConfigurationId}
                  onChange={(e) => setCommissionForm({ ...commissionForm, programConfigurationId: e.target.value })}
                  className="sims-select"
                >
                  <option value="">Any program</option>
                  {programs.map((program) => <option key={program.id} value={program.id}>{program.name}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Line of Business</label>
                <select
                  value={commissionForm.lineOfBusiness}
                  onChange={(e) => setCommissionForm({ ...commissionForm, lineOfBusiness: e.target.value })}
                  className="sims-select"
                >
                  {LOB_OPTIONS.map((o) => (
                    <option key={o.value} value={o.value}>{o.label}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Rate (%)</label>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  max="100"
                  value={commissionForm.rate}
                  onChange={(e) => setCommissionForm({ ...commissionForm, rate: e.target.value })}
                  placeholder="e.g. 15"
                  className="sims-input"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Effective Date</label>
                <input
                  type="date"
                  value={commissionForm.effectiveDate}
                  onChange={(e) => setCommissionForm({ ...commissionForm, effectiveDate: e.target.value })}
                  className="sims-input"
                />
              </div>
            </div>
            <div className="flex gap-2 mt-3">
              <button
                onClick={() => {
                  if (!commissionForm.rate || !commissionForm.effectiveDate) {
                    toast.error('Rate and effective date are required')
                    return
                  }
                  addCommissionMutation.mutate()
                }}
                disabled={addCommissionMutation.isPending}
                className="sd-btn primary sm"
              >
                <Check className="h-3.5 w-3.5" /> Save Rate
              </button>
              <button
                onClick={() => { setShowAddCommission(false); setCommissionForm({ programConfigurationId: '', lineOfBusiness: '', rate: '', effectiveDate: '' }) }}
                className="sd-btn outline sm"
              >
                <X className="h-3.5 w-3.5" /> Cancel
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
            const byLob = commissions.reduce<Record<string, AgentCommission[]>>((acc, c) => {
              const key = c.lineOfBusiness ?? '__all__'
              if (!acc[key]) acc[key] = []
              acc[key].push(c)
              return acc
            }, {})

            return (
              <div className="divide-y">
                {Object.entries(byLob).map(([lobKey, rows]) => {
                  const label = lobKey === '__all__' ? 'All Lines (default)' : (rows[0].lineOfBusinessLabel ?? lobKey)
                  const active = rows.find((r) => r.isActive)
                  const isExpanded = expandedLobs.has(lobKey)

                  return (
                    <div key={lobKey} className="py-2">
                      <div
                        className="flex items-center justify-between cursor-pointer hover:bg-slate-50 rounded px-2 py-1"
                        onClick={() => toggleLob(lobKey)}
                      >
                        <div className="flex items-center gap-3">
                          {isExpanded ? <ChevronUp className="h-3.5 w-3.5 text-slate-400" /> : <ChevronDown className="h-3.5 w-3.5 text-slate-400" />}
                          <span className="text-sm font-medium text-slate-700">{label}</span>
                          {active && (
                            <span className="text-sm text-slate-900 font-semibold">
                              {(active.commissionRate * 100).toFixed(2)}%
                            </span>
                          )}
                          {active?.programName && (
                            <span className="text-xs text-blue-600">{active.programName}</span>
                          )}
                        </div>
                        <div className="flex items-center gap-2">
                          {active ? (
                            <span className="px-1.5 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-700">Active</span>
                          ) : (
                            <span className="px-1.5 py-0.5 rounded-full text-xs font-medium bg-slate-100 text-slate-500">Inactive</span>
                          )}
                          <span className="text-xs text-slate-400">{rows.length} {rows.length === 1 ? 'version' : 'versions'}</span>
                        </div>
                      </div>

                      {isExpanded && (
                        <div className="mt-1 ml-6">
                          <table className="w-full text-xs">
                            <thead>
                              <tr className="text-slate-500">
                                <th className="text-left py-1 font-medium">Rate</th>
                                <th className="text-left py-1 font-medium">Program</th>
                                <th className="text-left py-1 font-medium">Effective</th>
                                <th className="text-left py-1 font-medium">Disabled</th>
                                <th className="text-left py-1 font-medium">Status</th>
                                <th />
                              </tr>
                            </thead>
                            <tbody className="divide-y divide-slate-100">
                              {rows.map((r) => (
                                <tr key={r.id}>
                                  <td className="py-1.5 font-medium text-slate-800">{(r.commissionRate * 100).toFixed(2)}%</td>
                                  <td className="py-1.5 text-slate-600">{r.programName ?? 'Any program'}</td>
                                  <td className="py-1.5 text-slate-600">{r.effectiveDate}</td>
                                  <td className="py-1.5 text-slate-600">{r.disabledDate ?? '—'}</td>
                                  <td className="py-1.5">
                                    {r.isActive ? (
                                      <span className="px-1.5 py-0.5 rounded-full bg-green-100 text-green-700 font-medium">Active</span>
                                    ) : (
                                      <span className="px-1.5 py-0.5 rounded-full bg-slate-100 text-slate-500">Disabled</span>
                                    )}
                                  </td>
                                  <td className="py-1.5 text-right">
                                    {r.isActive && (
                                      <button
                                        onClick={() => {
                                          if (confirm('Disable this commission rate?'))
                                            disableCommissionMutation.mutate(r.id)
                                        }}
                                        className="sims-icon-btn hover:text-red-500"
                                        title="Disable"
                                      >
                                        <X className="h-3.5 w-3.5" />
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

      {/* Offices & Contacts section */}
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-base font-semibold text-slate-800 flex items-center gap-2">
            <Building2 className="h-4 w-4 text-slate-400" />
            Offices &amp; Contacts
            <span className="text-xs font-normal text-slate-400">
              ({agent.locations.length} {agent.locations.length === 1 ? 'office' : 'offices'})
            </span>
          </h2>
          {!showNewLocation && (
            <button
              onClick={() => { setShowNewLocation(true); setShowNewContact(null) }}
              className="sd-btn primary sm"
            >
              <Plus className="h-3.5 w-3.5" /> Add Office
            </button>
          )}
        </div>

        {/* New location form */}
        {showNewLocation && (
          <div className="sd-card p-4">
            <h3 className="text-sm font-medium text-slate-700 mb-1">New Office</h3>
            <LocationForm
              form={newLocForm}
              setForm={setNewLocForm}
              onSave={() => addLocationMutation.mutate(formToLocationInput(newLocForm))}
              onCancel={() => { setShowNewLocation(false); setNewLocForm(emptyLocationForm()) }}
              isPending={addLocationMutation.isPending}
            />
          </div>
        )}

        {agent.locations.length === 0 && !showNewLocation && (
          <EmptyState
            icon={Building2}
            title="No offices added yet"
            action={<button onClick={() => setShowNewLocation(true)} className="sd-btn outline sm">Add the first office</button>}
          />
        )}

        {/* Location cards */}
        {agent.locations.map((location) => {
          const isCollapsed = collapsedLocations.has(location.id)
          const isEditingThisLoc = editingLocationId === location.id
          const addressParts = [location.addressLine1, location.addressLine2, location.city && location.state ? `${location.city}, ${location.state} ${location.zipCode ?? ''}`.trim() : null].filter(Boolean)

          return (
            <div key={location.id} className="sd-card overflow-hidden">
              {/* Location header */}
              <div className="flex items-center justify-between px-4 py-3 bg-slate-50 border-b">
                <div className="flex items-center gap-2">
                  <button onClick={() => toggleCollapse(location.id)} className="text-slate-400 hover:text-slate-600">
                    {isCollapsed ? <ChevronDown className="h-4 w-4" /> : <ChevronUp className="h-4 w-4" />}
                  </button>
                  <div>
                    <div className="flex items-center gap-2">
                      <span className="font-medium text-slate-800 text-sm">
                        {location.name || 'Office'}
                      </span>
                      {location.isPrimary && (
                        <span className="flex items-center gap-0.5 px-1.5 py-0.5 text-xs rounded-full bg-amber-50 text-amber-700 border border-amber-200">
                          <Star className="h-2.5 w-2.5" /> Primary
                        </span>
                      )}
                    </div>
                    {addressParts.length > 0 && (
                      <p className="text-xs text-slate-500 mt-0.5">{addressParts.join(' · ')}</p>
                    )}
                    {location.phone && (
                      <p className="text-xs text-slate-500 flex items-center gap-1 mt-0.5">
                        <Phone className="h-3 w-3" /> {location.phone}
                      </p>
                    )}
                  </div>
                </div>
                <div className="flex items-center gap-1">
                  <span className="text-xs text-slate-400 mr-2">
                    {location.contacts.length} {location.contacts.length === 1 ? 'contact' : 'contacts'}
                  </span>
                  <button
                    onClick={() => {
                      setEditingLocationId(location.id)
                      setEditLocForm(locationToForm(location))
                      setCollapsedLocations((prev) => { const n = new Set(prev); n.delete(location.id); return n })
                    }}
                    className="sims-icon-btn hover:text-sky-600"
                    title="Edit office"
                  >
                    <Pencil className="h-3.5 w-3.5" />
                  </button>
                  <button
                    onClick={() => {
                      if (confirm(`Delete "${location.name || 'this office'}" and all its contacts?`))
                        deleteLocationMutation.mutate(location.id)
                    }}
                    className="sims-icon-btn hover:text-red-500"
                    title="Delete office"
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>
                </div>
              </div>

              {/* Location body */}
              {!isCollapsed && (
                <div className="p-4">
                  {/* Edit location form */}
                  {isEditingThisLoc && (
                    <div className="mb-4 pb-4 border-b">
                      <p className="text-xs font-medium text-slate-600 mb-1">Edit Office</p>
                      <LocationForm
                        form={editLocForm}
                        setForm={setEditLocForm}
                        onSave={() => updateLocationMutation.mutate({ locationId: location.id, data: formToLocationInput(editLocForm) })}
                        onCancel={() => setEditingLocationId(null)}
                        isPending={updateLocationMutation.isPending}
                      />
                    </div>
                  )}

                  {/* Contacts list */}
                  <div className="space-y-2">
                    {location.contacts.length === 0 && showNewContact !== location.id && (
                      <p className="text-xs text-slate-400 italic">No contacts at this office.</p>
                    )}

                    {location.contacts.map((contact) => {
                      const isEditingThisContact =
                        editingContact?.locationId === location.id && editingContact?.contactId === contact.id
                      const fullName = [contact.firstName, contact.lastName].filter(Boolean).join(' ')

                      return (
                        <div key={contact.id}>
                          {isEditingThisContact ? (
                            <div className="bg-slate-50 rounded-lg p-3 border">
                              <ContactForm
                                form={editContactForm}
                                setForm={setEditContactForm}
                                onSave={() =>
                                  updateContactMutation.mutate({
                                    locationId: location.id,
                                    contactId: contact.id,
                                    data: formToContactInput(editContactForm),
                                  })
                                }
                                onCancel={() => setEditingContact(null)}
                                isPending={updateContactMutation.isPending}
                              />
                            </div>
                          ) : (
                            <div className="flex items-center justify-between py-1.5 px-2 rounded hover:bg-slate-50 group">
                              <div className="flex items-center gap-3">
                                <UserCircle className="h-7 w-7 text-slate-300 shrink-0" />
                                <div>
                                  <div className="flex items-center gap-1.5">
                                    <span className="text-sm font-medium text-slate-800">{fullName}</span>
                                    {contact.isPrimary && (
                                      <span className="text-xs text-amber-600">· Primary</span>
                                    )}
                                    {contact.title && (
                                      <span className="text-xs text-slate-400">· {contact.title}</span>
                                    )}
                                  </div>
                                  <div className="flex items-center gap-3 mt-0.5">
                                    {contact.email && (
                                      <a href={`mailto:${contact.email}`} className="text-xs text-sky-700 hover:underline flex items-center gap-1">
                                        <Mail className="h-3 w-3" /> {contact.email}
                                      </a>
                                    )}
                                    {contact.phone && (
                                      <span className="text-xs text-slate-500 flex items-center gap-1">
                                        <Phone className="h-3 w-3" /> {contact.phone}
                                      </span>
                                    )}
                                  </div>
                                </div>
                              </div>
                              <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                                <button
                                  onClick={() => {
                                    setEditingContact({ locationId: location.id, contactId: contact.id })
                                    setEditContactForm(contactToForm(contact))
                                    setShowNewContact(null)
                                  }}
                                  className="sims-icon-btn hover:text-sky-600"
                                  title="Edit contact"
                                >
                                  <Pencil className="h-3.5 w-3.5" />
                                </button>
                                <button
                                  onClick={() => {
                                    if (confirm(`Delete contact ${fullName}?`))
                                      deleteContactMutation.mutate({ locationId: location.id, contactId: contact.id })
                                  }}
                                  className="sims-icon-btn hover:text-red-500"
                                  title="Delete contact"
                                >
                                  <Trash2 className="h-3.5 w-3.5" />
                                </button>
                              </div>
                            </div>
                          )}
                        </div>
                      )
                    })}

                    {/* New contact form */}
                    {showNewContact === location.id && (
                      <div className="bg-slate-50 rounded-lg p-3 border mt-2">
                        <ContactForm
                          form={newContactForm}
                          setForm={setNewContactForm}
                          onSave={() =>
                            addContactMutation.mutate({
                              locationId: location.id,
                              data: formToContactInput(newContactForm),
                            })
                          }
                          onCancel={() => { setShowNewContact(null); setNewContactForm(emptyContactForm()) }}
                          isPending={addContactMutation.isPending}
                        />
                      </div>
                    )}

                    {showNewContact !== location.id && (
                      <button
                        onClick={() => {
                          setShowNewContact(location.id)
                          setNewContactForm(emptyContactForm())
                          setEditingContact(null)
                        }}
                        className="sd-btn ghost sm mt-1"
                      >
                        <Plus className="h-3.5 w-3.5" /> Add Contact
                      </button>
                    )}
                  </div>
                </div>
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}
