import { cn } from '@/lib/utils'

export function LoadingSpinner({ className }: { className?: string }) {
  return (
    <div className={cn('flex items-center justify-center py-12', className)}>
      <div
        className="animate-spin rounded-full"
        style={{
          width: 28,
          height: 28,
          border: '3px solid var(--line)',
          borderTopColor: 'var(--accent)',
        }}
      />
    </div>
  )
}
