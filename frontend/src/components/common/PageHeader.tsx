import { usePageTitle } from '@/hooks/usePageTitle'

interface PageHeaderProps {
  title: string
  subtitle?: string
  action?: React.ReactNode
}

export function PageHeader({ title, subtitle, action }: PageHeaderProps) {
  usePageTitle(title)
  return (
    <div className="flex items-start justify-between" style={{ marginBottom: 18 }}>
      <div>
        <h1 style={{ margin: 0, fontSize: 'var(--fs-xl)', fontWeight: 600, letterSpacing: '-0.01em', color: 'var(--ink)' }}>
          {title}
        </h1>
        {subtitle && (
          <p style={{ margin: '3px 0 0', fontSize: 'var(--fs-body)', color: 'var(--ink-3)' }}>
            {subtitle}
          </p>
        )}
      </div>
      {action && <div className="flex items-center gap-2">{action}</div>}
    </div>
  )
}
