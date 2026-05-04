import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { ratingApi } from '@/api/rating.api'
import type { ShadowRatingResult } from '@/types/rating.types'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
const fmtPct = (n: number) => `${n >= 0 ? '+' : ''}${n.toFixed(2)}%`

function DeltaBadge({ pct, isOutlier }: { pct: number; isOutlier: boolean }) {
  const color = isOutlier
    ? pct > 0
      ? { bg: '#fef2f2', text: '#dc2626' }
      : { bg: '#fff7ed', text: '#c2410c' }
    : Math.abs(pct) < 0.01
    ? { bg: '#f0fdf4', text: '#16a34a' }
    : { bg: '#fafafa', text: '#525252' }

  return (
    <span style={{
      display: 'inline-block',
      padding: '2px 8px',
      borderRadius: 9999,
      fontSize: 11,
      fontWeight: 600,
      background: color.bg,
      color: color.text,
    }}>
      {fmtPct(pct)}
    </span>
  )
}

export default function AdminShadowRatingPage() {
  const [days, setDays] = useState(30)

  const { data, isLoading, error } = useQuery({
    queryKey: ['shadow-results', days],
    queryFn: () => ratingApi.getShadowResults(days),
  })

  return (
    <div style={{ padding: '28px 32px', maxWidth: 1100 }}>
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 24 }}>
        <div>
          <h1 style={{ fontSize: 20, fontWeight: 700, color: 'var(--ink)', margin: 0 }}>Shadow Rating Dashboard</h1>
          <p style={{ fontSize: 12.5, color: 'var(--ink-3)', marginTop: 4 }}>
            Engine results run alongside the spreadsheet — compare to spot discrepancies before cutover.
          </p>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          {data && (
            <span style={{
              padding: '4px 12px',
              borderRadius: 9999,
              fontSize: 12,
              fontWeight: 600,
              background: data.shadowModeEnabled ? '#f0fdf4' : '#fef2f2',
              color: data.shadowModeEnabled ? '#16a34a' : '#dc2626',
            }}>
              Shadow Mode {data.shadowModeEnabled ? 'ON' : 'OFF'}
            </span>
          )}
          <select
            value={days}
            onChange={(e) => setDays(Number(e.target.value))}
            style={{
              padding: '5px 10px',
              fontSize: 12.5,
              border: '1px solid var(--line)',
              borderRadius: 'var(--r-sm)',
              background: 'var(--surface)',
              color: 'var(--ink)',
            }}
          >
            <option value={7}>Last 7 days</option>
            <option value={14}>Last 14 days</option>
            <option value={30}>Last 30 days</option>
            <option value={90}>Last 90 days</option>
          </select>
        </div>
      </div>

      {/* Summary cards */}
      {data && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 16, marginBottom: 28 }}>
          <SummaryCard label="Total Shadow Runs" value={String(data.totalResults)} />
          <SummaryCard
            label="Outliers (>0.5% delta)"
            value={String(data.outlierCount)}
            highlight={data.outlierCount > 0}
          />
          <SummaryCard
            label="Agreement Rate"
            value={data.totalResults > 0
              ? `${(((data.totalResults - data.outlierCount) / data.totalResults) * 100).toFixed(1)}%`
              : '—'}
          />
        </div>
      )}

      {isLoading && (
        <p style={{ color: 'var(--ink-3)', fontSize: 13 }}>Loading…</p>
      )}

      {error && (
        <p style={{ color: '#dc2626', fontSize: 13 }}>Failed to load shadow results.</p>
      )}

      {data && data.results.length === 0 && !isLoading && (
        <div style={{
          textAlign: 'center',
          padding: '60px 0',
          color: 'var(--ink-3)',
          fontSize: 13,
          border: '1px dashed var(--line)',
          borderRadius: 'var(--r)',
        }}>
          <p style={{ margin: 0, fontWeight: 600 }}>No shadow results in the last {days} days.</p>
          <p style={{ margin: '6px 0 0', fontSize: 12 }}>
            Click "Shadow Rate" on a quote to start comparing the engine against spreadsheet premiums.
          </p>
        </div>
      )}

      {data && data.results.length > 0 && (
        <div style={{ border: '1px solid var(--line)', borderRadius: 'var(--r)', overflow: 'hidden' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
            <thead>
              <tr style={{ background: 'var(--surface-2)', borderBottom: '1px solid var(--line)' }}>
                {['Quote', 'Insured', 'Plan / Version', 'Shadow Premium', 'Actual Premium', 'Delta', 'Rated At', 'By'].map((h) => (
                  <th key={h} style={{ padding: '8px 12px', textAlign: 'left', fontWeight: 600, color: 'var(--ink-2)', fontSize: 11 }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {data.results.map((r: ShadowRatingResult, i: number) => (
                <tr
                  key={r.id}
                  style={{
                    borderBottom: i < data.results.length - 1 ? '1px solid var(--line)' : 'none',
                    background: r.isOutlier ? '#fffbeb' : 'transparent',
                  }}
                >
                  <td style={{ padding: '8px 12px' }}>
                    <Link to={`/quotes/${r.quoteId}`} style={{ color: 'var(--accent)', textDecoration: 'none', fontWeight: 600 }}>
                      {r.quoteNumber}
                    </Link>
                  </td>
                  <td style={{ padding: '8px 12px', color: 'var(--ink)' }}>{r.insuredName}</td>
                  <td style={{ padding: '8px 12px', color: 'var(--ink-2)' }}>{r.planName} v{r.versionNumber}</td>
                  <td style={{ padding: '8px 12px', fontVariantNumeric: 'tabular-nums' }}>{fmt.format(r.shadowPremium)}</td>
                  <td style={{ padding: '8px 12px', fontVariantNumeric: 'tabular-nums' }}>{fmt.format(r.actualPremium)}</td>
                  <td style={{ padding: '8px 12px' }}>
                    <DeltaBadge pct={r.deltaPct} isOutlier={r.isOutlier} />
                  </td>
                  <td style={{ padding: '8px 12px', color: 'var(--ink-3)' }}>
                    {new Date(r.ratedAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}
                  </td>
                  <td style={{ padding: '8px 12px', color: 'var(--ink-3)' }}>{r.ratedByName}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Cutover checklist */}
      <div style={{ marginTop: 40, borderTop: '1px solid var(--line)', paddingTop: 28 }}>
        <h2 style={{ fontSize: 14, fontWeight: 700, color: 'var(--ink)', margin: '0 0 16px' }}>Cutover Checklist</h2>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {[
            'CarrierRatingAssignment exists for every carrier currently quoting IM.',
            'Every active equipment item on open quotes has EquipmentTypeId and Value.',
            'Run impact preview against the entire open book — no surprises.',
            'Communicate the change date to underwriters.',
            'Flip Rating:ShadowMode to false in appsettings.',
            'Keep shadow mode running for 30 days post-cutover for safety.',
            'Archive the spreadsheet to SharePoint with a note in the rating plan version\'s Notes.',
          ].map((item, i) => (
            <label key={i} style={{ display: 'flex', alignItems: 'flex-start', gap: 10, cursor: 'pointer' }}>
              <input type="checkbox" style={{ marginTop: 2, flexShrink: 0 }} />
              <span style={{ fontSize: 12.5, color: 'var(--ink-2)', lineHeight: 1.5 }}>{item}</span>
            </label>
          ))}
        </div>
      </div>
    </div>
  )
}

function SummaryCard({ label, value, highlight }: { label: string; value: string; highlight?: boolean }) {
  return (
    <div style={{
      padding: '16px 20px',
      border: `1px solid ${highlight ? '#fca5a5' : 'var(--line)'}`,
      borderRadius: 'var(--r)',
      background: highlight ? '#fff5f5' : 'var(--surface)',
    }}>
      <div style={{ fontSize: 11, color: 'var(--ink-3)', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '.05em' }}>{label}</div>
      <div style={{ fontSize: 26, fontWeight: 700, color: highlight ? '#dc2626' : 'var(--ink)', marginTop: 6 }}>{value}</div>
    </div>
  )
}
