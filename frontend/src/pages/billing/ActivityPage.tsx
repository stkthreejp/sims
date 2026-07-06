import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Activity, X, ChevronRight, AlertCircle, RotateCcw, CheckCircle2,
} from 'lucide-react'
import { toast } from 'sonner'
import {
  getActivity, voidReceipt, voidCashApplication, voidInvoice, voidDisbursement,
} from '@/api/activity.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { StatusBadge } from '@/components/common/StatusBadge'
import { EmptyState } from '@/components/common/EmptyState'
import { ErrorState } from '@/components/common/ErrorState'
import { getApiErrorMessage } from '@/lib/apiError'
import type { ActivityEvent, ActivityFilter } from '@/types/activity.types'
import { useAuthStore } from '@/store/authStore'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
const fmtDateTime = (s: string) =>
  new Date(s).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' })
const fmtDate = (s: string) =>
  new Date(s + 'T00:00:00').toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })

const SOURCE_TYPES = ['Invoice', 'Receipt', 'CashApplication', 'Disbursement', 'Distribution']

const POSTING_PILL: Record<string, string> = {
  Posted: 'bound',
  Voided: 'voided',
  Reversal: 'withdrawn',
}

const SOURCE_PILL: Record<string, string> = {
  Invoice: 'quoted',
  Receipt: 'submitted',
  CashApplication: 'inprogress',
  Disbursement: 'nonrenewed',
  Distribution: 'draft',
}
const SOURCE_LABEL: Record<string, string> = { CashApplication: 'Cash App' }

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
    onError: (e) => toast.error(getApiErrorMessage(e)),
  })

  return (
    <div className="sims-modal-backdrop" style={{ zIndex: 60 }}>
      <div className="sims-modal">
        <div className="sims-modal-head">
          <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <RotateCcw style={{ width: 14, height: 14, color: 'var(--bad-fg)' }} />
            Void {event.sourceNumber}
          </span>
          <button onClick={onClose} className="sims-modal-close"><X style={{ width: 16, height: 16 }} /></button>
        </div>
        <div className="sims-modal-body">
          <p style={{ fontSize: 13, color: 'var(--ink-2)', marginBottom: 14 }}>
            This will create reversing GL entries and mark{' '}
            <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 600 }}>{event.sourceNumber}</span> as voided.
            This action cannot be undone.
          </p>
          <label className="sims-field">
            <span className="sims-field-label">Reason (optional)</span>
            <textarea
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Enter void reason..."
              className="sims-input"
              style={{ resize: 'vertical', minHeight: 72 }}
            />
          </label>
        </div>
        <div className="sims-modal-foot">
          <button className="sd-btn outline" onClick={onClose}>Cancel</button>
          <button className="sd-btn danger" onClick={() => mutation.mutate()} disabled={mutation.isPending}>
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


  return (
    <div className="fixed inset-0 z-50 flex justify-end">
      <div className="absolute inset-0" style={{ background: 'rgba(0,0,0,.28)' }} onClick={onClose} />
      <div className="relative flex flex-col overflow-hidden" style={{ width: '100%', maxWidth: 500, background: 'var(--surface)', boxShadow: 'var(--shadow-xl)' }}>
        {/* Header */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '14px 20px', borderBottom: '1px solid var(--line)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <StatusBadge status={SOURCE_PILL[event.sourceType] ?? 'draft'} label={SOURCE_LABEL[event.sourceType] ?? event.sourceType} />
            <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--ink)', fontSize: 13.5 }}>{event.sourceNumber}</span>
            <StatusBadge status={POSTING_PILL[event.postingStatus] ?? 'draft'} label={event.postingStatus} />
          </div>
          <button onClick={onClose} style={{ color: 'var(--ink-3)', background: 'none', border: 0, cursor: 'pointer', display: 'grid', placeItems: 'center' }}>
            <X style={{ width: 16, height: 16 }} />
          </button>
        </div>

        {/* Meta */}
        <div style={{ padding: '14px 20px', borderBottom: '1px solid var(--line-2)', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          <div>
            <p style={{ fontSize: 11, color: 'var(--ink-4)', marginBottom: 2 }}>Effective date</p>
            <p style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)' }}>{fmtDate(event.effectiveDate)}</p>
          </div>
          <div>
            <p style={{ fontSize: 11, color: 'var(--ink-4)', marginBottom: 2 }}>Posted at</p>
            <p style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)' }}>{fmtDateTime(event.postedAt)}</p>
          </div>
          {event.sourceDescription && (
            <div style={{ gridColumn: '1 / -1' }}>
              <p style={{ fontSize: 11, color: 'var(--ink-4)', marginBottom: 2 }}>Description</p>
              <p style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)' }}>{event.sourceDescription}</p>
            </div>
          )}
          {event.sourcePolicyTransactionNumber && (
            <div>
              <p style={{ fontSize: 11, color: 'var(--ink-4)', marginBottom: 2 }}>Policy transaction</p>
              <p style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, fontWeight: 600, color: 'var(--ink)' }}>{event.sourcePolicyTransactionNumber}</p>
              {event.sourcePolicyTransactionType && <p style={{ fontSize: 11, color: 'var(--ink-3)' }}>{event.sourcePolicyTransactionType}</p>}
            </div>
          )}
          {event.sourcePolicyVersionNumber != null && (
            <div>
              <p style={{ fontSize: 11, color: 'var(--ink-4)', marginBottom: 2 }}>Policy version</p>
              <p style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)' }}>v{event.sourcePolicyVersionNumber}</p>
            </div>
          )}
          {event.postingStatus === 'Voided' && event.voidReason && (
            <div style={{ gridColumn: '1 / -1' }}>
              <p style={{ fontSize: 11, color: 'var(--ink-4)', marginBottom: 2 }}>Void reason</p>
              <p style={{ fontSize: 13, fontWeight: 600, color: 'var(--bad-fg)' }}>{event.voidReason}</p>
            </div>
          )}
          {event.postingStatus === 'Voided' && event.voidedByTransactionId && (
            <div style={{ gridColumn: '1 / -1' }}>
              <p style={{ fontSize: 11, color: 'var(--ink-4)', marginBottom: 2 }}>Voided by transaction</p>
              <p style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--ink-3)' }}>{event.voidedByTransactionId}</p>
            </div>
          )}
          {event.postingStatus === 'Reversal' && event.reversesTransactionId && (
            <div style={{ gridColumn: '1 / -1' }}>
              <p style={{ fontSize: 11, color: 'var(--ink-4)', marginBottom: 2 }}>Reverses transaction</p>
              <p style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--ink-3)' }}>{event.reversesTransactionId}</p>
            </div>
          )}
        </div>

        {/* Ledger lines */}
        <div style={{ flex: 1, overflowY: 'auto' }}>
          <div style={{ padding: '14px 20px 8px' }}>
            <p style={{ fontSize: 11, fontWeight: 700, color: 'var(--ink-4)', textTransform: 'uppercase', letterSpacing: '.05em' }}>Ledger Postings</p>
          </div>
          <table style={{ width: '100%', fontSize: 12, borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ background: 'var(--surface-2)', borderTop: '1px solid var(--line-2)', borderBottom: '1px solid var(--line-2)' }}>
                <th style={{ padding: '8px 20px', textAlign: 'left', fontWeight: 600, color: 'var(--ink-3)' }}>Account</th>
                <th style={{ padding: '8px 20px', textAlign: 'right', fontWeight: 600, color: 'var(--ink-3)' }}>Debit</th>
                <th style={{ padding: '8px 20px', textAlign: 'right', fontWeight: 600, color: 'var(--ink-3)' }}>Credit</th>
              </tr>
            </thead>
            <tbody>
              {event.lines.map((line) => (
                <tr key={line.id} style={{
                  borderBottom: '1px solid var(--line-2)',
                  opacity: line.postingStatus === 'Voided' ? 0.4 : 1,
                  textDecoration: line.postingStatus === 'Voided' ? 'line-through' : undefined,
                }}>
                  <td style={{ padding: '8px 20px' }}>
                    <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--ink-2)' }}>{line.accountCode}</span>
                    <span style={{ color: 'var(--ink-3)', marginLeft: 8 }}>{line.accountName}</span>
                    {line.memo && <p style={{ color: 'var(--ink-4)', marginTop: 2, fontSize: 11, maxWidth: 260, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{line.memo}</p>}
                  </td>
                  <td style={{ padding: '8px 20px', textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--ink-2)' }}>
                    {line.debit > 0 ? fmt.format(line.debit) : '—'}
                  </td>
                  <td style={{ padding: '8px 20px', textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--ink-2)' }}>
                    {line.credit > 0 ? fmt.format(line.credit) : '—'}
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr style={{ background: 'var(--surface-2)', borderTop: '1px solid var(--line)', fontWeight: 700 }}>
                <td style={{ padding: '8px 20px', fontSize: 12, color: 'var(--ink-3)' }}>Totals</td>
                <td style={{ padding: '8px 20px', textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--ink)' }}>{fmt.format(event.totalDebits)}</td>
                <td style={{ padding: '8px 20px', textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--ink)' }}>{fmt.format(event.totalCredits)}</td>
              </tr>
            </tfoot>
          </table>
        </div>

        {/* Actions */}
        <div style={{ padding: '12px 20px', borderTop: '1px solid var(--line)', background: 'var(--surface-2)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div />
          {event.postingStatus === 'Posted' && (
            event.canVoid ? (
              <button
                className="sd-btn danger"
                onClick={() => onVoidClick(event)}
              >
                <RotateCcw style={{ width: 12, height: 12 }} />
                Void
              </button>
            ) : (
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, color: 'var(--warn-fg)', background: 'var(--warn-bg)', border: '1px solid var(--warn-bg)', borderRadius: 'var(--r)', padding: '5px 10px' }}>
                <AlertCircle style={{ width: 13, height: 13, flexShrink: 0 }} />
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

  const { data: events = [], isLoading, isError, error, refetch } = useQuery({
    queryKey: ['activity', filter],
    queryFn: () => getActivity(filter),
  })

  const updateFilter = (patch: Partial<ActivityFilter>) =>
    setFilter((prev) => ({ ...prev, ...patch }))

  const clearFilter = (key: keyof ActivityFilter) =>
    setFilter((prev) => { const n = { ...prev }; delete n[key]; return n })

  return (
    <div className="subs-wrap">
      <div className="subs-page-head" style={{ marginBottom: 16 }}>
        <PageHeader title="Activity" subtitle="Audit trail of all accounting events" />
      </div>

      {/* Filter bar */}
      <div className="sd-card" style={{ marginBottom: 16 }}>
        <div className="sd-card-body" style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'flex-end', gap: 12 }}>
          {[
            { label: 'From date', type: 'date', key: 'fromDate' as keyof ActivityFilter, value: filter.fromDate ?? '' },
            { label: 'To date', type: 'date', key: 'toDate' as keyof ActivityFilter, value: filter.toDate ?? '' },
          ].map(({ label, type, key, value }) => (
            <label key={key} className="sims-field" style={{ margin: 0 }}>
              <span className="sims-field-label">{label}</span>
              <input
                type={type}
                value={value}
                onChange={(e) => e.target.value ? updateFilter({ [key]: e.target.value }) : clearFilter(key)}
                className="sims-input"
                style={{ width: 150 }}
              />
            </label>
          ))}

          <label className="sims-field" style={{ margin: 0 }}>
            <span className="sims-field-label">Type</span>
            <select
              value={filter.sourceType ?? ''}
              onChange={(e) => e.target.value ? updateFilter({ sourceType: e.target.value }) : clearFilter('sourceType')}
              className="sims-select"
              style={{ width: 160 }}
            >
              <option value="">All types</option>
              {SOURCE_TYPES.map((t) => (
                <option key={t} value={t}>{t === 'CashApplication' ? 'Cash Application' : t}</option>
              ))}
            </select>
          </label>

          <label className="sims-field" style={{ margin: 0 }}>
            <span className="sims-field-label">Status</span>
            <select
              value={filter.postingStatus ?? ''}
              onChange={(e) => e.target.value ? updateFilter({ postingStatus: e.target.value }) : clearFilter('postingStatus')}
              className="sims-select"
              style={{ width: 150 }}
            >
              <option value="">All statuses</option>
              <option value="Posted">Posted</option>
              <option value="Voided">Voided</option>
              <option value="Reversal">Reversal</option>
            </select>
          </label>

          {Object.keys(filter).length > 0 && (
            <button
              onClick={() => setFilter({})}
              style={{ fontSize: 12, color: 'var(--ink-3)', background: 'none', border: 0, cursor: 'pointer', textDecoration: 'underline', marginTop: 16 }}
            >
              Clear filters
            </button>
          )}
        </div>
      </div>

      {/* Results */}
      {isLoading ? (
        <LoadingSpinner />
      ) : isError ? (
        <ErrorState error={error} onRetry={refetch} />
      ) : events.length === 0 ? (
        <EmptyState icon={Activity} title="No activity found" description="Try adjusting the date range or filters." />
      ) : (
        <div className="subs-table-card">
          <table className="subs-table">
            <thead>
              <tr>
                <th className="subs-th">Date</th>
                <th className="subs-th">Type</th>
                <th className="subs-th">Reference</th>
                <th className="subs-th">Description</th>
                <th className="subs-th num">Amount</th>
                <th className="subs-th">Status</th>
                <th className="subs-th" style={{ width: 24 }} />
              </tr>
            </thead>
            <tbody>
              {events.map((evt) => (
                <tr
                  key={evt.transactionId}
                  className="subs-row"
                  style={{
                    opacity: evt.postingStatus === 'Voided' ? 0.5 : 1,
                    background: evt.postingStatus === 'Reversal' ? 'rgba(168,130,208,.06)' : undefined,
                  }}
                  onClick={() => setSelectedEvent(evt)}
                >
                  <td style={{ color: 'var(--ink-3)', whiteSpace: 'nowrap' }}>{fmtDate(evt.effectiveDate)}</td>
                  <td><StatusBadge status={SOURCE_PILL[evt.sourceType] ?? 'draft'} label={SOURCE_LABEL[evt.sourceType] ?? evt.sourceType} /></td>
                  <td>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--ink)', whiteSpace: 'nowrap' }}>
                      {evt.postingStatus === 'Voided' && <X style={{ width: 11, height: 11, color: 'var(--bad-fg)' }} />}
                      {evt.postingStatus === 'Reversal' && <RotateCcw style={{ width: 11, height: 11, color: 'var(--ink-3)' }} />}
                      {evt.sourceNumber}
                    </div>
                  </td>
                  <td style={{ color: 'var(--ink-3)', fontSize: 12, maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {evt.sourcePolicyTransactionNumber ?? evt.sourceDescription ?? '—'}
                  </td>
                  <td style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--ink)' }}>
                    {fmt.format(evt.totalDebits)}
                  </td>
                  <td><StatusBadge status={POSTING_PILL[evt.postingStatus] ?? 'draft'} label={evt.postingStatus} /></td>
                  <td style={{ color: 'var(--ink-4)' }}><ChevronRight style={{ width: 14, height: 14 }} /></td>
                </tr>
              ))}
            </tbody>
          </table>
          <div style={{ padding: '6px 14px', borderTop: '1px solid var(--line-2)', background: 'var(--surface-2)', fontSize: 12, color: 'var(--ink-4)' }}>
            {events.length} event{events.length !== 1 ? 's' : ''}
          </div>
        </div>
      )}

      {selectedEvent && (
        <DetailDrawer
          event={selectedEvent}
          onClose={() => setSelectedEvent(null)}
          onVoidClick={(evt) => { setSelectedEvent(null); setVoidTarget(evt) }}
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
