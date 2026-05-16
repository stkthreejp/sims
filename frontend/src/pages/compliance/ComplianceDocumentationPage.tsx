import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { AlertTriangle, CalendarClock, CheckCircle2, ClipboardCheck, FileCheck2, FileText, Plus, Search } from 'lucide-react'
import { toast } from 'sonner'
import { complianceDocumentsApi } from '@/api/complianceDocuments.api'
import { EmptyState } from '@/components/common/EmptyState'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { PageHeader } from '@/components/common/PageHeader'
import { formatDate } from '@/lib/utils'

const ALL = 'All'
const STATUSES = [ALL, 'Draft', 'Active', 'Under Review', 'Needs Update', 'Retired']
const CATEGORIES = [ALL, 'IT', 'Security', 'Business Continuity', 'Privacy', 'Operations', 'Vendor Management', 'HR', 'Finance']
type MetricFilter = 'All' | 'Active' | 'DraftReview' | 'Overdue'

export function ComplianceDocumentationPage() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [status, setStatus] = useState(ALL)
  const [category, setCategory] = useState(ALL)
  const [search, setSearch] = useState('')
  const [metricFilter, setMetricFilter] = useState<MetricFilter>('All')

  const summaryQuery = useQuery({
    queryKey: ['compliance-documents', 'summary'],
    queryFn: complianceDocumentsApi.getSummary,
  })

  const documentsQuery = useQuery({
    queryKey: ['compliance-documents', { status, category, search }],
    queryFn: () => complianceDocumentsApi.getAll({
      status: status === ALL ? undefined : status,
      category: category === ALL ? undefined : category,
      search: search.trim() || undefined,
    }),
  })

  const createMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.create({
      title: 'Untitled Compliance Document',
      category: 'IT',
      documentType: 'Policy',
      reviewCadence: 'Annual',
      tags: [],
      htmlContent: '<h1>Untitled Compliance Document</h1><p></p>',
    }),
    onSuccess: (document) => {
      qc.invalidateQueries({ queryKey: ['compliance-documents'] })
      toast.success('Compliance document created')
      navigate(`/compliance-documentation/${document.id}`)
    },
    onError: () => toast.error('Could not create compliance document'),
  })

  const documents = documentsQuery.data ?? []
  const categories = useMemo(() => Array.from(new Set([...CATEGORIES, ...documents.map((d) => d.category)])), [documents])
  const filteredDocuments = useMemo(() => {
    if (metricFilter === 'Active') return documents.filter((document) => document.status === 'Active')
    if (metricFilter === 'DraftReview') return documents.filter((document) => document.status === 'Draft' || document.status === 'Under Review')
    if (metricFilter === 'Overdue') {
      const today = startOfToday()
      return documents.filter((document) => {
        const nextReview = parseDate(document.nextReviewDate)
        return !!nextReview && nextReview < today
      })
    }
    return documents
  }, [documents, metricFilter])

  const chooseMetricFilter = (filter: MetricFilter) => {
    setMetricFilter(filter)
    setStatus(ALL)
  }

  const updateStatusFilter = (value: string) => {
    setStatus(value)
    setMetricFilter('All')
  }

  return (
    <div className="space-y-5 p-6" style={{ background: 'var(--surface-2)' }}>
      <PageHeader
        title="Compliance Documentation"
        subtitle="Maintain policies, plans, reviews, versions, evidence, and attestations"
        action={
          <>
            <button
              type="button"
              onClick={() => navigate('/compliance-documentation/reviews')}
              className="sd-btn outline"
            >
              <CalendarClock className="h-4 w-4" />
              Review Queue
            </button>
            <button
              type="button"
              onClick={() => navigate('/compliance-documentation/attestations')}
              className="sd-btn outline"
            >
              <ClipboardCheck className="h-4 w-4" />
              My Attestations
            </button>
            <button
              type="button"
              onClick={() => createMutation.mutate()}
              disabled={createMutation.isPending}
              className="sd-btn primary"
            >
              <Plus className="h-4 w-4" />
              New Document
            </button>
          </>
        }
      />

      <section className="grid grid-cols-1 gap-3 md:grid-cols-4">
        <Metric icon={FileText} label="Total Documents" value={summaryQuery.data?.totalDocuments ?? 0} active={metricFilter === 'All'} onClick={() => chooseMetricFilter('All')} />
        <Metric icon={CheckCircle2} label="Active" value={summaryQuery.data?.activeDocuments ?? 0} active={metricFilter === 'Active'} onClick={() => chooseMetricFilter('Active')} />
        <Metric icon={FileCheck2} label="Draft / Review" value={summaryQuery.data?.draftDocuments ?? 0} active={metricFilter === 'DraftReview'} onClick={() => chooseMetricFilter('DraftReview')} />
        <Metric icon={AlertTriangle} label="Overdue" value={summaryQuery.data?.overdue ?? 0} tone="danger" active={metricFilter === 'Overdue'} onClick={() => chooseMetricFilter('Overdue')} />
      </section>

      <section className="sd-card">
        <div className="sd-card-head flex-wrap gap-3">
          <SelectFilter label="Status" value={status} values={STATUSES} onChange={updateStatusFilter} />
          <SelectFilter label="Category" value={category} values={categories} onChange={setCategory} />
          <label className="relative min-w-[260px] flex-1">
            <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4" style={{ color: 'var(--ink-4)' }} />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search title, tags, or document body"
              className="sims-input pl-9"
            />
          </label>
        </div>

        {documentsQuery.isLoading ? (
          <div className="p-8"><LoadingSpinner /></div>
        ) : filteredDocuments.length === 0 ? (
          <div className="p-6">
            <EmptyState icon={FileText} title="No compliance documents" description="Create the first document or adjust the filters." />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b text-xs uppercase tracking-wide" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)', color: 'var(--ink-4)' }}>
                  <th className="px-4 py-3">Document</th>
                  <th className="px-4 py-3">Owner</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3">Review</th>
                  <th className="px-4 py-3">Version</th>
                  <th className="px-4 py-3">Tags</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {filteredDocuments.map((document) => (
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
                    <td className="whitespace-nowrap px-4 py-3"><StatusPill status={document.status} /></td>
                    <td className="whitespace-nowrap px-4 py-3 text-slate-600">
                      <div>Next: {formatDate(document.nextReviewDate)}</div>
                      <div className="text-xs text-slate-400">{document.reviewCadence}</div>
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-slate-600">
                      {document.currentPublishedVersionNumber ? `v${document.currentPublishedVersionNumber}` : 'Unpublished'}
                      {document.currentDraftVersionNumber && <div className="text-xs text-amber-600">Draft v{document.currentDraftVersionNumber}</div>}
                    </td>
                    <td className="min-w-[180px] px-4 py-3">
                      <div className="flex flex-wrap gap-1">
                        {document.tags.slice(0, 4).map((tag) => (
                          <span key={tag} className="sd-lob">{tag}</span>
                        ))}
                      </div>
                    </td>
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

function Metric({ icon: Icon, label, value, tone = 'default', active = false, onClick }: { icon: React.ElementType; label: string; value: number; tone?: 'default' | 'danger'; active?: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="sd-card p-4 text-left transition hover:-translate-y-0.5 hover:shadow-md"
      style={{ borderColor: active ? 'var(--accent)' : 'var(--line)' }}
    >
      <div className="flex items-center justify-between">
        <div className="text-xs font-medium uppercase tracking-wide" style={{ color: 'var(--ink-4)' }}>{label}</div>
        <Icon className={`h-4 w-4 ${tone === 'danger' ? 'text-red-500' : 'text-slate-400'}`} />
      </div>
      <div className="mt-2 text-xl font-semibold" style={{ color: 'var(--ink)' }}>{value.toLocaleString()}</div>
    </button>
  )
}

function SelectFilter({ label, value, values, onChange }: { label: string; value: string; values: string[]; onChange: (value: string) => void }) {
  return (
    <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--ink-3)' }}>
      <span className="font-medium">{label}</span>
      <select
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="sims-select"
      >
        {values.map((option) => <option key={option} value={option}>{option}</option>)}
      </select>
    </label>
  )
}

function StatusPill({ status }: { status: string }) {
  const styles = status === 'Active'
    ? 'border-green-200 bg-green-50 text-green-700'
    : status === 'Needs Update'
      ? 'border-red-200 bg-red-50 text-red-700'
      : status === 'Under Review'
        ? 'border-blue-200 bg-blue-50 text-blue-700'
        : 'border-amber-200 bg-amber-50 text-amber-700'

  return <span className={`rounded-full border px-2 py-0.5 text-xs font-medium ${styles}`}>{status}</span>
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
