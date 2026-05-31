import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Plus, Trash2, X, Check, MapPin, Users, UserCircle } from 'lucide-react'
import { toast } from 'sonner'
import { agentsApi } from '@/api/agents.api'
import type { AgentCreate } from '@/types/agent.types'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { EmptyState } from '@/components/common/EmptyState'
import { isValidEmail, isValidPhone, formatPhoneInput } from '@/lib/validators'

type NewAgentForm = {
  name: string
  agencyName: string
  licenseNumber: string
  email: string
  phone: string
}

const emptyForm = (): NewAgentForm => ({ name: '', agencyName: '', licenseNumber: '', email: '', phone: '' })

export function AgentsPage() {
  const qc = useQueryClient()
  const navigate = useNavigate()
  const [showCreate, setShowCreate] = useState(false)
  const [form, setForm] = useState<NewAgentForm>(emptyForm())

  const { data: agents = [], isLoading } = useQuery({
    queryKey: ['agents'],
    queryFn: () => agentsApi.getAll(),
  })

  const createMutation = useMutation({
    mutationFn: (data: AgentCreate) => agentsApi.create(data),
    onSuccess: (agent) => {
      qc.invalidateQueries({ queryKey: ['agents'] })
      setShowCreate(false)
      setForm(emptyForm())
      toast.success('Agent created')
      navigate(`/agents/${agent.id}`)
    },
    onError: () => toast.error('Failed to create agent'),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => agentsApi.delete(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['agents'] })
      toast.success('Agent deleted')
    },
    onError: (err: any) => {
      const msg = err?.response?.data?.errorCode === 'HAS_SUBMISSIONS'
        ? 'Cannot delete — agent has submissions'
        : 'Failed to delete agent'
      toast.error(msg)
    },
  })

  const handleCreate = () => {
    if (!form.name.trim()) { toast.error('Name is required'); return }
    if (form.email && !isValidEmail(form.email)) { toast.error('Enter a valid email address'); return }
    if (form.phone && !isValidPhone(form.phone)) { toast.error('Enter a valid 10-digit phone number'); return }
    createMutation.mutate({
      name: form.name.trim(),
      agencyName: form.agencyName || undefined,
      licenseNumber: form.licenseNumber || undefined,
      email: form.email || undefined,
      phone: form.phone || undefined,
    })
  }

  const emailError = form.email && !isValidEmail(form.email)
  const phoneError = form.phone && !isValidPhone(form.phone)

  return (
    <div className="subs-wrap">
      <div className="subs-page-head">
        <PageHeader title="Agents" />
        <button onClick={() => setShowCreate(true)} className="sd-btn primary">
          <Plus style={{ width: 13, height: 13 }} />
          New Agent
        </button>
      </div>

      <div className="subs-table-card">
        {isLoading ? (
          <LoadingSpinner />
        ) : agents.length === 0 ? (
          <EmptyState icon={UserCircle} title="No agents yet" description="Add your first agent to get started." />
        ) : (
          <table className="subs-table">
            <thead>
              <tr>
                <th className="subs-th">Name</th>
                <th className="subs-th">Agency</th>
                <th className="subs-th">License #</th>
                <th className="subs-th">Contact</th>
                <th className="subs-th">Offices</th>
                <th className="subs-th">Status</th>
                <th className="subs-th" style={{ width: 40 }} />
              </tr>
            </thead>
            <tbody>
              {agents.map((a) => {
                const location = a.primaryCity && a.primaryState
                  ? `${a.primaryCity}, ${a.primaryState}`
                  : a.primaryCity ?? a.primaryState ?? null

                return (
                  <tr key={a.id} className="subs-row" onClick={() => navigate(`/agents/${a.id}`)}>
                    <td style={{ fontWeight: 600, color: 'var(--accent-ink)' }}>{a.name}</td>
                    <td style={{ color: 'var(--ink-2)' }}>{a.agencyName ?? '—'}</td>
                    <td style={{ color: 'var(--ink-3)', fontFamily: 'var(--font-mono)', fontSize: 12 }}>{a.licenseNumber ?? '—'}</td>
                    <td style={{ color: 'var(--ink-2)' }}>{a.email ?? '—'}</td>
                    <td>
                      {location && (
                        <div style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 12, color: 'var(--ink-3)' }}>
                          <MapPin style={{ width: 11, height: 11 }} /> {location}
                        </div>
                      )}
                      {(a.locationCount > 0 || a.contactCount > 0) && (
                        <div style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 11.5, color: 'var(--ink-4)', marginTop: 2 }}>
                          <Users style={{ width: 11, height: 11 }} />
                          {a.locationCount} {a.locationCount === 1 ? 'office' : 'offices'} · {a.contactCount} {a.contactCount === 1 ? 'contact' : 'contacts'}
                        </div>
                      )}
                    </td>
                    <td>
                      <span className={`sd-pill ${a.isActive ? 'good' : 'withdrawn'}`}>
                        {a.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                    <td onClick={(e) => e.stopPropagation()}>
                      <button
                        onClick={() => { if (confirm(`Delete ${a.name}?`)) deleteMutation.mutate(a.id) }}
                        className="sims-icon-btn"
                        title="Delete agent"
                        style={{ color: 'var(--ink-4)' }}
                      >
                        <Trash2 style={{ width: 13, height: 13 }} />
                      </button>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        )}
      </div>

      {/* New Agent modal */}
      {showCreate && (
        <div className="sims-modal-backdrop" onClick={() => { setShowCreate(false); setForm(emptyForm()) }}>
          <div className="sims-modal" style={{ maxWidth: 520 }} onClick={(e) => e.stopPropagation()}>
            <div className="sims-modal-head">
              <span>New Agent</span>
              <button className="sims-icon-btn" onClick={() => { setShowCreate(false); setForm(emptyForm()) }}>
                <X style={{ width: 14, height: 14 }} />
              </button>
            </div>
            <div className="sims-modal-body">
              <p style={{ fontSize: 12.5, color: 'var(--ink-3)', marginBottom: 14 }}>
                Add offices and contacts after creating the agent.
              </p>
              <div className="sims-fields" style={{ gridTemplateColumns: 'repeat(3, 1fr)' }}>
                <div style={{ gridColumn: '1 / -1' }}>
                  <label className="sims-field-label">Name *</label>
                  <input
                    value={form.name}
                    onChange={(e) => setForm({ ...form, name: e.target.value })}
                    className="sims-input"
                    placeholder="Agent name"
                    autoFocus
                  />
                </div>
                <div>
                  <label className="sims-field-label">Agency</label>
                  <input value={form.agencyName} onChange={(e) => setForm({ ...form, agencyName: e.target.value })} className="sims-input" placeholder="Agency name" />
                </div>
                <div>
                  <label className="sims-field-label">License #</label>
                  <input value={form.licenseNumber} onChange={(e) => setForm({ ...form, licenseNumber: e.target.value })} className="sims-input" placeholder="License number" />
                </div>
                <div style={{ gridColumn: '1 / -1' }}>
                  <div className="sims-fields" style={{ gridTemplateColumns: '1fr 1fr' }}>
                    <div>
                      <label className="sims-field-label">Email</label>
                      <input value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} type="text" placeholder="email@example.com" className="sims-input" />
                      {emailError && <p style={{ fontSize: 11.5, color: 'var(--bad-fg)', marginTop: 2 }}>Enter a valid email</p>}
                    </div>
                    <div>
                      <label className="sims-field-label">Phone</label>
                      <input value={form.phone} onChange={(e) => setForm({ ...form, phone: formatPhoneInput(e.target.value) })} type="text" placeholder="(555) 123-4567" className="sims-input" />
                      {phoneError && <p style={{ fontSize: 11.5, color: 'var(--bad-fg)', marginTop: 2 }}>Enter a valid 10-digit number</p>}
                    </div>
                  </div>
                </div>
              </div>
            </div>
            <div className="sims-modal-foot">
              <button onClick={() => { setShowCreate(false); setForm(emptyForm()) }} className="sd-btn outline sm">
                <X style={{ width: 12, height: 12 }} /> Cancel
              </button>
              <button onClick={handleCreate} disabled={createMutation.isPending} className="sd-btn primary sm">
                <Check style={{ width: 12, height: 12 }} /> Create & Manage Offices
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
