import { cn } from '@/lib/utils'

const SIZES = { sm: 18, md: 28, lg: 40 }

interface LoadingSpinnerProps {
  className?: string
  size?: 'sm' | 'md' | 'lg'
  padded?: boolean
}

export function LoadingSpinner({ className, size = 'md', padded = true }: LoadingSpinnerProps) {
  const px = SIZES[size]
  return (
    <div className={cn('flex items-center justify-center', padded && 'py-12', className)}>
      <div
        className="animate-spin rounded-full"
        style={{
          width: px,
          height: px,
          border: `${size === 'sm' ? 2 : 3}px solid var(--line)`,
          borderTopColor: 'var(--accent)',
        }}
      />
    </div>
  )
}
