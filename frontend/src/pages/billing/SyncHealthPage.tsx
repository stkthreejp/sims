import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Download, RefreshCw, Play, CheckCircle2, XCircle, Clock, AlertCircle, FileText,
  Wifi, WifiOff, AlertTriangle,
} from 'lucide-react'
import { toast } from 'sonner'
import { getRollups, triggerRollup, resyncRollup, getRollupDownloadUrl, getXeroStatus } from '@/api/rollup.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ErrorState } from '@/components/common/ErrorState'
import { getApiErrorMessage } from '@/lib/apiError'
import { CashBalanceBadge } from '@/components/accounting/CashBalanceBadge'
import { usePermissions } from '@/hooks/usePermissions'
import type { RollupSummary, PendingJournalSync } from '@/types/rollup.types'

const MONTH_NAMES = [
  '', 'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
  'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
]

const fmtDateTime = (s: string) =>
  new Date(s).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' })

function StatusBadge({ status }: { status: RollupSummary['status'] }) {
  const cfg: Record<string, { cls: string; style?: React.CSSProperties; icon: React.ReactNode }> = {
    Pending:   { cls: '',  style: { background: 'var(--warn-bg)', color: 'var(--warn-fg)' },  icon: <Clock className="h-3 w-3" /> },
    Exported:  { cls: '',  style: { background: 'var(--good-bg)', color: 'var(--good-fg)' }, icon: <CheckCircle2 className="h-3 w-3" /> },
    Posted:    { cls: '',  style: { background: 'var(--surface-2)', color: 'var(--accent-ink)' }, icon: <CheckCircle2 className="h-3 w-3" /> },
    Failed:    { cls: '',  style: { background: 'var(--bad-bg)', color: 'var(--bad-fg)' },   icon: <XCircle className="h-3 w-3" /> },
    Divergent: { cls: '',  style: { background: 'var(--warn-bg)', color: 'var(--warn-fg)' }, icon: <AlertTriangle className="h-3 w-3" /> },
  }
  const { cls, style: badgeStyle, icon } = cfg[status] ?? { cls: '', style: { background: 'var(--surface-2)', color: 'var(--ink-3)' }, icon: null }
  return (
    <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium ${cls}`} style={badgeStyle}>
      {icon}{status}
    </span>
  )
}

const DRIVER_LABELS: Record<string, string> = { Xero: 'Xero', CSV: 'CSV' }

function DriverBadge({ type }: { type: string }) {
  const accent = type === 'Xero'
  return (
    <span
      className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium"
      style={accent ? { background: 'var(--surface-2)', color: 'var(--accent-ink)' } : { background: 'var(--surface-2)', color: 'var(--ink-3)' }}
    >
      {DRIVER_LABELS[type] ?? type}
    </span>
  )
}

// ---------- Provider Status Card ----------

function ProviderStatusCard({ providerName, connected, pending }: { providerName: string; connected: boolean; pending: PendingJournalSync[] }) {
  const activePending = pending.filter(p => p.status !== 'Done')
  const failedCount = pending.filter(p => p.status === 'Failed').length
  const retryingCount = pending.filter(p => p.status === 'Retrying' || p.status === 'Pending').length

  return (
    <div
      className="border rounded-lg px-5 py-4"
      style={
        failedCount > 0 ? { background: 'var(--bad-bg)', borderColor: 'var(--bad-fg)' } :
        retryingCount > 0 ? { background: 'var(--warn-bg)', borderColor: 'var(--warn-fg)' } :
        connected ? { background: 'var(--good-bg)', borderColor: 'var(--line)' } :
        { background: 'var(--surface-2)', borderColor: 'var(--line)' }
      }
    >
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2">
          {connected
            ? <Wifi className="h-4 w-4" style={{ color: 'var(--good-fg)' }} />
            : <WifiOff className="h-4 w-4" style={{ color: 'var(--ink-4)' }} />}
          <span className="text-sm font-semibold" style={{ color: 'var(--ink-2)' }}>{providerName}</span>
        </div>
        <span
          className="text-xs font-medium px-2 py-0.5 rounded-full"
          style={connected ? { background: 'var(--good-bg)', color: 'var(--good-fg)' } : { background: 'var(--surface-2)', color: 'var(--ink-3)' }}
        >
          {connected ? 'Connected' : 'Not Connected'}
        </span>
      </div>

      {connected && activePending.length === 0 && (
        <p className="text-xs" style={{ color: 'var(--good-fg)' }}>All syncs up to date</p>
      )}

      {activePending.length > 0 && (
        <div className="mt-1">
          <p className="text-xs font-medium mb-2" style={{ color: 'var(--ink-3)' }}>
            Pending sync queue ({activePending.length})
          </p>
          <div className="space-y-1">
            {activePending.map(sync => (
              <div key={sync.id} className="flex items-center justify-between text-xs rounded border px-2 py-1.5" style={{ background: 'var(--surface)', borderColor: 'var(--line-2)' }}>
                <div className="flex items-center gap-2">
                  <span className="font-medium" style={{ color: 'var(--ink-2)' }}>{sync.period}</span>
                  <span
                    className="px-1.5 py-0.5 rounded font-medium"
                    style={
                      sync.status === 'Failed'   ? { background: 'var(--bad-bg)', color: 'var(--bad-fg)' } :
                      sync.status === 'Retrying' ? { background: 'var(--warn-bg)', color: 'var(--warn-fg)' } :
                      { background: 'var(--surface-2)', color: 'var(--ink-3)' }
                    }
                  >{sync.status}</span>
                  <span style={{ color: 'var(--ink-4)' }}>attempt {sync.attemptCount}/6</span>
                </div>
                {sync.nextRetryAt && (
                  <span style={{ color: 'var(--ink-4)' }}>retry {fmtDateTime(sync.nextRetryAt)}</span>
                )}
              </div>
            ))}
          </div>
          {activePending.some(p => p.lastError) && (
            <div className="mt-2 flex items-start gap-1.5 text-xs" style={{ color: 'var(--bad-fg)' }}>
              <AlertCircle className="h-3.5 w-3.5 flex-shrink-0 mt-0.5" />
              <span className="truncate">{activePending.find(p => p.lastError)?.lastError}</span>
            </div>
          )}
        </div>
      )}

      {!connected && (
        <p className="text-xs mt-1" style={{ color: 'var(--ink-3)' }}>
          Configure OAuth credentials in <code className="px-1 rounded" style={{ background: 'var(--surface-2)' }}>appsettings.json</code> to enable {providerName} sync.
        </p>
      )}
    </div>
  )
}

// ---------- Trigger Modal ----------

interface TriggerModalProps {
  onClose: () => void
  xeroConnected: boolean
}

function TriggerModal({ onClose, xeroConnected }: TriggerModalProps) {
  const qc = useQueryClient()
  const today = new Date()
  const [year, setYear] = useState(today.getFullYear())
  const [month, setMonth] = useState(today.getMonth() + 1)
  // Default to Xero (the active accounting system); fall back to CSV if not yet connected.
  const [driver, setDriver] = useState(xeroConnected ? 'Xero' : 'CSV')

  const mutation = useMutation({
    mutationFn: () => triggerRollup(year, month, driver),
    onSuccess: (r) => {
      toast.success(`Rollup created: ${r.transactionCount} transaction(s), ${r.lineCount} lines`)
      qc.invalidateQueries({ queryKey: ['rollups'] })
      qc.invalidateQueries({ queryKey: ['xero-status'] })
      onClose()
    },
    onError: (e) => toast.error(getApiErrorMessage(e)),
  })

  const years = Array.from({ length: 3 }, (_, i) => today.getFullYear() - i)

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="rounded-lg shadow-xl w-full max-w-sm" style={{ background: 'var(--surface)' }}>
        <div className="px-6 py-4 border-b flex items-center justify-between" style={{ borderColor: 'var(--line)' }}>
          <h2 className="text-base font-semibold" style={{ color: 'var(--ink)' }}>Create Journal Entry Rollup</h2>
          <button onClick={onClose} className="text-lg leading-none" style={{ color: 'var(--ink-4)' }}>×</button>
        </div>
        <div className="p-6 space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium mb-1" style={{ color: 'var(--ink-3)' }}>Year</label>
              <select
                value={year}
                onChange={(e) => setYear(Number(e.target.value))}
                className="sd-input w-full px-2 py-1.5 text-sm"
              >
                {years.map((y) => <option key={y} value={y}>{y}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium mb-1" style={{ color: 'var(--ink-3)' }}>Month</label>
              <select
                value={month}
                onChange={(e) => setMonth(Number(e.target.value))}
                className="sd-input w-full px-2 py-1.5 text-sm"
              >
                {MONTH_NAMES.slice(1).map((m, i) => (
                  <option key={i + 1} value={i + 1}>{m}</option>
                ))}
              </select>
            </div>
          </div>
          <div>
            <label className="block text-xs font-medium mb-1" style={{ color: 'var(--ink-3)' }}>Driver</label>
            <div className="flex gap-2">
              {[
                { key: 'Xero', label: xeroConnected ? 'Xero' : 'Xero (not connected)', disabled: !xeroConnected },
                { key: 'CSV', label: 'CSV Download', disabled: false },
              ].map(({ key, label, disabled }) => (
                <button
                  key={key}
                  onClick={() => !disabled && setDriver(key)}
                  className={`flex-1 px-3 py-2 rounded-md text-sm font-medium border transition-colors ${disabled ? 'opacity-50 cursor-not-allowed' : ''}`}
                  style={driver === key
                    ? { borderColor: 'var(--accent)', background: 'var(--surface-2)', color: 'var(--accent-ink)' }
                    : { borderColor: 'var(--line)', color: 'var(--ink-3)' }
                  }
                  disabled={disabled}
                  title={disabled ? `${label} not connected — configure OAuth credentials first` : undefined}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>
          <p className="text-xs" style={{ color: 'var(--ink-4)' }}>
            {driver === 'CSV'
              ? 'All unrolled posted transactions in this period will be grouped into a balanced CSV and uploaded to Azure Blob.'
              : 'Each transaction group will be posted directly to Xero as a Manual Journal.'}
          </p>
        </div>
        <div className="flex justify-end gap-3 px-6 py-4 border-t rounded-b-lg" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
          <button onClick={onClose} className="sd-btn outline px-4 py-2 text-sm">
            Cancel
          </button>
          <button
            onClick={() => mutation.mutate()}
            disabled={mutation.isPending}
            className="sd-btn primary flex items-center gap-2 px-4 py-2 text-sm font-medium disabled:opacity-50"
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
  const { canAdminAccounting } = usePermissions()
  const [downloading, setDownloading] = useState(false)

  const resyncMutation = useMutation({
    mutationFn: () => resyncRollup(rollup.id),
    onSuccess: () => {
      toast.success('Resync complete')
      qc.invalidateQueries({ queryKey: ['rollups'] })
      qc.invalidateQueries({ queryKey: ['xero-status'] })
    },
    onError: (e) => toast.error(getApiErrorMessage(e)),
  })

  const handleResync = () => {
    // Failed rollups may resync freely; a non-Failed rollup is already Posted/Exported,
    // so re-exporting it risks duplicate posting — require an explicit confirmation.
    if (
      rollup.status !== 'Failed' &&
      !window.confirm(
        `${MONTH_NAMES[rollup.periodMonth]} ${rollup.periodYear} is already ${rollup.status}. ` +
        `Resyncing will re-export an already-posted rollup, which may create duplicate journal entries. Continue?`
      )
    ) {
      return
    }
    resyncMutation.mutate()
  }

  const handleDownload = async () => {
    setDownloading(true)
    try {
      const url = await getRollupDownloadUrl(rollup.id)
      window.open(url, '_blank')
    } catch (e: unknown) {
      toast.error(getApiErrorMessage(e, 'Download failed'))
    } finally {
      setDownloading(false)
    }
  }

  return (
    <tr className="hover:bg-gray-50 transition-colors">
      <td className="px-4 py-3 text-sm font-medium whitespace-nowrap" style={{ color: 'var(--ink-2)' }}>
        {MONTH_NAMES[rollup.periodMonth]} {rollup.periodYear}
      </td>
      <td className="px-4 py-3">
        <DriverBadge type={rollup.driverType} />
      </td>
      <td className="px-4 py-3 text-center">
        <StatusBadge status={rollup.status} />
      </td>
      <td className="px-4 py-3 text-right font-mono text-sm" style={{ color: 'var(--ink-2)' }}>
        {rollup.transactionCount.toLocaleString()}
      </td>
      <td className="px-4 py-3 text-xs whitespace-nowrap" style={{ color: 'var(--ink-4)' }}>
        {fmtDateTime(rollup.createdAt)}
      </td>
      <td className="px-4 py-3">
        {rollup.errorMessage && (
          <div className="flex items-center gap-1.5 text-xs" style={{ color: 'var(--bad-fg)' }}>
            <AlertCircle className="h-3.5 w-3.5 flex-shrink-0" />
            <span className="truncate max-w-[180px]" title={rollup.errorMessage}>
              {rollup.errorMessage}
            </span>
          </div>
        )}
        {rollup.externalId && (
          <span className="text-xs font-mono" style={{ color: 'var(--ink-3)' }}>{rollup.driverType}: {rollup.externalId.split(',')[0]}{rollup.externalId.includes(',') ? '…' : ''}</span>
        )}
        {rollup.status === 'Divergent' && (
          <div className="flex items-center gap-1 text-xs" style={{ color: 'var(--warn-fg)' }}>
            <AlertTriangle className="h-3.5 w-3.5 flex-shrink-0" />
            {rollup.driverType} divergence detected
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
              className="flex items-center gap-1 text-xs border rounded px-2 py-1 disabled:opacity-50"
              style={{ color: 'var(--accent-ink)', borderColor: 'var(--line)' }}
            >
              {downloading
                ? <RefreshCw className="h-3 w-3 animate-spin" />
                : <Download className="h-3 w-3" />}
              CSV
            </button>
          )}
          {canAdminAccounting && (
            <button
              onClick={handleResync}
              disabled={resyncMutation.isPending}
              title="Re-export this rollup"
              className="flex items-center gap-1 text-xs border rounded px-2 py-1 disabled:opacity-50"
              style={{ color: 'var(--ink-3)', borderColor: 'var(--line)' }}
            >
              <RefreshCw className={`h-3 w-3 ${resyncMutation.isPending ? 'animate-spin' : ''}`} />
              Resync
            </button>
          )}
        </div>
      </td>
    </tr>
  )
}

// ---------- Main Page ----------

export function SyncHealthPage() {
  const { isAdmin } = usePermissions()
  const [showTrigger, setShowTrigger] = useState(false)

  const { data: rollups = [], isLoading, isError, error, refetch } = useQuery({
    queryKey: ['rollups'],
    queryFn: getRollups,
  })

  const { data: xeroStatus } = useQuery({
    queryKey: ['xero-status'],
    queryFn: getXeroStatus,
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
        subtitle="Journal entry rollups and Xero export status"
        action={
          <div className="flex items-center gap-3">
            <CashBalanceBadge />
            {isAdmin && (
              <button
                onClick={() => setShowTrigger(true)}
                className="sd-btn primary flex items-center gap-2 px-3 py-2 text-sm font-medium"
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
        <div className="border rounded-lg px-5 py-4" style={{ background: 'var(--surface)', borderColor: 'var(--line)' }}>
          <p className="text-xs font-medium uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Total Rollups</p>
          <p className="text-2xl font-semibold mt-1" style={{ color: 'var(--ink)' }}>{rollups.length}</p>
        </div>
        <div className="border rounded-lg px-5 py-4" style={failedCount > 0 ? { background: 'var(--bad-bg)', borderColor: 'var(--bad-fg)' } : { background: 'var(--surface)', borderColor: 'var(--line)' }}>
          <p className="text-xs font-medium uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Failed</p>
          <p className="text-2xl font-semibold mt-1" style={{ color: failedCount > 0 ? 'var(--bad-fg)' : 'var(--ink)' }}>
            {failedCount}
          </p>
        </div>
        <div className="border rounded-lg px-5 py-4" style={{ background: 'var(--surface)', borderColor: 'var(--line)' }}>
          <p className="text-xs font-medium uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Exported</p>
          <p className="text-2xl font-semibold mt-1" style={{ color: 'var(--good-fg)' }}>{exportedCount}</p>
        </div>
        <div className="border rounded-lg px-5 py-4" style={divergentCount > 0 ? { background: 'var(--warn-bg)', borderColor: 'var(--warn-fg)' } : { background: 'var(--surface)', borderColor: 'var(--line)' }}>
          <p className="text-xs font-medium uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Divergent</p>
          <p className="text-2xl font-semibold mt-1" style={{ color: divergentCount > 0 ? 'var(--warn-fg)' : 'var(--ink)' }}>
            {divergentCount}
          </p>
        </div>
      </div>

      {/* Accounting provider connection status */}
      {xeroStatus && (
        <div className="mb-6">
          <ProviderStatusCard providerName="Xero" connected={xeroStatus.connected} pending={xeroStatus.pending} />
        </div>
      )}

      {isError ? (
        <ErrorState error={error} onRetry={refetch} />
      ) : isLoading ? (
        <div className="flex justify-center py-16"><LoadingSpinner /></div>
      ) : rollups.length === 0 ? (
        <div className="text-center py-16" style={{ color: 'var(--ink-4)' }}>
          <FileText className="h-10 w-10 mx-auto mb-3 opacity-30" />
          <p className="text-sm">No rollups yet. Click "New Rollup" to export a period.</p>
        </div>
      ) : (
        <div className="border rounded-lg overflow-hidden" style={{ background: 'var(--surface)', borderColor: 'var(--line)' }}>
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b" style={{ background: 'var(--surface-2)', borderColor: 'var(--line)' }}>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Period</th>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Driver</th>
                <th className="px-4 py-3 text-center text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Status</th>
                <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>JEs</th>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Created</th>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Detail</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {rollups.map((r) => <RollupRow key={r.id} rollup={r} />)}
            </tbody>
          </table>
          <div className="px-4 py-2 border-t text-xs" style={{ borderColor: 'var(--line-2)', background: 'var(--surface-2)', color: 'var(--ink-4)' }}>
            {rollups.length} rollup{rollups.length !== 1 ? 's' : ''}
          </div>
        </div>
      )}

      {showTrigger && (
        <TriggerModal
          onClose={() => setShowTrigger(false)}
          xeroConnected={xeroStatus?.connected ?? false}
        />
      )}
    </div>
  )
}
