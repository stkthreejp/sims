import { useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BookOpenCheck, Check, ClipboardList, History, RefreshCw, Search, Upload, X } from 'lucide-react'
import { toast } from 'sonner'
import {
  legalRequirementsApi,
  type LegalRequirementChangeLog,
  type LegalRequirementSection,
  type LegalSourceScanResult,
  type LegalSourceScanRun,
} from '@/api/legalRequirements.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { PageHeader } from '@/components/common/PageHeader'

const ALL = 'All'
type TabKey = 'requirements' | 'scans' | 'changes'

export function LegalRequirementsPage() {
  const [activeTab, setActiveTab] = useState<TabKey>('requirements')
  const [state, setState] = useState(ALL)
  const [category, setCategory] = useState(ALL)
  const [search, setSearch] = useState('')
  const fileInputRef = useRef<HTMLInputElement>(null)
  const queryClient = useQueryClient()

  const summaryQuery = useQuery({
    queryKey: ['legal-requirements', 'summary'],
    queryFn: legalRequirementsApi.getSummary,
  })

  const sectionsQuery = useQuery({
    queryKey: ['legal-requirements', 'sections', state, category, search],
    queryFn: () => legalRequirementsApi.getSections({
      state: state === ALL ? undefined : state,
      category: category === ALL ? undefined : category,
      search: search.trim() || undefined,
    }),
  })

  const scanRunsQuery = useQuery({
    queryKey: ['legal-requirements', 'scan-runs'],
    queryFn: legalRequirementsApi.getScanRuns,
  })

  const scanResultsQuery = useQuery({
    queryKey: ['legal-requirements', 'scan-results', state],
    queryFn: () => legalRequirementsApi.getScanResults({
      state: state === ALL ? undefined : state,
      reviewStatus: undefined,
    }),
  })

  const changeLogQuery = useQuery({
    queryKey: ['legal-requirements', 'change-log', state],
    queryFn: () => legalRequirementsApi.getChangeLog({
      state: state === ALL ? undefined : state,
    }),
  })

  const sections = sectionsQuery.data ?? []
  const summary = summaryQuery.data
  const noticeCount = useMemo(() => sections.filter((s) => s.category === 'NOTICE REQUIREMENTS').length, [sections])
  const reasonCount = useMemo(() => sections.filter((s) => s.category === 'REASONS').length, [sections])
  const isRefreshing = summaryQuery.isFetching || sectionsQuery.isFetching || scanRunsQuery.isFetching || scanResultsQuery.isFetching || changeLogQuery.isFetching
  const importMutation = useMutation({
    mutationFn: legalRequirementsApi.importOden,
    onSuccess: (run) => {
      toast.success(`Oden import completed: ${run.possibleChanges} possible changes found`)
      invalidateLegalQueries(queryClient)
    },
    onError: () => toast.error('Oden import could not be processed'),
  })
  const simulateMutation = useMutation({
    mutationFn: legalRequirementsApi.simulateChange,
    onSuccess: () => {
      toast.success('Simulated change added to the review queue')
      invalidateLegalQueries(queryClient)
      setActiveTab('scans')
    },
    onError: () => toast.error('Simulated change could not be created'),
  })

  if (summaryQuery.isLoading) return <LoadingSpinner />

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="Cancellation Compliance"
        subtitle="Commercial P&C cancellation requirements, source scans, and change history"
        action={
          <button
            type="button"
            onClick={() => {
              summaryQuery.refetch()
              sectionsQuery.refetch()
              scanRunsQuery.refetch()
              scanResultsQuery.refetch()
              changeLogQuery.refetch()
            }}
            disabled={isRefreshing}
            className="inline-flex items-center gap-2 rounded border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          >
            <RefreshCw className={`h-4 w-4 ${isRefreshing ? 'animate-spin' : ''}`} />
            Refresh
          </button>
        }
      />

      <input
        ref={fileInputRef}
        type="file"
        accept=".html,.htm,text/html"
        className="hidden"
        onChange={(event) => {
          const file = event.target.files?.[0]
          if (file) importMutation.mutate(file)
          event.target.value = ''
        }}
      />

      <section className="grid grid-cols-1 gap-3 md:grid-cols-4">
        <Metric label="Tracked states" value={(summary?.states.length ?? 0).toLocaleString()} />
        <Metric label="Requirement sections" value={(summary?.sectionCount ?? 0).toLocaleString()} />
        <Metric label="Pending scan items" value={(summary?.pendingScanResultCount ?? 0).toLocaleString()} />
        <Metric label="Change log entries" value={(summary?.changeLogCount ?? 0).toLocaleString()} />
      </section>

      <div className="rounded border bg-white">
        <div className="flex flex-wrap items-center gap-2 border-b px-4 py-3">
          <TabButton active={activeTab === 'requirements'} onClick={() => setActiveTab('requirements')} icon={BookOpenCheck} label="Requirements" />
          <TabButton active={activeTab === 'scans'} onClick={() => setActiveTab('scans')} icon={ClipboardList} label="Source Scans" />
          <TabButton active={activeTab === 'changes'} onClick={() => setActiveTab('changes')} icon={History} label="Change Log" />
        </div>

        <div className="flex flex-wrap items-center gap-3 border-b px-4 py-3">
          <SelectFilter label="State" value={state} values={[ALL, ...(summary?.states ?? [])]} onChange={setState} />
          {activeTab === 'requirements' && (
            <SelectFilter label="Category" value={category} values={[ALL, ...(summary?.categories ?? [])]} onChange={setCategory} />
          )}
          {activeTab === 'requirements' && (
            <label className="relative min-w-[260px] flex-1">
              <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
              <input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search requirements"
                className="w-full rounded border border-slate-300 py-2 pl-9 pr-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
              />
            </label>
          )}
        </div>

        {activeTab === 'requirements' && (
          sectionsQuery.isLoading ? <PanelLoader /> : <RequirementTable sections={sections} />
        )}
        {activeTab === 'scans' && (
          scanRunsQuery.isLoading || scanResultsQuery.isLoading
            ? <PanelLoader />
            : (
              <ScanPanel
                runs={scanRunsQuery.data ?? []}
                results={scanResultsQuery.data ?? []}
                onImport={() => fileInputRef.current?.click()}
                importing={importMutation.isPending}
                onSimulate={() => simulateMutation.mutate()}
                simulating={simulateMutation.isPending}
              />
            )
        )}
        {activeTab === 'changes' && (
          changeLogQuery.isLoading ? <PanelLoader /> : <ChangeLogTable logs={changeLogQuery.data ?? []} />
        )}
      </div>

      <section className="rounded border bg-white p-4">
        <div className="flex items-center gap-2 text-sm font-semibold text-slate-800">
          <BookOpenCheck className="h-4 w-4 text-slate-500" />
          Source
        </div>
        <div className="mt-2 text-sm text-slate-600">
          {summary?.sourceName ?? 'Oden Online'}: {summary?.sourceDocument ?? 'COMMERCIAL INSURANCE - CANCELLATION - P&C'}
          {summary?.sourceCreatedAt ? `, created ${formatDate(summary.sourceCreatedAt)}` : ''}
        </div>
      </section>
    </div>
  )
}

function RequirementTable({ sections }: { sections: LegalRequirementSection[] }) {
  if (sections.length === 0) {
    return <EmptyPanel text="No requirement sections match the current filters." />
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-sm">
        <thead>
          <tr className="border-b bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
            <th className="px-4 py-3">State</th>
            <th className="px-4 py-3">Category</th>
            <th className="px-4 py-3">Topic</th>
            <th className="px-4 py-3">Requirement</th>
            <th className="px-4 py-3">Citations</th>
            <th className="px-4 py-3">Status</th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {sections.map((section) => (
            <tr key={section.id} className="align-top">
              <td className="whitespace-nowrap px-4 py-3 font-medium text-slate-800">{section.state}</td>
              <td className="px-4 py-3 text-xs font-semibold uppercase tracking-wide text-slate-500">{cleanLabel(section.category)}</td>
              <td className="min-w-[180px] px-4 py-3 text-slate-700">{section.topic}</td>
              <td className="min-w-[420px] px-4 py-3 leading-6 text-slate-700">{section.requirementText}</td>
              <td className="min-w-[220px] px-4 py-3"><CitationList citations={section.citations} /></td>
              <td className="whitespace-nowrap px-4 py-3"><StatusPill status={section.reviewStatus} /></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function ScanPanel({
  runs,
  results,
  onImport,
  importing,
  onSimulate,
  simulating,
}: {
  runs: LegalSourceScanRun[]
  results: LegalSourceScanResult[]
  onImport: () => void
  importing: boolean
  onSimulate: () => void
  simulating: boolean
}) {
  return (
    <div>
      <div className="border-b p-4">
        <div className="flex items-center justify-between gap-3">
          <h3 className="text-sm font-semibold text-slate-800">Recent Runs</h3>
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              onClick={onSimulate}
              disabled={simulating}
              className="inline-flex items-center gap-2 rounded border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
            >
              {simulating ? <RefreshCw className="h-4 w-4 animate-spin" /> : <ClipboardList className="h-4 w-4" />}
              Simulate Change
            </button>
            <button
              type="button"
              onClick={onImport}
              disabled={importing}
              className="inline-flex items-center gap-2 rounded bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {importing ? <RefreshCw className="h-4 w-4 animate-spin" /> : <Upload className="h-4 w-4" />}
              Import Oden Export
            </button>
          </div>
        </div>
        {runs.length === 0 ? (
          <div className="mt-3 text-sm text-slate-500">No source scans have been run yet.</div>
        ) : (
          <div className="mt-3 overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-y bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                  <th className="px-3 py-2">Source</th>
                  <th className="px-3 py-2">Status</th>
                  <th className="px-3 py-2">Results</th>
                  <th className="px-3 py-2">Possible Changes</th>
                  <th className="px-3 py-2">Started</th>
                  <th className="px-3 py-2">By</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {runs.map((run) => (
                  <tr key={run.id}>
                    <td className="px-3 py-2">
                      <div className="font-medium text-slate-800">{run.sourceName}</div>
                      <div className="text-xs text-slate-500">{run.sourceType}</div>
                    </td>
                    <td className="px-3 py-2"><StatusPill status={run.status} /></td>
                    <td className="px-3 py-2">{run.resultsFound.toLocaleString()}</td>
                    <td className="px-3 py-2">{run.possibleChanges.toLocaleString()}</td>
                    <td className="px-3 py-2 text-slate-600">{formatDate(run.startedAt)}</td>
                    <td className="px-3 py-2 text-slate-600">{run.startedByName ?? '-'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <div className="p-4">
        <h3 className="text-sm font-semibold text-slate-800">Review Queue</h3>
        {results.length === 0 ? (
          <div className="mt-3 text-sm text-slate-500">No scan results are waiting for review.</div>
        ) : (
          <ScanResultTable results={results} />
        )}
      </div>
    </div>
  )
}

function ScanResultTable({ results }: { results: LegalSourceScanResult[] }) {
  const queryClient = useQueryClient()
  const approve = useMutation({
    mutationFn: (id: string) => legalRequirementsApi.approveScanResult(id),
    onSuccess: () => {
      toast.success('Scan result approved')
      invalidateLegalQueries(queryClient)
    },
    onError: () => toast.error('Scan result could not be approved'),
  })
  const reject = useMutation({
    mutationFn: (id: string) => legalRequirementsApi.rejectScanResult(id),
    onSuccess: () => {
      toast.success('Scan result rejected')
      invalidateLegalQueries(queryClient)
    },
    onError: () => toast.error('Scan result could not be rejected'),
  })

  return (
    <div className="mt-3 overflow-x-auto">
      <table className="w-full text-left text-sm">
        <thead>
          <tr className="border-y bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
            <th className="px-3 py-2">State</th>
            <th className="px-3 py-2">Topic</th>
            <th className="px-3 py-2">Match</th>
            <th className="px-3 py-2">Source Text</th>
            <th className="px-3 py-2">Suggested Text</th>
            <th className="px-3 py-2">Status</th>
            <th className="px-3 py-2">Actions</th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {results.map((result) => (
            <tr key={result.id} className="align-top">
              <td className="whitespace-nowrap px-3 py-2 font-medium text-slate-800">{result.state}</td>
              <td className="min-w-[180px] px-3 py-2 text-slate-700">
                <div>{result.topic}</div>
                <div className="text-xs uppercase tracking-wide text-slate-400">{cleanLabel(result.category)}</div>
              </td>
              <td className="px-3 py-2"><StatusPill status={result.matchStatus} /></td>
              <td className="min-w-[300px] px-3 py-2 leading-6 text-slate-700">{result.sourceText}</td>
              <td className="min-w-[300px] px-3 py-2 leading-6 text-slate-700">{result.suggestedRequirementText ?? '-'}</td>
              <td className="px-3 py-2"><StatusPill status={result.reviewStatus} /></td>
              <td className="whitespace-nowrap px-3 py-2">
                {result.reviewStatus === 'Pending' ? (
                  <div className="flex gap-2">
                    <IconButton label="Approve" onClick={() => approve.mutate(result.id)} disabled={approve.isPending || reject.isPending} icon={Check} tone="approve" />
                    <IconButton label="Reject" onClick={() => reject.mutate(result.id)} disabled={approve.isPending || reject.isPending} icon={X} tone="reject" />
                  </div>
                ) : (
                  <span className="text-xs text-slate-400">Reviewed</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function IconButton({ label, onClick, disabled, icon: Icon, tone }: { label: string; onClick: () => void; disabled: boolean; icon: React.ElementType; tone: 'approve' | 'reject' }) {
  const style = tone === 'approve'
    ? 'border-green-200 text-green-700 hover:bg-green-50'
    : 'border-red-200 text-red-700 hover:bg-red-50'

  return (
    <button
      type="button"
      title={label}
      aria-label={label}
      onClick={onClick}
      disabled={disabled}
      className={`rounded border p-1.5 disabled:opacity-50 ${style}`}
    >
      <Icon className="h-4 w-4" />
    </button>
  )
}

function ChangeLogTable({ logs }: { logs: LegalRequirementChangeLog[] }) {
  if (logs.length === 0) {
    return <EmptyPanel text="No requirement changes have been recorded yet." />
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-sm">
        <thead>
          <tr className="border-b bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
            <th className="px-4 py-3">Changed</th>
            <th className="px-4 py-3">State</th>
            <th className="px-4 py-3">Topic</th>
            <th className="px-4 py-3">Field</th>
            <th className="px-4 py-3">Old</th>
            <th className="px-4 py-3">New</th>
            <th className="px-4 py-3">By</th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {logs.map((log) => (
            <tr key={log.id} className="align-top">
              <td className="whitespace-nowrap px-4 py-3 text-slate-600">{formatDate(log.changedAt)}</td>
              <td className="whitespace-nowrap px-4 py-3 font-medium text-slate-800">{log.state}</td>
              <td className="min-w-[180px] px-4 py-3 text-slate-700">{log.topic}</td>
              <td className="px-4 py-3">
                <div className="font-medium text-slate-700">{log.fieldName}</div>
                <div className="text-xs text-slate-500">{log.changeType}</div>
              </td>
              <td className="min-w-[260px] px-4 py-3 leading-6 text-slate-600">{log.oldValue ?? '-'}</td>
              <td className="min-w-[260px] px-4 py-3 leading-6 text-slate-700">{log.newValue ?? '-'}</td>
              <td className="whitespace-nowrap px-4 py-3 text-slate-600">{log.changedByName}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded border bg-white p-4">
      <div className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-2 text-xl font-semibold text-slate-800">{value}</div>
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
        {values.map((option) => <option key={option} value={option}>{cleanLabel(option)}</option>)}
      </select>
    </label>
  )
}

function TabButton({ active, onClick, icon: Icon, label }: { active: boolean; onClick: () => void; icon: React.ElementType; label: string }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`inline-flex items-center gap-2 rounded px-3 py-2 text-sm font-medium ${active ? 'bg-blue-50 text-blue-700' : 'text-slate-600 hover:bg-slate-50'}`}
    >
      <Icon className="h-4 w-4" />
      {label}
    </button>
  )
}

function CitationList({ citations }: { citations: string[] }) {
  if (citations.length === 0) return <span className="text-xs text-slate-400">None found</span>

  return (
    <div className="flex flex-wrap gap-1.5">
      {citations.map((citation) => (
        <span key={citation} className="rounded bg-slate-100 px-2 py-1 text-xs text-slate-600">{citation}</span>
      ))}
    </div>
  )
}

function StatusPill({ status }: { status: string }) {
  const normalized = status.toLowerCase()
  const styles = normalized.includes('complete') || normalized.includes('approved') || normalized === 'nochange'
    ? 'border-green-200 bg-green-50 text-green-700'
    : normalized.includes('pending') || normalized.includes('review') || normalized.includes('seeded')
      ? 'border-amber-200 bg-amber-50 text-amber-700'
      : normalized.includes('fail') || normalized.includes('change')
        ? 'border-red-200 bg-red-50 text-red-700'
        : 'border-slate-200 bg-slate-50 text-slate-600'

  return <span className={`rounded-full border px-2 py-0.5 text-xs font-medium ${styles}`}>{status}</span>
}

function PanelLoader() {
  return <div className="p-8"><LoadingSpinner /></div>
}

function EmptyPanel({ text }: { text: string }) {
  return <div className="p-6 text-sm text-slate-500">{text}</div>
}

function cleanLabel(value: string) {
  return value === ALL ? value : value.toLowerCase().replace(/\b\w/g, (c) => c.toUpperCase())
}

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}

function invalidateLegalQueries(queryClient: ReturnType<typeof useQueryClient>) {
  queryClient.invalidateQueries({ queryKey: ['legal-requirements'] })
}
