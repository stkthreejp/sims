import { useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft, Plus, Pencil, Trash2, Check, X, Phone, Mail,
  Star, UserCircle, Globe, MapPin, Percent, BanknoteIcon,
} from 'lucide-react'
import { toast } from 'sonner'
import { carriersApi } from '@/api/carriers.api'
import type { CarrierContact, CarrierContactInput, CarrierUpdate } from '@/types/carrier.types'
import type { PolicyLineOfBusiness } from '@/types/quote.types'
import { LOB_LABELS, ACTIVE_LOBS } from '@/types/quote.types'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { AddressAutocomplete } from '@/components/common/AddressAutocomplete'
import { isValidEmail, isValidPhone, isValidZip, formatPhoneInput } from '@/lib/validators'
import { DocumentsSection } from '@/components/documents/DocumentsSection'
import { usePermissions } from '@/hooks/usePermissions'
import {
  getCarrierCommissions,
  createCarrierCommission,
  disableCarrierCommission,
} from '@/api/carrierCommissions.api'
import type { CarrierCommission } from '@/types/carrierCommission.types'

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
    <div className="space-y-3 pt-1">
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">First Name *</label>
          <input value={form.firstName} onChange={set('firstName')} placeholder="First name" className="w-full border rounded px-2 py-1.5 text-sm" autoFocus />
        </div>
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">Last Name</label>
          <input value={form.lastName} onChange={set('lastName')} placeholder="Last name" className="w-full border rounded px-2 py-1.5 text-sm" />
        </div>
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">Title / Role</label>
          <input value={form.title} onChange={set('title')} placeholder="e.g. Underwriter, Rep" className="w-full border rounded px-2 py-1.5 text-sm" />
        </div>
        <div className="flex items-end pb-1">
          <label className="flex items-center gap-2 text-sm text-slate-600 cursor-pointer">
            <input type="checkbox" checked={form.isPrimary} onChange={set('isPrimary')} className="rounded" />
            Primary contact
          </label>
        </div>
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">Email</label>
          <input
            value={form.email}
            onChange={set('email')}
            type="text"
            placeholder="email@example.com"
            className={`w-full border rounded px-2 py-1.5 text-sm ${emailError ? 'border-red-400' : ''}`}
          />
          {emailError && <p className="text-xs text-red-600 mt-0.5">Enter a valid email address</p>}
        </div>
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">Phone</label>
          <input
            value={form.phone}
            onChange={(e) => setForm({ ...form, phone: formatPhoneInput(e.target.value) })}
            type="text"
            placeholder="(555) 123-4567"
            className={`w-full border rounded px-2 py-1.5 text-sm ${phoneError ? 'border-red-400' : ''}`}
          />
          {phoneError && <p className="text-xs text-red-600 mt-0.5">Enter a valid 10-digit number</p>}
        </div>
      </div>
      <div className="flex gap-2">
        <button
          onClick={onSave}
          disabled={isPending || !form.firstName.trim()}
          className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-600 text-white rounded text-sm hover:bg-blue-700 disabled:opacity-50"
        >
          <Check className="h-3.5 w-3.5" /> Save Contact
        </button>
        <button onClick={onCancel} className="flex items-center gap-1.5 px-3 py-1.5 border rounded text-sm hover:bg-slate-50">
          <X className="h-3.5 w-3.5" /> Cancel
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
    <div className="grid grid-cols-2 gap-1.5">
      {ACTIVE_LOBS.map((lob) => (
        <label key={lob} className="flex items-center gap-2 text-sm cursor-pointer">
          <input type="checkbox" checked={selected.includes(lob)} onChange={() => toggle(lob)} className="rounded border-slate-300 text-blue-600 focus:ring-blue-500" />
          {LOB_LABELS[lob]}
        </label>
      ))}
    </div>
  )
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
  isActive: boolean
  linesOfBusiness: PolicyLineOfBusiness[]
}

export function CarrierDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { canUploadAttachments, canDeleteAttachments } = usePermissions()

  const [editingInfo, setEditingInfo] = useState(false)
  const [infoForm, setInfoForm] = useState<InfoFormData>({
    name: '', naic: '', amBestRating: '', addressLine1: '', addressLine2: '',
    city: '', state: '', zipCode: '', website: '', isActive: true, linesOfBusiness: [],
  })

  const [showNewContact, setShowNewContact] = useState(false)
  const [newContactForm, setNewContactForm] = useState<ContactFormData>(emptyContactForm())
  const [editingContactId, setEditingContactId] = useState<string | null>(null)
  const [editContactForm, setEditContactForm] = useState<ContactFormData>(emptyContactForm())

  const [showAddCommission, setShowAddCommission] = useState(false)
  const [commissionForm, setCommissionForm] = useState({ lineOfBusiness: '' as string, commissionRate: '', smmRetentionRate: '', effectiveDate: new Date().toISOString().slice(0, 10) })
  const [expandedLobs, setExpandedLobs] = useState<Set<string>>(new Set())

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

  const addCommissionMutation = useMutation({
    mutationFn: () => createCarrierCommission(id!, {
      lineOfBusiness: commissionForm.lineOfBusiness || null,
      commissionRate: parseFloat(commissionForm.commissionRate) / 100,
      smmRetentionRate: parseFloat(commissionForm.smmRetentionRate) / 100,
      effectiveDate: commissionForm.effectiveDate,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['carrier-commissions', id] })
      setShowAddCommission(false)
      setCommissionForm({ lineOfBusiness: '', commissionRate: '', smmRetentionRate: '', effectiveDate: new Date().toISOString().slice(0, 10) })
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

  // ─── Info mutations ──────────────────────────────────────────────────────────

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
      isActive: carrier.isActive,
      linesOfBusiness: [...carrier.linesOfBusiness],
    })
    setEditingInfo(true)
  }

  // ─── Contact mutations ───────────────────────────────────────────────────────

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
  if (!carrier) return <div className="p-6 text-sm text-slate-500">Carrier not found.</div>

  const address = [carrier.addressLine1, carrier.addressLine2, [carrier.city, carrier.state].filter(Boolean).join(', '), carrier.zipCode].filter(Boolean).join(' ')

  return (
    <div className="p-6 space-y-6 max-w-4xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-slate-500">
        <Link to="/carriers" className="hover:text-slate-700 flex items-center gap-1">
          <ArrowLeft className="h-4 w-4" /> Carriers
        </Link>
        <span>/</span>
        <span className="text-slate-800 font-medium">{carrier.name}</span>
      </div>

      {/* Carrier info panel */}
      <div className="bg-white border rounded-lg p-5">
        <div className="flex items-start justify-between mb-4">
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-xl font-semibold text-slate-900">{carrier.name}</h1>
              <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${carrier.isActive ? 'bg-green-100 text-green-700' : 'bg-slate-100 text-slate-500'}`}>
                {carrier.isActive ? 'Active' : 'Inactive'}
              </span>
            </div>
            <div className="flex items-center gap-3 mt-1 text-sm text-slate-500">
              {carrier.naic && <span>NAIC {carrier.naic}</span>}
              {carrier.amBestRating && <span>AM Best: {carrier.amBestRating}</span>}
            </div>
          </div>
          {!editingInfo && (
            <button onClick={startEditInfo} className="flex items-center gap-1.5 px-3 py-1.5 border rounded text-sm hover:bg-slate-50 text-slate-600">
              <Pencil className="h-3.5 w-3.5" /> Edit
            </button>
          )}
        </div>

        {editingInfo ? (
          <div className="space-y-4">
            <div className="grid grid-cols-3 gap-3">
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Carrier Name *</label>
                <input value={infoForm.name} onChange={infoSet('name')} className="w-full border rounded px-2 py-1.5 text-sm" />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">NAIC #</label>
                <input value={infoForm.naic} onChange={infoSet('naic')} placeholder="e.g. 14788" className="w-full border rounded px-2 py-1.5 text-sm" />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">AM Best Rating</label>
                <input value={infoForm.amBestRating} onChange={infoSet('amBestRating')} placeholder="e.g. A+" className="w-full border rounded px-2 py-1.5 text-sm" />
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Street Address</label>
                <AddressAutocomplete
                  value={infoForm.addressLine1}
                  onChange={(val) => setInfoForm({ ...infoForm, addressLine1: val })}
                  onSelect={(c) => setInfoForm({ ...infoForm, addressLine1: c.addressLine1, city: c.city, state: c.state, zipCode: c.zipCode })}
                  placeholder="Start typing an address…"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Suite / Unit</label>
                <input value={infoForm.addressLine2} onChange={infoSet('addressLine2')} placeholder="Apt, Suite, Unit…" className="w-full border rounded px-2 py-1.5 text-sm" />
              </div>
            </div>

            <div className="grid grid-cols-3 gap-3">
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">City</label>
                <input value={infoForm.city} onChange={infoSet('city')} placeholder="City" className="w-full border rounded px-2 py-1.5 text-sm" />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">State</label>
                <input value={infoForm.state} onChange={infoSet('state')} maxLength={2} placeholder="TX" className="w-full border rounded px-2 py-1.5 text-sm uppercase" />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">ZIP</label>
                <input
                  value={infoForm.zipCode}
                  onChange={infoSet('zipCode')}
                  placeholder="78701"
                  className={`w-full border rounded px-2 py-1.5 text-sm ${zipError ? 'border-red-400' : ''}`}
                />
                {zipError && <p className="text-xs text-red-600 mt-0.5">Invalid ZIP code</p>}
              </div>
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">Website</label>
              <input value={infoForm.website} onChange={infoSet('website')} placeholder="https://example.com" className="w-full border rounded px-2 py-1.5 text-sm" />
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-600 mb-2">Lines of Business</label>
              <LobCheckboxes
                selected={infoForm.linesOfBusiness}
                onChange={(lobs) => setInfoForm({ ...infoForm, linesOfBusiness: lobs })}
              />
            </div>

            <div className="flex items-center gap-2">
              <input type="checkbox" id="carrier-active" checked={infoForm.isActive} onChange={infoSet('isActive')} className="rounded" />
              <label htmlFor="carrier-active" className="text-sm text-slate-600">Active</label>
            </div>

            <div className="flex gap-2">
              <button
                onClick={() => {
                  if (!infoForm.name.trim()) { toast.error('Name is required'); return }
                  if (infoForm.linesOfBusiness.length === 0) { toast.error('Select at least one line of business'); return }
                  if (zipError) { toast.error('Enter a valid ZIP code'); return }
                  updateInfoMutation.mutate()
                }}
                disabled={updateInfoMutation.isPending}
                className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-600 text-white rounded text-sm hover:bg-blue-700 disabled:opacity-50"
              >
                <Check className="h-3.5 w-3.5" /> Save
              </button>
              <button onClick={() => setEditingInfo(false)} className="flex items-center gap-1.5 px-3 py-1.5 border rounded text-sm hover:bg-slate-50">
                <X className="h-3.5 w-3.5" /> Cancel
              </button>
            </div>
          </div>
        ) : (
          <div className="space-y-3">
            {/* LOBs */}
            <div className="flex flex-wrap gap-1">
              {carrier.linesOfBusiness.map((lob) => (
                <span key={lob} className="px-2 py-0.5 text-xs rounded-full bg-blue-50 text-blue-700 border border-blue-100">
                  {LOB_LABELS[lob]}
                </span>
              ))}
              {carrier.linesOfBusiness.length === 0 && (
                <span className="text-xs text-slate-400">No lines of business configured</span>
              )}
            </div>
            {/* Address & Website */}
            {(address || carrier.website) && (
              <div className="flex flex-col gap-1">
                {address && (
                  <span className="flex items-center gap-1.5 text-sm text-slate-600">
                    <MapPin className="h-3.5 w-3.5 text-slate-400 shrink-0" /> {address}
                  </span>
                )}
                {carrier.website && (
                  <a
                    href={carrier.website.startsWith('http') ? carrier.website : `https://${carrier.website}`}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center gap-1.5 text-sm text-blue-600 hover:underline"
                  >
                    <Globe className="h-3.5 w-3.5 shrink-0" /> {carrier.website}
                  </a>
                )}
              </div>
            )}
          </div>
        )}
      </div>

      {/* Documents */}
      <div className="bg-white border rounded-lg p-5">
        <DocumentsSection entityType="Carrier" entityId={id!} canUpload={canUploadAttachments} canDelete={canDeleteAttachments} />
      </div>

      {/* Commission Schedules */}
      <div className="bg-white border rounded-lg p-5 space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-base font-semibold text-slate-800 flex items-center gap-2">
            <Percent className="h-4 w-4 text-slate-400" />
            Commission Schedules
          </h2>
          {!showAddCommission && (
            <button
              onClick={() => setShowAddCommission(true)}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-600 text-white rounded text-sm hover:bg-blue-700"
            >
              <Plus className="h-3.5 w-3.5" /> Add Rate
            </button>
          )}
        </div>

        {/* Add commission form */}
        {showAddCommission && (
          <div className="bg-slate-50 border rounded-lg p-4 space-y-3">
            <p className="text-sm font-medium text-slate-700">New Commission Rate</p>
            <div className="grid grid-cols-4 gap-3">
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Line of Business</label>
                <select
                  value={commissionForm.lineOfBusiness}
                  onChange={(e) => setCommissionForm({ ...commissionForm, lineOfBusiness: e.target.value })}
                  className="w-full border rounded px-2 py-1.5 text-sm bg-white"
                >
                  <option value="">All Lines (default)</option>
                  {ACTIVE_LOBS.map((lob) => (
                    <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Total Commission %</label>
                <div className="relative">
                  <input
                    type="number"
                    min="0"
                    max="100"
                    step="0.01"
                    value={commissionForm.commissionRate}
                    onChange={(e) => setCommissionForm({ ...commissionForm, commissionRate: e.target.value })}
                    placeholder="e.g. 15"
                    className="w-full border rounded px-2 py-1.5 text-sm pr-6"
                  />
                  <span className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 text-sm">%</span>
                </div>
                <p className="text-xs text-slate-400 mt-0.5">What carrier pays SMM total</p>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">SMM Retention %</label>
                <div className="relative">
                  <input
                    type="number"
                    min="0"
                    max={commissionForm.commissionRate || '100'}
                    step="0.01"
                    value={commissionForm.smmRetentionRate}
                    onChange={(e) => setCommissionForm({ ...commissionForm, smmRetentionRate: e.target.value })}
                    placeholder="e.g. 5"
                    className="w-full border rounded px-2 py-1.5 text-sm pr-6"
                  />
                  <span className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 text-sm">%</span>
                </div>
                <p className="text-xs text-slate-400 mt-0.5">Portion SMM keeps</p>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Effective Date</label>
                <input
                  type="date"
                  value={commissionForm.effectiveDate}
                  onChange={(e) => setCommissionForm({ ...commissionForm, effectiveDate: e.target.value })}
                  className="w-full border rounded px-2 py-1.5 text-sm"
                />
              </div>
            </div>
            <div className="flex gap-2">
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
                className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-600 text-white rounded text-sm hover:bg-blue-700 disabled:opacity-50"
              >
                <Check className="h-3.5 w-3.5" /> Save
              </button>
              <button
                onClick={() => setShowAddCommission(false)}
                className="flex items-center gap-1.5 px-3 py-1.5 border rounded text-sm hover:bg-slate-50"
              >
                <X className="h-3.5 w-3.5" /> Cancel
              </button>
            </div>
          </div>
        )}

        {/* Commission table grouped by LOB */}
        {commissions.length === 0 && !showAddCommission ? (
          <div className="text-center py-6 border border-dashed rounded-lg">
            <BanknoteIcon className="h-8 w-8 text-slate-300 mx-auto mb-2" />
            <p className="text-sm text-slate-500">No commission rates configured.</p>
            <button onClick={() => setShowAddCommission(true)} className="mt-2 text-sm text-blue-600 hover:underline">
              Add the first rate
            </button>
          </div>
        ) : (
          (() => {
            // Group by LOB (null → "All Lines")
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
              <div className="border rounded-lg overflow-hidden divide-y">
                {sortedKeys.map((key) => {
                  const rows = grouped[key]
                  const activeRow = rows.find((r) => r.isActive)
                  const lobLabel = key === '__all__' ? 'All Lines (default)' : (LOB_LABELS[key as PolicyLineOfBusiness] ?? key)
                  const isExpanded = expandedLobs.has(key)

                  return (
                    <div key={key}>
                      <div
                        className="flex items-center justify-between px-4 py-3 hover:bg-slate-50 cursor-pointer"
                        onClick={() => setExpandedLobs((prev) => { const next = new Set(prev); isExpanded ? next.delete(key) : next.add(key); return next })}
                      >
                        <div className="flex items-center gap-3">
                          <span className="text-sm font-medium text-slate-800">{lobLabel}</span>
                          {activeRow ? (
                            <span className="text-sm font-semibold text-emerald-700 bg-emerald-50 border border-emerald-100 rounded px-2 py-0.5">
                              {(activeRow.commissionRate * 100).toFixed(2)}% total · {(activeRow.smmRetentionRate * 100).toFixed(2)}% SMM
                            </span>
                          ) : (
                            <span className="text-xs text-slate-400 italic">no active rate</span>
                          )}
                          {activeRow && (
                            <span className="text-xs text-slate-400">eff. {activeRow.effectiveDate}</span>
                          )}
                        </div>
                        <div className="flex items-center gap-2">
                          <span className="text-xs text-slate-400">{rows.length} version{rows.length !== 1 ? 's' : ''}</span>
                          <span className="text-xs text-slate-400">{isExpanded ? '▲' : '▼'}</span>
                        </div>
                      </div>

                      {isExpanded && (
                        <div className="bg-slate-50 border-t">
                          <table className="w-full text-xs">
                            <thead>
                              <tr className="text-left text-slate-500 border-b">
                                <th className="px-4 py-2 font-medium">Total Rate</th>
                                <th className="px-4 py-2 font-medium">SMM Retention</th>
                                <th className="px-4 py-2 font-medium">Effective</th>
                                <th className="px-4 py-2 font-medium">Disabled</th>
                                <th className="px-4 py-2 font-medium">Status</th>
                                <th className="px-4 py-2" />
                              </tr>
                            </thead>
                            <tbody className="divide-y">
                              {rows.map((r) => (
                                <tr key={r.id} className="hover:bg-white">
                                  <td className="px-4 py-2 font-semibold text-slate-800">{(r.commissionRate * 100).toFixed(2)}%</td>
                                  <td className="px-4 py-2 text-slate-700">{(r.smmRetentionRate * 100).toFixed(2)}%</td>
                                  <td className="px-4 py-2 text-slate-600">{r.effectiveDate}</td>
                                  <td className="px-4 py-2 text-slate-500">{r.disabledDate ?? '—'}</td>
                                  <td className="px-4 py-2">
                                    {r.isActive ? (
                                      <span className="px-1.5 py-0.5 rounded bg-emerald-100 text-emerald-700 font-medium">Active</span>
                                    ) : (
                                      <span className="px-1.5 py-0.5 rounded bg-slate-100 text-slate-500">Disabled</span>
                                    )}
                                  </td>
                                  <td className="px-4 py-2 text-right">
                                    {r.isActive && (
                                      <button
                                        onClick={(e) => { e.stopPropagation(); if (confirm('Disable this commission rate?')) disableCommissionMutation.mutate(r.id) }}
                                        className="text-xs text-red-500 hover:text-red-700 hover:underline"
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

      {/* Contacts section */}
      <div className="bg-white border rounded-lg p-5 space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-base font-semibold text-slate-800">
            Contacts
            <span className="ml-2 text-xs font-normal text-slate-400">
              ({carrier.contacts.length})
            </span>
          </h2>
          {!showNewContact && (
            <button
              onClick={() => { setShowNewContact(true); setNewContactForm(emptyContactForm()); setEditingContactId(null) }}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-600 text-white rounded text-sm hover:bg-blue-700"
            >
              <Plus className="h-3.5 w-3.5" /> Add Contact
            </button>
          )}
        </div>

        {/* New contact form */}
        {showNewContact && (
          <div className="bg-slate-50 rounded-lg p-3 border">
            <ContactForm
              form={newContactForm}
              setForm={setNewContactForm}
              onSave={() => addContactMutation.mutate(formToContactInput(newContactForm))}
              onCancel={() => { setShowNewContact(false); setNewContactForm(emptyContactForm()) }}
              isPending={addContactMutation.isPending}
            />
          </div>
        )}

        {/* Contact list */}
        {carrier.contacts.length === 0 && !showNewContact ? (
          <div className="text-center py-6 border border-dashed rounded-lg">
            <UserCircle className="h-8 w-8 text-slate-300 mx-auto mb-2" />
            <p className="text-sm text-slate-500">No contacts yet.</p>
            <button onClick={() => setShowNewContact(true)} className="mt-2 text-sm text-blue-600 hover:underline">
              Add the first contact
            </button>
          </div>
        ) : (
          <div className="space-y-1">
            {carrier.contacts.map((contact) => {
              const fullName = [contact.firstName, contact.lastName].filter(Boolean).join(' ')
              const isEditingThis = editingContactId === contact.id

              return (
                <div key={contact.id}>
                  {isEditingThis ? (
                    <div className="bg-slate-50 rounded-lg p-3 border">
                      <ContactForm
                        form={editContactForm}
                        setForm={setEditContactForm}
                        onSave={() => updateContactMutation.mutate({ contactId: contact.id, data: formToContactInput(editContactForm) })}
                        onCancel={() => setEditingContactId(null)}
                        isPending={updateContactMutation.isPending}
                      />
                    </div>
                  ) : (
                    <div className="flex items-center justify-between py-2 px-2 rounded hover:bg-slate-50 group">
                      <div className="flex items-center gap-3">
                        <UserCircle className="h-8 w-8 text-slate-300 shrink-0" />
                        <div>
                          <div className="flex items-center gap-1.5">
                            <span className="text-sm font-medium text-slate-800">{fullName}</span>
                            {contact.isPrimary && (
                              <span className="flex items-center gap-0.5 text-xs text-amber-600">
                                <Star className="h-3 w-3" /> Primary
                              </span>
                            )}
                            {contact.title && (
                              <span className="text-xs text-slate-400">· {contact.title}</span>
                            )}
                          </div>
                          <div className="flex items-center gap-3 mt-0.5">
                            {contact.email && (
                              <a
                                href={`mailto:${contact.email}`}
                                onClick={(e) => e.stopPropagation()}
                                className="text-xs text-blue-600 hover:underline flex items-center gap-1"
                              >
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
                          onClick={() => { setEditingContactId(contact.id); setEditContactForm(contactToForm(contact)); setShowNewContact(false) }}
                          className="p-1 text-slate-400 hover:text-blue-600 rounded"
                        >
                          <Pencil className="h-3.5 w-3.5" />
                        </button>
                        <button
                          onClick={() => { if (confirm(`Delete contact ${fullName}?`)) deleteContactMutation.mutate(contact.id) }}
                          className="p-1 text-slate-400 hover:text-red-600 rounded"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
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
  )
}
