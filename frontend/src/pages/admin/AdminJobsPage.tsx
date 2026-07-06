import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertCircle, CheckCircle2, Clock3, DatabaseZap, Play, RefreshCw } from 'lucide-react'
import { toast } from 'sonner'
import { adminJobsApi } from '@/api/admin.api'
import type { FmcsaAnalyticsImportBatch } from '@/api/admin.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ErrorState } from '@/components/common/ErrorState'
import { getApiErrorMessage } from '@/lib/apiError'

export function AdminJobsPage() {
  const { data, isLoading, isFetching, isError, error, refetch } = useQuery({
    queryKey: ['admin', 'jobs', 'safety'],
    queryFn: adminJobsApi.getSafetyStatus,
    refetchInterval: (query) => query.state.data?.hasRunningImport ? 10000 : false,
  })

  const refreshImported = useJobMutation(adminJobsApi.refreshImportedSafety, 'Imported carrier analytics refreshed')
  const smsSample = useJobMutation(adminJobsApi.importSmsSample, 'SMS sample import started')
  const smsFull = useJobMutation(adminJobsApi.importSmsFull, 'Full SMS import started')
  const enrichInspections = useJobMutation(adminJobsApi.enrichInspectionDetails, 'Inspection detail enrichment completed')

  if (isLoading) return <LoadingSpinner />
  if (isError) return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="Admin Jobs"
        subtitle="Recurring data pulls and operational imports"
      />
      <ErrorState error={error} onRetry={refetch} />
    </div>
  )

  const running = data?.hasRunningImport || smsSample.isPending || smsFull.isPending || refreshImported.isPending || enrichInspections.isPending

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="Admin Jobs"
        subtitle="Recurring data pulls and operational imports"
        action={
          <button
            type="button"
            onClick={() => refetch()}
            disabled={isFetching}
            className="sd-btn primary"
          >
            <RefreshCw className={`h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} />
            Refresh
          </button>
        }
      />

      <section className="admin-panel">
        <div className="admin-panel-head">
          <div className="flex items-center gap-2">
            <DatabaseZap className="h-4 w-4" style={{ color: 'var(--ink-3)' }} />
            <h3 className="admin-panel-title">Safety Analytics</h3>
          </div>
          <StatusPill status={data?.hasRunningImport ? 'Running' : 'Ready'} />
        </div>

        {!data?.isConfigured ? (
          <div className="flex gap-2 p-5 text-sm" style={{ borderBottom: '1px solid var(--line-2)', background: 'var(--warn-bg)', color: 'var(--warn-fg)' }}>
            <AlertCircle className="h-4 w-4 mt-0.5" />
            Safety analytics database is not configured.
          </div>
        ) : (
          <>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3 p-5" style={{ borderBottom: '1px solid var(--line-2)' }}>
              <Metric label="Peer carriers" value={data.carrierPeerSnapshotCount.toLocaleString()} />
              <Metric label="BASIC measures" value={data.basicPeerMeasureCount.toLocaleString()} />
              <Metric label="Latest status" value={data.latestBatches[0]?.status ?? 'No runs'} />
            </div>

            <div className="p-5 flex flex-wrap gap-3">
              <JobButton label="Refresh Imported Carriers" onClick={() => refreshImported.mutate()} busy={refreshImported.isPending} disabled={running} />
              <JobButton label="Enrich Inspection Details" onClick={() => enrichInspections.mutate()} busy={enrichInspections.isPending} disabled={running} />
              <JobButton label="Run SMS Sample Import" onClick={() => smsSample.mutate()} busy={smsSample.isPending} disabled={running} />
              <JobButton label="Run Full SMS Import" onClick={() => smsFull.mutate()} busy={smsFull.isPending} disabled={running} strong />
            </div>

            {data.scheduledJobs.length > 0 && <ScheduleTable jobs={data.scheduledJobs} />}
            <BatchTable batches={data.latestBatches} />
          </>
        )}
      </section>

      <PlaceholderJob title="Cancellation Data" description="Reserved for cancellation feed imports, status, and schedule controls." />
      <PlaceholderJob title="Claims Data" description="Reserved for claims/loss-run imports, status, and schedule controls." />
    </div>
  )
}

function useJobMutation(action: () => Promise<unknown>, successMessage: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: action,
    onSuccess: () => {
      toast.success(successMessage)
      qc.invalidateQueries({ queryKey: ['admin', 'jobs', 'safety'] })
    },
    onError: (err) => {
      toast.error(getApiErrorMessage(err, 'Job could not be started'))
    },
  })
}

function ScheduleTable({ jobs }: { jobs: Array<{ name: string; enabled: boolean; schedule: string; nextRunAtUtc: string | null; status: string }> }) {
  return (
    <div style={{ borderTop: '1px solid var(--line-2)' }}>
      <div className="px-5 py-3 text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Schedule</div>
      <div className="grid grid-cols-1 gap-3 px-5 pb-5 md:grid-cols-3">
        {jobs.map((job) => (
          <div key={job.name} className="admin-muted-panel p-4">
            <div className="flex items-start justify-between gap-3">
              <div>
                <div className="text-sm font-semibold" style={{ color: 'var(--ink)' }}>{job.name}</div>
                <div className="mt-1 text-xs" style={{ color: 'var(--ink-3)' }}>{job.schedule}</div>
              </div>
              <StatusPill status={job.enabled ? job.status : 'Off'} />
            </div>
            <div className="mt-3 text-xs" style={{ color: 'var(--ink-3)' }}>
              Next run: <span className="font-medium" style={{ color: 'var(--ink-2)' }}>{job.nextRunAtUtc ? formatDateTime(job.nextRunAtUtc) : '-'}</span>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}

function JobButton({ label, onClick, busy, disabled, strong = false }: { label: string; onClick: () => void; busy: boolean; disabled: boolean; strong?: boolean }) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled || busy}
      className={`sd-btn ${strong ? 'primary' : 'outline'}`}
    >
      {busy ? <RefreshCw className="h-4 w-4 animate-spin" /> : <Play className="h-4 w-4" />}
      {label}
    </button>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="admin-muted-panel p-4">
      <div className="text-xs font-medium uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>{label}</div>
      <div className="mt-2 text-lg font-semibold" style={{ color: 'var(--ink)' }}>{value}</div>
    </div>
  )
}

function BatchTable({ batches }: { batches: FmcsaAnalyticsImportBatch[] }) {
  if (batches.length === 0) {
    return <div className="px-5 pb-5 text-sm text-slate-500">No safety jobs have been run yet.</div>
  }

  return (
    <table className="sd-table">
      <thead>
        <tr>
          <th>Job</th>
          <th>Status</th>
          <th>Rows</th>
          <th>Started</th>
          <th>Completed</th>
        </tr>
      </thead>
      <tbody className="divide-y">
        {batches.map((batch) => (
          <tr key={`${batch.sourceName}-${batch.startedAt}`}>
            <td className="px-4 py-3">
              <div className="font-medium" style={{ color: 'var(--ink)' }}>{batch.sourceName}</div>
              {batch.errorMessage && <div className="mt-1 text-xs text-red-600">{batch.errorMessage}</div>}
            </td>
            <td className="px-4 py-3"><StatusPill status={batch.status} /></td>
            <td className="px-4 py-3 font-mono text-xs">{batch.rowsImported.toLocaleString()}</td>
            <td>{formatDateTime(batch.startedAt)}</td>
            <td>{batch.completedAt ? formatDateTime(batch.completedAt) : '-'}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function PlaceholderJob({ title, description }: { title: string; description: string }) {
  return (
    <section className="admin-panel p-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h3 className="admin-panel-title">{title}</h3>
          <p className="mt-1 text-sm" style={{ color: 'var(--ink-3)' }}>{description}</p>
        </div>
        <span className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-500">Not configured</span>
      </div>
    </section>
  )
}

function StatusPill({ status }: { status: string }) {
  const normalized = status.toLowerCase()
  const styles = normalized === 'completed' || normalized === 'ready'
    ? 'bg-green-50 text-green-700 border-green-200'
    : normalized === 'running'
      ? 'bg-blue-50 text-blue-700 border-blue-200'
      : normalized === 'failed'
        ? 'bg-red-50 text-red-700 border-red-200'
        : 'bg-slate-50 text-slate-600 border-slate-200'
  const Icon = normalized === 'running' ? Clock3 : normalized === 'failed' ? AlertCircle : CheckCircle2

  return (
    <span className={`inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-xs font-medium ${styles}`}>
      <Icon className="h-3.5 w-3.5" />
      {status}
    </span>
  )
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString()
}
