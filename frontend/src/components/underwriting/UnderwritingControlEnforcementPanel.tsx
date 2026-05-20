import { useState } from 'react'
import { AlertTriangle, CheckCircle2, ShieldAlert, ShieldCheck } from 'lucide-react'
import type { UnderwritingControlEnforcementResult, UnderwritingControlEvaluationSummary } from '@/types/underwritingGuidelines.types'

const STATUS_CLASS: Record<string, string> = {
  Passed: 'bound',
  Warning: 'quoted',
  ReferralRequired: 'submitted',
  Blocked: 'declined',
  NotApplicable: 'draft',
  UnknownField: 'expired',
  Overridden: 'bound',
}

const STATUS_LABEL: Record<string, string> = {
  ReferralRequired: 'Referral required',
  NotApplicable: 'Not applicable',
  UnknownField: 'Unknown field',
}

export function UnderwritingControlEnforcementPanel({
  title,
  summary,
  canOverride,
  isOverriding,
  onOverride,
}: {
  title: string
  summary?: UnderwritingControlEvaluationSummary
  canOverride: boolean
  isOverriding: boolean
  onOverride: (resultId: string, reason: string) => void
}) {
  const [activeResultId, setActiveResultId] = useState<string | null>(null)
  const [reason, setReason] = useState('')
  const visibleResults = summary?.results.filter((result) => result.status !== 'NotApplicable' && result.status !== 'Passed') ?? []
  const blockingCount = summary?.blockingResults.length ?? 0

  if (visibleResults.length === 0) return null

  const submitOverride = (result: UnderwritingControlEnforcementResult) => {
    const trimmed = reason.trim()
    if (!trimmed) return
    onOverride(result.id, trimmed)
    setActiveResultId(null)
    setReason('')
  }

  return (
    <div className="sd-card overflow-hidden">
      <div className="sd-card-head flex-wrap gap-3">
        <div>
          <h3>{title}</h3>
          <p className="mt-0.5 text-xs" style={{ color: 'var(--ink-3)' }}>
            {blockingCount > 0
              ? `${blockingCount} published blocker${blockingCount === 1 ? '' : 's'} active`
              : `${visibleResults.length} published control result${visibleResults.length === 1 ? '' : 's'} recorded`}
          </p>
        </div>
        {blockingCount > 0 ? (
          <span className="sd-pill declined"><ShieldAlert className="h-3.5 w-3.5" /> Blocked</span>
        ) : (
          <span className="sd-pill quoted"><ShieldCheck className="h-3.5 w-3.5" /> Review</span>
        )}
      </div>
      <div className="divide-y" style={{ borderColor: 'var(--line-2)' }}>
        {visibleResults.map((result) => {
          const canOverrideResult = canOverride && result.status === 'Blocked' && result.isBlocking && result.overrideAllowed
          const isActive = activeResultId === result.id

          return (
            <div key={result.id} className="px-4 py-3">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    {result.status === 'Blocked' ? <AlertTriangle className="h-4 w-4 text-red-600" /> : <CheckCircle2 className="h-4 w-4 text-amber-600" />}
                    <span className="font-semibold" style={{ color: 'var(--ink)' }}>{result.label}</span>
                    <span className={`sd-pill ${STATUS_CLASS[result.status] ?? 'draft'}`}>
                      {STATUS_LABEL[result.status] ?? result.status}
                    </span>
                    <span className="sd-lob">{result.stage}</span>
                  </div>
                  <div className="mt-1 text-sm" style={{ color: 'var(--ink-2)' }}>{result.message}</div>
                  <div className="mt-1 flex flex-wrap gap-2 text-xs" style={{ color: 'var(--ink-4)' }}>
                    <span>{new Date(result.evaluatedAt).toLocaleString()}</span>
                    <span className="font-mono">{result.ruleKey}</span>
                    {result.sourceCitation && <span>{result.sourceCitation}</span>}
                  </div>
                  {result.overrideReason && (
                    <div className="mt-2 rounded border px-2 py-1.5 text-xs" style={{ background: '#f0fdf4', borderColor: '#bbf7d0', color: '#166534' }}>
                      Override recorded{result.overriddenAt ? ` ${new Date(result.overriddenAt).toLocaleString()}` : ''}: {result.overrideReason}
                    </div>
                  )}
                </div>
                {canOverrideResult && (
                  <button type="button" className="sd-btn outline sm" onClick={() => { setActiveResultId(isActive ? null : result.id); setReason('') }}>
                    Override
                  </button>
                )}
              </div>
              {isActive && (
                <div className="mt-3 space-y-2">
                  <textarea
                    className="sims-textarea"
                    rows={2}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    placeholder="Document why this published blocker can proceed."
                  />
                  <div className="flex flex-wrap gap-2">
                    <button type="button" className="sd-btn primary sm" disabled={isOverriding || !reason.trim()} onClick={() => submitOverride(result)}>
                      {isOverriding ? 'Recording' : 'Record override'}
                    </button>
                    <button type="button" className="sd-btn outline sm" disabled={isOverriding} onClick={() => { setActiveResultId(null); setReason('') }}>
                      Cancel
                    </button>
                  </div>
                </div>
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}
