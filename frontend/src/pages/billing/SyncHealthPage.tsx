import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Download, RefreshCw, Play, CheckCircle2, XCircle, Clock, AlertCircle, FileText,
  Wifi, WifiOff, AlertTriangle,
} from 'lucide-react'
import { toast } from 'sonner'
import { getRollups, triggerRollup, resyncRollup, getRollupDownloadUrl, getQboStatus } from '@/api/rollup.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { CashBalanceBadge } from '@/components/accounting/CashBalanceBadge'
import type { RollupSummary, PendingQboSync } from '@/types/rollup.types'
import { useAuthStore } from '@/store/authStore'

const MONTH_NAMES = [
  '', 'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
  'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
]

const fmtDateTime = (s: string) =>
  new Date(s).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' })

function StatusBadge({ status }: { status: RollupSummary['status'] }) {
  const cfg: Record<string, { cls: string; icon: React.ReactNode }> = {
    Pending:   { cls: 'bg-yellow-100 text-yellow-700',  icon: <Clock className="h-3 w-3" /> },
    Exported:  { cls: 'bg-green-100 text-green-800',    icon: <CheckCircle2 className="h-3 w-3" /> },
    Posted:    { cls: 'bg-blue-100 text-blue-700',      icon: <CheckCircle2 className="h-3 w-3" /> },
    Failed:    { cls: 'bg-red-100 text-red-600',        icon: <XCircle className="h-3 w-3" /> },
    Divergent: { cls: 'bg-orange-100 text-orange-700',  icon: <AlertTriangle className="h-3 w-3" /> },
  }
  const { cls, icon } = cfg[status] ?? { cls: 'bg-gray-100 text-gray-600', icon: null }
  return (
    <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium ${cls}`}>
      {icon}{status}
    </span>
  )
}

function DriverBadge({ type }: { type: string }) {
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${
      type === 'QBO' ? 'bg-indigo-50 text-indigo-700' : 'bg-gray-100 text-gray-600'
    }`}>
      {type === 'QBO' ? 'QuickBooks' : 'CSV'}
    </span>
  )
}

// ---------- QBO Status Card ----------

function QboStatusCard({ connected, pending }: { connected: boolean; pending: PendingQboSync[] }) {
  const activePending = pending.filter(p => p.status !== 'Done')
  const failedCount = pending.filter(p => p.status === 'Failed').length
  const retryingCount = pending.filter(p => p.status === 'Retrying' || p.status === 'Pending').length

  return (
    <div className={`border rounded-lg px-5 py-4 ${
      failedCount > 0 ? 'bg-red-50 border-red-200' :
      retryingCount > 0 ? 'bg-yellow-50 border-yellow-200' :
      connected ? 'bg-green-50 border-green-200' : 'bg-gray-50 border-gray-200'
    }`}>
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2">
          {connected
            ? <Wifi className="h-4 w-4 text-green-600" />
            : <WifiOff className="h-4 w-4 text-gray-400" />}
          <span className="text-sm font-semibold text-gray-800">QuickBooks Online</span>
        </div>
        <span className={`text-xs font-medium px-2 py-0.5 rounded-full ${
          connected ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'
        }`}>
          {connected ? 'Connected' : 'Not Connected'}
        </span>
      </div>

      {connected && activePending.length === 0 && (
        <p className="text-xs text-green-700">All syncs up to date</p>
      )}

      {activePending.length > 0 && (
        <div className="mt-1">
          <p className="text-xs font-medium text-gray-600 mb-2">
            Pending sync queue ({activePending.length})
          </p>
          <div className="space-y-1">
            {activePending.map(sync => (
              <div key={sync.id} className="flex items-center justify-between text-xs bg-white rounded border border-gray-100 px-2 py-1.5">
                <div className="flex items-center gap-2">
                  <span className="font-medium text-gray-700">{sync.period}</span>
                  <span className={`px-1.5 py-0.5 rounded font-medium ${
                    sync.status === 'Failed'   ? 'bg-red-100 text-red-600' :
                    sync.status === 'Retrying' ? 'bg-yellow-100 text-yellow-700' :
                    'bg-gray-100 text-gray-600'
                  }`}>{sync.status}</span>
                  <span className="text-gray-400">attempt {sync.attemptCount}/6</span>
                </div>
                {sync.nextRetryAt && (
                  <span className="text-gray-400">retry {fmtDateTime(sync.nextRetryAt)}</span>
                )}
              </div>
            ))}
          </div>
          {activePending.some(p => p.lastError) && (
            <div className="mt-2 flex items-start gap-1.5 text-xs text-red-600">
              <AlertCircle className="h-3.5 w-3.5 flex-shrink-0 mt-0.5" />
              <span className="truncate">{activePending.find(p => p.lastError)?.lastError}</span>
            </div>
          )}
        </div>
      )}

      {!connected && (
        <p className="text-xs text-gray-500 mt-1">
          Configure OAuth credentials in <code className="bg-gray-100 px-1 rounded">appsettings.json</code> to enable QBO sync.
        </p>
      )}
    </div>
  )
}

// ---------- Trigger Modal ----------

interface TriggerModalProps {
  onClose: () => void
  qboConnected: boolean
}

function TriggerModal({ onClose, qboConnected }: TriggerModalProps) {
  const qc = useQueryClient()
  const today = new Date()
  const [year, setYear] = useState(today.getFullYear())
  const [month, setMonth] = useState(today.getMonth() + 1)
  const [driver, setDriver] = useState('CSV')

  const mutation = useMutation({
    mutationFn: () => triggerRollup(year, month, driver),
    onSuccess: (r) => {
      toast.success(`Rollup created: ${r.transactionCount} transaction(s), ${r.lineCount} lines`)
      qc.invalidateQueries({ queryKey: ['rollups'] })
      qc.invalidateQueries({ queryKey: ['qbo-status'] })
      onClose()
    },
    onError: (e: Error) => toast.error(e.message),
  })

  const years = Array.from({ length: 3 }, (_, i) => today.getFullYear() - i)

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-sm">
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <h2 className="text-base font-semibold text-gray-900">Create Journal Entry Rollup</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-lg leading-none">×</button>
        </div>
        <div className="p-6 space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">Year</label>
              <select
                value={year}
                onChange={(e) => setYear(Number(e.target.value))}
                className="w-full border border-gray-300 rounded px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-blue-400"
              >
                {years.map((y) => <option key={y} value={y}>{y}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">Month</label>
              <select
                value={month}
                onChange={(e) => setMonth(Number(e.target.value))}
                className="w-full border border-gray-300 rounded px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-blue-400"
              >
                {MONTH_NAMES.slice(1).map((m, i) => (
                  <option key={i + 1} value={i + 1}>{m}</option>
                ))}
              </select>
            </div>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">Driver</label>
            <div className="flex gap-2">
              {[
                { key: 'CSV', label: 'CSV Download', disabled: false },
                { key: 'QBO', label: qboConnected ? 'QuickBooks' : 'QuickBooks (not connected)', disabled: !qboConnected },
              ].map(({ key, label, disabled }) => (
                <button
                  key={key}
                  onClick={() => !disabled && setDriver(key)}
                  className={`flex-1 px-3 py-2 rounded-md text-sm font-medium border transition-colors ${
                    driver === key
                      ? 'border-blue-500 bg-blue-50 text-blue-700'
                      : 'border-gray-200 text-gray-600 hover:bg-gray-50'
                  } ${disabled ? 'opacity-50 cursor-not-allowed' : ''}`}
                  disabled={disabled}
                  title={disabled ? 'QBO not connected — configure OAuth credentials first' : undefined}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>
          <p className="text-xs text-gray-400">
            {driver === 'CSV'
              ? 'All unrolled posted transactions in this period will be grouped into a balanced CSV and uploaded to Azure Blob.'
              : 'Each transaction group will be posted directly to QuickBooks Online as a JournalEntry.'}
          </p>
        </div>
        <div className="flex justify-end gap-3 px-6 py-4 border-t border-gray-200 bg-gray-50 rounded-b-lg">
          <button onClick={onClose} className="px-4 py-2 text-sm text-gray-600 border border-gray-300 rounded-md hover:bg-gray-100">
            Cancel
          </button>
          <button
            onClick={() => mutation.mutate()}
            disabled={mutation.isPending}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-md hover:bg-blue-700 disabled:opacity-50"
          >
            <Play className="h-3.5 w-3.5" />
            {mutation.isPending ? 'Running…' : 'Create Rollup'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ---------- Row actions ----------

function RollupRow({ rollup }: { rollup: RollupSummary }) {
  const qc = useQueryClient()
  const [downloading, setDownloading] = useState(false)

  const resyncMutation = useMutation({
    mutationFn: () => resyncRollup(rollup.id),
    onSuccess: () => {
      toast.success('Resync complete')
      qc.invalidateQueries({ queryKey: ['rollups'] })
      qc.invalidateQueries({ queryKey: ['qbo-status'] })
    },
    onError: (e: Error) => toast.error(e.message),
  })

  const handleDownload = async () => {
    setDownloading(true)
    try {
      const url = await getRollupDownloadUrl(rollup.id)
      window.open(url, '_blank')
    } catch (e: unknown) {
      toast.error(e instanceof Error ? e.message : 'Download failed')
    } finally {
      setDownloading(false)
    }
  }

  return (
    <tr className="hover:bg-gray-50 transition-colors">
      <td className="px-4 py-3 text-sm text-gray-700 font-medium whitespace-nowrap">
        {MONTH_NAMES[rollup.periodMonth]} {rollup.periodYear}
      </td>
      <td className="px-4 py-3">
        <DriverBadge type={rollup.driverType} />
      </td>
      <td className="px-4 py-3 text-center">
        <StatusBadge status={rollup.status} />
      </td>
      <td className="px-4 py-3 text-right font-mono text-sm text-gray-700">
        {rollup.transactionCount.toLocaleString()}
      </td>
      <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">
        {fmtDateTime(rollup.createdAt)}
      </td>
      <td className="px-4 py-3">
        {rollup.errorMessage && (
          <div className="flex items-center gap-1.5 text-xs text-red-600">
            <AlertCircle className="h-3.5 w-3.5 flex-shrink-0" />
            <span className="truncate max-w-[180px]" title={rollup.errorMessage}>
              {rollup.errorMessage}
            </span>
          </div>
        )}
        {rollup.externalId && (
          <span className="text-xs font-mono text-gray-500">QBO: {rollup.externalId.split(',')[0]}{rollup.externalId.includes(',') ? '…' : ''}</span>
        )}
        {rollup.status === 'Divergent' && (
          <div className="flex items-center gap-1 text-xs text-orange-600">
            <AlertTriangle className="h-3.5 w-3.5 flex-shrink-0" />
            QBO divergence detected
          </div>
        )}
      </td>
      <td className="px-4 py-3">
        <div className="flex items-center justify-end gap-2">
          {rollup.blobUri && (
            <button
              onClick={handleDownload}
              disabled={downloading}
              title="Download CSV"
              className="flex items-center gap-1 text-xs text-blue-600 hover:text-blue-800 border border-blue-200 rounded px-2 py-1 hover:bg-blue-50 disabled:opacity-50"
            >
              {downloading
                ? <RefreshCw className="h-3 w-3 animate-spin" />
                : <Download className="h-3 w-3" />}
              CSV
            </button>
          )}
          <button
            onClick={() => resyncMutation.mutate()}
            disabled={resyncMutation.isPending}
            title="Re-export this rollup"
            className="flex items-center gap-1 text-xs text-gray-500 hover:text-gray-800 border border-gray-200 rounded px-2 py-1 hover:bg-gray-50 disabled:opacity-50"
          >
            <RefreshCw className={`h-3 w-3 ${resyncMutation.isPending ? 'animate-spin' : ''}`} />
            Resync
          </button>
        </div>
      </td>
    </tr>
  )
}

// ---------- Main Page ----------

export function SyncHealthPage() {
  const isAdmin = useAuthStore((s) => s.user?.roles?.includes('Admin') ?? false)
  const [showTrigger, setShowTrigger] = useState(false)

  const { data: rollups = [], isLoading } = useQuery({
    queryKey: ['rollups'],
    queryFn: getRollups,
  })

  const { data: qboStatus } = useQuery({
    queryKey: ['qbo-status'],
    queryFn: getQboStatus,
    staleTime: 30_000,
  })

  const pendingCount = rollups.filter(r => r.status === 'Pending').length
  const failedCount = rollups.filter(r => r.status === 'Failed').length
  const exportedCount = rollups.filter(r => r.status === 'Exported' || r.status === 'Posted').length
  const divergentCount = rollups.filter(r => r.status === 'Divergent').length

  return (
    <div className="p-6">
      <PageHeader
        title="Sync Health"
        subtitle="Journal entry rollups and QB export status"
        action={
          <div className="flex items-center gap-3">
            <CashBalanceBadge />
            {isAdmin && (
              <button
                onClick={() => setShowTrigger(true)}
                className="flex items-center gap-2 px-3 py-2 text-sm font-medium text-white bg-blue-600 rounded-md hover:bg-blue-700"
              >
                <Play className="h-3.5 w-3.5" />
                New Rollup
              </button>
            )}
          </div>
        }
      />

      {/* Summary cards */}
      <div className="grid grid-cols-4 gap-4 mb-6">
        <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
          <p className="text-xs text-gray-500 font-medium uppercase tracking-wide">Total Rollups</p>
          <p className="text-2xl font-semibold text-gray-900 mt-1">{rollups.length}</p>
        </div>
        <div className={`border rounded-lg px-5 py-4 ${failedCount > 0 ? 'bg-red-50 border-red-200' : 'bg-white border-gray-200'}`}>
          <p className="text-xs text-gray-500 font-medium uppercase tracking-wide">Failed</p>
          <p className={`text-2xl font-semibold mt-1 ${failedCount > 0 ? 'text-red-700' : 'text-gray-900'}`}>
            {failedCount}
          </p>
        </div>
        <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
          <p className="text-xs text-gray-500 font-medium uppercase tracking-wide">Exported</p>
          <p className="text-2xl font-semibold text-green-700 mt-1">{exportedCount}</p>
        </div>
        <div className={`border rounded-lg px-5 py-4 ${divergentCount > 0 ? 'bg-orange-50 border-orange-200' : 'bg-white border-gray-200'}`}>
          <p className="text-xs text-gray-500 font-medium uppercase tracking-wide">Divergent</p>
          <p className={`text-2xl font-semibold mt-1 ${divergentCount > 0 ? 'text-orange-700' : 'text-gray-900'}`}>
            {divergentCount}
          </p>
        </div>
      </div>

      {/* QBO connection status */}
      {qboStatus && (
        <div className="mb-6">
          <QboStatusCard connected={qboStatus.connected} pending={qboStatus.pending} />
        </div>
      )}

      {isLoading ? (
        <div className="flex justify-center py-16"><LoadingSpinner /></div>
      ) : rollups.length === 0 ? (
        <div className="text-center py-16 text-gray-400">
          <FileText className="h-10 w-10 mx-auto mb-3 opacity-30" />
          <p className="text-sm">No rollups yet. Click "New Rollup" to export a period.</p>
        </div>
      ) : (
        <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-50 border-b border-gray-200">
                <th className="px-4 py-3 text-left text-xs font-semibold text-gray-600 uppercase tracking-wide">Period</th>
                <th className="px-4 py-3 text-left text-xs font-semibold text-gray-600 uppercase tracking-wide">Driver</th>
                <th className="px-4 py-3 text-center text-xs font-semibold text-gray-600 uppercase tracking-wide">Status</th>
                <th className="px-4 py-3 text-right text-xs font-semibold text-gray-600 uppercase tracking-wide">JEs</th>
                <th className="px-4 py-3 text-left text-xs font-semibold text-gray-600 uppercase tracking-wide">Created</th>
                <th className="px-4 py-3 text-left text-xs font-semibold text-gray-600 uppercase tracking-wide">Detail</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {rollups.map((r) => <RollupRow key={r.id} rollup={r} />)}
            </tbody>
          </table>
          <div className="px-4 py-2 border-t border-gray-100 bg-gray-50 text-xs text-gray-400">
            {rollups.length} rollup{rollups.length !== 1 ? 's' : ''}
          </div>
        </div>
      )}

      {showTrigger && (
        <TriggerModal
          onClose={() => setShowTrigger(false)}
          qboConnected={qboStatus?.connected ?? false}
        />
      )}
    </div>
  )
}
