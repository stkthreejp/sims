import { useState } from 'react'
import { useQuery, useMutation } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Plus, Check, X, Users, Trash2, AlertTriangle } from 'lucide-react'
import { toast } from 'sonner'
import { carriersApi } from '@/api/carriers.api'
import { queryClient } from '@/lib/queryClient'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { EmptyState } from '@/components/common/EmptyState'
import { LOB_LABELS, ACTIVE_LOBS } from '@/types/quote.types'
import type { CarrierCreate } from '@/types/carrier.types'
import type { PolicyLineOfBusiness } from '@/types/quote.types'
import { Building2 } from 'lucide-react'

const EMPTY_FORM: CarrierCreate = { name: '', naic: '', amBestRating: '', defaultCurrencyCode: 'USD', linesOfBusiness: [] }

function StatCard({ label, value, icon, warn = false }: { label: string; value: number; icon?: React.ReactNode; warn?: boolean }) {
  return (
    <div style={{
      background: warn ? 'var(--warn-bg)' : 'var(--surface)',
      border: `1px solid ${warn ? 'var(--warn-fg)' : 'var(--border)'}`,
      borderRadius: 8,
      padding: '10px 14px',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 5, fontSize: 11.5, color: warn ? 'var(--warn-fg)' : 'var(--ink-3)', marginBottom: 4 }}>
        {icon}{label}
      </div>
      <div style={{ fontSize: 22, fontWeight: 700, color: warn ? 'var(--warn-fg)' : 'var(--ink-1)', lineHeight: 1.2 }}>{value}</div>
    </div>
  )
}

function LobCheckboxes({ selected, onChange }: { selected: PolicyLineOfBusiness[]; onChange: (lobs: PolicyLineOfBusiness[]) => void }) {
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

export function CarriersPage() {
  const navigate = useNavigate()
  const [showCreate, setShowCreate] = useState(false)
  const [form, setForm] = useState<CarrierCreate>(EMPTY_FORM)

  const { data: carriers, isLoading } = useQuery({
    queryKey: ['carriers'],
    queryFn: () => carriersApi.getAll(false),
  })

  const { data: stats } = useQuery({
    queryKey: ['carriers', 'summary-stats'],
    queryFn: () => carriersApi.getSummaryStats(),
  })

  const createMutation = useMutation({
    mutationFn: carriersApi.create,
    onSuccess: (carrier) => {
      toast.success('Carrier created')
      queryClient.invalidateQueries({ queryKey: ['carriers'] })
      setShowCreate(false)
      setForm(EMPTY_FORM)
      navigate(`/carriers/${carrier.id}`)
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to create carrier'),
  })

  const deleteMutation = useMutation({
    mutationFn: carriersApi.delete,
    onSuccess: () => {
      toast.success('Carrier deleted')
      queryClient.invalidateQueries({ queryKey: ['carriers'] })
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Cannot delete — carrier has policies. Deactivate instead.'),
  })

  const set = (k: keyof CarrierCreate) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm({ ...form, [k]: e.target.value })

  return (
    <div className="subs-wrap">
      <div className="subs-page-head">
        <PageHeader title="Carriers" />
        <button onClick={() => setShowCreate(true)} className="sd-btn primary">
          <Plus style={{ width: 13, height: 13 }} />
          New Carrier
        </button>
      </div>

      {stats && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 10, marginBottom: 14 }}>
          <StatCard label="Total Carriers" value={stats.totalCarriers} />
          <StatCard label="Active Lines of Business" value={stats.activeLobCount} />
          <StatCard
            label="Pending Guideline Reviews"
            value={stats.pendingGuidelineReviews}
            icon={<AlertTriangle style={{ width: 13, height: 13 }} />}
            warn={stats.pendingGuidelineReviews > 0}
          />
        </div>
      )}

      <div className="subs-table-card">
        {isLoading ? (
          <LoadingSpinner />
        ) : carriers?.length === 0 ? (
          <EmptyState icon={Building2} title="No carriers yet" description="Add your first carrier to get started." />
        ) : (
          <table className="subs-table">
            <thead>
              <tr>
                <th className="subs-th">Name</th>
                <th className="subs-th">Lines of Business</th>
                <th className="subs-th">Location</th>
                <th className="subs-th">Contacts</th>
                <th className="subs-th">Status</th>
                <th className="subs-th" style={{ width: 40 }} />
              </tr>
            </thead>
            <tbody>
              {carriers?.map((carrier) => (
                <tr key={carrier.id} className="subs-row" onClick={() => navigate(`/carriers/${carrier.id}`)}>
                  <td>
                    <div style={{ fontWeight: 600, color: 'var(--ink)' }}>{carrier.name}</div>
                    {(carrier.naic || carrier.amBestRating) && (
                      <div style={{ fontSize: 11.5, color: 'var(--ink-4)', marginTop: 2 }}>
                        {carrier.naic && `NAIC ${carrier.naic}`}
                        {carrier.naic && carrier.amBestRating && ' · '}
                        {carrier.amBestRating && `AM Best: ${carrier.amBestRating}`}
                      </div>
                    )}
                  </td>
                  <td>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 3 }}>
                      {carrier.linesOfBusiness.map((lob) => (
                        <span key={lob} className="sd-lob">{LOB_LABELS[lob]}</span>
                      ))}
                      {carrier.linesOfBusiness.length === 0 && (
                        <span style={{ fontSize: 12, color: 'var(--ink-4)' }}>—</span>
                      )}
                    </div>
                  </td>
                  <td style={{ color: 'var(--ink-2)' }}>
                    {carrier.city && carrier.state ? `${carrier.city}, ${carrier.state}` : (carrier.city || carrier.state || '—')}
                  </td>
                  <td>
                    {carrier.contactCount > 0 ? (
                      <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, fontSize: 12, color: 'var(--ink-3)' }}>
                        <Users style={{ width: 12, height: 12 }} />
                        {carrier.contactCount}
                      </span>
                    ) : (
                      <span style={{ color: 'var(--ink-4)', fontSize: 12 }}>—</span>
                    )}
                  </td>
                  <td>
                    <span className={`sd-pill ${carrier.isActive ? 'good' : 'withdrawn'}`}>
                      {carrier.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td onClick={(e) => e.stopPropagation()}>
                    <button
                      onClick={() => { if (confirm(`Delete ${carrier.name}?`)) deleteMutation.mutate(carrier.id) }}
                      className="sims-icon-btn"
                      title="Delete carrier"
                      style={{ color: 'var(--ink-4)' }}
                    >
                      <Trash2 style={{ width: 13, height: 13 }} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* New Carrier modal */}
      {showCreate && (
        <div className="sims-modal-backdrop" onClick={() => { setShowCreate(false); setForm(EMPTY_FORM) }}>
          <div className="sims-modal" style={{ maxWidth: 560 }} onClick={(e) => e.stopPropagation()}>
            <div className="sims-modal-head">
              <span>New Carrier</span>
              <button className="sims-icon-btn" onClick={() => { setShowCreate(false); setForm(EMPTY_FORM) }}>
                <X style={{ width: 14, height: 14 }} />
              </button>
            </div>
            <div className="sims-modal-body">
              <p style={{ fontSize: 12.5, color: 'var(--ink-3)', marginBottom: 14 }}>
                Add address and contacts after creating.
              </p>
              <div className="sims-fields" style={{ gridTemplateColumns: 'repeat(3, 1fr)', marginBottom: 12 }}>
                <div style={{ gridColumn: '1 / -1' }}>
                  <label className="sims-field-label">Carrier Name *</label>
                  <input value={form.name} onChange={set('name')} placeholder="e.g. Acuity Insurance" autoFocus className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">NAIC #</label>
                  <input value={form.naic ?? ''} onChange={set('naic')} placeholder="14788" className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">AM Best Rating</label>
                  <input value={form.amBestRating ?? ''} onChange={set('amBestRating')} placeholder="A+" className="sims-input" />
                </div>
                <div>
                  <label className="sims-field-label">Currency</label>
                  <input value={form.defaultCurrencyCode ?? 'USD'} onChange={set('defaultCurrencyCode')} maxLength={3} placeholder="USD" className="sims-input" style={{ textTransform: 'uppercase' }} />
                </div>
              </div>
              <div>
                <label className="sims-field-label" style={{ marginBottom: 8 }}>Lines of Business</label>
                <LobCheckboxes selected={form.linesOfBusiness} onChange={(lobs) => setForm({ ...form, linesOfBusiness: lobs })} />
              </div>
            </div>
            <div className="sims-modal-foot">
              <button
                onClick={() => { setShowCreate(false); setForm(EMPTY_FORM) }}
                className="sd-btn outline sm"
              >
                <X style={{ width: 12, height: 12 }} /> Cancel
              </button>
              <button
                disabled={createMutation.isPending || !form.name.trim() || form.linesOfBusiness.length === 0}
                onClick={() => createMutation.mutate(form)}
                className="sd-btn primary sm"
              >
                <Check style={{ width: 12, height: 12 }} /> Create & Manage
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
