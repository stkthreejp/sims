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
import type { AccountingPeriod, ChecklistItem } from '@/types/periodClose.types'
import { useAuthStore } from '@/store/authStore'

const MONTH_NAMES = [
  '', 'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
]

function periodLabel(p: AccountingPeriod) {
  return `${MONTH_NAMES[p.periodMonth]} ${p.periodYear}`
}

function StatusBadge({ status }: { status: AccountingPeriod['status'] }) {
  const styles: Record<string, string> = {
    Open: 'bg-green-100 text-green-800',
    Closing: 'bg-yellow-100 text-yellow-700',
    Closed: 'bg-gray-200 text-gray-600',
    Reopened: 'bg-blue-100 text-blue-700',
  }
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${styles[status] ?? 'bg-gray-100 text-gray-600'}`}>
      {status}
    </span>
  )
}

function ChecklistRow({ item }: { item: ChecklistItem }) {
  const icon = item.passed
    ? <CheckCircle2 className="h-4 w-4 text-green-500 flex-shrink-0" />
    : item.isBlocking
      ? <XCircle className="h-4 w-4 text-red-500 flex-shrink-0" />
      : <AlertTriangle className="h-4 w-4 text-amber-500 flex-shrink-0" />

  return (
    <div className={`flex items-center justify-between px-4 py-3 rounded-lg border ${
      item.passed
        ? 'border-green-100 bg-green-50/50'
        : item.isBlocking
          ? 'border-red-100 bg-red-50/50'
          : 'border-amber-100 bg-amber-50/50'
    }`}>
      <div className="flex items-center gap-3">
        {icon}
        <div>
          <p className="text-sm font-medium text-gray-900">{item.label}</p>
          {!item.passed && (
            <p className={`text-xs mt-0.5 ${item.isBlocking ? 'text-red-600' : 'text-amber-600'}`}>
              {item.issueCount} item{item.issueCount !== 1 ? 's' : ''} outstanding
              {item.isBlocking ? ' — blocks close' : ' — warning only'}
            </p>
          )}
        </div>
      </div>
      {item.passed ? (
        <span className="text-xs text-green-600 font-medium">Clear</span>
      ) : (
        <span className={`text-xs font-mono font-semibold ${item.isBlocking ? 'text-red-600' : 'text-amber-600'}`}>
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
  onUpdated: (p: AccountingPeriod) => void
}

function PeriodPanel({ period, isAdmin, onUpdated }: PeriodPanelProps) {
  const [closeNotes, setCloseNotes] = useState('')
  const [reopenReason, setReopenReason] = useState('')
  const [showCloseForm, setShowCloseForm] = useState(false)
  const [showReopenForm, setShowReopenForm] = useState(false)
  const qc = useQueryClient()

  const hasBlockers = period.checklist.some(c => c.isBlocking && !c.passed)
  const hasWarnings = period.checklist.some(c => !c.isBlocking && !c.passed)
  const checklistEvaluated = period.checklist.length > 0

  const evaluateMutation = useMutation({
    mutationFn: () => evaluateChecklist(period.id),
    onSuccess: (p) => { onUpdated(p); toast.success('Checklist refreshed') },
    onError: (e: Error) => toast.error(e.message),
  })

  const closeMutation = useMutation({
    mutationFn: () => closePeriod(period.id, closeNotes || undefined),
    onSuccess: (p) => {
      onUpdated(p)
      qc.invalidateQueries({ queryKey: ['periods'] })
      toast.success(`${periodLabel(period)} closed`)
      setShowCloseForm(false)
    },
    onError: (e: Error) => toast.error(e.message),
  })

  const reopenMutation = useMutation({
    mutationFn: () => reopenPeriod(period.id, reopenReason || undefined),
    onSuccess: (p) => {
      onUpdated(p)
      qc.invalidateQueries({ queryKey: ['periods'] })
      toast.success(`${periodLabel(period)} reopened`)
      setShowReopenForm(false)
    },
    onError: (e: Error) => toast.error(e.message),
  })

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <h2 className="text-base font-semibold text-gray-900">{periodLabel(period)}</h2>
          <StatusBadge status={period.status} />
        </div>
        <button
          onClick={() => evaluateMutation.mutate()}
          disabled={evaluateMutation.isPending}
          className="flex items-center gap-1.5 text-xs text-gray-500 hover:text-gray-800 border border-gray-200 rounded px-2.5 py-1.5 hover:bg-gray-50 disabled:opacity-50"
        >
          <RefreshCw className={`h-3.5 w-3.5 ${evaluateMutation.isPending ? 'animate-spin' : ''}`} />
          Refresh checklist
        </button>
      </div>

      {/* Checklist */}
      {!checklistEvaluated ? (
        <p className="text-sm text-gray-400 italic px-1">
          Click "Refresh checklist" to evaluate close readiness.
        </p>
      ) : (
        <div className="space-y-2">
          {period.checklist.map(item => (
            <ChecklistRow key={item.checkKey} item={item} />
          ))}
        </div>
      )}

      {/* Notes on closed/reopened */}
      {period.notes && (
        <div className="bg-gray-50 border border-gray-200 rounded-lg px-4 py-3 text-xs text-gray-600 whitespace-pre-line">
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
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-gray-800 rounded-md hover:bg-gray-900 disabled:opacity-40 disabled:cursor-not-allowed"
            >
              <Lock className="h-4 w-4" />
              Close {periodLabel(period)}
              {hasWarnings && !hasBlockers && (
                <span className="ml-1 text-amber-300 text-xs">(warnings)</span>
              )}
            </button>
          ) : (
            <div className="border border-gray-200 rounded-lg p-4 space-y-3 bg-gray-50">
              <p className="text-sm font-medium text-gray-800">
                Confirm close — {periodLabel(period)}
              </p>
              {hasWarnings && (
                <div className="flex items-start gap-2 text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded p-2">
                  <AlertTriangle className="h-3.5 w-3.5 flex-shrink-0 mt-0.5" />
                  There are unresolved warnings. You can proceed, but review them first.
                </div>
              )}
              <textarea
                rows={2}
                value={closeNotes}
                onChange={(e) => setCloseNotes(e.target.value)}
                placeholder="Close notes (optional)…"
                className="w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-blue-400"
              />
              <div className="flex gap-2">
                <button
                  onClick={() => closeMutation.mutate()}
                  disabled={closeMutation.isPending}
                  className="px-4 py-2 text-sm font-medium text-white bg-gray-800 rounded-md hover:bg-gray-900 disabled:opacity-50"
                >
                  {closeMutation.isPending ? 'Closing…' : 'Confirm Close'}
                </button>
                <button
                  onClick={() => setShowCloseForm(false)}
                  className="px-4 py-2 text-sm text-gray-600 border border-gray-300 rounded-md hover:bg-gray-100"
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
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-blue-700 border border-blue-300 rounded-md hover:bg-blue-50"
            >
              <LockOpen className="h-4 w-4" />
              Reopen Period
            </button>
          ) : (
            <div className="border border-blue-200 rounded-lg p-4 space-y-3 bg-blue-50">
              <p className="text-sm font-medium text-blue-900">
                Reopen {periodLabel(period)}? This allows new postings.
              </p>
              <textarea
                rows={2}
                value={reopenReason}
                onChange={(e) => setReopenReason(e.target.value)}
                placeholder="Reason for reopening (optional)…"
                className="w-full border border-blue-200 rounded px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-blue-400"
              />
              <div className="flex gap-2">
                <button
                  onClick={() => reopenMutation.mutate()}
                  disabled={reopenMutation.isPending}
                  className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-md hover:bg-blue-700 disabled:opacity-50"
                >
                  {reopenMutation.isPending ? 'Reopening…' : 'Confirm Reopen'}
                </button>
                <button
                  onClick={() => setShowReopenForm(false)}
                  className="px-4 py-2 text-sm text-gray-600 border border-gray-300 rounded-md hover:bg-gray-100"
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
  const isAdmin = useAuthStore((s) => s.user?.roles?.includes('Admin') ?? false)
  const qc = useQueryClient()
  const today = new Date()
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [periods, setPeriods] = useState<AccountingPeriod[]>([])

  const { isLoading, data: periodsData } = useQuery({
    queryKey: ['periods'],
    queryFn: getPeriods,
  })

  useEffect(() => {
    if (periodsData) {
      setPeriods(periodsData)
      if (periodsData.length > 0 && selectedId === null) setSelectedId(periodsData[0].id)
    }
  }, [periodsData])

  const openCurrentMutation = useMutation({
    mutationFn: () => getOrCreatePeriod(today.getFullYear(), today.getMonth() + 1),
    onSuccess: (p) => {
      setPeriods((prev) => {
        const exists = prev.find(x => x.id === p.id)
        return exists ? prev.map(x => x.id === p.id ? p : x) : [p, ...prev]
      })
      setSelectedId(p.id)
      qc.invalidateQueries({ queryKey: ['periods'] })
    },
    onError: (e: Error) => toast.error(e.message),
  })

  const updatePeriod = (p: AccountingPeriod) => {
    setPeriods((prev) => prev.map(x => x.id === p.id ? p : x))
  }

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
              className="flex items-center gap-2 px-3 py-2 text-sm font-medium text-white bg-blue-600 rounded-md hover:bg-blue-700 disabled:opacity-50"
            >
              Open {MONTH_NAMES[today.getMonth() + 1]} {today.getFullYear()}
            </button>
          ) : undefined
        }
      />

      {isLoading ? (
        <div className="flex justify-center py-16"><LoadingSpinner /></div>
      ) : (
        <div className="flex gap-6">
          {/* Period list sidebar */}
          <div className="w-56 flex-shrink-0 space-y-1">
            {periods.length === 0 ? (
              <p className="text-sm text-gray-400 italic px-2">No periods yet</p>
            ) : (
              periods.map(p => (
                <button
                  key={p.id}
                  onClick={() => setSelectedId(p.id)}
                  className={`w-full flex items-center justify-between px-3 py-2.5 rounded-lg text-sm transition-colors ${
                    selectedId === p.id
                      ? 'bg-blue-50 border border-blue-200 text-blue-900'
                      : 'hover:bg-gray-50 border border-transparent text-gray-700'
                  }`}
                >
                  <span className="font-medium">{periodLabel(p)}</span>
                  <StatusBadge status={p.status} />
                </button>
              ))
            )}
          </div>

          {/* Detail panel */}
          <div className="flex-1 bg-white border border-gray-200 rounded-lg p-6">
            {selectedPeriod ? (
              <PeriodPanel
                period={selectedPeriod}
                isAdmin={isAdmin}
                onUpdated={updatePeriod}
              />
            ) : (
              <p className="text-sm text-gray-400 italic">Select a period or open the current month.</p>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
