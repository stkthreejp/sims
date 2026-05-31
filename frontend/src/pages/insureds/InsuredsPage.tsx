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
    <div className="subs-wrap">
      <div className="subs-page-head">
        <PageHeader title="Insureds" />
        {canCreateInsureds && (
          <Link to="/insureds/new" className="sd-btn primary">
            <Plus style={{ width: 13, height: 13 }} />
            New Insured
          </Link>
        )}
      </div>

      {/* Search */}
      <div className="sd-card" style={{ marginBottom: 14 }}>
        <div className="sd-card-body" style={{ padding: '10px 14px' }}>
          <div style={{ position: 'relative' }}>
            <Search style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', width: 13, height: 13, color: 'var(--ink-4)', pointerEvents: 'none' }} />
            <input
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1) }}
              placeholder="Search by name, email…"
              className="sims-input"
              style={{ paddingLeft: 30 }}
            />
          </div>
        </div>
      </div>

      <div className="subs-table-card">
        {isLoading ? (
          <LoadingSpinner />
        ) : data?.items.length === 0 ? (
          <EmptyState icon={Building2} title="No insureds found" description="Add your first insured to get started." />
        ) : (
          <>
            <table className="subs-table">
              <thead>
                <tr>
                  <th className="subs-th">Name</th>
                  <th className="subs-th">Type</th>
                  <th className="subs-th">Contact</th>
                  <th className="subs-th">Location</th>
                  <th className="subs-th num">Policies</th>
                  <th className="subs-th">Status</th>
                </tr>
              </thead>
              <tbody>
                {data?.items.map((insured) => (
                  <tr key={insured.id} className="subs-row">
                    <td>
                      <Link to={`/insureds/${insured.id}`} style={{ fontWeight: 600, color: 'var(--accent-ink)', textDecoration: 'none' }}>
                        {insured.displayName}
                      </Link>
                    </td>
                    <td style={{ color: 'var(--ink-2)' }}>{insured.insuredType}</td>
                    <td>
                      <div style={{ color: 'var(--ink-2)' }}>{insured.email ?? '—'}</div>
                      {insured.phone && <div style={{ color: 'var(--ink-4)', fontSize: 12 }}>{insured.phone}</div>}
                    </td>
                    <td style={{ color: 'var(--ink-2)' }}>{insured.city}, {insured.state}</td>
                    <td style={{ textAlign: 'right', color: 'var(--ink-2)' }}>{insured.policyCount}</td>
                    <td>
                      <span className={`sd-pill ${insured.isActive ? 'good' : 'withdrawn'}`}>
                        {insured.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {(data?.totalPages ?? 0) > 1 && (
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '10px 14px', borderTop: '1px solid var(--line-2)', fontSize: 13, color: 'var(--ink-3)' }}>
                <span>{data?.totalCount} total</span>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <button className="sd-btn sm outline" onClick={() => setPage(p => p - 1)} disabled={!data?.hasPreviousPage}>Prev</button>
                  <span style={{ padding: '0 8px', color: 'var(--ink-2)' }}>Page {data?.page} of {data?.totalPages}</span>
                  <button className="sd-btn sm outline" onClick={() => setPage(p => p + 1)} disabled={!data?.hasNextPage}>Next</button>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  )
}
