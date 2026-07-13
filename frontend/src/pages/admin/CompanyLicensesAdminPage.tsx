import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BadgeCheck, Pencil, Plus, Save, Trash2, Upload, X } from 'lucide-react'
import { toast } from 'sonner'
import { companyLicensesApi } from '@/api/companyLicenses.api'
import { parseCsv } from '@/lib/csv'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { CompanyLicense, UpsertCompanyLicense } from '@/types/companyLicense.types'

const emptyForm: UpsertCompanyLicense = {
  holderName: '',
  licenseNumber: '',
  licenseState: '',
  licenseType: 'Surplus Lines Broker',
  effectiveDate: '',
  expirationDate: '',
  addressLine1: '',
  addressLine2: '',
  city: '',
  state: '',
  zipCode: '',
  country: 'USA',
  isActive: true,
  notes: '',
}

// CSV import: header (case/space/underscore-insensitive) -> field.
const HEADER_MAP: Record<string, keyof UpsertCompanyLicense> = {
  holdername: 'holderName',
  licensenumber: 'licenseNumber',
  licensestate: 'licenseState',
  licensetype: 'licenseType',
  effectivedate: 'effectiveDate',
  expirationdate: 'expirationDate',
  isactive: 'isActive',
  notes: 'notes',
  addressline1: 'addressLine1',
  addressline2: 'addressLine2',
  city: 'city',
  state: 'state',
  zipcode: 'zipCode',
  country: 'country',
}

function rowsFromCsv(text: string): UpsertCompanyLicense[] {
  const grid = parseCsv(text)
  if (grid.length < 2) return []
  const headers = grid[0].map((h) => HEADER_MAP[h.replace(/[\s_]/g, '').toLowerCase()] ?? null)
  return grid.slice(1).map((cells) => {
    const row: UpsertCompanyLicense = {
      holderName: '', licenseNumber: '', licenseState: '', licenseType: '',
      effectiveDate: null, expirationDate: null, addressLine1: '', addressLine2: '',
      city: '', state: '', zipCode: '', country: 'USA', isActive: true, notes: '',
    }
    headers.forEach((key, idx) => {
      if (!key) return
      const val = (cells[idx] ?? '').trim()
      if (key === 'isActive') row.isActive = !/^(false|0|no|n)$/i.test(val)
      else if (key === 'effectiveDate' || key === 'expirationDate') row[key] = val || null
      else (row as unknown as Record<string, string>)[key] = val
    })
    return row
  })
}

export function CompanyLicensesAdminPage() {
  const qc = useQueryClient()
  const [form, setForm] = useState<UpsertCompanyLicense>(emptyForm)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)

  const { data: licenses = [], isLoading } = useQuery({
    queryKey: ['company-licenses'],
    queryFn: () => companyLicensesApi.getAll(true),
  })

  const invalidate = () => qc.invalidateQueries({ queryKey: ['company-licenses'] })
  const onError = (e: unknown) =>
    toast.error((e as { response?: { data?: { errorMessage?: string } } })?.response?.data?.errorMessage ?? 'Could not save license')

  const importMutation = useMutation({
    mutationFn: (rows: UpsertCompanyLicense[]) => companyLicensesApi.import(rows),
    onSuccess: (res) => {
      invalidate()
      const parts = [`Imported ${res.created}`]
      if (res.skipped) parts.push(`${res.skipped} skipped (already present)`)
      if (res.errors.length) parts.push(`${res.errors.length} error(s)`)
      toast.success(parts.join(' · '))
      if (res.errors.length) console.warn('Company license import errors:', res.errors)
    },
    onError,
  })

  const handleImportFile = async (file: File) => {
    const rows = rowsFromCsv(await file.text())
    if (rows.length === 0) {
      toast.error('No rows found — the CSV needs a header row (holderName, licenseNumber, licenseState, licenseType, …) and at least one data row')
      return
    }
    importMutation.mutate(rows)
  }

  const resetForm = () => { setForm(emptyForm); setEditingId(null); setShowForm(false) }

  const saveMutation = useMutation({
    mutationFn: () => {
      const payload: UpsertCompanyLicense = {
        ...form,
        effectiveDate: form.effectiveDate || null,
        expirationDate: form.expirationDate || null,
      }
      return editingId ? companyLicensesApi.update(editingId, payload) : companyLicensesApi.create(payload)
    },
    onSuccess: () => { invalidate(); resetForm(); toast.success(editingId ? 'License updated' : 'License added') },
    onError,
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => companyLicensesApi.delete(id),
    onSuccess: () => { invalidate(); toast.success('License removed') },
    onError: () => toast.error('Could not remove license'),
  })

  const startEdit = (l: CompanyLicense) => {
    setEditingId(l.id)
    setShowForm(true)
    setForm({
      holderName: l.holderName,
      licenseNumber: l.licenseNumber,
      licenseState: l.licenseState,
      licenseType: l.licenseType,
      effectiveDate: l.effectiveDate ?? '',
      expirationDate: l.expirationDate ?? '',
      addressLine1: l.addressLine1 ?? '',
      addressLine2: l.addressLine2 ?? '',
      city: l.city ?? '',
      state: l.state ?? '',
      zipCode: l.zipCode ?? '',
      country: l.country ?? 'USA',
      isActive: l.isActive,
      notes: l.notes ?? '',
    })
  }

  if (isLoading) return <LoadingSpinner />

  return (
    <div className="mx-auto max-w-5xl p-6">
      <div className="mb-5 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <BadgeCheck className="h-5 w-5" style={{ color: 'var(--brand)' }} />
          <h1 className="text-lg font-semibold" style={{ color: 'var(--ink)' }}>Company Licenses</h1>
        </div>
        {!showForm && (
          <div className="flex items-center gap-2">
            <label className="sd-btn outline cursor-pointer" title="Upload a CSV (holderName, licenseNumber, licenseState, licenseType, effectiveDate, expirationDate, isActive, notes)">
              <Upload className="h-4 w-4" /> {importMutation.isPending ? 'Importing…' : 'Import CSV'}
              <input
                type="file"
                accept=".csv,text/csv"
                className="hidden"
                disabled={importMutation.isPending}
                onChange={(e) => { const f = e.target.files?.[0]; if (f) handleImportFile(f); e.target.value = '' }}
              />
            </label>
            <button onClick={() => { setForm(emptyForm); setEditingId(null); setShowForm(true) }} className="sd-btn primary">
              <Plus className="h-4 w-4" /> Add license
            </button>
          </div>
        )}
      </div>
      <p className="mb-5 text-sm" style={{ color: 'var(--ink-3)' }}>
        Store SMM's and individual brokers' surplus-lines licenses once. Surplus Lines setup references a
        license instead of re-keying broker and license details per state.
      </p>

      {showForm && (
        <div className="sd-card mb-6">
          <div className="sd-card-head"><h3>{editingId ? 'Edit license' : 'New license'}</h3></div>
          <div className="sd-card-body grid gap-3">
            <div className="grid grid-cols-2 gap-3">
              <label className="block">
                <span className="sims-field-label">License holder</span>
                <input value={form.holderName} onChange={(e) => setForm((f) => ({ ...f, holderName: e.target.value }))} className="sims-input" placeholder="Specialty Market Managers, LLC" />
              </label>
              <label className="block">
                <span className="sims-field-label">License type</span>
                <input value={form.licenseType} onChange={(e) => setForm((f) => ({ ...f, licenseType: e.target.value }))} className="sims-input" placeholder="Surplus Lines Broker" />
              </label>
            </div>
            <div className="grid grid-cols-3 gap-3">
              <label className="block">
                <span className="sims-field-label">License number</span>
                <input value={form.licenseNumber} onChange={(e) => setForm((f) => ({ ...f, licenseNumber: e.target.value }))} className="sims-input" />
              </label>
              <label className="block">
                <span className="sims-field-label">License state</span>
                <input value={form.licenseState} onChange={(e) => setForm((f) => ({ ...f, licenseState: e.target.value.toUpperCase().slice(0, 2) }))} className="sims-input" placeholder="TX" maxLength={2} />
              </label>
              <div />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <label className="block">
                <span className="sims-field-label">Effective date</span>
                <input type="date" value={form.effectiveDate ?? ''} onChange={(e) => setForm((f) => ({ ...f, effectiveDate: e.target.value }))} className="sims-input" />
              </label>
              <label className="block">
                <span className="sims-field-label">Expiration date</span>
                <input type="date" value={form.expirationDate ?? ''} onChange={(e) => setForm((f) => ({ ...f, expirationDate: e.target.value }))} className="sims-input" />
              </label>
            </div>
            <label className="block">
              <span className="sims-field-label">Address line 1</span>
              <input value={form.addressLine1 ?? ''} onChange={(e) => setForm((f) => ({ ...f, addressLine1: e.target.value }))} className="sims-input" />
            </label>
            <label className="block">
              <span className="sims-field-label">Address line 2</span>
              <input value={form.addressLine2 ?? ''} onChange={(e) => setForm((f) => ({ ...f, addressLine2: e.target.value }))} className="sims-input" />
            </label>
            <div className="grid grid-cols-3 gap-3">
              <label className="block">
                <span className="sims-field-label">City</span>
                <input value={form.city ?? ''} onChange={(e) => setForm((f) => ({ ...f, city: e.target.value }))} className="sims-input" />
              </label>
              <label className="block">
                <span className="sims-field-label">State</span>
                <input value={form.state ?? ''} onChange={(e) => setForm((f) => ({ ...f, state: e.target.value.toUpperCase().slice(0, 2) }))} className="sims-input" placeholder="TX" maxLength={2} />
              </label>
              <label className="block">
                <span className="sims-field-label">ZIP</span>
                <input value={form.zipCode ?? ''} onChange={(e) => setForm((f) => ({ ...f, zipCode: e.target.value }))} className="sims-input" />
              </label>
            </div>
            <label className="block">
              <span className="sims-field-label">Notes</span>
              <input value={form.notes ?? ''} onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))} className="sims-input" placeholder="Optional" />
            </label>
            <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--ink-2)' }}>
              <input type="checkbox" checked={form.isActive} onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.checked }))} className="h-4 w-4 rounded" />
              Active
            </label>
            <div className="flex gap-2">
              <button
                onClick={() => saveMutation.mutate()}
                disabled={saveMutation.isPending || !form.holderName || !form.licenseNumber || form.licenseState.length !== 2 || !form.licenseType}
                className="sd-btn primary disabled:opacity-50"
              >
                <Save className="h-4 w-4" /> {editingId ? 'Save' : 'Add'}
              </button>
              <button onClick={resetForm} className="sd-btn outline"><X className="h-4 w-4" /> Cancel</button>
            </div>
          </div>
        </div>
      )}

      <div className="sd-card">
        <div className="sd-card-body p-0">
          <table className="w-full text-sm">
            <thead>
              <tr style={{ color: 'var(--ink-3)' }} className="text-left text-xs uppercase tracking-wide">
                <th className="px-4 py-2">Holder</th>
                <th className="px-4 py-2">Type</th>
                <th className="px-4 py-2">Number</th>
                <th className="px-4 py-2">State</th>
                <th className="px-4 py-2">Expires</th>
                <th className="px-4 py-2">Status</th>
                <th className="px-4 py-2"></th>
              </tr>
            </thead>
            <tbody>
              {licenses.length === 0 && (
                <tr><td colSpan={7} className="px-4 py-6 text-center" style={{ color: 'var(--ink-3)' }}>No licenses yet.</td></tr>
              )}
              {licenses.map((l) => (
                <tr key={l.id} className="border-t" style={{ borderColor: 'var(--line)' }}>
                  <td className="px-4 py-2" style={{ color: 'var(--ink)' }}>{l.holderName}</td>
                  <td className="px-4 py-2" style={{ color: 'var(--ink-2)' }}>{l.licenseType}</td>
                  <td className="px-4 py-2" style={{ color: 'var(--ink-2)' }}>{l.licenseNumber}</td>
                  <td className="px-4 py-2" style={{ color: 'var(--ink-2)' }}>{l.licenseState}</td>
                  <td className="px-4 py-2" style={{ color: 'var(--ink-2)' }}>{l.expirationDate ?? '—'}</td>
                  <td className="px-4 py-2">
                    <span className={`sd-pill ${l.isActive ? 'good' : 'withdrawn'}`}>{l.isActive ? 'Active' : 'Inactive'}</span>
                  </td>
                  <td className="px-4 py-2">
                    <div className="flex justify-end gap-1">
                      <button onClick={() => startEdit(l)} className="sims-icon-btn" title="Edit"><Pencil className="h-3.5 w-3.5" /></button>
                      <button onClick={() => { if (confirm(`Remove ${l.holderName} (${l.licenseState})?`)) deleteMutation.mutate(l.id) }} className="sims-icon-btn" title="Remove"><Trash2 className="h-3.5 w-3.5" /></button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}
