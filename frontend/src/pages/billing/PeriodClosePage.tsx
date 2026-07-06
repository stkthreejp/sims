import { useState, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Lock, LockOpen, RefreshCw, CheckCircle2, XCircle, AlertTriangle, ChevronDown, ChevronRight,
} from 'lucide-react'
import { toast } from 'sonner'
import {
  getPeriods, getOrCreatePeriod, evaluateChecklist, closePeriod, reopenPeriod,
} from '@/api/periodClose.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ErrorState } from '@/components/common/ErrorState'
import { getApiErrorMessage } from '@/lib/apiError'
import type { AccountingPeriod, ChecklistItem } from '@/types/periodClose.types'
import { usePermissions } from '@/hooks/usePermissions'

const MONTH_NAMES = [
  '', 'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
]

function periodLabel(p: AccountingPeriod) {
  return `${MONTH_NAMES[p.periodMonth]} ${p.periodYear}`
}

function StatusBadge({ status }: { status: AccountingPeriod['status'] }) {
  const styles: Record<string, React.CSSProperties> = {
    Open:     { background: 'var(--good-bg)', color: 'var(--good-fg)' },
    Closing:  { background: 'var(--warn-bg)', color: 'var(--warn-fg)' },
    Closed:   { background: 'var(--surface-2)', color: 'var(--ink-3)' },
    Reopened: { background: 'var(--surface-2)', color: 'var(--accent-ink)' },
  }
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium" style={styles[status] ?? { background: 'var(--surface-2)', color: 'var(--ink-3)' }}>
      {status}
    </span>
  )
}

function ChecklistRow({ item }: { item: ChecklistItem }) {
  const icon = item.passed
    ? <CheckCircle2 className="h-4 w-4 flex-shrink-0" style={{ color: 'var(--good-fg)' }} />
    : item.isBlocking
      ? <XCircle className="h-4 w-4 flex-shrink-0" style={{ color: 'var(--bad-fg)' }} />
      : <AlertTriangle className="h-4 w-4 flex-shrink-0" style={{ color: 'var(--warn-fg)' }} />

  return (
    <div
      className="flex items-center justify-between px-4 py-3 rounded-lg border"
      style={item.passed
        ? { borderColor: 'var(--line-2)', background: 'var(--good-bg)' }
        : item.isBlocking
          ? { borderColor: 'var(--bad-fg)', background: 'var(--bad-bg)' }
          : { borderColor: 'var(--warn-fg)', background: 'var(--warn-bg)' }
      }
    >
      <div className="flex items-center gap-3">
        {icon}
        <div>
          <p className="text-sm font-medium" style={{ color: 'var(--ink)' }}>{item.label}</p>
          {!item.passed && (
            <p className="text-xs mt-0.5" style={{ color: item.isBlocking ? 'var(--bad-fg)' : 'var(--warn-fg)' }}>
              {item.issueCount} item{item.issueCount !== 1 ? 's' : ''} outstanding
              {item.isBlocking ? ' — blocks close' : ' — warning only'}
            </p>
          )}
        </div>
      </div>
      {item.passed ? (
        <span className="text-xs font-medium" style={{ color: 'var(--good-fg)' }}>Clear</span>
      ) : (
        <span className="text-xs font-mono font-semibold" style={{ color: item.isBlocking ? 'var(--bad-fg)' : 'var(--warn-fg)' }}>
          {item.issueCount}
        </span>
      )}
    </div>
  )
}

// ---------- Period Detail Panel ----------

interface PeriodPanelProps {
  period: AccountingPeriod
  isAdmin: boolean
}

function PeriodPanel({ period, isAdmin }: PeriodPanelProps) {
  const [closeNotes, setCloseNotes] = useState('')
  const [reopenReason, setReopenReason] = useState('')
  const [showCloseForm, setShowCloseForm] = useState(false)
  const [showReopenForm, setShowReopenForm] = useState(false)
  const qc = useQueryClient()

  const isClosed = period.status === 'Closed'
  const reopenReasonValid = reopenReason.trim().length > 0

  // Auto-evaluate close readiness when a period is opened (PeriodPanel is keyed per
  // period, so this remounts on select) instead of gating Close on a manually-
  // refreshed, possibly-stale checklist (audit B17). POST-backed, so we don't refetch
  // on window focus — only on select (mount) and the explicit Refresh button.
  const {
    data: liveChecklist,
    isFetching: evaluating,
    refetch: refetchChecklist,
  } = useQuery({
    queryKey: ['period-checklist', period.id],
    queryFn: () => evaluateChecklist(period.id).then((p) => p.checklist),
    enabled: !isClosed,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  })

  // Closed periods keep the snapshot persisted at close time.
  const checklist = isClosed ? period.checklist : (liveChecklist ?? [])
  const checklistEvaluated = checklist.length > 0
  const hasBlockers = checklist.some(c => c.isBlocking && !c.passed)
  const hasWarnings = checklist.some(c => !c.isBlocking && !c.passed)

  const closeMutation = useMutation({
    mutationFn: () => closePeriod(period.id, closeNotes || undefined),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['periods'] })
      qc.invalidateQueries({ queryKey: ['period-checklist', period.id] })
      toast.success(`${periodLabel(period)} closed`)
      setShowCloseForm(false)
    },
    onError: (e) => toast.error(getApiErrorMessage(e)),
  })

  const reopenMutation = useMutation({
    mutationFn: () => reopenPeriod(period.id, reopenReason.trim()),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['periods'] })
      qc.invalidateQueries({ queryKey: ['period-checklist', period.id] })
      toast.success(`${periodLabel(period)} reopened`)
      setShowReopenForm(false)
    },
    onError: (e) => toast.error(getApiErrorMessage(e)),
  })

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <h2 className="text-base font-semibold" style={{ color: 'var(--ink)' }}>{periodLabel(period)}</h2>
          <StatusBadge status={period.status} />
        </div>
        {!isClosed && (
          <button
            onClick={() => refetchChecklist()}
            disabled={evaluating}
            className="flex items-center gap-1.5 text-xs border rounded px-2.5 py-1.5 disabled:opacity-50"
            style={{ color: 'var(--ink-3)', borderColor: 'var(--line)' }}
          >
            <RefreshCw className={`h-3.5 w-3.5 ${evaluating ? 'animate-spin' : ''}`} />
            Refresh checklist
          </button>
        )}
      </div>

      {/* Checklist */}
      {evaluating && !checklistEvaluated ? (
        <p className="text-sm italic px-1" style={{ color: 'var(--ink-4)' }}>
          Evaluating close readiness…
        </p>
      ) : !checklistEvaluated ? (
        <p className="text-sm italic px-1" style={{ color: 'var(--ink-4)' }}>
          No checklist available for this period.
        </p>
      ) : (
        <div className="space-y-2">
          {checklist.map(item => (
            <ChecklistRow key={item.checkKey} item={item} />
          ))}
        </div>
      )}

      {/* Notes on closed/reopened */}
      {period.notes && (
        <div className="border rounded-lg px-4 py-3 text-xs whitespace-pre-line" style={{ background: 'var(--surface-2)', borderColor: 'var(--line)', color: 'var(--ink-3)' }}>
          {period.notes}
        </div>
      )}

      {/* Actions */}
      {period.status !== 'Closed' && isAdmin && checklistEvaluated && (
        <div className="pt-2">
          {!showCloseForm ? (
            <button
              onClick={() => setShowCloseForm(true)}
              disabled={hasBlockers}
              title={hasBlockers ? 'Resolve all blocking checklist items first' : undefined}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-md disabled:opacity-40 disabled:cursor-not-allowed"
              style={{ color: 'white', background: 'var(--ink)' }}
            >
              <Lock className="h-4 w-4" />
              Close {periodLabel(period)}
              {hasWarnings && !hasBlockers && (
                <span className="ml-1 text-xs" style={{ color: 'var(--warn-fg)' }}>(warnings)</span>
              )}
            </button>
          ) : (
            <div className="border rounded-lg p-4 space-y-3" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
              <p className="text-sm font-medium" style={{ color: 'var(--ink-2)' }}>
                Confirm close — {periodLabel(period)}
              </p>
              {hasWarnings && (
                <div className="flex items-start gap-2 text-xs border rounded p-2" style={{ color: 'var(--warn-fg)', background: 'var(--warn-bg)', borderColor: 'var(--warn-fg)' }}>
                  <AlertTriangle className="h-3.5 w-3.5 flex-shrink-0 mt-0.5" />
                  There are unresolved warnings. You can proceed, but review them first.
                </div>
              )}
              <textarea
                rows={2}
                value={closeNotes}
                onChange={(e) => setCloseNotes(e.target.value)}
                placeholder="Close notes (optional)…"
                className="sd-input w-full px-3 py-2 text-sm"
              />
              <div className="flex gap-2">
                <button
                  onClick={() => closeMutation.mutate()}
                  disabled={closeMutation.isPending}
                  className="px-4 py-2 text-sm font-medium rounded-md disabled:opacity-50"
                  style={{ color: 'white', background: 'var(--ink)' }}
                >
                  {closeMutation.isPending ? 'Closing…' : 'Confirm Close'}
                </button>
                <button
                  onClick={() => setShowCloseForm(false)}
                  className="sd-btn outline px-4 py-2 text-sm"
                >
                  Cancel
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {period.status === 'Closed' && isAdmin && (
        <div className="pt-2">
          {!showReopenForm ? (
            <button
              onClick={() => setShowReopenForm(true)}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium border rounded-md"
              style={{ color: 'var(--accent-ink)', borderColor: 'var(--line)' }}
            >
              <LockOpen className="h-4 w-4" />
              Reopen Period
            </button>
          ) : (
            <div className="border rounded-lg p-4 space-y-3" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
              <p className="text-sm font-medium" style={{ color: 'var(--accent-ink)' }}>
                Reopen {periodLabel(period)}? This allows new postings.
              </p>
              <textarea
                rows={2}
                value={reopenReason}
                onChange={(e) => setReopenReason(e.target.value)}
                placeholder="Reason for reopening…"
                className="sd-input w-full px-3 py-2 text-sm"
              />
              <div className="flex gap-2">
                <button
                  onClick={() => reopenMutation.mutate()}
                  disabled={reopenMutation.isPending || !reopenReasonValid}
                  className="sd-btn primary px-4 py-2 text-sm font-medium disabled:opacity-50"
                >
                  {reopenMutation.isPending ? 'Reopening…' : 'Confirm Reopen'}
                </button>
                <button
                  onClick={() => setShowReopenForm(false)}
                  className="sd-btn outline px-4 py-2 text-sm"
                >
                  Cancel
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

// ---------- Main Page ----------

export function PeriodClosePage() {
  const { canAdminAccounting: isAdmin } = usePermissions()
  const qc = useQueryClient()
  const today = new Date()
  const [selectedId, setSelectedId] = useState<number | null>(null)

  const { isLoading, isError, error, refetch, data: periodsData } = useQuery({
    queryKey: ['periods'],
    queryFn: getPeriods,
  })
  // Query cache is the single source of truth — no local copy to fork or get
  // clobbered by a background refetch / another tab (audit B17).
  const periods = periodsData ?? []

  useEffect(() => {
    if (selectedId === null && periods.length > 0) setSelectedId(periods[0].id)
  }, [periods, selectedId])

  const openCurrentMutation = useMutation({
    mutationFn: () => getOrCreatePeriod(today.getFullYear(), today.getMonth() + 1),
    onSuccess: (p) => {
      setSelectedId(p.id)
      qc.invalidateQueries({ queryKey: ['periods'] })
    },
    onError: (e) => toast.error(getApiErrorMessage(e)),
  })

  const selectedPeriod = periods.find(p => p.id === selectedId) ?? null

  const currentPeriodExists = periods.some(
    p => p.periodYear === today.getFullYear() && p.periodMonth === today.getMonth() + 1
  )

  return (
    <div className="p-6">
      <PageHeader
        title="Period Close"
        subtitle="Manage monthly accounting period close and reopen workflow"
        action={
          !currentPeriodExists && isAdmin ? (
            <button
              onClick={() => openCurrentMutation.mutate()}
              disabled={openCurrentMutation.isPending}
              className="sd-btn primary flex items-center gap-2 px-3 py-2 text-sm font-medium disabled:opacity-50"
            >
              Open {MONTH_NAMES[today.getMonth() + 1]} {today.getFullYear()}
            </button>
          ) : undefined
        }
      />

      {isError ? (
        <ErrorState error={error} onRetry={refetch} />
      ) : isLoading ? (
        <div className="flex justify-center py-16"><LoadingSpinner /></div>
      ) : (
        <div className="flex gap-6">
          {/* Period list sidebar */}
          <div className="w-56 flex-shrink-0 space-y-1">
            {periods.length === 0 ? (
              <p className="text-sm italic px-2" style={{ color: 'var(--ink-4)' }}>No periods yet</p>
            ) : (
              periods.map(p => (
                <button
                  key={p.id}
                  onClick={() => setSelectedId(p.id)}
                  className="w-full flex items-center justify-between px-3 py-2.5 rounded-lg text-sm transition-colors border"
                  style={selectedId === p.id
                    ? { background: 'var(--surface-2)', borderColor: 'var(--line)', color: 'var(--accent-ink)' }
                    : { borderColor: 'transparent', color: 'var(--ink-2)' }
                  }
                >
                  <span className="font-medium">{periodLabel(p)}</span>
                  <StatusBadge status={p.status} />
                </button>
              ))
            )}
          </div>

          {/* Detail panel */}
          <div className="flex-1 border rounded-lg p-6" style={{ background: 'var(--surface)', borderColor: 'var(--line)' }}>
            {selectedPeriod ? (
              <PeriodPanel
                key={selectedPeriod.id}
                period={selectedPeriod}
                isAdmin={isAdmin}
              />
            ) : (
              <p className="text-sm italic" style={{ color: 'var(--ink-4)' }}>Select a period or open the current month.</p>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
