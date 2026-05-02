import type { QuoteStatus } from '@/types/quote.types'

const pillStyles: Record<QuoteStatus, { background: string; color: string }> = {
  Draft:     { background: 'var(--pill-draft-bg)',    color: 'var(--pill-draft-fg)' },
  Submitted: { background: 'var(--pill-inprog-bg)',   color: 'var(--pill-inprog-fg)' },
  Quoted:    { background: 'var(--pill-quoted-bg)',   color: 'var(--pill-quoted-fg)' },
  Bound:     { background: 'var(--pill-bound-bg)',    color: 'var(--pill-bound-fg)' },
  Declined:  { background: 'var(--pill-declined-bg)', color: 'var(--pill-declined-fg)' },
  Cancelled: { background: 'var(--pill-declined-bg)', color: 'var(--pill-declined-fg)' },
  Expired:   { background: 'var(--pill-draft-bg)',    color: 'var(--pill-draft-fg)' },
}

export function StatusBadge({ status }: { status: QuoteStatus }) {
  const style = pillStyles[status]
  return (
    <span
      style={{
        ...style,
        display: 'inline-flex',
        alignItems: 'center',
        gap: 6,
        padding: '3px 9px',
        borderRadius: 'var(--r-pill)',
        fontSize: 'var(--fs-sm)',
        fontWeight: 600,
        letterSpacing: '0.005em',
        lineHeight: 1.3,
      }}
    >
      <span
        style={{
          width: 5,
          height: 5,
          borderRadius: '50%',
          background: 'currentColor',
          opacity: 0.85,
          flexShrink: 0,
        }}
      />
      {status}
    </span>
  )
}
