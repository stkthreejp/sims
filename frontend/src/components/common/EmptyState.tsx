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
      className="flex flex-col items-center justify-center text-center"
      style={{ padding: '36px 16px', color: 'var(--ink-3)' }}
    >
      <div
        className="grid place-items-center"
        style={{
          width: 36,
          height: 36,
          marginBottom: 10,
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
