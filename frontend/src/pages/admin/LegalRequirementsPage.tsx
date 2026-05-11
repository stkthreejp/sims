import { useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BookOpenCheck, Check, ClipboardList, Database, Eye, History, Pencil, Plus, RefreshCw, Search, Upload, X } from 'lucide-react'
import { toast } from 'sonner'
import {
  legalRequirementsApi,
  type LegalRequirementChangeLog,
  type LegalRequirementSection,
  type LegalSourceScanResult,
  type LegalSourceScanRun,
  type LegalTrackedSourceInput,
  type LegalTrackedSource,
} from '@/api/legalRequirements.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { PageHeader } from '@/components/common/PageHeader'

const ALL = 'All'
type TabKey = 'requirements' | 'sources' | 'scans' | 'changes'
type DiffKind = 'same' | 'added' | 'removed'
type DiffPart = { text: string; kind: DiffKind }

export function LegalRequirementsPage() {
  const [activeTab, setActiveTab] = useState<TabKey>('requirements')
  const [state, setState] = useState(ALL)
  const [action, setAction] = useState(ALL)
  const [category, setCategory] = useState(ALL)
  const [search, setSearch] = useState('')
  const [scanReviewStatus, setScanReviewStatus] = useState('Pending')
  const [selectedScanResult, setSelectedScanResult] = useState<LegalSourceScanResult | null>(null)
  const [sourceEditor, setSourceEditor] = useState<LegalTrackedSource | 'new' | null>(null)
  const [selectedScanRunId, setSelectedScanRunId] = useState<string | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const queryClient = useQueryClient()

  const summaryQuery = useQuery({
    queryKey: ['legal-requirements', 'summary'],
    queryFn: legalRequirementsApi.getSummary,
  })

  const sectionsQuery = useQuery({
    queryKey: ['legal-requirements', 'sections', state, action, category, search],
    queryFn: () => legalRequirementsApi.getSections({
      state: state === ALL ? undefined : state,
      action: action === ALL ? undefined : action,
      category: category === ALL ? undefined : category,
      search: search.trim() || undefined,
    }),
  })

  const scanRunsQuery = useQuery({
    queryKey: ['legal-requirements', 'scan-runs'],
    queryFn: legalRequirementsApi.getScanRuns,
  })

  const sourcesQuery = useQuery({
    queryKey: ['legal-requirements', 'sources', state],
    queryFn: () => legalRequirementsApi.getSources({
      state: state === ALL ? undefined : state,
    }),
  })

  const scanResultsQuery = useQuery({
    queryKey: ['legal-requirements', 'scan-results', state, scanReviewStatus, selectedScanRunId],
    queryFn: () => legalRequirementsApi.getScanResults({
      state: state === ALL ? undefined : state,
      reviewStatus: scanReviewStatus === ALL ? undefined : scanReviewStatus,
      scanRunId: selectedScanRunId ?? undefined,
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
  const isRefreshing = summaryQuery.isFetching || sectionsQuery.isFetching || scanRunsQuery.isFetching || sourcesQuery.isFetching || scanResultsQuery.isFetching || changeLogQuery.isFetching
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
  const scanSourceMutation = useMutation({
    mutationFn: legalRequirementsApi.scanSource,
    onSuccess: (run) => {
      toast.success(`${run.sourceName} checked`)
      invalidateLegalQueries(queryClient)
      setActiveTab('sources')
    },
    onError: () => toast.error('Source could not be checked'),
  })
  const saveSourceMutation = useMutation({
    mutationFn: ({ source, input }: { source: LegalTrackedSource | 'new'; input: LegalTrackedSourceInput }) =>
      source === 'new' ? legalRequirementsApi.createSource(input) : legalRequirementsApi.updateSource(source.id, input),
    onSuccess: () => {
      toast.success('Tracked source saved')
      invalidateLegalQueries(queryClient)
      setSourceEditor(null)
      setActiveTab('sources')
    },
    onError: () => toast.error('Tracked source could not be saved'),
  })

  if (summaryQuery.isLoading) return <LoadingSpinner />

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="Cancellation Compliance"
        subtitle="Commercial P&C cancellation and non-renewal requirements, source scans, and change history"
        action={
          <button
            type="button"
            onClick={() => {
              summaryQuery.refetch()
              sectionsQuery.refetch()
              scanRunsQuery.refetch()
              sourcesQuery.refetch()
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
        <Metric label="Tracked sources" value={(summary?.trackedSourceCount ?? 0).toLocaleString()} />
        <Metric label="Change log entries" value={(summary?.changeLogCount ?? 0).toLocaleString()} />
      </section>

      <div className="rounded border bg-white">
        <div className="flex flex-wrap items-center gap-2 border-b px-4 py-3">
          <TabButton active={activeTab === 'requirements'} onClick={() => setActiveTab('requirements')} icon={BookOpenCheck} label="Requirements" />
          <TabButton active={activeTab === 'sources'} onClick={() => setActiveTab('sources')} icon={Database} label="Sources" />
          <TabButton active={activeTab === 'scans'} onClick={() => setActiveTab('scans')} icon={ClipboardList} label="Source Scans" />
          <TabButton active={activeTab === 'changes'} onClick={() => setActiveTab('changes')} icon={History} label="Change Log" />
        </div>

        <div className="flex flex-wrap items-center gap-3 border-b px-4 py-3">
          <SelectFilter label="State" value={state} values={[ALL, ...(summary?.states ?? [])]} onChange={setState} />
          {activeTab === 'requirements' && (
            <SelectFilter label="Action" value={action} values={[ALL, ...(summary?.actions ?? [])]} onChange={setAction} />
          )}
          {activeTab === 'requirements' && (
            <SelectFilter label="Category" value={category} values={[ALL, ...(summary?.categories ?? [])]} onChange={setCategory} />
          )}
          {activeTab === 'scans' && (
            <SelectFilter label="Review" value={scanReviewStatus} values={['Pending', ALL, 'Reviewed', 'Approved', 'Rejected']} onChange={setScanReviewStatus} />
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
        {activeTab === 'sources' && (
          sourcesQuery.isLoading
            ? <PanelLoader />
            : (
              <SourcePanel
                sources={sourcesQuery.data ?? []}
                scanningSourceId={scanSourceMutation.isPending ? scanSourceMutation.variables ?? null : null}
                onScan={(sourceId) => scanSourceMutation.mutate(sourceId)}
                onAdd={() => setSourceEditor('new')}
                onEdit={setSourceEditor}
              />
            )
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
                onReview={setSelectedScanResult}
                selectedRunId={selectedScanRunId}
                onSelectRun={(runId) => {
                  setSelectedScanRunId(runId)
                  setScanReviewStatus(ALL)
                }}
                onClearRun={() => setSelectedScanRunId(null)}
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
          {summary?.sourceName ?? 'Oden Online'}: {summary?.sourceDocument ?? 'COMMERCIAL INSURANCE - CANCELLATION / NONRENEWAL - P&C'}
          {summary?.sourceCreatedAt ? `, created ${formatDate(summary.sourceCreatedAt)}` : ''}
        </div>
      </section>

      {selectedScanResult && (
        <ScanReviewDrawer
          result={selectedScanResult}
          onClose={() => setSelectedScanResult(null)}
        />
      )}
      {sourceEditor && (
        <SourceEditorModal
          source={sourceEditor}
          states={summary?.states ?? []}
          saving={saveSourceMutation.isPending}
          onClose={() => setSourceEditor(null)}
          onSave={(input) => saveSourceMutation.mutate({ source: sourceEditor, input })}
        />
      )}
    </div>
  )
}

function SourcePanel({
  sources,
  scanningSourceId,
  onScan,
  onAdd,
  onEdit,
}: {
  sources: LegalTrackedSource[]
  scanningSourceId: string | null
  onScan: (sourceId: string) => void
  onAdd: () => void
  onEdit: (source: LegalTrackedSource) => void
}) {
  return (
    <div>
      <div className="flex items-center justify-end border-b px-4 py-3">
        <button
          type="button"
          onClick={onAdd}
          className="inline-flex items-center gap-2 rounded bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          <Plus className="h-4 w-4" />
          Add Source
        </button>
      </div>

      {sources.length === 0 ? (
        <EmptyPanel text="No tracked sources match the current filters." />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
                <th className="px-4 py-3">State</th>
                <th className="px-4 py-3">Source</th>
                <th className="px-4 py-3">Cadence</th>
                <th className="px-4 py-3">Last Checked</th>
                <th className="px-4 py-3">Last Changed</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {sources.map((source) => {
                const scanning = scanningSourceId === source.id
                return (
                  <tr key={source.id} className="align-top">
                    <td className="whitespace-nowrap px-4 py-3 font-medium text-slate-800">{source.state}</td>
                    <td className="min-w-[280px] px-4 py-3">
                      <div className="font-medium text-slate-800">{source.name}</div>
                      <div className="mt-1 text-xs text-slate-500">{source.sourceType}</div>
                      {source.notes && <div className="mt-2 max-w-xl leading-5 text-slate-600">{source.notes}</div>}
                      {source.url && <div className="mt-2 break-all text-xs text-blue-700">{source.url}</div>}
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-slate-700">{source.scanCadence}</td>
                    <td className="whitespace-nowrap px-4 py-3 text-slate-600">{source.lastCheckedAt ? formatDate(source.lastCheckedAt) : '-'}</td>
                    <td className="whitespace-nowrap px-4 py-3 text-slate-600">{source.lastChangedAt ? formatDate(source.lastChangedAt) : '-'}</td>
                    <td className="whitespace-nowrap px-4 py-3">
                      <StatusPill status={source.isEnabled ? source.lastStatus : 'Disabled'} />
                      {source.lastErrorMessage && <div className="mt-2 max-w-xs text-xs text-red-600">{source.lastErrorMessage}</div>}
                    </td>
                    <td className="whitespace-nowrap px-4 py-3">
                      <div className="flex items-center gap-2">
                        <button
                          type="button"
                          onClick={() => onEdit(source)}
                          className="inline-flex items-center gap-1.5 rounded border border-slate-300 px-2.5 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
                        >
                          <Pencil className="h-3.5 w-3.5" />
                          Edit
                        </button>
                        <button
                          type="button"
                          onClick={() => onScan(source.id)}
                          disabled={!source.isEnabled || scanningSourceId !== null}
                          className="inline-flex items-center gap-2 rounded border border-slate-300 px-2.5 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                        >
                          <RefreshCw className={`h-3.5 w-3.5 ${scanning ? 'animate-spin' : ''}`} />
                          Check
                        </button>
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function SourceEditorModal({
  source,
  states,
  saving,
  onClose,
  onSave,
}: {
  source: LegalTrackedSource | 'new'
  states: string[]
  saving: boolean
  onClose: () => void
  onSave: (input: LegalTrackedSourceInput) => void
}) {
  const [stateValue, setStateValue] = useState(source === 'new' ? 'All' : source.state)
  const [name, setName] = useState(source === 'new' ? '' : source.name)
  const [sourceType, setSourceType] = useState(source === 'new' ? 'Statute/Regulation' : source.sourceType)
  const [url, setUrl] = useState(source === 'new' ? '' : source.url ?? '')
  const [scanCadence, setScanCadence] = useState(source === 'new' ? 'Monthly' : source.scanCadence)
  const [isEnabled, setIsEnabled] = useState(source === 'new' ? true : source.isEnabled)
  const [notes, setNotes] = useState(source === 'new' ? '' : source.notes ?? '')
  const stateOptions = Array.from(new Set(['All', ...states, stateValue])).filter(Boolean)

  function submit(event: React.FormEvent) {
    event.preventDefault()
    onSave({
      state: stateValue,
      name,
      sourceType,
      url: url.trim() || null,
      isEnabled,
      scanCadence,
      notes: notes.trim() || null,
    })
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/30 p-4">
      <form onSubmit={submit} className="w-full max-w-2xl rounded border bg-white shadow-xl">
        <div className="flex items-center justify-between gap-4 border-b px-5 py-4">
          <h2 className="text-lg font-semibold text-slate-900">{source === 'new' ? 'Add Source' : 'Edit Source'}</h2>
          <button type="button" onClick={onClose} className="rounded p-2 text-slate-500 hover:bg-slate-100" aria-label="Close">
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="grid grid-cols-1 gap-4 p-5 md:grid-cols-2">
          <label className="block text-sm font-medium text-slate-700">
            State
            <select
              value={stateValue}
              onChange={(event) => setStateValue(event.target.value)}
              className="mt-1 w-full rounded border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
            >
              {stateOptions.map((option) => <option key={option} value={option}>{option}</option>)}
            </select>
          </label>

          <label className="block text-sm font-medium text-slate-700">
            Source Type
            <select
              value={sourceType}
              onChange={(event) => setSourceType(event.target.value)}
              className="mt-1 w-full rounded border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
            >
              {['Oden Export', 'Statute/Regulation', 'DOI Bulletin', 'OpenLaw API', 'LegiScan API', 'Other'].map((option) => (
                <option key={option} value={option}>{option}</option>
              ))}
            </select>
          </label>

          <label className="block text-sm font-medium text-slate-700 md:col-span-2">
            Source Name
            <input
              value={name}
              onChange={(event) => setName(event.target.value)}
              required
              maxLength={160}
              className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
            />
          </label>

          <label className="block text-sm font-medium text-slate-700 md:col-span-2">
            URL or API Endpoint
            <input
              value={url}
              onChange={(event) => setUrl(event.target.value)}
              placeholder="https://"
              className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
            />
          </label>

          <label className="block text-sm font-medium text-slate-700">
            Cadence
            <select
              value={scanCadence}
              onChange={(event) => setScanCadence(event.target.value)}
              className="mt-1 w-full rounded border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
            >
              {['Manual', 'Weekly', 'Monthly', 'Quarterly'].map((option) => <option key={option} value={option}>{option}</option>)}
            </select>
          </label>

          <label className="flex items-center gap-2 pt-6 text-sm font-medium text-slate-700">
            <input
              type="checkbox"
              checked={isEnabled}
              onChange={(event) => setIsEnabled(event.target.checked)}
              className="h-4 w-4 rounded border-slate-300 text-blue-600 focus:ring-blue-500"
            />
            Enabled
          </label>

          <label className="block text-sm font-medium text-slate-700 md:col-span-2">
            Notes
            <textarea
              value={notes}
              onChange={(event) => setNotes(event.target.value)}
              rows={4}
              maxLength={2000}
              className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
            />
          </label>
        </div>

        <div className="flex items-center justify-end gap-2 border-t px-5 py-4">
          <button type="button" onClick={onClose} className="rounded border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
            Cancel
          </button>
          <button type="submit" disabled={saving} className="inline-flex items-center gap-2 rounded bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50">
            {saving ? <RefreshCw className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
            Save
          </button>
        </div>
      </form>
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
            <th className="px-4 py-3">Action</th>
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
              <td className="whitespace-nowrap px-4 py-3 text-slate-700">{cleanAction(section.action)}</td>
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
  onReview,
  selectedRunId,
  onSelectRun,
  onClearRun,
}: {
  runs: LegalSourceScanRun[]
  results: LegalSourceScanResult[]
  onImport: () => void
  importing: boolean
  onSimulate: () => void
  simulating: boolean
  onReview: (result: LegalSourceScanResult) => void
  selectedRunId: string | null
  onSelectRun: (runId: string) => void
  onClearRun: () => void
}) {
  const selectedRun = runs.find((run) => run.id === selectedRunId)
  const runCounts = countScanResults(results)

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
        {selectedRun && (
          <div className="mt-4 rounded border bg-slate-50 p-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <div className="text-sm font-semibold text-slate-800">{selectedRun.sourceName} run detail</div>
                <div className="mt-1 text-xs text-slate-500">{formatDate(selectedRun.startedAt)} - {selectedRun.sourceType}</div>
              </div>
              <button
                type="button"
                onClick={onClearRun}
                className="rounded border border-slate-300 px-2.5 py-1.5 text-xs font-medium text-slate-700 hover:bg-white"
              >
                Show all runs
              </button>
            </div>
            <div className="mt-3 grid grid-cols-2 gap-2 md:grid-cols-5">
              <MiniMetric label="Results" value={results.length.toLocaleString()} />
              <MiniMetric label="No Change" value={(runCounts.NoChange ?? 0).toLocaleString()} />
              <MiniMetric label="Possible" value={(runCounts.PossibleChange ?? 0).toLocaleString()} />
              <MiniMetric label="New" value={(runCounts.NewRequirement ?? 0).toLocaleString()} />
              <MiniMetric label="Pending" value={(runCounts.Pending ?? 0).toLocaleString()} />
            </div>
          </div>
        )}

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
                  <tr key={run.id} className={run.id === selectedRunId ? 'bg-blue-50/60' : undefined}>
                    <td className="px-3 py-2">
                      <div className="font-medium text-slate-800">{run.sourceName}</div>
                      <div className="text-xs text-slate-500">{run.sourceType}</div>
                    </td>
                    <td className="px-3 py-2"><StatusPill status={run.status} /></td>
                    <td className="px-3 py-2">{run.resultsFound.toLocaleString()}</td>
                    <td className="px-3 py-2">{run.possibleChanges.toLocaleString()}</td>
                    <td className="px-3 py-2 text-slate-600">{formatDate(run.startedAt)}</td>
                    <td className="px-3 py-2 text-slate-600">
                      <div>{run.startedByName ?? '-'}</div>
                      <button
                        type="button"
                        onClick={() => onSelectRun(run.id)}
                        className="mt-1 text-xs font-medium text-blue-700 hover:text-blue-800"
                      >
                        View results
                      </button>
                    </td>
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
          <ScanResultTable results={results} onReview={onReview} />
        )}
      </div>
    </div>
  )
}

function countScanResults(results: LegalSourceScanResult[]) {
  return results.reduce<Record<string, number>>((acc, result) => {
    acc[result.matchStatus] = (acc[result.matchStatus] ?? 0) + 1
    acc[result.reviewStatus] = (acc[result.reviewStatus] ?? 0) + 1
    return acc
  }, {})
}

function ScanResultTable({ results, onReview }: { results: LegalSourceScanResult[]; onReview: (result: LegalSourceScanResult) => void }) {
  return (
    <div className="mt-3 overflow-x-auto">
      <table className="w-full text-left text-sm">
        <thead>
          <tr className="border-y bg-slate-50 text-xs uppercase tracking-wide text-slate-500">
            <th className="px-3 py-2">State</th>
            <th className="px-3 py-2">Topic</th>
            <th className="px-3 py-2">Match</th>
            <th className="px-3 py-2">Current</th>
            <th className="px-3 py-2">Proposed</th>
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
              <td className="min-w-[260px] px-3 py-2 leading-6 text-slate-600">{truncate(result.currentRequirementText ?? '-')}</td>
              <td className="min-w-[260px] px-3 py-2 leading-6 text-slate-700">{truncate(result.suggestedRequirementText ?? result.sourceText)}</td>
              <td className="px-3 py-2"><StatusPill status={result.reviewStatus} /></td>
              <td className="whitespace-nowrap px-3 py-2">
                <button
                  type="button"
                  onClick={() => onReview(result)}
                  className="inline-flex items-center gap-1.5 rounded border border-slate-300 px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-50"
                >
                  <Eye className="h-3.5 w-3.5" />
                  Review
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function ScanReviewDrawer({ result, onClose }: { result: LegalSourceScanResult; onClose: () => void }) {
  const [comment, setComment] = useState('')
  const queryClient = useQueryClient()
  const approve = useMutation({
    mutationFn: () => legalRequirementsApi.approveScanResult(result.id, comment),
    onSuccess: () => {
      toast.success('Scan result approved')
      invalidateLegalQueries(queryClient)
      onClose()
    },
    onError: () => toast.error('Scan result could not be approved'),
  })
  const reject = useMutation({
    mutationFn: () => legalRequirementsApi.rejectScanResult(result.id, comment),
    onSuccess: () => {
      toast.success('Scan result rejected')
      invalidateLegalQueries(queryClient)
      onClose()
    },
    onError: () => toast.error('Scan result could not be rejected'),
  })
  const proposedText = result.suggestedRequirementText ?? result.sourceText
  const canReview = result.reviewStatus === 'Pending'
  const busy = approve.isPending || reject.isPending

  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-slate-900/25">
      <aside className="flex h-full w-full max-w-5xl flex-col bg-white shadow-xl">
        <div className="flex items-start justify-between gap-4 border-b px-5 py-4">
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="text-lg font-semibold text-slate-900">{result.state} {result.topic}</h2>
              <StatusPill status={result.matchStatus} />
              <StatusPill status={result.reviewStatus} />
            </div>
            <div className="mt-1 text-sm text-slate-500">{cleanLabel(result.category)} - {result.sourceName}</div>
          </div>
          <button type="button" onClick={onClose} className="rounded p-2 text-slate-500 hover:bg-slate-100" aria-label="Close">
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="flex-1 overflow-auto p-5">
          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            <DiffPane
              title="Current Requirement"
              text={result.currentRequirementText ?? 'No existing requirement matched this source result.'}
              citations={result.currentCitations}
              diffParts={buildWordDiff(result.currentRequirementText ?? '', proposedText, 'removed')}
              muted
            />
            <DiffPane
              title="Proposed Requirement"
              text={proposedText}
              citations={splitCitationText(result.sourceCitation)}
              diffParts={buildWordDiff(result.currentRequirementText ?? '', proposedText, 'added')}
            />
          </div>

          <div className="mt-4 rounded border bg-slate-50 p-4">
            <div className="text-sm font-semibold text-slate-800">Source Text</div>
            <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-slate-700">{result.sourceText}</p>
            {result.confidenceScore !== null && (
              <div className="mt-3 text-xs text-slate-500">Confidence: {(result.confidenceScore * 100).toFixed(0)}%</div>
            )}
          </div>

          <label className="mt-4 block">
            <span className="text-sm font-semibold text-slate-800">Reviewer note</span>
            <textarea
              value={comment}
              onChange={(event) => setComment(event.target.value)}
              rows={4}
              placeholder="Add context for the change log"
              className="mt-2 w-full rounded border border-slate-300 p-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
            />
          </label>
        </div>

        <div className="flex items-center justify-end gap-2 border-t px-5 py-4">
          <button type="button" onClick={onClose} className="rounded border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
            Close
          </button>
          {canReview && (
            <>
              <button
                type="button"
                onClick={() => reject.mutate()}
                disabled={busy}
                className="inline-flex items-center gap-2 rounded border border-red-200 px-3 py-2 text-sm font-medium text-red-700 hover:bg-red-50 disabled:opacity-50"
              >
                <X className="h-4 w-4" />
                Reject
              </button>
              <button
                type="button"
                onClick={() => approve.mutate()}
                disabled={busy}
                className="inline-flex items-center gap-2 rounded bg-green-600 px-3 py-2 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50"
              >
                <Check className="h-4 w-4" />
                Approve
              </button>
            </>
          )}
        </div>
      </aside>
    </div>
  )
}

function DiffPane({
  title,
  text,
  citations,
  diffParts,
  muted = false,
}: {
  title: string
  text: string
  citations: string[]
  diffParts?: DiffPart[]
  muted?: boolean
}) {
  return (
    <section className={`rounded border p-4 ${muted ? 'bg-slate-50' : 'bg-white'}`}>
      <div className="text-sm font-semibold text-slate-800">{title}</div>
      {diffParts && diffParts.length > 0 ? (
        <p className="mt-3 whitespace-pre-wrap text-sm leading-7 text-slate-700">
          {diffParts.map((part, index) => (
            <span key={`${part.text}-${index}`} className={diffClass(part.kind)}>
              {part.text}
            </span>
          ))}
        </p>
      ) : (
        <p className="mt-3 whitespace-pre-wrap text-sm leading-6 text-slate-700">{text}</p>
      )}
      <div className="mt-4">
        <CitationList citations={citations} />
      </div>
    </section>
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

function MiniMetric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded border bg-white px-3 py-2">
      <div className="text-[11px] font-medium uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-1 text-base font-semibold text-slate-800">{value}</div>
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

function cleanAction(value: string) {
  return value === 'NonRenewal' ? 'Non-Renewal' : cleanLabel(value)
}

function truncate(value: string) {
  return value.length > 260 ? `${value.slice(0, 260)}...` : value
}

function splitCitationText(value: string) {
  return value.split(';').map((item) => item.trim()).filter(Boolean)
}

function buildWordDiff(current: string, proposed: string, side: 'removed' | 'added'): DiffPart[] {
  const left = tokenizeForDiff(current)
  const right = tokenizeForDiff(proposed)
  if (left.length === 0 && right.length === 0) return []
  if (normalizeForDiff(current) === normalizeForDiff(proposed)) {
    return [{ text: side === 'removed' ? current : proposed, kind: 'same' }]
  }

  const dp: number[][] = Array.from({ length: left.length + 1 }, () => Array(right.length + 1).fill(0))
  for (let i = left.length - 1; i >= 0; i--) {
    for (let j = right.length - 1; j >= 0; j--) {
      dp[i][j] = left[i] === right[j] ? dp[i + 1][j + 1] + 1 : Math.max(dp[i + 1][j], dp[i][j + 1])
    }
  }

  const parts: DiffPart[] = []
  let i = 0
  let j = 0
  while (i < left.length && j < right.length) {
    if (left[i] === right[j]) {
      pushDiffPart(parts, left[i], 'same')
      i++
      j++
    } else if (dp[i + 1][j] >= dp[i][j + 1]) {
      if (side === 'removed') pushDiffPart(parts, left[i], 'removed')
      i++
    } else {
      if (side === 'added') pushDiffPart(parts, right[j], 'added')
      j++
    }
  }

  while (i < left.length) {
    if (side === 'removed') pushDiffPart(parts, left[i], 'removed')
    i++
  }
  while (j < right.length) {
    if (side === 'added') pushDiffPart(parts, right[j], 'added')
    j++
  }

  return parts
}

function tokenizeForDiff(value: string) {
  return value.match(/\S+\s*/g) ?? []
}

function normalizeForDiff(value: string) {
  return value.replace(/\s+/g, ' ').trim()
}

function pushDiffPart(parts: DiffPart[], text: string, kind: DiffKind) {
  const last = parts[parts.length - 1]
  if (last && last.kind === kind) {
    last.text += text
    return
  }
  parts.push({ text, kind })
}

function diffClass(kind: DiffKind) {
  if (kind === 'added') return 'rounded bg-green-100 px-0.5 text-green-900'
  if (kind === 'removed') return 'rounded bg-red-100 px-0.5 text-red-900 line-through decoration-red-500'
  return ''
}

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}

function invalidateLegalQueries(queryClient: ReturnType<typeof useQueryClient>) {
  queryClient.invalidateQueries({ queryKey: ['legal-requirements'] })
}
