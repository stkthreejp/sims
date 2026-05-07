import { useState, useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import {
  Plus, Search, Filter, Download, Upload,
  MoreHorizontal, Calendar, ChevronDown,
} from 'lucide-react'
import { submissionsApi } from '@/api/submissions.api'
import { SUBMISSION_STATUS_LABELS, type SubmissionStatus } from '@/types/submission.types'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'

type SortKey = 'submissionNumber' | 'insuredName' | 'status' | 'effectiveDate' | 'createdAt'
type SortDir = 'asc' | 'desc'
type TabKey = 'all' | 'new' | 'inprogress' | 'quoted' | 'bound' | 'declined'

function statusTabKey(status: SubmissionStatus): TabKey {
  switch (status) {
    case 'New':        return 'new'
    case 'InProgress': return 'inprogress'
    case 'Quoted':     return 'quoted'
    case 'Bound':      return 'bound'
    case 'Declined':
    case 'Withdrawn':  return 'declined'
  }
}

function pillClass(status: SubmissionStatus): string {
  switch (status) {
    case 'New':        return 'draft'
    case 'InProgress': return 'inprogress'
    case 'Quoted':     return 'quoted'
    case 'Bound':      return 'bound'
    case 'Declined':   return 'declined'
    case 'Withdrawn':  return 'withdrawn'
  }
}

function rowBg(status: SubmissionStatus): string {
  switch (status) {
    case 'Quoted':     return 'var(--row-quoted)'
    case 'Bound':      return 'var(--row-bound)'
    case 'Declined':
    case 'Withdrawn':  return 'var(--row-declined)'
    case 'InProgress': return 'var(--row-inprog)'
    default:           return 'transparent'
  }
}

function uwInitials(name: string): string {
  if (!name || name === '—') return '—'
  return name.split(' ').map(x => x[0]).join('').slice(0, 2).toUpperCase()
}

function uwAvatarClass(name: string): string {
  if (!name || name === '—') return 'g0'
  const h = name.split('').reduce((a, c) => a + c.charCodeAt(0), 0) % 3
  return ['', 'g2', 'g3'][h]
}

function fmtDate(iso: string | null | undefined): string {
  if (!iso) return '—'
  const d = new Date(iso)
  return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

function daysToEff(iso: string | null | undefined): number | null {
  if (!iso) return null
  const eff = new Date(iso)
  const now = new Date()
  now.setHours(0, 0, 0, 0)
  eff.setHours(0, 0, 0, 0)
  return Math.round((eff.getTime() - now.getTime()) / 86_400_000)
}

const TABS: Array<[TabKey, string]> = [
  ['all',        'All'],
  ['new',        'Draft'],
  ['inprogress', 'In Progress'],
  ['quoted',     'Quoted'],
  ['bound',      'Bound'],
  ['declined',   'Declined'],
]

export function SubmissionsPage() {
  const navigate = useNavigate()
  const [tab, setTab]   = useState<TabKey>('all')
  const [sort, setSort] = useState<{ key: SortKey; dir: SortDir }>({ key: 'createdAt', dir: 'desc' })
  const [q, setQ]       = useState('')
  const [page, setPage] = useState(1)
  const pageSize = 25

  const { data, isLoading } = useQuery({
    queryKey: ['submissions', 'list-all'],
    queryFn:  () => submissionsApi.getAll({ pageSize: 500, sortBy: 'createdAt', sortDir: 'desc' }),
  })

  const list = data?.items ?? []

  const counts = useMemo(() => {
    const c: Record<string, number> = { all: list.length }
    list.forEach(s => {
      const k = statusTabKey(s.status)
      c[k] = (c[k] ?? 0) + 1
    })
    return c
  }, [list])

  const filtered = useMemo(() => {
    let r = list
    if (tab !== 'all') r = r.filter(s => statusTabKey(s.status) === tab)
    if (q) {
      const qq = q.toLowerCase()
      r = r.filter(s =>
        s.insuredName.toLowerCase().includes(qq) ||
        s.submissionNumber.toLowerCase().includes(qq) ||
        (s.agentName ?? '').toLowerCase().includes(qq),
      )
    }
    r = [...r].sort((a, b) => {
      let av: string = '', bv: string = ''
      switch (sort.key) {
        case 'submissionNumber': av = a.submissionNumber;    bv = b.submissionNumber;    break
        case 'insuredName':      av = a.insuredName;         bv = b.insuredName;         break
        case 'status':           av = a.status;              bv = b.status;              break
        case 'effectiveDate':    av = a.effectiveDate ?? ''; bv = b.effectiveDate ?? ''; break
        case 'createdAt':        av = a.createdAt;           bv = b.createdAt;           break
      }
      const cmp = av < bv ? -1 : av > bv ? 1 : 0
      return sort.dir === 'desc' ? -cmp : cmp
    })
    return r
  }, [list, tab, q, sort])

  const paged = useMemo(() => {
    const start = (page - 1) * pageSize
    return filtered.slice(start, start + pageSize)
  }, [filtered, page, pageSize])

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize))

  const openPipeline = list.filter(s => s.status === 'Quoted' || s.status === 'InProgress').length
  const bindRatio = useMemo(() => {
    const bound = counts.bound    ?? 0
    const dec   = counts.declined ?? 0
    const quot  = counts.quoted   ?? 0
    const total = bound + dec + quot
    return total ? Math.round((bound / total) * 100) : 0
  }, [counts])
  const expiring = useMemo(() =>
    list.filter(s => { const d = daysToEff(s.effectiveDate); return d !== null && d >= 0 && d <= 14 }).length,
    [list],
  )

  function toggleSort(key: SortKey) {
    setSort(s => ({ key, dir: s.key === key && s.dir === 'desc' ? 'asc' : 'desc' }))
    setPage(1)
  }

  function SortIco({ k }: { k: SortKey }) {
    if (sort.key !== k) return <span style={{ marginLeft: 4, color: 'var(--ink-4)', fontSize: 9 }}>↕</span>
    return <span style={{ marginLeft: 4, fontSize: 9 }}>{sort.dir === 'desc' ? '▼' : '▲'}</span>
  }

  function Th({ label, k, num }: { label: string; k: SortKey; num?: boolean }) {
    return (
      <th
        className={'subs-th' + (sort.key === k ? ' sorted' : '') + (num ? ' num' : '')}
        onClick={() => toggleSort(k)}
      >
        {label}<SortIco k={k} />
      </th>
    )
  }

  if (isLoading) return <LoadingSpinner />

  return (
    <div className="subs-wrap">
      {/* Page header */}
      <header className="subs-page-head">
        <div>
          <h1 className="subs-h1">Submissions</h1>
          <div className="subs-sub">All submissions across your book · {list.length} records</div>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="sd-btn outline"><Upload size={13} />Import</button>
          <button className="sd-btn outline"><Download size={13} />Export</button>
          <button className="sd-btn primary" onClick={() => navigate('/submissions/new')}>
            <Plus size={13} />New submission
          </button>
        </div>
      </header>

      {/* Metrics strip */}
      <div className="subs-metrics">
        <div className="subs-metric accent">
          <div className="k">Open pipeline</div>
          <div className="v">{openPipeline}</div>
          <div className="s">Quoted + In-Progress submissions</div>
        </div>
        <div className="subs-metric">
          <div className="k">Active quotes</div>
          <div className="v">{counts.quoted ?? 0}</div>
          <div className="s">{counts.inprogress ?? 0} in review</div>
        </div>
        <div className="subs-metric">
          <div className="k">Bind ratio (90d)</div>
          <div className="v">{bindRatio}%</div>
          <div className="s">{counts.bound ?? 0} bound · {counts.declined ?? 0} declined</div>
        </div>
        <div className="subs-metric">
          <div className="k">Needs action</div>
          <div className="v">{counts.inprogress ?? 0}</div>
          <div className="s">In review</div>
        </div>
        <div className="subs-metric">
          <div className="k">Expiring ≤ 14 days</div>
          <div className="v">{expiring}</div>
          <div className="s">Target effective approaching</div>
        </div>
      </div>

      {/* Toolbar */}
      <div className="subs-toolbar">
        <div className="subs-tabs">
          {TABS.map(([k, label]) => (
            <button
              key={k}
              className={'subs-tab' + (tab === k ? ' active' : '')}
              onClick={() => { setTab(k); setPage(1) }}
            >
              {label}<span className="c">{counts[k] ?? 0}</span>
            </button>
          ))}
        </div>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <div className="subs-search">
            <Search size={13} />
            <input
              placeholder="Search insured, agent, ID…"
              value={q}
              onChange={e => { setQ(e.target.value); setPage(1) }}
            />
          </div>
          <button className="subs-filter"><Filter size={12} />Filters</button>
          <button className="subs-filter">
            <Calendar size={12} />Effective<ChevronDown size={10} />
          </button>
          <button className="sd-btn ghost"><MoreHorizontal size={14} /></button>
        </div>
      </div>

      {/* Table card */}
      <div className="subs-table-card">
        {!paged.length ? (
          <div style={{ padding: '48px 0', textAlign: 'center', color: 'var(--ink-3)', fontSize: 13 }}>
            {q || tab !== 'all' ? 'No submissions match your filters.' : 'No submissions yet.'}
          </div>
        ) : (
          <table className="subs-table">
            <thead>
              <tr>
                <Th label="Submission" k="submissionNumber" />
                <Th label="Insured"    k="insuredName" />
                <th className="subs-th">Lines</th>
                <Th label="Status"     k="status" />
                <Th label="Effective"  k="effectiveDate" />
                <th className="subs-th">Underwriter</th>
                <th className="subs-th">Producer</th>
                <Th label="Received"   k="createdAt" />
                <th style={{ width: 32, background: 'var(--surface-2)', borderBottom: '1px solid var(--line)' }} />
              </tr>
            </thead>
            <tbody>
              {paged.map(s => {
                const days      = daysToEff(s.effectiveDate)
                const daysLabel = days === null ? '' : days >= 0 ? `${days}d to eff.` : `${Math.abs(days)}d past`
                const daysCls   = days === null ? '' : days < 0 ? 'past' : days <= 14 ? 'soon' : ''
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                const lobs      = (s as any).linesOfBusiness as string[] | undefined
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                const agency    = (s as any).agencyName as string | null | undefined

                return (
                  <tr
                    key={s.id}
                    className="subs-row"
                    style={{ background: rowBg(s.status) }}
                    onClick={() => navigate(`/submissions/${s.id}`)}
                  >
                    <td><div className="subs-id">{s.submissionNumber}</div></td>
                    <td><div className="subs-insured">{s.insuredName}</div></td>
                    <td>
                      {lobs && lobs.length > 0 ? (
                        <div className="subs-lob-list">
                          {lobs.slice(0, 3).map(l => <span key={l} className="sd-lob">{l}</span>)}
                          {lobs.length > 3 && <span className="sd-lob">+{lobs.length - 3}</span>}
                        </div>
                      ) : (
                        <span className="subs-dash">—</span>
                      )}
                    </td>
                    <td>
                      <span className={`sd-pill ${pillClass(s.status)}`}>
                        {SUBMISSION_STATUS_LABELS[s.status]}
                      </span>
                    </td>
                    <td>
                      <div className="subs-eff">
                        {fmtDate(s.effectiveDate)}
                        {daysLabel && <small className={daysCls}>{daysLabel}</small>}
                      </div>
                    </td>
                    <td>
                      <span className="subs-uw">
                        <span className={`subs-uw-dot ${uwAvatarClass(s.underwriterName)}`}>
                          {uwInitials(s.underwriterName)}
                        </span>
                        <span style={{ fontSize: 12.5 }}>{s.underwriterName}</span>
                      </span>
                    </td>
                    <td>
                      <div style={{ fontSize: 12.5, fontWeight: 500 }}>{s.agentName ?? '—'}</div>
                      {agency && <div style={{ fontSize: 11, color: 'var(--ink-3)' }}>{agency}</div>}
                    </td>
                    <td className="subs-muted">{fmtDate(s.createdAt)}</td>
                    <td>
                      <button className="sd-btn ghost" onClick={e => e.stopPropagation()}>
                        <MoreHorizontal size={14} />
                      </button>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        )}

        {/* Pagination */}
        <div className="subs-pagination">
          <div>Showing {paged.length} of {filtered.length} submissions</div>
          <div className="subs-pager">
            <button
              className="subs-page-btn"
              onClick={() => setPage(p => p - 1)}
              disabled={page === 1}
            >‹</button>
            {Array.from({ length: Math.min(totalPages, 5) }, (_, i) => {
              const n = i + 1
              return (
                <button
                  key={n}
                  className={'subs-page-btn' + (page === n ? ' active' : '')}
                  onClick={() => setPage(n)}
                >
                  {n}
                </button>
              )
            })}
            <button
              className="subs-page-btn"
              onClick={() => setPage(p => p + 1)}
              disabled={page >= totalPages}
            >›</button>
          </div>
          <div>Rows: <span style={{ fontWeight: 500 }}>{pageSize}</span></div>
        </div>
      </div>
    </div>
  )
}
