import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Activity, X, ChevronRight, AlertCircle, RotateCcw, CheckCircle2, Clock,
} from 'lucide-react'
import { toast } from 'sonner'
import {
  getActivity, voidReceipt, voidCashApplication, voidInvoice, voidDisbursement,
} from '@/api/activity.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { ActivityEvent, ActivityFilter } from '@/types/activity.types'
import { useAuthStore } from '@/store/authStore'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
const fmtDateTime = (s: string) =>
  new Date(s).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' })
const fmtDate = (s: string) =>
  new Date(s + 'T00:00:00').toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })

const SOURCE_TYPES = ['Invoice', 'Receipt', 'CashApplication', 'Disbursement', 'Distribution']

function StatusBadge({ status }: { status: string }) {
  const styles: Record<string, string> = {
    Posted: 'bg-green-100 text-green-800',
    Voided: 'bg-red-100 text-red-600',
    Reversal: 'bg-purple-100 text-purple-700',
  }
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${styles[status] ?? 'bg-gray-100 text-gray-600'}`}>
      {status}
    </span>
  )
}

function SourceTypeBadge({ type }: { type: string }) {
  const styles: Record<string, string> = {
    Invoice: 'bg-blue-50 text-blue-700',
    Receipt: 'bg-teal-50 text-teal-700',
    CashApplication: 'bg-indigo-50 text-indigo-700',
    Disbursement: 'bg-orange-50 text-orange-700',
    Distribution: 'bg-yellow-50 text-yellow-700',
  }
  const labels: Record<string, string> = { CashApplication: 'Cash App' }
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${styles[type] ?? 'bg-gray-100 text-gray-600'}`}>
      {labels[type] ?? type}
    </span>
  )
}

// ---------- Void Modal ----------

interface VoidModalProps {
  event: ActivityEvent
  onClose: () => void
  onVoided: () => void
}

function VoidModal({ event, onClose, onVoided }: VoidModalProps) {
  const qc = useQueryClient()
  const [reason, setReason] = useState('')

  const voidFn = (id: number, r?: string) => {
    switch (event.sourceType) {
      case 'Receipt': return voidReceipt(id, r)
      case 'CashApplication': return voidCashApplication(id, r)
      case 'Invoice': return voidInvoice(id, r)
      case 'Disbursement': return voidDisbursement(id, r)
      default: return Promise.reject(new Error('Void not supported for this type'))
    }
  }

  const mutation = useMutation({
    mutationFn: () => voidFn(event.sourceId, reason || undefined),
    onSuccess: () => {
      toast.success(`${event.sourceNumber} voided successfully`)
      qc.invalidateQueries({ queryKey: ['activity'] })
      onVoided()
      onClose()
    },
    onError: (e: Error) => toast.error(e.message),
  })

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-[60] p-4">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-md">
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200">
          <h2 className="text-base font-semibold text-gray-900 flex items-center gap-2">
            <RotateCcw className="h-4 w-4 text-red-500" />
            Void {event.sourceNumber}
          </h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-700">
            <X className="h-5 w-5" />
          </button>
        </div>
        <div className="p-6 space-y-4">
          <p className="text-sm text-gray-600">
            This will create reversing GL entries and mark <span className="font-mono font-semibold">{event.sourceNumber}</span> as voided.
            This action cannot be undone.
          </p>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Reason (optional)</label>
            <textarea
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Enter void reason..."
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>
        <div className="flex justify-end gap-3 px-6 py-4 border-t border-gray-200 bg-gray-50 rounded-b-lg">
          <button
            onClick={onClose}
            className="px-4 py-2 text-sm text-gray-700 border border-gray-300 rounded-md hover:bg-gray-100"
          >
            Cancel
          </button>
          <button
            onClick={() => mutation.mutate()}
            disabled={mutation.isPending}
            className="px-4 py-2 text-sm font-medium text-white bg-red-600 rounded-md hover:bg-red-700 disabled:opacity-50"
          >
            {mutation.isPending ? 'Voiding…' : 'Confirm Void'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ---------- Detail Drawer ----------

interface DrawerProps {
  event: ActivityEvent
  onClose: () => void
  onVoidClick: (event: ActivityEvent) => void
}

function DetailDrawer({ event, onClose, onVoidClick }: DrawerProps) {
  const isAdmin = useAuthStore((s) => s.user?.roles?.includes('Admin') ?? false)

  const qbDeepLink = event.lines.some((l) => l.memo?.includes('QB'))
    ? '#' // placeholder — real QB link from RolledUpIn if available
    : null

  return (
    <div className="fixed inset-0 z-50 flex justify-end">
      <div className="absolute inset-0 bg-black/30" onClick={onClose} />
      <div className="relative bg-white w-full max-w-lg shadow-2xl flex flex-col overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200">
          <div className="flex items-center gap-3">
            <SourceTypeBadge type={event.sourceType} />
            <span className="font-mono font-semibold text-gray-900">{event.sourceNumber}</span>
            <StatusBadge status={event.postingStatus} />
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Meta */}
        <div className="px-6 py-4 border-b border-gray-100 grid grid-cols-2 gap-3 text-sm">
          <div>
            <span className="text-gray-500">Effective date</span>
            <p className="font-medium text-gray-900">{fmtDate(event.effectiveDate)}</p>
          </div>
          <div>
            <span className="text-gray-500">Posted at</span>
            <p className="font-medium text-gray-900">{fmtDateTime(event.postedAt)}</p>
          </div>
          {event.sourceDescription && (
            <div className="col-span-2">
              <span className="text-gray-500">Description</span>
              <p className="font-medium text-gray-900">{event.sourceDescription}</p>
            </div>
          )}
          {event.postingStatus === 'Voided' && event.voidReason && (
            <div className="col-span-2">
              <span className="text-gray-500">Void reason</span>
              <p className="font-medium text-red-700">{event.voidReason}</p>
            </div>
          )}
          {event.postingStatus === 'Voided' && event.voidedByTransactionId && (
            <div className="col-span-2">
              <span className="text-gray-500 text-xs">Voided by transaction</span>
              <p className="font-mono text-xs text-gray-500 truncate">{event.voidedByTransactionId}</p>
            </div>
          )}
          {event.postingStatus === 'Reversal' && event.reversesTransactionId && (
            <div className="col-span-2">
              <span className="text-gray-500 text-xs">Reverses transaction</span>
              <p className="font-mono text-xs text-gray-500 truncate">{event.reversesTransactionId}</p>
            </div>
          )}
        </div>

        {/* Ledger lines */}
        <div className="flex-1 overflow-y-auto">
          <div className="px-6 pt-4 pb-2">
            <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Ledger Postings</p>
          </div>
          <table className="w-full text-xs">
            <thead>
              <tr className="bg-gray-50 border-y border-gray-100">
                <th className="px-6 py-2 text-left font-semibold text-gray-600">Account</th>
                <th className="px-6 py-2 text-right font-semibold text-gray-600">Debit</th>
                <th className="px-6 py-2 text-right font-semibold text-gray-600">Credit</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {event.lines.map((line) => (
                <tr key={line.id} className={line.postingStatus === 'Voided' ? 'opacity-40 line-through' : ''}>
                  <td className="px-6 py-2">
                    <span className="font-mono text-gray-700">{line.accountCode}</span>
                    <span className="text-gray-500 ml-2">{line.accountName}</span>
                    {line.memo && <p className="text-gray-400 mt-0.5 truncate max-w-[260px]">{line.memo}</p>}
                  </td>
                  <td className="px-6 py-2 text-right font-mono text-gray-700">
                    {line.debit > 0 ? fmt.format(line.debit) : '—'}
                  </td>
                  <td className="px-6 py-2 text-right font-mono text-gray-700">
                    {line.credit > 0 ? fmt.format(line.credit) : '—'}
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="bg-gray-50 border-t border-gray-200 font-semibold">
                <td className="px-6 py-2 text-xs text-gray-500">Totals</td>
                <td className="px-6 py-2 text-right font-mono text-gray-900">{fmt.format(event.totalDebits)}</td>
                <td className="px-6 py-2 text-right font-mono text-gray-900">{fmt.format(event.totalCredits)}</td>
              </tr>
            </tfoot>
          </table>
        </div>

        {/* Actions */}
        <div className="px-6 py-4 border-t border-gray-200 bg-gray-50 flex items-center justify-between">
          {qbDeepLink && (
            <a href={qbDeepLink} target="_blank" rel="noreferrer"
              className="text-xs text-blue-600 hover:underline">
              Open QB Journal Entry ↗
            </a>
          )}
          <div className="flex-1" />
          {event.postingStatus === 'Posted' && (
            event.canVoid ? (
              <button
                onClick={() => onVoidClick(event)}
                className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-red-700 border border-red-300 rounded-md hover:bg-red-50"
              >
                <RotateCcw className="h-3.5 w-3.5" />
                Void
              </button>
            ) : (
              <div className="flex items-center gap-2 text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-md px-3 py-1.5">
                <AlertCircle className="h-3.5 w-3.5 flex-shrink-0" />
                {event.voidBlockReason}
              </div>
            )
          )}
        </div>
      </div>
    </div>
  )
}

// ---------- Main Page ----------

export function ActivityPage() {
  const [filter, setFilter] = useState<ActivityFilter>({})
  const [selectedEvent, setSelectedEvent] = useState<ActivityEvent | null>(null)
  const [voidTarget, setVoidTarget] = useState<ActivityEvent | null>(null)

  const { data: events = [], isLoading } = useQuery({
    queryKey: ['activity', filter],
    queryFn: () => getActivity(filter),
  })

  const updateFilter = (patch: Partial<ActivityFilter>) =>
    setFilter((prev) => ({ ...prev, ...patch }))

  const clearFilter = (key: keyof ActivityFilter) =>
    setFilter((prev) => { const n = { ...prev }; delete n[key]; return n })

  return (
    <div className="p-6">
      <PageHeader
        title="Activity"
        subtitle="Audit trail of all accounting events"
      />

      {/* Filter bar */}
      <div className="flex flex-wrap items-end gap-3 mb-5 p-4 bg-gray-50 border border-gray-200 rounded-lg">
        <div className="flex flex-col gap-1">
          <label className="text-xs font-medium text-gray-600">From date</label>
          <input
            type="date"
            value={filter.fromDate ?? ''}
            onChange={(e) => e.target.value ? updateFilter({ fromDate: e.target.value }) : clearFilter('fromDate')}
            className="border border-gray-300 rounded px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-blue-400"
          />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs font-medium text-gray-600">To date</label>
          <input
            type="date"
            value={filter.toDate ?? ''}
            onChange={(e) => e.target.value ? updateFilter({ toDate: e.target.value }) : clearFilter('toDate')}
            className="border border-gray-300 rounded px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-blue-400"
          />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs font-medium text-gray-600">Type</label>
          <select
            value={filter.sourceType ?? ''}
            onChange={(e) => e.target.value ? updateFilter({ sourceType: e.target.value }) : clearFilter('sourceType')}
            className="border border-gray-300 rounded px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-blue-400"
          >
            <option value="">All types</option>
            {SOURCE_TYPES.map((t) => (
              <option key={t} value={t}>{t === 'CashApplication' ? 'Cash Application' : t}</option>
            ))}
          </select>
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs font-medium text-gray-600">Status</label>
          <select
            value={filter.postingStatus ?? ''}
            onChange={(e) => e.target.value ? updateFilter({ postingStatus: e.target.value }) : clearFilter('postingStatus')}
            className="border border-gray-300 rounded px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-blue-400"
          >
            <option value="">All statuses</option>
            <option value="Posted">Posted</option>
            <option value="Voided">Voided</option>
            <option value="Reversal">Reversal</option>
          </select>
        </div>
        {Object.keys(filter).length > 0 && (
          <button
            onClick={() => setFilter({})}
            className="text-xs text-gray-500 hover:text-gray-800 underline mt-4"
          >
            Clear filters
          </button>
        )}
      </div>

      {/* Results */}
      {isLoading ? (
        <div className="flex justify-center py-16">
          <LoadingSpinner />
        </div>
      ) : events.length === 0 ? (
        <div className="text-center py-16 text-gray-400">
          <Activity className="h-10 w-10 mx-auto mb-3 opacity-30" />
          <p className="text-sm">No activity found for the selected filters</p>
        </div>
      ) : (
        <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-50 border-b border-gray-200">
                <th className="px-4 py-3 text-left font-semibold text-gray-600 text-xs uppercase tracking-wide">Date</th>
                <th className="px-4 py-3 text-left font-semibold text-gray-600 text-xs uppercase tracking-wide">Type</th>
                <th className="px-4 py-3 text-left font-semibold text-gray-600 text-xs uppercase tracking-wide">Reference</th>
                <th className="px-4 py-3 text-left font-semibold text-gray-600 text-xs uppercase tracking-wide">Description</th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600 text-xs uppercase tracking-wide">Amount</th>
                <th className="px-4 py-3 text-center font-semibold text-gray-600 text-xs uppercase tracking-wide">Status</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {events.map((evt) => (
                <tr
                  key={evt.transactionId}
                  onClick={() => setSelectedEvent(evt)}
                  className={`cursor-pointer hover:bg-gray-50 transition-colors ${
                    evt.postingStatus === 'Voided' ? 'opacity-50' :
                    evt.postingStatus === 'Reversal' ? 'bg-purple-50/40' : ''
                  }`}
                >
                  <td className="px-4 py-3 text-gray-500 whitespace-nowrap">{fmtDate(evt.effectiveDate)}</td>
                  <td className="px-4 py-3">
                    <SourceTypeBadge type={evt.sourceType} />
                  </td>
                  <td className="px-4 py-3 font-mono text-xs text-gray-800 whitespace-nowrap">
                    <div className="flex items-center gap-1.5">
                      {evt.postingStatus === 'Voided' && (
                        <span title="Voided"><X className="h-3 w-3 text-red-400" /></span>
                      )}
                      {evt.postingStatus === 'Reversal' && (
                        <span title="Reversal entry"><RotateCcw className="h-3 w-3 text-purple-500" /></span>
                      )}
                      {evt.sourceNumber}
                    </div>
                  </td>
                  <td className="px-4 py-3 text-gray-500 text-xs max-w-[200px] truncate">
                    {evt.sourceDescription ?? '—'}
                  </td>
                  <td className="px-4 py-3 text-right font-mono text-gray-900">
                    {fmt.format(evt.totalDebits)}
                  </td>
                  <td className="px-4 py-3 text-center">
                    <StatusBadge status={evt.postingStatus} />
                  </td>
                  <td className="px-4 py-3 text-gray-300">
                    <ChevronRight className="h-4 w-4" />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="px-4 py-2 border-t border-gray-100 bg-gray-50 text-xs text-gray-400">
            {events.length} event{events.length !== 1 ? 's' : ''}
          </div>
        </div>
      )}

      {selectedEvent && (
        <DetailDrawer
          event={selectedEvent}
          onClose={() => setSelectedEvent(null)}
          onVoidClick={(evt) => {
            setSelectedEvent(null)
            setVoidTarget(evt)
          }}
        />
      )}

      {voidTarget && (
        <VoidModal
          event={voidTarget}
          onClose={() => setVoidTarget(null)}
          onVoided={() => setVoidTarget(null)}
        />
      )}
    </div>
  )
}
