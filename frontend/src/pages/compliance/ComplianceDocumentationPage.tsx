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

export function ComplianceDocumentationPage() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [status, setStatus] = useState(ALL)
  const [category, setCategory] = useState(ALL)
  const [search, setSearch] = useState('')

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

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="Compliance Documentation"
        subtitle="Maintain policies, plans, reviews, versions, evidence, and attestations"
        action={
          <>
            <button
              type="button"
              onClick={() => navigate('/compliance-documentation/reviews')}
              className="inline-flex items-center gap-2 rounded border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              <CalendarClock className="h-4 w-4" />
              Review Queue
            </button>
            <button
              type="button"
              onClick={() => navigate('/compliance-documentation/attestations')}
              className="inline-flex items-center gap-2 rounded border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              <ClipboardCheck className="h-4 w-4" />
              My Attestations
            </button>
            <button
              type="button"
              onClick={() => createMutation.mutate()}
              disabled={createMutation.isPending}
              className="inline-flex items-center gap-2 rounded bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              <Plus className="h-4 w-4" />
              New Document
            </button>
          </>
        }
      />

      <section className="grid grid-cols-1 gap-3 md:grid-cols-4">
        <Metric icon={FileText} label="Total Documents" value={summaryQuery.data?.totalDocuments ?? 0} />
        <Metric icon={CheckCircle2} label="Active" value={summaryQuery.data?.activeDocuments ?? 0} />
        <Metric icon={FileCheck2} label="Draft / Review" value={summaryQuery.data?.draftDocuments ?? 0} />
        <Metric icon={AlertTriangle} label="Overdue" value={summaryQuery.data?.overdue ?? 0} tone="danger" />
      </section>

      <section className="rounded border bg-white">
        <div className="flex flex-wrap items-center gap-3 border-b px-4 py-3">
          <SelectFilter label="Status" value={status} values={STATUSES} onChange={setStatus} />
          <SelectFilter label="Category" value={category} values={categories} onChange={setCategory} />
          <label className="relative min-w-[260px] flex-1">
            <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search title, tags, or document body"
              className="w-full rounded border border-slate-300 py-2 pl-9 pr-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
            />
          </label>
        </div>

        {documentsQuery.isLoading ? (
          <div className="p-8"><LoadingSpinner /></div>
        ) : documents.length === 0 ? (
          <div className="p-6">
            <EmptyState icon={FileText} title="No compliance documents" description="Create the first document or adjust the filters." />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                  <th className="px-4 py-3">Document</th>
                  <th className="px-4 py-3">Owner</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3">Review</th>
                  <th className="px-4 py-3">Version</th>
                  <th className="px-4 py-3">Tags</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {documents.map((document) => (
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
                          <span key={tag} className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{tag}</span>
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

function Metric({ icon: Icon, label, value, tone = 'default' }: { icon: React.ElementType; label: string; value: number; tone?: 'default' | 'danger' }) {
  return (
    <div className="rounded border bg-white p-4">
      <div className="flex items-center justify-between">
        <div className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</div>
        <Icon className={`h-4 w-4 ${tone === 'danger' ? 'text-red-500' : 'text-slate-400'}`} />
      </div>
      <div className="mt-2 text-xl font-semibold text-slate-800">{value.toLocaleString()}</div>
    </div>
  )
}

function SelectFilter({ label, value, values, onChange }: { label: string; value: string; values: string[]; onChange: (value: string) => void }) {
  return (
    <label className="flex items-center gap-2 text-sm text-slate-600">
      <span className="font-medium">{label}</span>
      <select
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-700 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
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
