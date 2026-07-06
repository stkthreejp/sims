import { AlertTriangle } from 'lucide-react'
import { getApiErrorMessage } from '@/lib/apiError'

interface ErrorStateProps {
  /** The error thrown by the query/mutation. */
  error?: unknown
  /** Overrides the derived message. */
  title?: string
  /** Wires up a "Try again" button (e.g. react-query's refetch). */
  onRetry?: () => void
}

/**
 * Standard "this failed to load" card so a failed fetch reads as an error the
 * user can retry — not as an empty list (audit X3/G13). Use on list/detail
 * pages: `if (isError) return <ErrorState error={error} onRetry={refetch} />`.
 */
export function ErrorState({ error, title, onRetry }: ErrorStateProps) {
  const message = title ?? getApiErrorMessage(error, "This didn't load. Please try again.")
  return (
    <div
      style={{
        display: 'flex', flexDirection: 'column', alignItems: 'center',
        justifyContent: 'center', textAlign: 'center',
        padding: 'var(--s-8) var(--s-4)', color: 'var(--ink-3)',
      }}
    >
      <div
        style={{
          display: 'grid', placeItems: 'center', width: 36, height: 36,
          marginBottom: 'var(--s-2)', borderRadius: 'var(--r-md)',
          background: 'var(--bad-bg)', color: 'var(--bad-fg)',
        }}
      >
        <AlertTriangle size={18} strokeWidth={1.7} />
      </div>
      <h3 style={{ margin: 0, fontSize: 'var(--fs-body)', fontWeight: 600, color: 'var(--ink-2)' }}>
        Couldn't load this
      </h3>
      <p style={{ margin: '4px 0 0', maxWidth: 360, fontSize: 'var(--fs-base)', color: 'var(--ink-3)' }}>
        {message}
      </p>
      {onRetry && (
        <button className="sd-btn outline sm" style={{ marginTop: 14 }} onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  )
}
