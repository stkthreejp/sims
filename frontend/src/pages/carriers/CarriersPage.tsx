import { useState } from 'react'
import { useQuery, useMutation } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Plus, Pencil, Trash2, X, Check, Users } from 'lucide-react'
import { toast } from 'sonner'
import { carriersApi } from '@/api/carriers.api'
import { queryClient } from '@/lib/queryClient'
import { PageHeader } from '@/components/common/PageHeader'
import { LOB_LABELS, ACTIVE_LOBS } from '@/types/quote.types'
import type { CarrierCreate } from '@/types/carrier.types'
import type { PolicyLineOfBusiness } from '@/types/quote.types'

const EMPTY_FORM: CarrierCreate = { name: '', naic: '', amBestRating: '', defaultCurrencyCode: 'USD', linesOfBusiness: [] }

function LobCheckboxes({ selected, onChange }: { selected: PolicyLineOfBusiness[]; onChange: (lobs: PolicyLineOfBusiness[]) => void }) {
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

export function CarriersPage() {
  const navigate = useNavigate()
  const [showCreate, setShowCreate] = useState(false)
  const [form, setForm] = useState<CarrierCreate>(EMPTY_FORM)

  const { data: carriers, isLoading } = useQuery({
    queryKey: ['carriers'],
    queryFn: () => carriersApi.getAll(false),
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
    <div>
      <PageHeader
        title="Carriers"
        description="Manage carriers, lines of business, and contacts"
        actions={
          !showCreate && (
            <button
              onClick={() => setShowCreate(true)}
              className="flex items-center gap-1.5 px-3 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium rounded-md"
            >
              <Plus className="h-4 w-4" /> New Carrier
            </button>
          )
        }
      />

      <div className="space-y-3">
        {/* Quick-create form */}
        {showCreate && (
          <div className="bg-slate-50 border border-slate-200 rounded-lg p-4 space-y-4">
            <h3 className="text-sm font-medium text-slate-700">New Carrier</h3>
            <p className="text-xs text-slate-500">Add address and contacts after creating.</p>
            <div className="grid grid-cols-3 gap-3">
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Carrier Name <span className="text-red-500">*</span></label>
                <input value={form.name} onChange={set('name')} placeholder="e.g. Acuity Insurance" autoFocus className="w-full px-3 py-1.5 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">NAIC #</label>
                <input value={form.naic ?? ''} onChange={set('naic')} placeholder="e.g. 14788" className="w-full px-3 py-1.5 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">AM Best Rating</label>
                <input value={form.amBestRating ?? ''} onChange={set('amBestRating')} placeholder="e.g. A+" className="w-full px-3 py-1.5 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Currency</label>
                <input value={form.defaultCurrencyCode ?? 'USD'} onChange={set('defaultCurrencyCode')} maxLength={3} placeholder="USD" className="w-full px-3 py-1.5 border border-slate-300 rounded-md text-sm uppercase focus:outline-none focus:ring-2 focus:ring-blue-500" />
              </div>
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-2">Lines of Business Offered</label>
              <LobCheckboxes selected={form.linesOfBusiness} onChange={(lobs) => setForm({ ...form, linesOfBusiness: lobs })} />
            </div>
            <div className="flex gap-2">
              <button
                type="button"
                disabled={createMutation.isPending || !form.name.trim() || form.linesOfBusiness.length === 0}
                onClick={() => createMutation.mutate(form)}
                className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-600 text-white text-sm rounded-md hover:bg-blue-700 disabled:opacity-40"
              >
                <Check className="h-3.5 w-3.5" /> Create & Manage Contacts
              </button>
              <button type="button" onClick={() => { setShowCreate(false); setForm(EMPTY_FORM) }} className="flex items-center gap-1.5 px-3 py-1.5 border border-slate-300 text-sm rounded-md hover:bg-slate-50">
                <X className="h-3.5 w-3.5" /> Cancel
              </button>
            </div>
          </div>
        )}

        {isLoading && <p className="text-sm text-slate-400 py-8 text-center">Loading…</p>}

        {/* Carrier rows */}
        {carriers?.map((carrier) => (
          <div
            key={carrier.id}
            onClick={() => navigate(`/carriers/${carrier.id}`)}
            className="bg-white border border-slate-200 rounded-lg px-5 py-4 flex items-start justify-between gap-4 cursor-pointer hover:border-blue-200 hover:shadow-sm transition-all"
          >
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 mb-1">
                <span className="font-medium text-slate-900">{carrier.name}</span>
                {!carrier.isActive && (
                  <span className="px-1.5 py-0.5 text-xs rounded bg-slate-100 text-slate-500">Inactive</span>
                )}
                {carrier.naic && <span className="text-xs text-slate-400">NAIC {carrier.naic}</span>}
                {carrier.amBestRating && <span className="text-xs text-slate-400">AM Best: {carrier.amBestRating}</span>}
                {(carrier.city || carrier.state) && (
                  <span className="text-xs text-slate-400">
                    {[carrier.city, carrier.state].filter(Boolean).join(', ')}
                  </span>
                )}
              </div>
              <div className="flex items-center gap-3 flex-wrap">
                <div className="flex gap-1 flex-wrap">
                  {carrier.linesOfBusiness.map((lob) => (
                    <span key={lob} className="px-2 py-0.5 text-xs rounded-full bg-blue-50 text-blue-700 border border-blue-100">
                      {LOB_LABELS[lob]}
                    </span>
                  ))}
                  {carrier.linesOfBusiness.length === 0 && (
                    <span className="text-xs text-slate-400">No lines of business configured</span>
                  )}
                </div>
                {carrier.contactCount > 0 && (
                  <span className="flex items-center gap-1 text-xs text-slate-400">
                    <Users className="h-3 w-3" />
                    {carrier.contactCount} {carrier.contactCount === 1 ? 'contact' : 'contacts'}
                  </span>
                )}
              </div>
            </div>
            <div className="flex gap-1 shrink-0" onClick={(e) => e.stopPropagation()}>
              <button onClick={() => navigate(`/carriers/${carrier.id}`)} className="p-1.5 rounded hover:bg-slate-100" title="Edit">
                <Pencil className="h-4 w-4 text-slate-400 hover:text-slate-700" />
              </button>
              <button
                onClick={() => { if (confirm(`Delete ${carrier.name}?`)) deleteMutation.mutate(carrier.id) }}
                className="p-1.5 rounded hover:bg-slate-100"
                title="Delete"
              >
                <Trash2 className="h-4 w-4 text-slate-400 hover:text-red-500" />
              </button>
            </div>
          </div>
        ))}

        {!isLoading && carriers?.length === 0 && (
          <div className="text-center py-12 text-slate-400 text-sm">
            No carriers yet. Add your first carrier above.
          </div>
        )}
      </div>
    </div>
  )
}
