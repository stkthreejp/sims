import { useQuery } from '@tanstack/react-query'
import { FileText, Building2, TrendingUp, AlertCircle } from 'lucide-react'
import { quotesApi } from '@/api/quotes.api'
import { insuredsApi } from '@/api/insureds.api'
import { submissionsApi } from '@/api/submissions.api'
import { PageHeader } from '@/components/common/PageHeader'
import { formatCurrency } from '@/lib/utils'

function StatCard({ label, value, icon: Icon, color }: {
  label: string; value: string | number; icon: React.ElementType; color: string
}) {
  return (
    <div className="bg-white rounded-lg border border-slate-200 p-5">
      <div className="flex items-center justify-between">
        <p className="text-sm text-slate-500">{label}</p>
        <div className={`p-2 rounded-md ${color}`}>
          <Icon className="h-4 w-4 text-white" />
        </div>
      </div>
      <p className="text-2xl font-semibold text-slate-900 mt-2">{value}</p>
    </div>
  )
}

export function DashboardPage() {
  const { data: quotes } = useQuery({
    queryKey: ['quotes', 'dashboard'],
    queryFn: () => quotesApi.getAll({ pageSize: 1000, page: 1, sortBy: 'createdAt', sortDir: 'desc' }),
  })

  const { data: insureds } = useQuery({
    queryKey: ['insureds', 'dashboard'],
    queryFn: () => insuredsApi.getAll({ pageSize: 1, page: 1, sortBy: 'createdAt', sortDir: 'desc' }),
  })

  const { data: submissions } = useQuery({
    queryKey: ['submissions', 'dashboard'],
    queryFn: () => submissionsApi.getAll({ pageSize: 1, page: 1, sortBy: 'createdAt', sortDir: 'desc' }),
  })

  const allQuotes = quotes?.items ?? []
  const boundPolicies = allQuotes.filter((q) => q.status === 'Bound')
  const openQuotes = allQuotes.filter((q) => ['Draft', 'Submitted', 'Quoted'].includes(q.status))
  const totalPremium = boundPolicies.reduce((sum, q) => sum + q.totalPremium, 0)

  return (
    <div>
      <PageHeader title="Dashboard" description="Overview of your book of business" />

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        <StatCard label="Bound Policies" value={boundPolicies.length} icon={FileText} color="bg-blue-500" />
        <StatCard label="Open Quotes" value={openQuotes.length} icon={AlertCircle} color="bg-yellow-500" />
        <StatCard label="Total Insureds" value={insureds?.totalCount ?? 0} icon={Building2} color="bg-green-500" />
        <StatCard label="Written Premium" value={formatCurrency(totalPremium)} icon={TrendingUp} color="bg-purple-500" />
      </div>

      <div className="bg-white rounded-lg border border-slate-200">
        <div className="px-5 py-4 border-b border-slate-100">
          <h2 className="text-sm font-semibold text-slate-900">Recent Quotes &amp; Policies</h2>
        </div>
        <div className="divide-y divide-slate-100">
          {allQuotes.slice(0, 10).map((q) => (
            <div key={q.id} className="flex items-center justify-between px-5 py-3">
              <div>
                <p className="text-sm font-medium text-slate-900">
                  {q.policyNumber ?? q.quoteNumber}
                </p>
                <p className="text-xs text-slate-500">{q.insuredName} · {q.carrierName}</p>
              </div>
              <div className="text-right">
                <p className="text-sm font-medium text-slate-900">{formatCurrency(q.totalPremium)}</p>
                <p className="text-xs text-slate-500">{q.status}</p>
              </div>
            </div>
          ))}
          {allQuotes.length === 0 && (
            <p className="text-sm text-slate-400 px-5 py-8 text-center">No quotes yet.</p>
          )}
        </div>
      </div>
    </div>
  )
}
