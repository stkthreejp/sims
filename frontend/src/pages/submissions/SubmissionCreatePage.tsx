import { useNavigate, useSearchParams, Link } from 'react-router-dom'
import { useQuery, useMutation } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { ArrowLeft, Search, X } from 'lucide-react'
import { toast } from 'sonner'
import { submissionsApi } from '@/api/submissions.api'
import { usersApi } from '@/api/users.api'
import { agentsApi } from '@/api/agents.api'
import { insuredsApi } from '@/api/insureds.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { SubmissionCreate } from '@/types/submission.types'
import type { InsuredListItem } from '@/types/insured.types'
import { ACTIVE_LOBS, LOB_LABELS } from '@/types/quote.types'

// ── Insured search combobox ───────────────────────────────────────────────────

function InsuredCombobox({
  value,
  onChange,
}: {
  value: string
  onChange: (id: string, name: string) => void
}) {
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const [selectedName, setSelectedName] = useState('')
  const containerRef = useRef<HTMLDivElement>(null)

  const { data: results = [], isFetching } = useQuery({
    queryKey: ['insureds', 'search', query],
    queryFn: () => insuredsApi.getAll({ search: query, pageSize: 10 }),
    enabled: query.length >= 2,
    select: (d) => d.items,
  })

  const handleSelect = (insured: InsuredListItem) => {
    setSelectedName(insured.displayName)
    setQuery('')
    setOpen(false)
    onChange(insured.id, insured.displayName)
  }

  const handleClear = () => {
    setSelectedName('')
    setQuery('')
    onChange('', '')
  }

  return (
    <div ref={containerRef} className="relative">
      {value && selectedName ? (
        <div className="flex items-center justify-between border rounded px-3 py-2 bg-slate-50">
          <span className="text-sm text-slate-800">{selectedName}</span>
          <button type="button" onClick={handleClear} className="text-slate-400 hover:text-slate-600">
            <X className="h-3.5 w-3.5" />
          </button>
        </div>
      ) : (
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-400" />
          <input
            value={query}
            onChange={(e) => { setQuery(e.target.value); setOpen(true) }}
            onFocus={() => setOpen(true)}
            onBlur={() => setTimeout(() => setOpen(false), 150)}
            placeholder="Type to search insureds…"
            className="w-full border rounded pl-8 pr-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          {isFetching && (
            <div className="absolute right-3 top-1/2 -translate-y-1/2">
              <LoadingSpinner />
            </div>
          )}
        </div>
      )}

      {open && query.length >= 2 && (
        <div className="absolute z-50 top-full left-0 right-0 mt-1 bg-white border border-slate-200 rounded-lg shadow-lg max-h-56 overflow-y-auto">
          {results.length === 0 && !isFetching ? (
            <p className="text-xs text-slate-400 px-3 py-3 text-center">No insureds found</p>
          ) : (
            results.map((insured: InsuredListItem) => (
              <button
                key={insured.id}
                type="button"
                onMouseDown={() => handleSelect(insured)}
                className="w-full flex items-start gap-2 px-3 py-2 text-left hover:bg-blue-50"
              >
                <div>
                  <p className="text-sm text-slate-800 font-medium">{insured.displayName}</p>
                  <p className="text-xs text-slate-400">{insured.city}, {insured.state}</p>
                </div>
              </button>
            ))
          )}
        </div>
      )}
    </div>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────

export function SubmissionCreatePage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const insuredIdParam = searchParams.get('insuredId') ?? ''

  const [form, setForm] = useState<SubmissionCreate>({
    insuredId: insuredIdParam,
    underwriterId: '',
    agentId: undefined,
    assistantUWId: undefined,
    effectiveDate: undefined,
    expirationDate: undefined,
    linesOfBusiness: [],
  })

  const { data: insured } = useQuery({
    queryKey: ['insureds', insuredIdParam],
    queryFn: () => insuredsApi.getById(insuredIdParam),
    enabled: !!insuredIdParam,
  })

  const { data: usersData } = useQuery({
    queryKey: ['users', { pageSize: 100 }],
    queryFn: () => usersApi.getAll({ pageSize: 100 }),
  })
  const users = usersData?.items ?? []

  const { data: agents = [] } = useQuery({
    queryKey: ['agents', 'active'],
    queryFn: () => agentsApi.getAll(true),
  })

  const createMutation = useMutation({
    mutationFn: submissionsApi.create,
    onSuccess: (submission) => {
      toast.success('Submission created')
      navigate(`/submissions/${submission.id}`)
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to create submission'),
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!form.insuredId || !form.underwriterId || form.linesOfBusiness.length === 0) {
      toast.error('Insured, underwriter, and at least one line of business are required')
      return
    }
    createMutation.mutate({
      ...form,
      agentId: form.agentId || undefined,
      assistantUWId: form.assistantUWId || undefined,
      effectiveDate: form.effectiveDate || undefined,
      expirationDate: form.expirationDate || undefined,
      linesOfBusiness: form.linesOfBusiness,
    })
  }

  const set = (k: keyof SubmissionCreate) => (e: React.ChangeEvent<HTMLSelectElement | HTMLInputElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }))

  return (
    <div className="p-6 max-w-2xl space-y-6">
      {insuredIdParam && (
        <Link
          to={`/insureds/${insuredIdParam}`}
          className="inline-flex items-center gap-1 text-sm text-slate-500 hover:text-slate-900"
        >
          <ArrowLeft className="h-3.5 w-3.5" /> {insured?.displayName ?? 'Back'}
        </Link>
      )}

      <PageHeader title="New Submission" description={insured ? `For ${insured.displayName}` : undefined} />

      <form onSubmit={handleSubmit} className="bg-white border rounded-lg p-6 space-y-5">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">

          {!insuredIdParam && (
            <div className="col-span-2">
              <label className="block text-xs font-medium text-slate-600 mb-1">Insured *</label>
              <InsuredCombobox
                value={form.insuredId}
                onChange={(id) => setForm((f) => ({ ...f, insuredId: id }))}
              />
            </div>
          )}

          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Underwriter *</label>
            <select value={form.underwriterId} onChange={set('underwriterId')} className="w-full border rounded px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500">
              <option value="">— Select underwriter —</option>
              {users.map((u) => (
                <option key={u.id} value={u.id}>{u.fullName}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Assistant Underwriter</label>
            <select value={form.assistantUWId ?? ''} onChange={set('assistantUWId')} className="w-full border rounded px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500">
              <option value="">— None —</option>
              {users.map((u) => (
                <option key={u.id} value={u.id}>{u.fullName}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Agent</label>
            <select value={form.agentId ?? ''} onChange={set('agentId')} className="w-full border rounded px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500">
              <option value="">— None —</option>
              {agents.map((a) => (
                <option key={a.id} value={a.id}>{a.name}{a.agencyName ? ` (${a.agencyName})` : ''}</option>
              ))}
            </select>
          </div>

          <div />

          <div className="col-span-2">
            <label className="block text-xs font-medium text-slate-600 mb-2">Lines of Business *</label>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
              {ACTIVE_LOBS.map((lob) => (
                <label key={lob} className="flex items-center gap-2 rounded border border-slate-200 px-3 py-2 text-sm text-slate-700">
                  <input
                    type="checkbox"
                    checked={form.linesOfBusiness.includes(lob)}
                    onChange={(e) => setForm((f) => ({
                      ...f,
                      linesOfBusiness: e.target.checked
                        ? [...f.linesOfBusiness, lob]
                        : f.linesOfBusiness.filter((value) => value !== lob),
                    }))}
                  />
                  {LOB_LABELS[lob]}
                </label>
              ))}
            </div>
          </div>

          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Target Effective Date</label>
            <input type="date" value={form.effectiveDate ?? ''} onChange={set('effectiveDate')} className="w-full border rounded px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>

          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Target Expiration Date</label>
            <input type="date" value={form.expirationDate ?? ''} onChange={set('expirationDate')} className="w-full border rounded px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
        </div>

        <div className="flex gap-3 pt-2">
          <button
            type="submit"
            disabled={createMutation.isPending || !form.insuredId || !form.underwriterId || form.linesOfBusiness.length === 0}
            className="px-4 py-2 bg-blue-600 text-white rounded-md text-sm font-medium hover:bg-blue-700 disabled:opacity-40"
          >
            {createMutation.isPending ? 'Creating…' : 'Create Submission'}
          </button>
          <button
            type="button"
            onClick={() => navigate(insuredIdParam ? `/insureds/${insuredIdParam}` : '/insureds')}
            className="px-4 py-2 border rounded-md text-sm hover:bg-slate-50"
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  )
}
