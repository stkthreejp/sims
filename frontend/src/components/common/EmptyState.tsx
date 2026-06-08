import type { LucideIcon } from 'lucide-react'

interface EmptyStateProps {
  icon: LucideIcon
  title: string
  description?: string
  action?: React.ReactNode
}

export function EmptyState({ icon: Icon, title, description, action }: EmptyStateProps) {
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
          display: 'grid', placeItems: 'center',
          width: 36, height: 36,
          marginBottom: 'var(--s-2)',
          borderRadius: 'var(--r-md)',
          background: 'var(--surface-2)',
          color: 'var(--ink-3)',
        }}
      >
        <Icon size={18} strokeWidth={1.7} />
      </div>
      <h3 style={{ margin: 0, fontSize: 'var(--fs-body)', fontWeight: 600, color: 'var(--ink-2)' }}>
        {title}
      </h3>
      {description && (
        <p style={{ margin: '4px 0 0', maxWidth: 360, fontSize: 'var(--fs-base)', color: 'var(--ink-3)' }}>
          {description}
        </p>
      )}
      {action && <div style={{ marginTop: 14 }}>{action}</div>}
    </div>
  )
}
