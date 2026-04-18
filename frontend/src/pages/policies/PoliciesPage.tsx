import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Search } from 'lucide-react'
import { quotesApi } from '@/api/quotes.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { LOB_LABELS } from '@/types/quote.types'
import { formatCurrency } from '@/lib/utils'

export function PoliciesPage() {
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  const { data, isLoading } = useQuery({
    queryKey: ['policies', { page, search }],
    queryFn: () => quotesApi.getAllPolicies({ page, pageSize: 25, search }),
  })

  return (
    <div>
      <PageHeader title="Policies" description="All bound policies" />

      <div className="mb-4 relative max-w-sm">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 pointer-events-none" />
        <input
          type="text"
          placeholder="Search policy #, insured, carrier…"
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1) }}
          className="w-full pl-9 pr-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div className="bg-white border rounded-lg overflow-hidden">
        <table className="min-w-full divide-y divide-slate-200 text-sm">
          <thead className="bg-slate-50">
            <tr>
              <th className="px-5 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide">Policy #</th>
              <th className="px-5 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide">Insured</th>
              <th className="px-5 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide">Carrier</th>
              <th className="px-5 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide">LOB</th>
              <th className="px-5 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide">Effective</th>
              <th className="px-5 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wide">Expiration</th>
              <th className="px-5 py-3 text-right text-xs font-semibold text-slate-500 uppercase tracking-wide">Premium</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {isLoading && (
              <tr><td colSpan={7} className="py-12 text-center"><LoadingSpinner /></td></tr>
            )}
            {!isLoading && data?.items.length === 0 && (
              <tr>
                <td colSpan={7} className="py-12 text-center text-slate-400 text-sm">
                  No bound policies yet.
                </td>
              </tr>
            )}
            {data?.items.map((p) => (
              <tr key={p.id} className="hover:bg-slate-50">
                <td className="px-5 py-3">
                  <Link to={`/policies/${p.id}`} className="font-medium text-blue-600 hover:underline">
                    {p.policyNumber}
                  </Link>
                </td>
                <td className="px-5 py-3 text-slate-700">{p.insuredName}</td>
                <td className="px-5 py-3 text-slate-500">{p.carrierName}</td>
                <td className="px-5 py-3 text-slate-500">{LOB_LABELS[p.lineOfBusiness]}</td>
                <td className="px-5 py-3 text-slate-500">{new Date(p.effectiveDate).toLocaleDateString()}</td>
                <td className="px-5 py-3 text-slate-500">{new Date(p.expirationDate).toLocaleDateString()}</td>
                <td className="px-5 py-3 text-right font-medium text-slate-700">{formatCurrency(p.totalPremium)}</td>
              </tr>
            ))}
          </tbody>
        </table>

        {data && data.totalPages > 1 && (
          <div className="flex items-center justify-between px-5 py-3 border-t text-sm text-slate-500">
            <span>{data.totalCount} total</span>
            <div className="flex gap-2">
              <button
                disabled={!data.hasPreviousPage}
                onClick={() => setPage((p) => p - 1)}
                className="px-3 py-1 border rounded disabled:opacity-40 hover:bg-slate-50"
              >
                Prev
              </button>
              <span className="px-3 py-1">Page {data.page} of {data.totalPages}</span>
              <button
                disabled={!data.hasNextPage}
                onClick={() => setPage((p) => p + 1)}
                className="px-3 py-1 border rounded disabled:opacity-40 hover:bg-slate-50"
              >
                Next
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
