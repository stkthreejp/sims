import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import axios from 'axios'
import { AlertCircle, CheckCircle2, Clock3, DatabaseZap, Play, RefreshCw } from 'lucide-react'
import { toast } from 'sonner'
import { adminJobsApi } from '@/api/admin.api'
import type { FmcsaAnalyticsImportBatch } from '@/api/admin.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'

export function AdminJobsPage() {
  const { data, isLoading, isFetching, refetch } = useQuery({
    queryKey: ['admin', 'jobs', 'safety'],
    queryFn: adminJobsApi.getSafetyStatus,
    refetchInterval: (query) => query.state.data?.hasRunningImport ? 10000 : false,
  })

  const refreshImported = useJobMutation(adminJobsApi.refreshImportedSafety, 'Imported carrier analytics refreshed')
  const smsSample = useJobMutation(adminJobsApi.importSmsSample, 'SMS sample import started')
  const smsFull = useJobMutation(adminJobsApi.importSmsFull, 'Full SMS import started')

  if (isLoading) return <LoadingSpinner />

  const running = data?.hasRunningImport || smsSample.isPending || smsFull.isPending || refreshImported.isPending

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
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg text-sm hover:bg-blue-700 disabled:opacity-50"
          >
            <RefreshCw className={`h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} />
            Refresh
          </button>
        }
      />

      <section className="bg-white border rounded-lg overflow-hidden">
        <div className="px-5 py-4 border-b flex items-center justify-between gap-4">
          <div className="flex items-center gap-2">
            <DatabaseZap className="h-4 w-4 text-slate-500" />
            <h3 className="font-semibold text-slate-800">Safety Analytics</h3>
          </div>
          <StatusPill status={data?.hasRunningImport ? 'Running' : 'Ready'} />
        </div>

        {!data?.isConfigured ? (
          <div className="p-5 text-sm text-amber-800 bg-amber-50 border-b border-amber-100 flex gap-2">
            <AlertCircle className="h-4 w-4 mt-0.5" />
            Safety analytics database is not configured.
          </div>
        ) : (
          <>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3 p-5 border-b">
              <Metric label="Peer carriers" value={data.carrierPeerSnapshotCount.toLocaleString()} />
              <Metric label="BASIC measures" value={data.basicPeerMeasureCount.toLocaleString()} />
              <Metric label="Latest status" value={data.latestBatches[0]?.status ?? 'No runs'} />
            </div>

            <div className="p-5 flex flex-wrap gap-3">
              <JobButton label="Refresh Imported Carriers" onClick={() => refreshImported.mutate()} busy={refreshImported.isPending} disabled={running} />
              <JobButton label="Run SMS Sample Import" onClick={() => smsSample.mutate()} busy={smsSample.isPending} disabled={running} />
              <JobButton label="Run Full SMS Import" onClick={() => smsFull.mutate()} busy={smsFull.isPending} disabled={running} strong />
            </div>

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
      const message = axios.isAxiosError(err)
        ? err.response?.data?.errorMessage ?? 'Job could not be started'
        : 'Job could not be started'
      toast.error(message)
    },
  })
}

function JobButton({ label, onClick, busy, disabled, strong = false }: { label: string; onClick: () => void; busy: boolean; disabled: boolean; strong?: boolean }) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled || busy}
      className={`inline-flex items-center gap-2 rounded px-3 py-2 text-sm font-medium disabled:opacity-50 ${strong ? 'bg-blue-600 text-white hover:bg-blue-700' : 'border border-slate-300 bg-white text-slate-700 hover:bg-slate-50'}`}
    >
      {busy ? <RefreshCw className="h-4 w-4 animate-spin" /> : <Play className="h-4 w-4" />}
      {label}
    </button>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded border bg-slate-50 p-4">
      <div className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-2 text-lg font-semibold text-slate-800">{value}</div>
    </div>
  )
}

function BatchTable({ batches }: { batches: FmcsaAnalyticsImportBatch[] }) {
  if (batches.length === 0) {
    return <div className="px-5 pb-5 text-sm text-slate-500">No safety jobs have been run yet.</div>
  }

  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="border-y bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
          <th className="px-4 py-3">Job</th>
          <th className="px-4 py-3">Status</th>
          <th className="px-4 py-3">Rows</th>
          <th className="px-4 py-3">Started</th>
          <th className="px-4 py-3">Completed</th>
        </tr>
      </thead>
      <tbody className="divide-y">
        {batches.map((batch) => (
          <tr key={`${batch.sourceName}-${batch.startedAt}`}>
            <td className="px-4 py-3">
              <div className="font-medium text-slate-800">{batch.sourceName}</div>
              {batch.errorMessage && <div className="mt-1 text-xs text-red-600">{batch.errorMessage}</div>}
            </td>
            <td className="px-4 py-3"><StatusPill status={batch.status} /></td>
            <td className="px-4 py-3 font-mono text-xs">{batch.rowsImported.toLocaleString()}</td>
            <td className="px-4 py-3 text-slate-600">{formatDateTime(batch.startedAt)}</td>
            <td className="px-4 py-3 text-slate-600">{batch.completedAt ? formatDateTime(batch.completedAt) : '-'}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function PlaceholderJob({ title, description }: { title: string; description: string }) {
  return (
    <section className="bg-white border rounded-lg p-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h3 className="font-semibold text-slate-800">{title}</h3>
          <p className="mt-1 text-sm text-slate-500">{description}</p>
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
