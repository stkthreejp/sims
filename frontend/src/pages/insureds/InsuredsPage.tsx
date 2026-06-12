import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Plus, Search, Building2 } from 'lucide-react'
import { insuredsApi } from '@/api/insureds.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { EmptyState } from '@/components/common/EmptyState'
import { usePermissions } from '@/hooks/usePermissions'
import type { InsuredListItem } from '@/types/insured.types'

function MetricCard({ label, value, sub, variant }: { label: string; value: string | number; sub?: string; variant?: 'warn' | 'bad' | 'good' }) {
  const bgVar = variant ? `var(--${variant}-bg)` : 'var(--surface)'
  const fgVar = variant ? `var(--${variant}-fg)` : 'var(--ink)'
  return (
    <div style={{ flex: 1, minWidth: 0, padding: '10px 14px', background: bgVar, borderRadius: 'var(--r)', border: '1px solid var(--border)' }}>
      <div style={{ fontSize: 10.5, textTransform: 'uppercase', letterSpacing: '.06em', color: variant ? fgVar : 'var(--ink-4)', fontWeight: 600, marginBottom: 4 }}>{label}</div>
      <div style={{ fontSize: 22, fontWeight: 700, color: fgVar, lineHeight: 1.1 }}>{value}</div>
      {sub && <div style={{ fontSize: 11, color: variant ? fgVar : 'var(--ink-4)', marginTop: 3, opacity: .85 }}>{sub}</div>}
    </div>
  )
}

const today = new Date(); today.setHours(0, 0, 0, 0)
const in90 = new Date(today); in90.setDate(today.getDate() + 90)

function nearestExp(i: InsuredListItem): number {
  if (!i.nearestPolicyExpiration) return Infinity
  return new Date(i.nearestPolicyExpiration + 'T00:00:00').getTime()
}

export function InsuredsPage() {
  const { canCreateInsureds } = usePermissions()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  const { data, isLoading } = useQuery({
    queryKey: ['insureds', 'list', { search, page }],
    queryFn: () => insuredsApi.getAll({ search, page, pageSize: 500 }),
  })

  const { data: stats } = useQuery({
    queryKey: ['insureds', 'summary-stats'],
    queryFn: () => insuredsApi.getSummaryStats(),
  })

  const sorted = data?.items ? [...data.items].sort((a, b) => nearestExp(a) - nearestExp(b)) : undefined

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

      {/* Summary stats strip */}
      {stats && (
        <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
          <MetricCard label="Total Insureds" value={stats.totalInsureds} />
          <MetricCard label="Active Policies" value={stats.activePolicies} variant="good" />
          <MetricCard
            label="Expiring ≤90d"
            value={stats.expiringPolicies90d}
            variant={stats.expiringPolicies90d > 0 ? 'warn' : undefined}
          />
          <MetricCard
            label="Recent Cancellations"
            value={stats.recentCancellations}
            sub="last 90 days"
            variant={stats.recentCancellations > 0 ? 'bad' : undefined}
          />
        </div>
      )}

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
        ) : (sorted?.length ?? 0) === 0 ? (
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
                  <th className="subs-th">Nearest Expiry</th>
                  <th className="subs-th">Status</th>
                </tr>
              </thead>
              <tbody>
                {sorted?.map((insured) => {
                  const expMs = nearestExp(insured)
                  const expDate = isFinite(expMs) ? new Date(expMs) : null
                  const daysToExp = expDate ? Math.round((expDate.getTime() - today.getTime()) / 86400000) : null
                  const rowBg = insured.hasCancelledPolicy
                    ? 'var(--bad-bg)'
                    : daysToExp != null && daysToExp <= 30
                      ? 'var(--bad-bg)'
                      : daysToExp != null && daysToExp <= 90
                        ? 'var(--warn-bg)'
                        : undefined
                  return (
                    <tr key={insured.id} className="subs-row" style={rowBg ? { background: rowBg } : undefined}>
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
                      <td style={{ fontVariantNumeric: 'tabular-nums', fontSize: 12.5, color: daysToExp != null && daysToExp <= 30 ? 'var(--bad-fg)' : daysToExp != null && daysToExp <= 90 ? 'var(--warn-fg)' : 'var(--ink-2)' }}>
                        {expDate
                          ? <>{expDate.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}<br /><span style={{ fontSize: 11, fontFamily: 'var(--font-mono)' }}>{daysToExp}d</span></>
                          : <span style={{ color: 'var(--ink-4)' }}>—</span>
                        }
                      </td>
                      <td>
                        <span className={`sd-pill ${insured.isActive ? 'good' : 'withdrawn'}`}>
                          {insured.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </td>
                    </tr>
                  )
                })}
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
