import { useState, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Search, FileText } from 'lucide-react'
import { policiesApi } from '@/api/policies.api'
import { LOB_LABELS } from '@/types/quote.types'
import { POLICY_STATUS_LABELS, type PolicyStatus, type PolicyListItem } from '@/types/policy.types'
import { formatCurrency, parseDateOnly } from '@/lib/utils'
import { EmptyState } from '@/components/common/EmptyState'

type SortKey = 'policyNumber' | 'insuredName' | 'expirationDate' | 'totalPremium'
type SortDir = 'asc' | 'desc'
type TabKey = 'all' | 'active' | 'expiring' | 'renewed' | 'ended'

const TABS: Array<[TabKey, string]> = [
  ['all',      'All'],
  ['active',   'Active'],
  ['expiring', 'Expiring Soon'],
  ['renewed',  'Renewed'],
  ['ended',    'Cancelled / Ended'],
]

const RENEWAL_LABELS: Record<string, string> = {
  New:        'Renewal started',
  InProgress: 'In review',
  Quoted:     'Quote ready',
  Bound:      'Renewal bound',
  Declined:   'Declined',
  Withdrawn:  'Withdrawn',
}

function daysToExp(iso: string): number {
  const exp = parseDateOnly(iso)
  const now = new Date()
  now.setHours(0, 0, 0, 0)
  exp.setHours(0, 0, 0, 0)
  return Math.round((exp.getTime() - now.getTime()) / 86_400_000)
}

function fmtDate(iso: string): string {
  return parseDateOnly(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

function tabKey(p: PolicyListItem): TabKey {
  if (p.status === 'Cancelled' || p.status === 'NonRenewed' || p.status === 'Expired') return 'ended'
  if (p.status === 'Renewed') return 'renewed'
  if (p.status === 'Active' && daysToExp(p.expirationDate) <= 90) return 'expiring'
  return 'active'
}

function rowBg(p: PolicyListItem): string {
  if (p.status === 'Cancelled' || p.status === 'NonRenewed') return 'var(--row-declined)'
  if (p.status !== 'Active') return 'transparent'
  const days = daysToExp(p.expirationDate)
  if (days <= 30 && !p.renewalSubmissionId) return 'var(--bad-bg)'
  if (days <= 90 && !p.renewalSubmissionId) return 'var(--warn-bg)'
  if (p.renewalSubmissionId) return 'var(--row-inprog)'
  return 'transparent'
}

export function PoliciesPage() {
  const navigate  = useNavigate()
  const [tab, setTab]   = useState<TabKey>('all')
  const [sort, setSort] = useState<{ key: SortKey; dir: SortDir }>({ key: 'expirationDate', dir: 'asc' })
  const [q, setQ]       = useState('')
  const [page, setPage] = useState(1)
  const pageSize = 25

  const { data, isLoading } = useQuery({
    queryKey: ['policies', 'list-all'],
    queryFn:  () => policiesApi.getAll({ pageSize: 500, sortBy: 'expirationDate', sortDir: 'asc' }),
  })

  const list = useMemo(() => data?.items ?? [], [data])

  const counts = useMemo(() => {
    const c: Record<string, number> = { all: list.length }
    list.forEach(p => {
      const k = tabKey(p)
      c[k] = (c[k] ?? 0) + 1
    })
    return c
  }, [list])

  const filtered = useMemo(() => {
    let r = list
    if (tab !== 'all') r = r.filter(p => tabKey(p) === tab)
    if (q) {
      const qq = q.toLowerCase()
      r = r.filter(p =>
        p.policyNumber.toLowerCase().includes(qq) ||
        p.insuredName.toLowerCase().includes(qq) ||
        p.carrierName.toLowerCase().includes(qq),
      )
    }
    r = [...r].sort((a, b) => {
      let cmp = 0
      switch (sort.key) {
        case 'policyNumber':   cmp = a.policyNumber.localeCompare(b.policyNumber); break
        case 'insuredName':    cmp = a.insuredName.localeCompare(b.insuredName); break
        case 'expirationDate': cmp = a.expirationDate < b.expirationDate ? -1 : a.expirationDate > b.expirationDate ? 1 : 0; break
        case 'totalPremium':   cmp = a.totalPremium - b.totalPremium; break
      }
      return sort.dir === 'desc' ? -cmp : cmp
    })
    return r
  }, [list, tab, q, sort])

  const paged = useMemo(() => {
    const start = (page - 1) * pageSize
    return filtered.slice(start, start + pageSize)
  }, [filtered, page, pageSize])

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize))

  const totalActive   = list.filter(p => p.status === 'Active').length
  const expiring30    = list.filter(p => p.status === 'Active' && !p.renewalSubmissionId && daysToExp(p.expirationDate) <= 30).length
  const renewalInProg = list.filter(p => p.status === 'Active' && !!p.renewalSubmissionId).length
  const endedCount    = list.filter(p => p.status === 'Cancelled' || p.status === 'NonRenewed').length

  function toggleSort(key: SortKey) {
    setSort(s => ({ key, dir: s.key === key && s.dir === 'asc' ? 'desc' : 'asc' }))
    setPage(1)
  }

  function Th({ label, k, num }: { label: string; k: SortKey; num?: boolean }) {
    const dir = sort.key === k ? sort.dir : null
    return (
      <th
        className={'subs-th' + (dir ? ` sorted ${dir}` : '') + (num ? ' num' : '')}
        onClick={() => toggleSort(k)}
      >
        {label}
      </th>
    )
  }

  if (isLoading) {
    return (
      <div className="subs-wrap">
        <header className="subs-page-head">
          <div>
            <h1 className="subs-h1">Policies</h1>
            <div className="subs-sub">All bound policies</div>
          </div>
        </header>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10, marginBottom: 20 }}>
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} style={{ height: 72, borderRadius: 'var(--r-lg)', background: 'var(--surface-2)', border: '1px solid var(--line)' }} />
          ))}
        </div>
        <div className="subs-table-card">
          <table className="subs-table">
            <tbody>
              {Array.from({ length: 8 }).map((_, i) => (
                <tr key={i} className="subs-row" style={{ pointerEvents: 'none' }}>
                  <td colSpan={8} style={{ padding: '12px 14px' }}>
                    <div style={{ height: 14, borderRadius: 4, background: 'var(--surface-2)', width: `${55 + (i % 4) * 12}%` }} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    )
  }

  return (
    <div className="subs-wrap">
      {/* Page header */}
      <header className="subs-page-head">
        <div>
          <h1 className="subs-h1">Policies</h1>
          <div className="subs-sub">All bound policies · {list.length} records</div>
        </div>
      </header>

      {/* Metrics strip */}
      <div className="sd-metrics">
        <div className="sd-metric accent">
          <div className="k">Total Active</div>
          <div className="v">{totalActive}</div>
          <div className="s">Currently in force</div>
        </div>
        <div className="sd-metric">
          <div className="k">Expiring ≤ 30 days</div>
          <div className="v" style={expiring30 > 0 ? { color: 'var(--bad-fg)' } : undefined}>{expiring30}</div>
          <div className="s">No renewal started</div>
        </div>
        <div className="sd-metric">
          <div className="k">Renewal in progress</div>
          <div className="v">{renewalInProg}</div>
          <div className="s">Submission open</div>
        </div>
        <div className="sd-metric">
          <div className="k">Cancelled / Non-Renewed</div>
          <div className="v">{endedCount}</div>
          <div className="s">This policy term</div>
        </div>
      </div>

      {/* Toolbar */}
      <div className="subs-toolbar">
        <div className="subs-tabs">
          {TABS.map(([key, label]) => (
            <button
              key={key}
              className={'subs-tab' + (tab === key ? ' active' : '')}
              onClick={() => { setTab(key); setPage(1) }}
            >
              {label}
              <span className="c">{counts[key] ?? 0}</span>
            </button>
          ))}
        </div>
        <label className="subs-search">
          <Search size={13} />
          <input
            placeholder="Policy #, insured, carrier…"
            value={q}
            onChange={e => { setQ(e.target.value); setPage(1) }}
          />
        </label>
      </div>

      {/* Table */}
      <div className="subs-table-card">
        <table className="subs-table">
          <thead>
            <tr>
              <Th label="Policy #"   k="policyNumber" />
              <Th label="Insured"    k="insuredName" />
              <th className="subs-th">Carrier</th>
              <th className="subs-th">LOB</th>
              <Th label="Expiration" k="expirationDate" />
              <th className="subs-th">Status</th>
              <th className="subs-th">Renewal</th>
              <Th label="Premium"    k="totalPremium" num />
            </tr>
          </thead>
          <tbody>
            {paged.length === 0 && (
              <tr>
                <td colSpan={8}>
                  <EmptyState
                    icon={FileText}
                    title="No policies found"
                    description={q ? 'Try a different search term.' : 'No policies match the selected filter.'}
                  />
                </td>
              </tr>
            )}
            {paged.map(p => {
              const days = p.status === 'Active' ? daysToExp(p.expirationDate) : null
              return (
                <tr
                  key={p.id}
                  className="subs-row"
                  style={{ background: rowBg(p) }}
                  onClick={() => navigate(`/policies/${p.id}`)}
                >
                  <td className="subs-id">{p.policyNumber}</td>
                  <td className="subs-insured">{p.insuredName}</td>
                  <td className="subs-muted">{p.carrierName}</td>
                  <td className="subs-muted">{LOB_LABELS[p.lineOfBusiness] ?? p.lineOfBusiness}</td>
                  <td className="subs-eff">
                    {fmtDate(p.expirationDate)}
                    {days !== null && days <= 90 && (
                      <small className={days <= 30 ? 'soon' : ''}>
                        {days < 0 ? `${Math.abs(days)}d ago` : days === 0 ? 'today' : `in ${days}d`}
                      </small>
                    )}
                  </td>
                  <td>
                    <span className={`sd-pill ${(p.status as PolicyStatus).toLowerCase()}`}>
                      {POLICY_STATUS_LABELS[p.status as PolicyStatus]}
                    </span>
                  </td>
                  <td>
                    {p.renewalSubmissionId ? (
                      <span className={`sd-pill ${(p.renewalSubmissionStatus ?? 'new').toLowerCase()}`}>
                        {RENEWAL_LABELS[p.renewalSubmissionStatus ?? 'New'] ?? p.renewalSubmissionStatus}
                      </span>
                    ) : (
                      <span className="subs-dash">—</span>
                    )}
                  </td>
                  <td className="subs-eff num" style={{ fontVariantNumeric: 'tabular-nums' }}>
                    {formatCurrency(p.totalPremium)}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>

        {totalPages > 1 && (
          <div className="subs-pagination">
            <span>{filtered.length !== list.length ? `${filtered.length} filtered · ` : ''}{list.length} total</span>
            <div className="subs-pager">
              <button className="subs-page-btn" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>‹</button>
              {Array.from({ length: Math.min(totalPages, 5) }).map((_, i) => {
                const n = i + 1
                return (
                  <button key={n} className={'subs-page-btn' + (n === page ? ' active' : '')} onClick={() => setPage(n)}>{n}</button>
                )
              })}
              <button className="subs-page-btn" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>›</button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
