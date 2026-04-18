import { useNavigate, useSearchParams, Link } from 'react-router-dom'
import { useQuery, useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { ArrowLeft } from 'lucide-react'
import { toast } from 'sonner'
import { submissionsApi } from '@/api/submissions.api'
import { usersApi } from '@/api/users.api'
import { agentsApi } from '@/api/agents.api'
import { insuredsApi } from '@/api/insureds.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { SubmissionCreate } from '@/types/submission.types'

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
    if (!form.insuredId || !form.underwriterId) {
      toast.error('Insured and underwriter are required')
      return
    }
    createMutation.mutate({
      ...form,
      agentId: form.agentId || undefined,
      assistantUWId: form.assistantUWId || undefined,
      effectiveDate: form.effectiveDate || undefined,
      expirationDate: form.expirationDate || undefined,
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
              <label className="block text-xs font-medium text-slate-600 mb-1">Insured ID *</label>
              <input
                value={form.insuredId}
                onChange={set('insuredId')}
                placeholder="Paste insured ID"
                className="w-full border rounded px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
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
            disabled={createMutation.isPending || !form.insuredId || !form.underwriterId}
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
