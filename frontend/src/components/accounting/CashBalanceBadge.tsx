import { useQuery } from '@tanstack/react-query'
import { Landmark } from 'lucide-react'
import { apiClient } from '@/api/client'

interface BalanceResponse { balance: number; accountLabel: string }

async function getTrustBalance(): Promise<BalanceResponse> {
  const { data } = await apiClient.get<BalanceResponse>('/billing/balance/trust')
  return data
}

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

export function CashBalanceBadge() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['trust-balance'],
    queryFn: getTrustBalance,
    staleTime: 60_000,
  })

  if (isLoading) return null

  if (isError || !data) {
    return (
      <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-amber-50 border border-amber-200 text-amber-700 text-xs font-medium">
        <Landmark className="h-3.5 w-3.5 flex-shrink-0" />
        <span>Trust balance unavailable</span>
      </div>
    )
  }

  return (
    <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-teal-50 border border-teal-200 text-teal-800 text-xs font-medium">
      <Landmark className="h-3.5 w-3.5 flex-shrink-0" />
      <span>{data.accountLabel}</span>
      <span className="font-mono">{fmt.format(data.balance)}</span>
    </div>
  )
}
