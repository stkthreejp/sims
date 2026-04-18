import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Plus, Search, Building2 } from 'lucide-react'
import { insuredsApi } from '@/api/insureds.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { EmptyState } from '@/components/common/EmptyState'
import { usePermissions } from '@/hooks/usePermissions'

export function InsuredsPage() {
  const { canCreateInsureds } = usePermissions()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  const { data, isLoading } = useQuery({
    queryKey: ['insureds', 'list', { search, page }],
    queryFn: () => insuredsApi.getAll({ search, page, pageSize: 25 }),
  })

  return (
    <div>
      <PageHeader
        title="Insureds"
        description="Manage your insured clients"
        actions={
          canCreateInsureds && (
            <Link
              to="/insureds/new"
              className="flex items-center gap-1.5 px-3 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium rounded-md transition-colors"
            >
              <Plus className="h-4 w-4" />
              New Insured
            </Link>
          )
        }
      />

      <div className="bg-white rounded-lg border border-slate-200">
        <div className="p-4 border-b border-slate-100">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
            <input
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1) }}
              placeholder="Search by name, email…"
              className="w-full pl-9 pr-3 py-2 border border-slate-200 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>

        {isLoading ? <LoadingSpinner /> : data?.items.length === 0 ? (
          <EmptyState icon={Building2} title="No insureds found" description="Add your first insured to get started." />
        ) : (
          <>
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 bg-slate-50 text-left">
                  <th className="px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wide">Name</th>
                  <th className="px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wide">Type</th>
                  <th className="px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wide">Contact</th>
                  <th className="px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wide">Location</th>
                  <th className="px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wide">Policies</th>
                  <th className="px-4 py-3 text-xs font-medium text-slate-500 uppercase tracking-wide">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {data?.items.map((insured) => (
                  <tr key={insured.id} className="hover:bg-slate-50 transition-colors">
                    <td className="px-4 py-3">
                      <Link to={`/insureds/${insured.id}`} className="font-medium text-blue-600 hover:underline">
                        {insured.displayName}
                      </Link>
                    </td>
                    <td className="px-4 py-3 text-slate-600">{insured.insuredType}</td>
                    <td className="px-4 py-3 text-slate-600">
                      <div>{insured.email ?? '—'}</div>
                      <div className="text-slate-400">{insured.phone ?? ''}</div>
                    </td>
                    <td className="px-4 py-3 text-slate-600">{insured.city}, {insured.state}</td>
                    <td className="px-4 py-3 text-slate-600">{insured.policyCount}</td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${insured.isActive ? 'bg-green-100 text-green-700' : 'bg-slate-100 text-slate-500'}`}>
                        {insured.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {(data?.totalPages ?? 0) > 1 && (
              <div className="flex items-center justify-between px-4 py-3 border-t border-slate-100 text-sm text-slate-600">
                <span>{data?.totalCount} total</span>
                <div className="flex gap-2">
                  <button onClick={() => setPage(p => p - 1)} disabled={!data?.hasPreviousPage} className="px-3 py-1 border rounded disabled:opacity-40">Prev</button>
                  <span className="px-3 py-1">Page {data?.page} of {data?.totalPages}</span>
                  <button onClick={() => setPage(p => p + 1)} disabled={!data?.hasNextPage} className="px-3 py-1 border rounded disabled:opacity-40">Next</button>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  )
}
