import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Plus, Pencil, Trash2, MapPin, Users, X, Check } from 'lucide-react'
import { toast } from 'sonner'
import { agentsApi } from '@/api/agents.api'
import type { AgentCreate } from '@/types/agent.types'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
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

  if (isLoading) return <LoadingSpinner />

  return (
    <div className="p-6 space-y-6">
      <PageHeader
        title="Agents"
        description="Manage agents, office locations, and contacts"
        actions={!showCreate ? (
          <button
            onClick={() => setShowCreate(true)}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg text-sm hover:bg-blue-700"
          >
            <Plus className="h-4 w-4" /> New Agent
          </button>
        ) : undefined}
      />

      {/* Quick-create panel */}
      {showCreate && (
        <div className="bg-white border rounded-lg p-4 space-y-3">
          <h3 className="font-medium text-sm text-slate-700">New Agent</h3>
          <p className="text-xs text-slate-500">Add offices and contacts after creating the agent.</p>
          <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">Name *</label>
              <input
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                className="w-full border rounded px-2 py-1.5 text-sm"
                placeholder="Agent name"
                autoFocus
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">Agency</label>
              <input
                value={form.agencyName}
                onChange={(e) => setForm({ ...form, agencyName: e.target.value })}
                className="w-full border rounded px-2 py-1.5 text-sm"
                placeholder="Agency name"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">License #</label>
              <input
                value={form.licenseNumber}
                onChange={(e) => setForm({ ...form, licenseNumber: e.target.value })}
                className="w-full border rounded px-2 py-1.5 text-sm"
                placeholder="License number"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">Email</label>
              <input
                value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })}
                type="text"
                placeholder="email@example.com"
                className={`w-full border rounded px-2 py-1.5 text-sm ${emailError ? 'border-red-400' : ''}`}
              />
              {emailError && <p className="text-xs text-red-600 mt-0.5">Enter a valid email</p>}
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
          <div className="flex gap-2 pt-1">
            <button
              onClick={handleCreate}
              disabled={createMutation.isPending}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-600 text-white rounded text-sm hover:bg-blue-700 disabled:opacity-50"
            >
              <Check className="h-3.5 w-3.5" /> Create & Manage Offices
            </button>
            <button
              onClick={() => { setShowCreate(false); setForm(emptyForm()) }}
              className="flex items-center gap-1.5 px-3 py-1.5 border rounded text-sm hover:bg-slate-50"
            >
              <X className="h-3.5 w-3.5" /> Cancel
            </button>
          </div>
        </div>
      )}

      {/* Agent list */}
      <div className="bg-white border rounded-lg overflow-hidden">
        {agents.length === 0 ? (
          <div className="p-8 text-center text-slate-500 text-sm">No agents yet. Add one to get started.</div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-slate-50 border-b">
              <tr>
                <th className="text-left px-4 py-3 font-medium text-slate-600">Name</th>
                <th className="text-left px-4 py-3 font-medium text-slate-600">Agency</th>
                <th className="text-left px-4 py-3 font-medium text-slate-600">License #</th>
                <th className="text-left px-4 py-3 font-medium text-slate-600">Email</th>
                <th className="text-left px-4 py-3 font-medium text-slate-600">Offices</th>
                <th className="text-left px-4 py-3 font-medium text-slate-600">Status</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y">
              {agents.map((a) => {
                const location = a.primaryCity && a.primaryState
                  ? `${a.primaryCity}, ${a.primaryState}`
                  : a.primaryCity ?? a.primaryState ?? null

                return (
                  <tr
                    key={a.id}
                    className="hover:bg-slate-50 cursor-pointer"
                    onClick={() => navigate(`/agents/${a.id}`)}
                  >
                    <td className="px-4 py-3 font-medium text-blue-700 hover:underline">{a.name}</td>
                    <td className="px-4 py-3 text-slate-600">{a.agencyName ?? '—'}</td>
                    <td className="px-4 py-3 text-slate-600">{a.licenseNumber ?? '—'}</td>
                    <td className="px-4 py-3 text-slate-600">{a.email ?? '—'}</td>
                    <td className="px-4 py-3">
                      <div className="flex flex-col gap-0.5">
                        {location && (
                          <span className="flex items-center gap-1 text-slate-500 text-xs">
                            <MapPin className="h-3 w-3" /> {location}
                          </span>
                        )}
                        {(a.locationCount > 0 || a.contactCount > 0) && (
                          <span className="flex items-center gap-1 text-slate-400 text-xs">
                            <Users className="h-3 w-3" />
                            {a.locationCount} {a.locationCount === 1 ? 'office' : 'offices'} · {a.contactCount} {a.contactCount === 1 ? 'contact' : 'contacts'}
                          </span>
                        )}
                        {a.locationCount === 0 && (
                          <span className="text-xs text-slate-400 italic">No offices added</span>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex px-2 py-0.5 rounded-full text-xs font-medium ${a.isActive ? 'bg-green-100 text-green-700' : 'bg-slate-100 text-slate-500'}`}>
                        {a.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                    <td className="px-4 py-3" onClick={(e) => e.stopPropagation()}>
                      <div className="flex items-center justify-end gap-1">
                        <button
                          onClick={() => navigate(`/agents/${a.id}`)}
                          className="p-1 text-slate-400 hover:text-blue-600 rounded"
                          title="Edit agent"
                        >
                          <Pencil className="h-4 w-4" />
                        </button>
                        <button
                          onClick={() => { if (confirm(`Delete ${a.name}?`)) deleteMutation.mutate(a.id) }}
                          className="p-1 text-slate-400 hover:text-red-600 rounded"
                          title="Delete agent"
                        >
                          <Trash2 className="h-4 w-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}
