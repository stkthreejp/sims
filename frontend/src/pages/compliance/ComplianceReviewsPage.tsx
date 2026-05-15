import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { ArrowLeft, AlertTriangle, CalendarClock, CheckCircle2, Search } from 'lucide-react'
import { complianceDocumentsApi } from '@/api/complianceDocuments.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { PageHeader } from '@/components/common/PageHeader'
import { formatDate } from '@/lib/utils'
import type { ComplianceDocumentListItem } from '@/types/compliance.types'

type QueueFilter = 'Needs Review' | 'Overdue' | 'Due Soon' | 'All'

export function ComplianceReviewsPage() {
  const navigate = useNavigate()
  const [filter, setFilter] = useState<QueueFilter>('Needs Review')
  const [search, setSearch] = useState('')

  const documentsQuery = useQuery({
    queryKey: ['compliance-documents', 'review-queue'],
    queryFn: () => complianceDocumentsApi.getAll({}),
  })

  const queue = useMemo(() => {
    const today = startOfToday()
    const soon = addDays(today, 30)
    const term = search.trim().toLowerCase()

    return (documentsQuery.data ?? [])
      .filter((document) => document.status !== 'Retired')
      .filter((document) => {
        if (!term) return true
        return (
          document.title.toLowerCase().includes(term) ||
          document.category.toLowerCase().includes(term) ||
          document.documentType.toLowerCase().includes(term) ||
          document.tags.some((tag) => tag.toLowerCase().includes(term))
        )
      })
      .filter((document) => {
        const due = parseDate(document.nextReviewDate)
        if (filter === 'All') return true
        if (!due) return filter === 'Needs Review'
        if (filter === 'Overdue') return due < today
        if (filter === 'Due Soon') return due >= today && due <= soon
        return due <= soon
      })
      .sort((a, b) => sortByDueDate(a, b))
  }, [documentsQuery.data, filter, search])

  const counts = useMemo(() => {
    const today = startOfToday()
    const soon = addDays(today, 30)
    const docs = (documentsQuery.data ?? []).filter((document) => document.status !== 'Retired')
    return {
      overdue: docs.filter((document) => {
        const due = parseDate(document.nextReviewDate)
        return !!due && due < today
      }).length,
      dueSoon: docs.filter((document) => {
        const due = parseDate(document.nextReviewDate)
        return !!due && due >= today && due <= soon
      }).length,
      missingDate: docs.filter((document) => !document.nextReviewDate).length,
    }
  }, [documentsQuery.data])

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="Compliance Review Queue"
        subtitle="Documents due for owner review or compliance follow-up"
        action={
          <button
            type="button"
            onClick={() => navigate('/compliance-documentation')}
            className="inline-flex items-center gap-2 rounded border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            <ArrowLeft className="h-4 w-4" />
            Compliance Register
          </button>
        }
      />

      <section className="grid grid-cols-1 gap-3 md:grid-cols-3">
        <Metric icon={AlertTriangle} label="Overdue" value={counts.overdue} tone="danger" />
        <Metric icon={CalendarClock} label="Due Soon" value={counts.dueSoon} tone="warning" />
        <Metric icon={CheckCircle2} label="Missing Review Date" value={counts.missingDate} />
      </section>

      <section className="rounded border bg-white">
        <div className="flex flex-wrap items-center gap-3 border-b px-4 py-3">
          <div className="flex flex-wrap gap-2">
            {(['Needs Review', 'Overdue', 'Due Soon', 'All'] as QueueFilter[]).map((option) => (
              <button
                key={option}
                type="button"
                onClick={() => setFilter(option)}
                className={`rounded px-3 py-1.5 text-sm font-medium ${filter === option ? 'bg-blue-50 text-blue-700' : 'text-slate-600 hover:bg-slate-50'}`}
              >
                {option}
              </button>
            ))}
          </div>
          <label className="relative min-w-[260px] flex-1">
            <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search review queue"
              className="w-full rounded border border-slate-300 py-2 pl-9 pr-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
            />
          </label>
        </div>

        {documentsQuery.isLoading ? (
          <div className="p-8"><LoadingSpinner /></div>
        ) : queue.length === 0 ? (
          <div className="p-6 text-sm text-slate-500">No documents match this review view.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                  <th className="px-4 py-3">Document</th>
                  <th className="px-4 py-3">Owner</th>
                  <th className="px-4 py-3">Last Reviewed</th>
                  <th className="px-4 py-3">Next Review</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3">Cadence</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {queue.map((document) => (
                  <tr
                    key={document.id}
                    onClick={() => navigate(`/compliance-documentation/${document.id}`)}
                    className="cursor-pointer align-top hover:bg-slate-50"
                  >
                    <td className="min-w-[260px] px-4 py-3">
                      <div className="font-medium text-slate-900">{document.title}</div>
                      <div className="mt-1 text-xs text-slate-500">{document.category} · {document.documentType}</div>
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-slate-600">{document.ownerName ?? '-'}</td>
                    <td className="whitespace-nowrap px-4 py-3 text-slate-600">{formatDate(document.lastReviewedDate)}</td>
                    <td className="whitespace-nowrap px-4 py-3">
                      <DueBadge date={document.nextReviewDate} />
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-slate-600">{document.status}</td>
                    <td className="whitespace-nowrap px-4 py-3 text-slate-600">{document.reviewCadence}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}

function Metric({ icon: Icon, label, value, tone = 'default' }: { icon: React.ElementType; label: string; value: number; tone?: 'default' | 'warning' | 'danger' }) {
  const color = tone === 'danger' ? 'text-red-500' : tone === 'warning' ? 'text-amber-500' : 'text-slate-400'
  return (
    <div className="rounded border bg-white p-4">
      <div className="flex items-center justify-between">
        <div className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</div>
        <Icon className={`h-4 w-4 ${color}`} />
      </div>
      <div className="mt-2 text-xl font-semibold text-slate-800">{value.toLocaleString()}</div>
    </div>
  )
}

function DueBadge({ date }: { date: string | null }) {
  const due = parseDate(date)
  if (!due) {
    return <span className="rounded-full border border-slate-200 bg-slate-50 px-2 py-0.5 text-xs font-medium text-slate-600">Missing</span>
  }

  const today = startOfToday()
  const soon = addDays(today, 30)
  const styles = due < today
    ? 'border-red-200 bg-red-50 text-red-700'
    : due <= soon
      ? 'border-amber-200 bg-amber-50 text-amber-700'
      : 'border-green-200 bg-green-50 text-green-700'

  return <span className={`rounded-full border px-2 py-0.5 text-xs font-medium ${styles}`}>{formatDate(date)}</span>
}

function sortByDueDate(a: ComplianceDocumentListItem, b: ComplianceDocumentListItem) {
  const left = parseDate(a.nextReviewDate)?.getTime() ?? Number.MAX_SAFE_INTEGER
  const right = parseDate(b.nextReviewDate)?.getTime() ?? Number.MAX_SAFE_INTEGER
  return left - right || a.title.localeCompare(b.title)
}

function parseDate(value: string | null) {
  if (!value) return null
  const date = new Date(`${value}T00:00:00`)
  return Number.isNaN(date.getTime()) ? null : date
}

function startOfToday() {
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  return today
}

function addDays(date: Date, days: number) {
  const next = new Date(date)
  next.setDate(next.getDate() + days)
  return next
}
