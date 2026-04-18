import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Plus, Search, ChevronLeft, ChevronRight } from 'lucide-react'
import { submissionsApi } from '@/api/submissions.api'
import { SUBMISSION_STATUS_LABELS, type SubmissionStatus } from '@/types/submission.types'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'

const STATUS_COLORS: Record<SubmissionStatus, string> = {
  New: 'bg-slate-100 text-slate-600',
  InProgress: 'bg-blue-100 text-blue-700',
  Quoted: 'bg-yellow-100 text-yellow-700',
  Bound: 'bg-green-100 text-green-700',
  Declined: 'bg-red-100 text-red-700',
  Withdrawn: 'bg-slate-100 text-slate-500',
}

export function SubmissionsPage() {
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const pageSize = 20

  const { data, isLoading } = useQuery({
    queryKey: ['submissions', { search, page, pageSize }],
    queryFn: () => submissionsApi.getAll({ search, page, pageSize, sortBy: 'createdAt', sortDir: 'desc' }),
  })

  const totalPages = data ? Math.ceil(data.totalCount / pageSize) : 1

  if (isLoading) return <LoadingSpinner />

  return (
    <div className="p-6 space-y-6">
      <PageHeader
        title="Submissions"
        subtitle={data ? `${data.totalCount} total` : ''}
        action={
          <button onClick={() => navigate('/submissions/new')} className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg text-sm hover:bg-blue-700">
            <Plus className="h-4 w-4" /> New Submission
          </button>
        }
      />

      {/* Search */}
      <div className="relative max-w-sm">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
        <input
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1) }}
          placeholder="Search submissions..."
          className="w-full pl-9 pr-3 py-2 border rounded-lg text-sm"
        />
      </div>

      {/* Table */}
      <div className="bg-white border rounded-lg overflow-hidden">
        {!data?.items.length ? (
          <div className="p-8 text-center text-slate-500 text-sm">
            {search ? 'No submissions match your search.' : 'No submissions yet. Create the first one.'}
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-slate-50 border-b">
              <tr>
                <th className="text-left px-4 py-3 font-medium text-slate-600">Submission #</th>
                <th className="text-left px-4 py-3 font-medium text-slate-600">Insured</th>
                <th className="text-left px-4 py-3 font-medium text-slate-600">Agent</th>
                <th className="text-left px-4 py-3 font-medium text-slate-600">Underwriter</th>
                <th className="text-left px-4 py-3 font-medium text-slate-600">Eff. Date</th>
                <th className="text-left px-4 py-3 font-medium text-slate-600">Quotes</th>
                <th className="text-left px-4 py-3 font-medium text-slate-600">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {data.items.map((s) => (
                <tr
                  key={s.id}
                  onClick={() => navigate(`/submissions/${s.id}`)}
                  className="hover:bg-slate-50 cursor-pointer"
                >
                  <td className="px-4 py-3 font-medium text-blue-600">{s.submissionNumber}</td>
                  <td className="px-4 py-3">{s.insuredName}</td>
                  <td className="px-4 py-3 text-slate-600">{s.agentName ?? '—'}</td>
                  <td className="px-4 py-3 text-slate-600">{s.underwriterName}</td>
                  <td className="px-4 py-3 text-slate-600">
                    {s.effectiveDate ? new Date(s.effectiveDate).toLocaleDateString() : '—'}
                  </td>
                  <td className="px-4 py-3 text-slate-600">{s.quoteCount}</td>
                  <td className="px-4 py-3">
                    <span className={`inline-flex px-2 py-0.5 rounded-full text-xs font-medium ${STATUS_COLORS[s.status]}`}>
                      {SUBMISSION_STATUS_LABELS[s.status]}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between text-sm text-slate-600">
          <span>Page {page} of {totalPages}</span>
          <div className="flex gap-2">
            <button onClick={() => setPage((p) => p - 1)} disabled={page === 1} className="p-1.5 border rounded hover:bg-slate-50 disabled:opacity-40">
              <ChevronLeft className="h-4 w-4" />
            </button>
            <button onClick={() => setPage((p) => p + 1)} disabled={page === totalPages} className="p-1.5 border rounded hover:bg-slate-50 disabled:opacity-40">
              <ChevronRight className="h-4 w-4" />
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
