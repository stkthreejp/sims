import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Plus, Calendar, ChevronRight, Clock, Star, FileText } from 'lucide-react'
import { quotesApi } from '@/api/quotes.api'
import { submissionsApi } from '@/api/submissions.api'
import { insuredsApi } from '@/api/insureds.api'
import { useAuthStore } from '@/store/authStore'
import type { SubmissionListItem } from '@/types/submission.types'
import type { QuoteListItem } from '@/types/quote.types'

// ─── helpers ────────────────────────────────────────────────────────────────

function fmtMoney(n: number | null | undefined, compact = false): string {
  if (n == null) return '—'
  if (compact) {
    if (Math.abs(n) >= 1e6) return '$' + (n / 1e6).toFixed(1) + 'M'
    if (Math.abs(n) >= 1000) return '$' + (n / 1000).toFixed(0) + 'k'
  }
  return '$' + n.toLocaleString('en-US', { maximumFractionDigits: 0 })
}

function greeting(): string {
  const h = new Date().getHours()
  if (h < 12) return 'Good morning'
  if (h < 17) return 'Good afternoon'
  return 'Good evening'
}

function daysUntil(dateStr: string | null): number {
  if (!dateStr) return Infinity
  const today = new Date(); today.setHours(0, 0, 0, 0)
  const target = new Date(dateStr); target.setHours(0, 0, 0, 0)
  return Math.round((target.getTime() - today.getTime()) / 86400000)
}

function ageInDays(dateStr: string): number {
  return Math.abs(daysUntil(dateStr))
}

function fmtAge(dateStr: string): string {
  const d = ageInDays(dateStr)
  if (d === 0) return 'today'
  if (d === 1) return '1d'
  if (d < 7) return `${d}d`
  return `${Math.floor(d / 7)}w`
}

/** Bucket bound-quote premiums into 12 weekly buckets (latest = rightmost). */
function buildSparkline(quotes: QuoteListItem[]): number[] {
  const now = Date.now()
  const weeks = 12
  const buckets = Array(weeks).fill(0)
  const msPerWeek = 7 * 86400 * 1000
  for (const q of quotes) {
    if (q.status !== 'Bound') continue
    const age = (now - new Date(q.createdAt).getTime()) / msPerWeek
    const idx = weeks - 1 - Math.floor(age)
    if (idx >= 0 && idx < weeks) buckets[idx] += q.totalPremium
  }
  // forward-fill zeros so we have a non-flat line
  for (let i = 1; i < weeks; i++) if (!buckets[i]) buckets[i] = buckets[i - 1]
  return buckets
}

// ─── sub-components ──────────────────────────────────────────────────────────

function Sparkline({ data, width = 140, height = 36, color = 'var(--accent)' }: {
  data: number[]; width?: number; height?: number; color?: string
}) {
  if (!data.length) return null
  const min = Math.min(...data), max = Math.max(...data)
  const range = max - min || 1
  const step = width / (data.length - 1)
  const pts = data.map((v, i): [number, number] => [
    i * step,
    height - ((v - min) / range) * (height - 4) - 2,
  ])
  const d = pts.map(([x, y], i) => (i === 0 ? `M${x},${y}` : `L${x},${y}`)).join(' ')
  const area = d + ` L${width},${height} L0,${height} Z`
  const [lx, ly] = pts[pts.length - 1]
  return (
    <svg width={width} height={height} style={{ display: 'block', flexShrink: 0 }}>
      <path d={area} fill={color} opacity="0.12" />
      <path d={d} fill="none" stroke={color} strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
      <circle cx={lx} cy={ly} r="2.5" fill={color} />
    </svg>
  )
}

function Delta({ value, suffix = '', invert = false }: { value: number | null; suffix?: string; invert?: boolean }) {
  if (value == null) return null
  const up = invert ? value < 0 : value > 0
  const neutral = value === 0
  const color = neutral ? 'var(--ink-3)' : up ? 'var(--good-fg)' : 'var(--bad-fg)'
  const arrow = neutral ? '→' : value > 0 ? '↑' : '↓'
  const fmt = (Math.abs(value) < 10 && !Number.isInteger(value))
    ? Math.abs(value).toFixed(1)
    : String(Math.abs(Math.round(value)))
  return <span style={{ color, fontSize: 11.5, fontWeight: 600 }}>{arrow} {fmt}{suffix}</span>
}

function Funnel({ data }: { data: { stage: string; count: number }[] }) {
  const max = Math.max(...data.map((d) => d.count), 1)
  return (
    <div style={{ padding: '14px 16px', display: 'flex', flexDirection: 'column', gap: 8 }}>
      {data.map((item) => (
        <div key={item.stage} style={{ display: 'grid', gridTemplateColumns: '100px 1fr 36px', gap: 10, alignItems: 'center' }}>
          <div style={{ fontSize: 11.5, color: 'var(--ink-3)', fontWeight: 500 }}>{item.stage}</div>
          <div style={{ height: 22, background: 'var(--surface-2)', borderRadius: 'var(--r-sm)', overflow: 'hidden' }}>
            <div style={{
              height: '100%', width: `${(item.count / max) * 100}%`,
              background: 'linear-gradient(90deg,var(--accent-light) 0%,var(--accent) 100%)',
              borderRadius: 'var(--r-sm)', display: 'flex', alignItems: 'center',
              justifyContent: 'flex-end', paddingRight: 8, minWidth: 28, transition: 'width .3s',
            }}>
              <span style={{ color: '#fff', fontSize: 11, fontWeight: 700 }}>{item.count}</span>
            </div>
          </div>
          <div style={{ fontSize: 11.5, color: 'var(--ink-2)', fontWeight: 600, textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>
            {item.count}
          </div>
        </div>
      ))}
    </div>
  )
}

function Card({ children, style }: { children: React.ReactNode; style?: React.CSSProperties }) {
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--line)',
      borderRadius: 'var(--r-xl)', boxShadow: 'var(--shadow-sm)', overflow: 'hidden', ...style,
    }}>
      {children}
    </div>
  )
}

function CardHead({ children }: { children: React.ReactNode }) {
  return (
    <div style={{
      display: 'flex', justifyContent: 'space-between', alignItems: 'center',
      padding: '13px 16px', borderBottom: '1px solid var(--line-2)', gap: 12,
    }}>
      {children}
    </div>
  )
}

function CardTitle({ eyebrow, title }: { eyebrow?: string; title: string }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
      {eyebrow && (
        <span style={{ fontSize: 9.5, letterSpacing: '.08em', textTransform: 'uppercase', color: 'var(--accent)', fontWeight: 700 }}>
          {eyebrow}
        </span>
      )}
      <h3 style={{ margin: 0, fontSize: 14, fontWeight: 600, letterSpacing: '-.005em', color: 'var(--ink)' }}>
        {title}
      </h3>
    </div>
  )
}

function CardAction({ children, onClick }: { children: React.ReactNode; onClick?: () => void }) {
  return (
    <button
      onClick={onClick}
      style={{ fontSize: 12, color: 'var(--accent)', fontWeight: 500, background: 'none', border: 'none', cursor: 'pointer', padding: 0 }}
    >
      {children}
    </button>
  )
}

// ─── priority helpers ────────────────────────────────────────────────────────

function getPriority(s: SubmissionListItem): 'high' | 'med' | 'low' {
  const age = ageInDays(s.createdAt)
  if (age >= 14) return 'high'
  if (age >= 7) return 'med'
  return 'low'
}

function getQueueReason(s: SubmissionListItem): string {
  if (s.status === 'New') return 'Awaiting UW assignment'
  if (s.status === 'InProgress') {
    if (s.quoteCount === 0) return 'No quotes yet — follow up with carrier'
    return 'Quote ready to review'
  }
  if (s.status === 'Quoted') return 'Awaiting bind decision'
  return 'Review needed'
}

// ─── main component ──────────────────────────────────────────────────────────

export function DashboardPage() {
  const navigate = useNavigate()
  const user = useAuthStore((s) => s.user)
  const firstName = user?.fullName?.split(' ')[0] ?? 'there'

  const { data: subsData } = useQuery({
    queryKey: ['submissions', 'dashboard'],
    queryFn: () => submissionsApi.getAll({ pageSize: 200, page: 1, sortBy: 'createdAt', sortDir: 'asc' }),
  })

  const { data: quotesData } = useQuery({
    queryKey: ['quotes', 'dashboard'],
    queryFn: () => quotesApi.getAll({ pageSize: 500, page: 1, sortBy: 'createdAt', sortDir: 'desc' }),
  })

  const { data: insuredsData } = useQuery({
    queryKey: ['insureds', 'dashboard-count'],
    queryFn: () => insuredsApi.getAll({ pageSize: 1, page: 1, sortBy: 'createdAt', sortDir: 'desc' }),
  })

  const allSubs = subsData?.items ?? []
  const allQuotes = quotesData?.items ?? []

  // KPIs
  const boundQuotes = allQuotes.filter((q) => q.status === 'Bound')
  const boundPremium = boundQuotes.reduce((s, q) => s + q.totalPremium, 0)
  const openSubs = allSubs.filter((s) => s.status === 'New' || s.status === 'InProgress' || s.status === 'Quoted')

  const effSoon = allQuotes
    .filter((q) => !['Bound', 'Declined', 'Cancelled', 'Expired'].includes(q.status))
    .map((q) => ({ ...q, daysToEff: daysUntil(q.effectiveDate) }))
    .filter((q) => q.daysToEff >= 0 && q.daysToEff <= 3)
    .sort((a, b) => a.daysToEff - b.daysToEff)

  const queue = [...openSubs]
    .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())
    .slice(0, 6)

  // Pipeline funnel
  const funnel = [
    { stage: 'New / Intake', count: allSubs.filter((s) => s.status === 'New').length },
    { stage: 'In Progress', count: allSubs.filter((s) => s.status === 'InProgress').length },
    { stage: 'Quoted', count: allSubs.filter((s) => s.status === 'Quoted').length },
    { stage: 'Bound', count: allSubs.filter((s) => s.status === 'Bound').length },
    { stage: 'Declined', count: allSubs.filter((s) => s.status === 'Declined').length },
  ].filter((f) => f.count > 0)

  const spark = buildSparkline(allQuotes)
  const highPriority = queue.filter((s) => getPriority(s) === 'high').length

  return (
    <>
      <style>{`
        .dp-queue-item { display: grid; grid-template-columns: 3px 1fr auto auto; gap: 12px; align-items: center; padding: 12px 16px; border-bottom: 1px solid var(--line-2); cursor: pointer; transition: background .1s; }
        .dp-queue-item:last-child { border-bottom: 0; }
        .dp-queue-item:hover { background: var(--hover); }
        .dp-eff-row { display: grid; grid-template-columns: 54px 1fr auto; gap: 10px; align-items: center; padding: 10px 16px; border-bottom: 1px solid var(--line-2); cursor: pointer; transition: background .1s; }
        .dp-eff-row:last-child { border-bottom: 0; }
        .dp-eff-row:hover { background: var(--hover); }
        .dp-activity-row { display: flex; align-items: flex-start; gap: 10px; padding: 8px 16px; }
        .dp-pin:hover { background: var(--hover); color: var(--ink); }
        .dp-btn-mini { display: inline-flex; align-items: center; gap: 5px; height: 24px; padding: 0 9px; font-size: 11.5px; font-weight: 500; color: var(--ink-2); border: 1px solid var(--line); border-radius: var(--r-sm); background: var(--surface); cursor: pointer; white-space: nowrap; }
        .dp-btn-mini:hover { border-color: var(--accent-light); background: var(--hover); color: var(--ink); }
        .dp-mini-th { text-align: left; font-weight: 500; color: var(--ink-3); font-size: 10.5px; padding: 8px 16px; background: var(--surface-2); border-bottom: 1px solid var(--line); letter-spacing: .04em; text-transform: uppercase; }
        .dp-mini-tr { border-bottom: 1px solid var(--line-2); cursor: pointer; transition: background .1s; }
        .dp-mini-tr:last-child { border-bottom: 0; }
        .dp-mini-tr:hover { background: var(--hover); }
        .dp-mini-td { padding: 11px 16px; vertical-align: middle; }
      `}</style>

      {/* Greeting */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end', gap: 20, marginBottom: 18 }}>
        <div>
          <h1 style={{ margin: 0, fontSize: 'var(--fs-xl)', fontWeight: 600, letterSpacing: '-.015em', color: 'var(--ink)' }}>
            {greeting()}, {firstName}
          </h1>
          <div style={{ color: 'var(--ink-3)', fontSize: 13, marginTop: 4 }}>
            You have{' '}
            <b style={{ color: 'var(--ink)', fontWeight: 600 }}>{highPriority} items</b> needing attention
            {effSoon.length > 0 && (
              <> · <b style={{ color: 'var(--ink)', fontWeight: 600 }}>{effSoon.length} quotes</b> with effective dates in the next 3 days</>
            )}
          </div>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button
            style={{
              display: 'inline-flex', alignItems: 'center', gap: 6, padding: '0 12px', height: 32,
              borderRadius: 'var(--r)', fontSize: 'var(--fs-base)', fontWeight: 500,
              color: 'var(--ink-2)', border: '1px solid var(--line)', background: 'var(--surface)',
              cursor: 'pointer',
            }}
          >
            <Calendar style={{ width: 13, height: 13 }} /> Today
          </button>
          <button
            onClick={() => navigate('/submissions/new')}
            style={{
              display: 'inline-flex', alignItems: 'center', gap: 6, padding: '0 12px', height: 32,
              borderRadius: 'var(--r)', fontSize: 'var(--fs-base)', fontWeight: 500,
              color: '#fff', background: 'var(--accent)', border: 'none', cursor: 'pointer',
            }}
          >
            <Plus style={{ width: 13, height: 13 }} /> New submission
          </button>
        </div>
      </div>

      {/* KPI strip */}
      <div style={{ display: 'grid', gridTemplateColumns: '1.6fr 1fr 1fr 1fr', gap: 10, marginBottom: 18 }}>
        {/* Hero — bound premium */}
        <div style={{
          background: 'linear-gradient(180deg,var(--accent-soft) 0%,var(--surface) 70%)',
          border: '1px solid #cfe0ef', borderRadius: 'var(--r-lg)', padding: '14px 16px',
          boxShadow: 'var(--shadow-sm)', display: 'flex', flexDirection: 'column', gap: 6,
        }}>
          <div style={{ fontSize: 10.5, letterSpacing: '.04em', textTransform: 'uppercase', color: 'var(--ink-3)', fontWeight: 600 }}>
            Bound Premium · All Time
          </div>
          <div style={{ fontSize: 26, fontWeight: 600, letterSpacing: '-.02em', lineHeight: 1, color: 'var(--accent-ink)' }}>
            {fmtMoney(boundPremium)}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 11.5, color: 'var(--ink-3)', marginTop: 'auto' }}>
            <span style={{ color: 'var(--ink-3)' }}>{boundQuotes.length} bound policies</span>
            {spark.some((v) => v > 0) && <Sparkline data={spark} width={140} height={36} />}
          </div>
        </div>

        {/* Open submissions */}
        <div style={{
          background: 'var(--surface)', border: '1px solid var(--line)', borderRadius: 'var(--r-lg)',
          padding: '14px 16px', boxShadow: 'var(--shadow-sm)', display: 'flex', flexDirection: 'column', gap: 6,
        }}>
          <div style={{ fontSize: 10.5, letterSpacing: '.04em', textTransform: 'uppercase', color: 'var(--ink-3)', fontWeight: 600 }}>
            Open Submissions
          </div>
          <div style={{ fontSize: 26, fontWeight: 600, letterSpacing: '-.02em', lineHeight: 1, color: 'var(--ink)' }}>
            {openSubs.length}
          </div>
          <div style={{ fontSize: 11.5, color: 'var(--ink-3)', marginTop: 'auto' }}>
            {allSubs.length} total across all statuses
          </div>
        </div>

        {/* Insureds */}
        <div style={{
          background: 'var(--surface)', border: '1px solid var(--line)', borderRadius: 'var(--r-lg)',
          padding: '14px 16px', boxShadow: 'var(--shadow-sm)', display: 'flex', flexDirection: 'column', gap: 6,
        }}>
          <div style={{ fontSize: 10.5, letterSpacing: '.04em', textTransform: 'uppercase', color: 'var(--ink-3)', fontWeight: 600 }}>
            Total Insureds
          </div>
          <div style={{ fontSize: 26, fontWeight: 600, letterSpacing: '-.02em', lineHeight: 1, color: 'var(--ink)' }}>
            {insuredsData?.totalCount ?? '—'}
          </div>
          <div style={{ fontSize: 11.5, color: 'var(--ink-3)', marginTop: 'auto' }}>
            in the book
          </div>
        </div>

        {/* Eff. date soon — alert card */}
        <div style={{
          background: effSoon.length > 0 ? '#fff8f5' : 'var(--surface)',
          border: `1px solid ${effSoon.length > 0 ? '#f5d2c9' : 'var(--line)'}`,
          borderRadius: 'var(--r-lg)', padding: '14px 16px', boxShadow: 'var(--shadow-sm)',
          display: 'flex', flexDirection: 'column', gap: 6,
        }}>
          <div style={{ fontSize: 10.5, letterSpacing: '.04em', textTransform: 'uppercase', color: 'var(--ink-3)', fontWeight: 600 }}>
            Eff. Date in 3 Days
          </div>
          <div style={{ fontSize: 26, fontWeight: 600, letterSpacing: '-.02em', lineHeight: 1, color: effSoon.length > 0 ? 'var(--bad-fg)' : 'var(--ink)' }}>
            {effSoon.length}
          </div>
          <div style={{ fontSize: 11.5, marginTop: 'auto' }}>
            <button style={{ color: 'var(--accent)', fontWeight: 500, background: 'none', border: 'none', cursor: 'pointer', padding: 0, fontSize: 12 }}>
              View all →
            </button>
          </div>
        </div>
      </div>

      {/* Main 2-column grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0,1.8fr) minmax(0,1fr)', gap: 14, alignItems: 'start' }}>

        {/* ── Left column ── */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>

          {/* My queue / needs attention */}
          <Card>
            <CardHead>
              <CardTitle eyebrow="PRIORITY" title="Needs attention today" />
              <div style={{ display: 'flex', gap: 2, background: 'var(--surface-2)', padding: 2, borderRadius: 'var(--r)' }}>
                {['My queue · ' + queue.length, 'All open · ' + openSubs.length].map((label, i) => (
                  <button key={label} style={{
                    padding: '5px 10px', fontSize: 11.5, color: i === 0 ? 'var(--ink)' : 'var(--ink-3)',
                    borderRadius: 5, fontWeight: i === 0 ? 600 : 500, cursor: 'pointer', border: 'none',
                    background: i === 0 ? 'var(--surface)' : 'transparent',
                    boxShadow: i === 0 ? 'var(--shadow-sm)' : 'none',
                  }}>
                    {label}
                  </button>
                ))}
              </div>
            </CardHead>

            {queue.length === 0 ? (
              <div style={{ padding: '32px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>
                No open submissions — your queue is clear.
              </div>
            ) : (
              <div>
                {queue.map((s) => {
                  const priority = getPriority(s)
                  const prioColor = priority === 'high' ? 'var(--bad-fg)' : priority === 'med' ? 'var(--warn-fg)' : 'var(--ink-4)'
                  return (
                    <div
                      key={s.id}
                      className="dp-queue-item"
                      onClick={() => navigate(`/submissions/${s.id}`)}
                    >
                      <div style={{ width: 3, alignSelf: 'stretch', borderRadius: 2, background: prioColor }} />
                      <div style={{ minWidth: 0 }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, lineHeight: 1.3 }}>
                          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--ink-3)' }}>
                            {s.submissionNumber}
                          </span>
                          <span style={{ width: 3, height: 3, borderRadius: '50%', background: 'var(--ink-4)', display: 'inline-block' }} />
                          <span style={{ fontWeight: 600, color: 'var(--ink)' }}>{s.insuredName}</span>
                        </div>
                        <div style={{ fontSize: 12, color: 'var(--ink-3)', marginTop: 3 }}>{getQueueReason(s)}</div>
                      </div>
                      <div style={{ display: 'inline-flex', alignItems: 'center', gap: 4, fontSize: 11.5, color: 'var(--ink-3)', whiteSpace: 'nowrap' }}>
                        <Clock style={{ width: 11, height: 11 }} /> {fmtAge(s.createdAt)}
                      </div>
                      <div style={{ width: 24, height: 24, display: 'grid', placeItems: 'center', color: 'var(--ink-4)' }}>
                        <ChevronRight style={{ width: 14, height: 14 }} />
                      </div>
                    </div>
                  )
                })}
              </div>
            )}
          </Card>

          {/* Quotes effective soon */}
          {effSoon.length > 0 && (
            <Card>
              <CardHead>
                <CardTitle
                  eyebrow="FOLLOW-UP"
                  title={`Quotes with effective date in next 3 days`}
                />
                <CardAction onClick={() => navigate('/submissions')}>Open all in list →</CardAction>
              </CardHead>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
                <thead>
                  <tr>
                    {['Quote', 'Insured', 'Carrier', 'Premium', 'Effective', ''].map((h, i) => (
                      <th key={i} className="dp-mini-th" style={{ textAlign: i === 3 ? 'right' : 'left' }}>{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {effSoon.slice(0, 8).map((q) => {
                    const urgentCls = q.daysToEff <= 1 ? { background: 'var(--bad-bg)', color: 'var(--bad-fg)' }
                      : q.daysToEff <= 2 ? { background: 'var(--warn-bg)', color: 'var(--warn-fg)' }
                      : { background: 'var(--surface-2)', color: 'var(--ink-3)' }
                    return (
                      <tr key={q.id} className="dp-mini-tr" onClick={() => navigate(`/submissions/${q.submissionId}`)}>
                        <td className="dp-mini-td" style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--ink-3)' }}>
                          {q.quoteNumber}
                        </td>
                        <td className="dp-mini-td" style={{ fontWeight: 600, color: 'var(--ink)' }}>{q.insuredName}</td>
                        <td className="dp-mini-td" style={{ color: 'var(--ink-2)' }}>{q.carrierName}</td>
                        <td className="dp-mini-td" style={{ textAlign: 'right', fontWeight: 600, fontVariantNumeric: 'tabular-nums' }}>
                          {fmtMoney(q.totalPremium)}
                        </td>
                        <td className="dp-mini-td">
                          <span style={{ fontVariantNumeric: 'tabular-nums', color: 'var(--ink-2)', marginRight: 8 }}>
                            {new Date(q.effectiveDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}
                          </span>
                          <span style={{
                            display: 'inline-block', fontSize: 10.5, fontWeight: 600, padding: '2px 6px',
                            borderRadius: 'var(--r-xs)', fontFamily: 'var(--font-mono)', letterSpacing: '.02em', ...urgentCls,
                          }}>
                            {q.daysToEff === 0 ? 'today' : `in ${q.daysToEff}d`}
                          </span>
                        </td>
                        <td className="dp-mini-td">
                          <button className="dp-btn-mini">Follow up</button>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </Card>
          )}

          {/* Fallback when no eff-soon quotes */}
          {effSoon.length === 0 && (
            <Card>
              <CardHead>
                <CardTitle eyebrow="FOLLOW-UP" title="Quotes with effective date in next 3 days" />
              </CardHead>
              <div style={{ padding: '28px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>
                No quotes with effective dates in the next 3 days.
              </div>
            </Card>
          )}
        </div>

        {/* ── Right column ── */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>

          {/* Tasks stub */}
          <Card>
            <CardHead>
              <h3 style={{ margin: 0, fontSize: 14, fontWeight: 600, color: 'var(--ink)' }}>Tasks</h3>
              <CardAction>All →</CardAction>
            </CardHead>
            <div style={{ padding: '24px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12 }}>
              Task management coming soon.
            </div>
          </Card>

          {/* Pipeline funnel */}
          {funnel.length > 0 && (
            <Card>
              <CardHead>
                <h3 style={{ margin: 0, fontSize: 14, fontWeight: 600, color: 'var(--ink)' }}>Pipeline</h3>
                <CardAction onClick={() => navigate('/submissions')}>View all →</CardAction>
              </CardHead>
              <Funnel data={funnel} />
            </Card>
          )}

          {/* Recent bound policies */}
          <Card>
            <CardHead>
              <h3 style={{ margin: 0, fontSize: 14, fontWeight: 600, color: 'var(--ink)' }}>Recent Bound Policies</h3>
              <CardAction onClick={() => navigate('/policies')}>All →</CardAction>
            </CardHead>
            {boundQuotes.length === 0 ? (
              <div style={{ padding: '24px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12 }}>
                No bound policies yet.
              </div>
            ) : (
              <div>
                {boundQuotes.slice(0, 5).map((q, i) => (
                  <div key={q.id} style={{
                    display: 'flex', alignItems: 'flex-start', gap: 10, padding: '10px 16px',
                    borderBottom: i < Math.min(boundQuotes.length, 5) - 1 ? '1px solid var(--line-2)' : 'none',
                  }}>
                    <span style={{
                      width: 8, height: 8, borderRadius: '50%', marginTop: 5, flexShrink: 0,
                      border: '2px solid currentColor', color: 'var(--good-fg)', background: 'var(--good-bg)',
                    }} />
                    <div style={{ minWidth: 0, flex: 1 }}>
                      <div style={{ fontSize: 12, color: 'var(--ink-2)', lineHeight: 1.4 }}>
                        <b style={{ fontWeight: 600, color: 'var(--ink)' }}>{q.insuredName}</b>{' '}
                        <span style={{ color: 'var(--ink-3)' }}>{q.carrierName}</span>
                      </div>
                      <div style={{ fontSize: 10.5, color: 'var(--ink-4)', marginTop: 2, fontFamily: 'var(--font-mono)' }}>
                        {q.policyNumber ?? q.quoteNumber}
                      </div>
                    </div>
                    <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--ink)', fontVariantNumeric: 'tabular-nums', flexShrink: 0 }}>
                      {fmtMoney(q.totalPremium, true)}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </Card>

        </div>
      </div>
    </>
  )
}
