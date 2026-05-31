import { useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft, Plus, Pencil, Trash2, Check, X, MapPin, Phone, Mail,
  Star, Building2, ChevronDown, ChevronUp, UserCircle, Percent, BanknoteIcon,
} from 'lucide-react'
import { toast } from 'sonner'
import { agentsApi } from '@/api/agents.api'
import { carriersApi } from '@/api/carriers.api'
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
  { value: 'InlandMarine', label: 'Inland Marine' },
  { value: 'AutoLiability', label: 'Auto Liability' },
  { value: 'AutoPhysicalDamage', label: 'Auto Physical Damage' },
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
  form, setForm, onSave, onCancel, isPending,
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
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12, paddingTop: 8 }}>
      <div className="sims-fields" style={{ gridTemplateColumns: '1fr 1fr' }}>
        <div>
          <label className="sims-field-label">Office Label</label>
          <input value={form.name} onChange={set('name')} placeholder="e.g. Main Office, Downtown Branch" className="sims-input" />
        </div>
        <div>
          <label className="sims-field-label">Office Phone</label>
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
      <div className="sims-fields" style={{ gridTemplateColumns: '1fr 1fr' }}>
        <div>
          <label className="sims-field-label">Street Address</label>
          <AddressAutocomplete
            value={form.addressLine1}
            onChange={(val) => setForm({ ...form, addressLine1: val })}
            onSelect={(c) => setForm({ ...form, addressLine1: c.addressLine1, city: c.city, state: c.state, zipCode: c.zipCode })}
            placeholder="Start typing an address…"
          />
        </div>
        <div>
          <label className="sims-field-label">Suite / Unit</label>
          <input value={form.addressLine2} onChange={set('addressLine2')} placeholder="Apt, Suite, Unit…" className="sims-input" />
        </div>
      </div>
      <div className="sims-fields" style={{ gridTemplateColumns: 'repeat(3, 1fr)' }}>
        <div>
          <label className="sims-field-label">City</label>
          <input value={form.city} onChange={set('city')} placeholder="City" className="sims-input" />
        </div>
        <div>
          <label className="sims-field-label">State</label>
          <input value={form.state} onChange={set('state')} maxLength={2} placeholder="TX" className="sims-input" style={{ textTransform: 'uppercase' }} />
        </div>
        <div>
          <label className="sims-field-label">ZIP</label>
          <input value={form.zipCode} onChange={set('zipCode')} placeholder="78701" className="sims-input" />
          {zipError && <p style={{ fontSize: 11.5, color: 'var(--bad-fg)', marginTop: 2 }}>Invalid ZIP code</p>}
        </div>
      </div>
      <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, color: 'var(--ink-2)', cursor: 'pointer' }}>
        <input type="checkbox" id="loc-primary" checked={form.isPrimary} onChange={set('isPrimary')} />
        Mark as primary office
      </label>
      <div style={{ display: 'flex', gap: 8 }}>
        <button onClick={onSave} disabled={isPending} className="sd-btn primary sm">
          <Check style={{ width: 12, height: 12 }} /> Save Office
        </button>
        <button onClick={onCancel} className="sd-btn outline sm">
          <X style={{ width: 12, height: 12 }} /> Cancel
        </button>
      </div>
    </div>
  )
}

// ─── Contact form component ────────────────────────────────────────────────────

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
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12, paddingTop: 8 }}>
      <div className="sims-fields" style={{ gridTemplateColumns: 'repeat(4, 1fr)' }}>
        <div>
          <label className="sims-field-label">First Name *</label>
          <input value={form.firstName} onChange={set('firstName')} placeholder="First name" className="sims-input" />
        </div>
        <div>
          <label className="sims-field-label">Last Name</label>
          <input value={form.lastName} onChange={set('lastName')} placeholder="Last name" className="sims-input" />
        </div>
        <div>
          <label className="sims-field-label">Title / Role</label>
          <input value={form.title} onChange={set('title')} placeholder="e.g. Producer, Account Mgr" className="sims-input" />
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
          <label className="sims-field-label">Direct Phone</label>
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

// ─── Main page ─────────────────────────────────────────────────────────────────

export function AgentDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { canUploadAttachments, canDeleteAttachments } = usePermissions()

  const [editingInfo, setEditingInfo] = useState(false)
  const [infoForm, setInfoForm] = useState({ name: '', agencyName: '', licenseNumber: '', email: '', phone: '', isActive: true })

  const [showNewLocation, setShowNewLocation] = useState(false)
  const [newLocForm, setNewLocForm] = useState<LocationFormData>(emptyLocationForm())
  const [editingLocationId, setEditingLocationId] = useState<string | null>(null)
  const [editLocForm, setEditLocForm] = useState<LocationFormData>(emptyLocationForm())
  const [collapsedLocations, setCollapsedLocations] = useState<Set<string>>(new Set())

  const [showNewContact, setShowNewContact] = useState<string | null>(null)
  const [newContactForm, setNewContactForm] = useState<ContactFormData>(emptyContactForm())
  const [editingContact, setEditingContact] = useState<{ locationId: string; contactId: string } | null>(null)
  const [editContactForm, setEditContactForm] = useState<ContactFormData>(emptyContactForm())

  const [showAddCommission, setShowAddCommission] = useState(false)
  const [commissionForm, setCommissionForm] = useState({ programConfigurationId: '', carrierId: '', lineOfBusiness: '', stateCode: '', rate: '', effectiveDate: '' })
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

  const { data: carriers = [] } = useQuery({
    queryKey: ['carriers', 'active'],
    queryFn: () => carriersApi.getAll(true),
  })

  const { data: programs = [] } = useQuery({
    queryKey: ['admin', 'program-configurations', 'active'],
    queryFn: () => programConfigurationsApi.getAll(false),
  })

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

  const addCommissionMutation = useMutation({
    mutationFn: () => createAgentCommission(id!, {
      programConfigurationId: commissionForm.programConfigurationId || null,
      carrierId: commissionForm.carrierId || null,
      lineOfBusiness: commissionForm.lineOfBusiness || null,
      stateCode: commissionForm.stateCode || null,
      commissionRate: parseFloat(commissionForm.rate) / 100,
      effectiveDate: commissionForm.effectiveDate,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['agent-commissions', id] })
      setShowAddCommission(false)
      setCommissionForm({ programConfigurationId: '', carrierId: '', lineOfBusiness: '', stateCode: '', rate: '', effectiveDate: '' })
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
  const selectedCommissionProgram = programs.find((p) => p.id === commissionForm.programConfigurationId)
  const selectedProgramCarriers = selectedCommissionProgram?.carriers.filter((pc) => pc.isActive) ?? []
  const commissionCarrierOptions = commissionForm.programConfigurationId
    ? carriers.filter((c) => selectedProgramCarriers.some((pc) => pc.carrierId === c.id))
    : carriers
  const selectedProgramCarrier = selectedProgramCarriers.find((pc) => pc.carrierId === commissionForm.carrierId)
  const programLobs = selectedProgramCarrier
    ? selectedProgramCarrier.linesOfBusiness.filter((lob) => lob.isActive)
    : selectedProgramCarriers.flatMap((pc) => pc.linesOfBusiness).filter((lob) => lob.isActive)
  const carrierLobs = commissionForm.carrierId
    ? carriers.find((c) => c.id === commissionForm.carrierId)?.linesOfBusiness ?? []
    : []
  const commissionLobValues = commissionForm.programConfigurationId
    ? Array.from(new Set(programLobs.map((lob) => lob.lineOfBusiness)))
    : commissionForm.carrierId
      ? carrierLobs
      : LOB_OPTIONS.filter((o) => o.value).map((o) => o.value)
  const commissionLobOptions = [
    LOB_OPTIONS[0],
    ...LOB_OPTIONS.filter((o) => o.value && commissionLobValues.includes(o.value)),
  ]
  const selectedProgramLob = selectedProgramCarrier?.linesOfBusiness.find((lob) => lob.lineOfBusiness === commissionForm.lineOfBusiness && lob.isActive)
  const commissionStateOptions = selectedProgramLob?.states.filter((s) => s.isActive).map((s) => s.stateCode) ?? []

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
        <Link to="/agents" className="sd-btn ghost sm">
          <ArrowLeft style={{ width: 13, height: 13 }} /> Agents
        </Link>
        <span>/</span>
        <span style={{ color: 'var(--ink)', fontWeight: 600 }}>{agent.name}</span>
      </div>

      {/* Agent info panel */}
      <div className="sd-card">
        <div className="sd-card-head" style={{ justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <h3 style={{ fontWeight: 600, fontSize: 16, color: 'var(--ink)' }}>{agent.name}</h3>
            <span className={`sd-pill ${agent.isActive ? 'good' : 'withdrawn'}`}>
              {agent.isActive ? 'Active' : 'Inactive'}
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
                  <label className="sims-field-label">Name *</label>
                  <input value={infoForm.name} onChange={(e) => setInfoForm({ ...infoForm, name: e.target.value })} className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">Agency</label>
                  <input value={infoForm.agencyName} onChange={(e) => setInfoForm({ ...infoForm, agencyName: e.target.value })} className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">License #</label>
                  <input value={infoForm.licenseNumber} onChange={(e) => setInfoForm({ ...infoForm, licenseNumber: e.target.value })} className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">Email</label>
                  <input value={infoForm.email} onChange={(e) => setInfoForm({ ...infoForm, email: e.target.value })} type="text" className="sims-input" />
                  {infoEmailError && <p style={{ fontSize: 11.5, color: 'var(--bad-fg)', marginTop: 2 }}>Enter a valid email</p>}
                </div>
                <div>
                  <label className="sims-field-label">Phone</label>
                  <input value={infoForm.phone} onChange={(e) => setInfoForm({ ...infoForm, phone: formatPhoneInput(e.target.value) })} type="text" className="sims-input" />
                  {infoPhoneError && <p style={{ fontSize: 11.5, color: 'var(--bad-fg)', marginTop: 2 }}>Enter a valid 10-digit number</p>}
                </div>
                <div style={{ display: 'flex', alignItems: 'center', paddingTop: 18 }}>
                  <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, color: 'var(--ink-2)', cursor: 'pointer' }}>
                    <input type="checkbox" checked={infoForm.isActive} onChange={(e) => setInfoForm({ ...infoForm, isActive: e.target.checked })} />
                    Active
                  </label>
                </div>
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
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
                  <Check style={{ width: 12, height: 12 }} /> Save
                </button>
                <button onClick={() => setEditingInfo(false)} className="sd-btn outline sm">
                  <X style={{ width: 12, height: 12 }} /> Cancel
                </button>
              </div>
            </div>
          ) : (
            <div style={{ display: 'flex', gap: 32, flexWrap: 'wrap' }}>
              {agent.agencyName && (
                <div>
                  <span style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '.06em', textTransform: 'uppercase', color: 'var(--ink-4)' }}>Agency</span>
                  <p style={{ fontSize: 13, color: 'var(--ink)', marginTop: 2 }}>{agent.agencyName}</p>
                </div>
              )}
              {agent.licenseNumber && (
                <div>
                  <span style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '.06em', textTransform: 'uppercase', color: 'var(--ink-4)' }}>License #</span>
                  <p style={{ fontSize: 13, color: 'var(--ink)', marginTop: 2, fontFamily: 'var(--font-mono)' }}>{agent.licenseNumber}</p>
                </div>
              )}
              {agent.email && (
                <div>
                  <span style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '.06em', textTransform: 'uppercase', color: 'var(--ink-4)' }}>Email</span>
                  <p style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 13, marginTop: 2 }}>
                    <Mail style={{ width: 12, height: 12, color: 'var(--ink-4)' }} />
                    <a href={`mailto:${agent.email}`} style={{ color: 'var(--accent-ink)', textDecoration: 'none' }}>{agent.email}</a>
                  </p>
                </div>
              )}
              {agent.phone && (
                <div>
                  <span style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '.06em', textTransform: 'uppercase', color: 'var(--ink-4)' }}>Phone</span>
                  <p style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 13, color: 'var(--ink)', marginTop: 2 }}>
                    <Phone style={{ width: 12, height: 12, color: 'var(--ink-4)' }} />
                    {agent.phone}
                  </p>
                </div>
              )}
              {primaryLocation && (primaryLocation.city || primaryLocation.state) && (
                <div>
                  <span style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '.06em', textTransform: 'uppercase', color: 'var(--ink-4)' }}>Primary Location</span>
                  <p style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 13, color: 'var(--ink)', marginTop: 2 }}>
                    <MapPin style={{ width: 12, height: 12, color: 'var(--ink-4)' }} />
                    {[primaryLocation.city, primaryLocation.state].filter(Boolean).join(', ')}
                  </p>
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Documents */}
      <DocumentsSection entityType="Agent" entityId={id!} canUpload={canUploadAttachments} canDelete={canDeleteAttachments} />

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
              <p style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--ink-3)' }}>New Commission Rate</p>
              <div className="sims-fields" style={{ gridTemplateColumns: 'repeat(6, 1fr)' }}>
                <div>
                  <label className="sims-field-label">Program</label>
                  <select
                    value={commissionForm.programConfigurationId}
                    onChange={(e) => setCommissionForm({ ...commissionForm, programConfigurationId: e.target.value, carrierId: '', lineOfBusiness: '', stateCode: '' })}
                    className="sims-select"
                  >
                    <option value="">Any program</option>
                    {programs.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
                  </select>
                </div>
                <div>
                  <label className="sims-field-label">Carrier</label>
                  <select
                    value={commissionForm.carrierId}
                    onChange={(e) => setCommissionForm({ ...commissionForm, carrierId: e.target.value, lineOfBusiness: '', stateCode: '' })}
                    className="sims-select"
                  >
                    <option value="">Any carrier</option>
                    {commissionCarrierOptions.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                  </select>
                </div>
                <div>
                  <label className="sims-field-label">Line of Business</label>
                  <select
                    value={commissionForm.lineOfBusiness}
                    onChange={(e) => setCommissionForm({ ...commissionForm, lineOfBusiness: e.target.value, stateCode: '' })}
                    className="sims-select"
                  >
                    {commissionLobOptions.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                  </select>
                </div>
                <div>
                  <label className="sims-field-label">State</label>
                  <select
                    value={commissionForm.stateCode}
                    onChange={(e) => setCommissionForm({ ...commissionForm, stateCode: e.target.value })}
                    disabled={!commissionForm.programConfigurationId || !commissionForm.carrierId || !commissionForm.lineOfBusiness}
                    className="sims-select"
                  >
                    <option value="">Any state</option>
                    {commissionStateOptions.map((s) => <option key={s} value={s}>{s}</option>)}
                  </select>
                </div>
                <div>
                  <label className="sims-field-label">Rate (%)</label>
                  <input type="number" step="0.01" min="0" max="100" value={commissionForm.rate} onChange={(e) => setCommissionForm({ ...commissionForm, rate: e.target.value })} placeholder="15" className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">Effective Date</label>
                  <input type="date" value={commissionForm.effectiveDate} onChange={(e) => setCommissionForm({ ...commissionForm, effectiveDate: e.target.value })} className="sims-input" />
                </div>
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                <button
                  onClick={() => {
                    if (commissionForm.stateCode && (!commissionForm.carrierId || !commissionForm.lineOfBusiness)) {
                      toast.error('State-specific rates require carrier and line of business')
                      return
                    }
                    if (!commissionForm.rate || !commissionForm.effectiveDate) {
                      toast.error('Rate and effective date are required')
                      return
                    }
                    addCommissionMutation.mutate()
                  }}
                  disabled={addCommissionMutation.isPending}
                  className="sd-btn primary sm"
                >
                  <Check style={{ width: 12, height: 12 }} /> Save Rate
                </button>
                <button
                  onClick={() => { setShowAddCommission(false); setCommissionForm({ programConfigurationId: '', carrierId: '', lineOfBusiness: '', stateCode: '', rate: '', effectiveDate: '' }) }}
                  className="sd-btn outline sm"
                >
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
              const byLob = commissions.reduce<Record<string, AgentCommission[]>>((acc, c) => {
                const key = c.lineOfBusiness ?? '__all__'
                if (!acc[key]) acc[key] = []
                acc[key].push(c)
                return acc
              }, {})

              return (
                <div style={{ border: '1px solid var(--line)', borderRadius: 'var(--r)', overflow: 'hidden' }}>
                  {Object.entries(byLob).map(([lobKey, rows], i) => {
                    const label = lobKey === '__all__' ? 'All Lines (default)' : (rows[0].lineOfBusinessLabel ?? lobKey)
                    const active = rows.find((r) => r.isActive)
                    const isExpanded = expandedLobs.has(lobKey)

                    return (
                      <div key={lobKey} style={{ borderTop: i > 0 ? '1px solid var(--line)' : undefined }}>
                        <div
                          style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '8px 14px', cursor: 'pointer', background: 'var(--surface)' }}
                          onClick={() => toggleLob(lobKey)}
                        >
                          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                            {isExpanded ? <ChevronUp style={{ width: 13, height: 13, color: 'var(--ink-4)' }} /> : <ChevronDown style={{ width: 13, height: 13, color: 'var(--ink-4)' }} />}
                            <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)' }}>{label}</span>
                            {active && (
                              <span style={{ fontSize: 12.5, fontWeight: 700, color: 'var(--ink)' }}>
                                {(active.commissionRate * 100).toFixed(2)}%
                              </span>
                            )}
                            {active?.programName && <span style={{ fontSize: 11.5, color: 'var(--accent-ink)' }}>{active.programName}</span>}
                            {active?.carrierName && <span style={{ fontSize: 11.5, color: 'var(--ink-3)' }}>{active.carrierName}</span>}
                            {active?.stateCode && <span style={{ fontSize: 11.5, color: 'var(--ink-3)' }}>{active.stateCode}</span>}
                          </div>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                            <span className={`sd-pill ${active ? 'bound' : 'withdrawn'}`}>{active ? 'Active' : 'Inactive'}</span>
                            <span style={{ fontSize: 11.5, color: 'var(--ink-4)' }}>{rows.length} {rows.length === 1 ? 'version' : 'versions'}</span>
                          </div>
                        </div>

                        {isExpanded && (
                          <div style={{ borderTop: '1px solid var(--line)', background: 'var(--surface-2)' }}>
                            <table className="subs-table">
                              <thead>
                                <tr>
                                  <th className="subs-th">Rate</th>
                                  <th className="subs-th">Program</th>
                                  <th className="subs-th">Carrier</th>
                                  <th className="subs-th">State</th>
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
                                    <td style={{ color: 'var(--ink-2)' }}>{r.carrierName ?? 'Any carrier'}</td>
                                    <td style={{ color: 'var(--ink-2)' }}>{r.stateCode ?? 'Any state'}</td>
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
                                          onClick={() => { if (confirm('Disable this commission rate?')) disableCommissionMutation.mutate(r.id) }}
                                          className="sims-icon-btn"
                                          title="Disable"
                                        >
                                          <X style={{ width: 12, height: 12 }} />
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

      {/* Offices & Contacts */}
      <div className="sd-card">
        <div className="sd-card-head" style={{ justifyContent: 'space-between' }}>
          <h3>
            <Building2 style={{ width: 13, height: 13, marginRight: 6, display: 'inline', verticalAlign: 'text-bottom' }} />
            Offices &amp; Contacts
            <span className="cnt">{agent.locations.length}</span>
          </h3>
          {!showNewLocation && (
            <button onClick={() => { setShowNewLocation(true); setShowNewContact(null) }} className="sd-btn primary sm">
              <Plus style={{ width: 12, height: 12 }} /> Add Office
            </button>
          )}
        </div>

        <div className="sd-card-body">
          {showNewLocation && (
            <div style={{ ...formPanelStyle, marginBottom: 14 }}>
              <p style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--ink-3)' }}>New Office</p>
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

          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            {agent.locations.map((location) => {
              const isCollapsed = collapsedLocations.has(location.id)
              const isEditingThisLoc = editingLocationId === location.id
              const addressParts = [location.addressLine1, location.addressLine2, location.city && location.state ? `${location.city}, ${location.state} ${location.zipCode ?? ''}`.trim() : null].filter(Boolean)

              return (
                <div key={location.id} style={{ border: '1px solid var(--line)', borderRadius: 'var(--r)', overflow: 'hidden' }}>
                  {/* Location header */}
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '10px 14px', background: 'var(--surface-2)', borderBottom: isCollapsed ? 'none' : '1px solid var(--line)' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                      <button onClick={() => toggleCollapse(location.id)} className="sims-icon-btn" style={{ color: 'var(--ink-4)' }}>
                        {isCollapsed ? <ChevronDown style={{ width: 14, height: 14 }} /> : <ChevronUp style={{ width: 14, height: 14 }} />}
                      </button>
                      <div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                          <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)' }}>
                            {location.name || 'Office'}
                          </span>
                          {location.isPrimary && (
                            <span style={{ display: 'flex', alignItems: 'center', gap: 3, fontSize: 11, color: 'var(--warn-fg)', background: 'var(--warn-bg)', padding: '1px 6px', borderRadius: 10 }}>
                              <Star style={{ width: 10, height: 10 }} /> Primary
                            </span>
                          )}
                        </div>
                        {addressParts.length > 0 && (
                          <p style={{ fontSize: 11.5, color: 'var(--ink-3)', marginTop: 1 }}>{addressParts.join(' · ')}</p>
                        )}
                        {location.phone && (
                          <p style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 11.5, color: 'var(--ink-3)', marginTop: 1 }}>
                            <Phone style={{ width: 11, height: 11 }} /> {location.phone}
                          </p>
                        )}
                      </div>
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span style={{ fontSize: 11.5, color: 'var(--ink-4)', marginRight: 4 }}>
                        {location.contacts.length} {location.contacts.length === 1 ? 'contact' : 'contacts'}
                      </span>
                      <button
                        onClick={() => {
                          setEditingLocationId(location.id)
                          setEditLocForm(locationToForm(location))
                          setCollapsedLocations((prev) => { const n = new Set(prev); n.delete(location.id); return n })
                        }}
                        className="sims-icon-btn"
                        title="Edit office"
                      >
                        <Pencil style={{ width: 13, height: 13 }} />
                      </button>
                      <button
                        onClick={() => { if (confirm(`Delete "${location.name || 'this office'}" and all its contacts?`)) deleteLocationMutation.mutate(location.id) }}
                        className="sims-icon-btn"
                        title="Delete office"
                      >
                        <Trash2 style={{ width: 13, height: 13 }} />
                      </button>
                    </div>
                  </div>

                  {/* Location body */}
                  {!isCollapsed && (
                    <div style={{ padding: '12px 14px' }}>
                      {isEditingThisLoc && (
                        <div style={{ ...formPanelStyle, marginBottom: 12 }}>
                          <p style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--ink-3)' }}>Edit Office</p>
                          <LocationForm
                            form={editLocForm}
                            setForm={setEditLocForm}
                            onSave={() => updateLocationMutation.mutate({ locationId: location.id, data: formToLocationInput(editLocForm) })}
                            onCancel={() => setEditingLocationId(null)}
                            isPending={updateLocationMutation.isPending}
                          />
                        </div>
                      )}

                      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                        {location.contacts.length === 0 && showNewContact !== location.id && (
                          <p style={{ fontSize: 12, color: 'var(--ink-4)', fontStyle: 'italic' }}>No contacts at this office.</p>
                        )}

                        {location.contacts.map((contact) => {
                          const isEditingThisContact = editingContact?.locationId === location.id && editingContact?.contactId === contact.id
                          const fullName = [contact.firstName, contact.lastName].filter(Boolean).join(' ')

                          return (
                            <div key={contact.id}>
                              {isEditingThisContact ? (
                                <div style={formPanelStyle}>
                                  <ContactForm
                                    form={editContactForm}
                                    setForm={setEditContactForm}
                                    onSave={() => updateContactMutation.mutate({ locationId: location.id, contactId: contact.id, data: formToContactInput(editContactForm) })}
                                    onCancel={() => setEditingContact(null)}
                                    isPending={updateContactMutation.isPending}
                                  />
                                </div>
                              ) : (
                                <div className="subs-row" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '6px 4px' }}>
                                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                                    <UserCircle style={{ width: 26, height: 26, color: 'var(--line)', flexShrink: 0 }} />
                                    <div>
                                      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                                        <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)' }}>{fullName}</span>
                                        {contact.isPrimary && <span style={{ fontSize: 11.5, color: 'var(--warn-fg)' }}>· Primary</span>}
                                        {contact.title && <span style={{ fontSize: 11.5, color: 'var(--ink-4)' }}>· {contact.title}</span>}
                                      </div>
                                      <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginTop: 2 }}>
                                        {contact.email && (
                                          <a href={`mailto:${contact.email}`} style={{ display: 'flex', alignItems: 'center', gap: 3, fontSize: 12, color: 'var(--accent-ink)', textDecoration: 'none' }}>
                                            <Mail style={{ width: 11, height: 11 }} /> {contact.email}
                                          </a>
                                        )}
                                        {contact.phone && (
                                          <span style={{ display: 'flex', alignItems: 'center', gap: 3, fontSize: 12, color: 'var(--ink-3)' }}>
                                            <Phone style={{ width: 11, height: 11 }} /> {contact.phone}
                                          </span>
                                        )}
                                      </div>
                                    </div>
                                  </div>
                                  <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                                    <button
                                      onClick={() => { setEditingContact({ locationId: location.id, contactId: contact.id }); setEditContactForm(contactToForm(contact)); setShowNewContact(null) }}
                                      className="sims-icon-btn"
                                      title="Edit contact"
                                    >
                                      <Pencil style={{ width: 12, height: 12 }} />
                                    </button>
                                    <button
                                      onClick={() => { if (confirm(`Delete contact ${fullName}?`)) deleteContactMutation.mutate({ locationId: location.id, contactId: contact.id }) }}
                                      className="sims-icon-btn"
                                      title="Delete contact"
                                    >
                                      <Trash2 style={{ width: 12, height: 12 }} />
                                    </button>
                                  </div>
                                </div>
                              )}
                            </div>
                          )
                        })}

                        {showNewContact === location.id && (
                          <div style={{ ...formPanelStyle, marginTop: 8 }}>
                            <ContactForm
                              form={newContactForm}
                              setForm={setNewContactForm}
                              onSave={() => addContactMutation.mutate({ locationId: location.id, data: formToContactInput(newContactForm) })}
                              onCancel={() => { setShowNewContact(null); setNewContactForm(emptyContactForm()) }}
                              isPending={addContactMutation.isPending}
                            />
                          </div>
                        )}

                        {showNewContact !== location.id && (
                          <button
                            onClick={() => { setShowNewContact(location.id); setNewContactForm(emptyContactForm()); setEditingContact(null) }}
                            className="sd-btn ghost sm"
                            style={{ marginTop: 4 }}
                          >
                            <Plus style={{ width: 12, height: 12 }} /> Add Contact
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
      </div>
    </div>
  )
}
