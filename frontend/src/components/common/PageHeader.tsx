interface PageHeaderProps {
  title: string
  subtitle?: string
  description?: string
  action?: React.ReactNode
  actions?: React.ReactNode
}

export function PageHeader({ title, subtitle, description, action, actions }: PageHeaderProps) {
  const sub = subtitle ?? description
  const right = action ?? actions
  return (
    <div className="flex items-start justify-between" style={{ marginBottom: 18 }}>
      <div>
        <h1 style={{ margin: 0, fontSize: 'var(--fs-xl)', fontWeight: 600, letterSpacing: '-0.01em', color: 'var(--ink)' }}>
          {title}
        </h1>
        {sub && (
          <p style={{ margin: '3px 0 0', fontSize: 'var(--fs-body)', color: 'var(--ink-3)' }}>
            {sub}
          </p>
        )}
      </div>
      {right && <div className="flex items-center gap-2">{right}</div>}
    </div>
  )
}
