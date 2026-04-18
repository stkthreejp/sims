import { cn } from '@/lib/utils'
import type { QuoteStatus } from '@/types/quote.types'

const statusStyles: Record<QuoteStatus, string> = {
  Draft: 'bg-slate-100 text-slate-600',
  Submitted: 'bg-blue-100 text-blue-800',
  Quoted: 'bg-yellow-100 text-yellow-800',
  Bound: 'bg-green-100 text-green-800',
  Declined: 'bg-red-100 text-red-800',
  Cancelled: 'bg-red-100 text-red-600',
  Expired: 'bg-slate-100 text-slate-500',
}

export function StatusBadge({ status }: { status: QuoteStatus }) {
  return (
    <span className={cn('inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium', statusStyles[status])}>
      {status}
    </span>
  )
}
