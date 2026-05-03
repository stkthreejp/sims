import { useSearchParams, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  getTrustReconciliation,
  getCarrierPayableAging,
  getSlTaxAging,
  getBrokerArAging,
  getCommissionSummary,
} from '@/api/reports.api'
import type {
  TrustReconciliation,
  PayableAging,
  BrokerArAging,
  CommissionSummary,
  AgingBucket,
  AgingRow,
  BrokerArRow,
} from '@/types/report.types'

// ── Helpers ────────────────────────────────────────────────────────────────

function fmt(n: number) {
  return n.toLocaleString('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 2 })
}

function fmtMonth(year: number, month: number) {
  return new Date(year, month - 1).toLocaleString('en-US', { month: 'short', year: 'numeric' })
}

const MONTH_NAMES = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec']

// ── Shared Components ───────────────────────────────────────────────────────

function KpiCard({ label, value, sub, highlight }: {
  label: string; value: string; sub?: string; highlight?: 'good' | 'warn' | 'bad'
}) {
  const bg = highlight === 'good' ? 'var(--green-soft, #f0fdf4)'
           : highlight === 'warn' ? 'var(--yellow-soft, #fefce8)'
           : highlight === 'bad'  ? 'var(--red-soft, #fef2f2)'
           : 'var(--surface-2, #f8f9fa)'
  return (
    <div style={{ background: bg, border: '1px solid var(--line)', borderRadius: 'var(--r-md)', padding: '14px 18px', minWidth: 160 }}>
      <div style={{ fontSize: 11, fontWeight: 600, letterSpacing: '.05em', textTransform: 'uppercase', color: 'var(--ink-3)', marginBottom: 6 }}>{label}</div>
      <div style={{ fontSize: 20, fontWeight: 700, color: 'var(--ink)' }}>{value}</div>
      {sub && <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 3 }}>{sub}</div>}
    </div>
  )
}

function AgingTable({ summary, rows, colLabel }: { summary: AgingBucket; rows: (AgingRow | BrokerArRow)[]; colLabel: string }) {
  const cols = ['Current (0–30)', '31–60', '61–90', '90+', 'Total']
  return (
    <div style={{ overflowX: 'auto' }}>
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
        <thead>
          <tr style={{ borderBottom: '1px solid var(--line)' }}>
            <th style={thStyle}>{colLabel}</th>
            {cols.map(c => <th key={c} style={{ ...thStyle, textAlign: 'right' }}>{c}</th>)}
          </tr>
        </thead>
        <tbody>
          <tr style={{ background: 'var(--surface-2, #f8f9fa)', fontWeight: 600 }}>
            <td style={tdStyle}>Total</td>
            <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(summary.current)}</td>
            <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(summary.days31to60)}</td>
            <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(summary.days61to90)}</td>
            <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(summary.over90)}</td>
            <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(summary.total)}</td>
          </tr>
          {rows.map((r, i) => {
            const name = 'payeeName' in r ? r.payeeName : r.agentName
            return (
              <tr key={i} style={{ borderBottom: '1px solid var(--line)' }}>
                <td style={tdStyle}>{name}</td>
                <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(r.current)}</td>
                <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(r.days31to60)}</td>
                <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(r.days61to90)}</td>
                <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(r.over90)}</td>
                <td style={{ ...tdStyle, textAlign: 'right', fontWeight: 600 }}>{fmt(r.total)}</td>
              </tr>
            )
          })}
          {rows.length === 0 && (
            <tr><td colSpan={6} style={{ ...tdStyle, color: 'var(--ink-4)', textAlign: 'center' }}>No open items</td></tr>
          )}
        </tbody>
      </table>
    </div>
  )
}

const thStyle: React.CSSProperties = { padding: '8px 12px', textAlign: 'left', fontWeight: 600, fontSize: 11, color: 'var(--ink-3)', whiteSpace: 'nowrap' }
const tdStyle: React.CSSProperties = { padding: '7px 12px', color: 'var(--ink)' }

function ReportShell({ title, children, isLoading, error }: {
  title: string; children: React.ReactNode; isLoading?: boolean; error?: Error | null
}) {
  return (
    <div style={{ padding: '24px 28px', maxWidth: 1100 }}>
      <h2 style={{ fontSize: 16, fontWeight: 700, color: 'var(--ink)', margin: '0 0 20px' }}>{title}</h2>
      {isLoading && <div style={{ color: 'var(--ink-4)', fontSize: 13 }}>Loading…</div>}
      {error && <div style={{ color: 'var(--red, #dc2626)', fontSize: 13 }}>Error: {error.message}</div>}
      {!isLoading && !error && children}
    </div>
  )
}

// ── Report: Trust Reconciliation ────────────────────────────────────────────

function TrustReconciliationReport() {
  const { data, isLoading, error } = useQuery<TrustReconciliation>({
    queryKey: ['report', 'trust-reconciliation'],
    queryFn: () => getTrustReconciliation(),
  })

  const diff = data?.reconcilingDifference ?? 0
  const diffHighlight = Math.abs(diff) < 0.01 ? 'good' : Math.abs(diff) < 100 ? 'warn' : 'bad'

  return (
    <ReportShell title="Trust Account Reconciliation" isLoading={isLoading} error={error as Error}>
      {data && (
        <>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 28 }}>
            <KpiCard label="Trust Balance" value={fmt(data.trustBalance)} sub={`As of ${data.asOf}`} />
            <KpiCard label="Open Invoices (AR)" value={fmt(data.openInvoices)} />
            <KpiCard label="Unapplied Receipts" value={fmt(data.unappliedReceipts)} />
            <KpiCard
              label="Reconciling Difference"
              value={fmt(diff)}
              sub={Math.abs(diff) < 0.01 ? 'Reconciled' : diff > 0 ? 'Trust excess' : 'Trust shortage'}
              highlight={diffHighlight}
            />
          </div>

          <h3 style={sectionHead}>Recent Activity (30 days)</h3>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--line)' }}>
                  {['Date', 'Source', 'Memo', 'Debit', 'Credit', 'Balance'].map(h => (
                    <th key={h} style={{ ...thStyle, textAlign: h === 'Date' || h === 'Source' || h === 'Memo' ? 'left' : 'right' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {data.recentActivity.map((t, i) => (
                  <tr key={i} style={{ borderBottom: '1px solid var(--line)' }}>
                    <td style={tdStyle}>{t.effectiveDate}</td>
                    <td style={tdStyle}>{t.sourceType}</td>
                    <td style={{ ...tdStyle, color: 'var(--ink-3)' }}>{t.memo ?? '—'}</td>
                    <td style={{ ...tdStyle, textAlign: 'right' }}>{t.debit ? fmt(t.debit) : '—'}</td>
                    <td style={{ ...tdStyle, textAlign: 'right' }}>{t.credit ? fmt(t.credit) : '—'}</td>
                    <td style={{ ...tdStyle, textAlign: 'right', fontWeight: 600 }}>{fmt(t.runningBalance)}</td>
                  </tr>
                ))}
                {data.recentActivity.length === 0 && (
                  <tr><td colSpan={6} style={{ ...tdStyle, color: 'var(--ink-4)', textAlign: 'center' }}>No activity in last 30 days</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}
    </ReportShell>
  )
}

// ── Report: Carrier Payable Aging ───────────────────────────────────────────

function CarrierPayableAgingReport() {
  const { data, isLoading, error } = useQuery<PayableAging>({
    queryKey: ['report', 'carrier-payable-aging'],
    queryFn: getCarrierPayableAging,
  })

  return (
    <ReportShell title="Carrier Payable Aging" isLoading={isLoading} error={error as Error}>
      {data && (
        <>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 28 }}>
            <KpiCard label="Current (0–30)" value={fmt(data.summary.current)} />
            <KpiCard label="31–60 Days" value={fmt(data.summary.days31to60)} highlight={data.summary.days31to60 > 0 ? 'warn' : undefined} />
            <KpiCard label="61–90 Days" value={fmt(data.summary.days61to90)} highlight={data.summary.days61to90 > 0 ? 'warn' : undefined} />
            <KpiCard label="Over 90" value={fmt(data.summary.over90)} highlight={data.summary.over90 > 0 ? 'bad' : undefined} />
            <KpiCard label="Total Outstanding" value={fmt(data.summary.total)} />
          </div>
          <AgingTable summary={data.summary} rows={data.rows} colLabel="Carrier" />
        </>
      )}
    </ReportShell>
  )
}

// ── Report: SL Tax Payable Aging ────────────────────────────────────────────

function SlTaxAgingReport() {
  const { data, isLoading, error } = useQuery<PayableAging>({
    queryKey: ['report', 'sl-tax-aging'],
    queryFn: getSlTaxAging,
  })

  return (
    <ReportShell title="SL Tax & Fee Payable Aging" isLoading={isLoading} error={error as Error}>
      {data && (
        <>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 28 }}>
            <KpiCard label="Current (0–30)" value={fmt(data.summary.current)} />
            <KpiCard label="31–60 Days" value={fmt(data.summary.days31to60)} highlight={data.summary.days31to60 > 0 ? 'warn' : undefined} />
            <KpiCard label="61–90 Days" value={fmt(data.summary.days61to90)} highlight={data.summary.days61to90 > 0 ? 'warn' : undefined} />
            <KpiCard label="Over 90" value={fmt(data.summary.over90)} highlight={data.summary.over90 > 0 ? 'bad' : undefined} />
            <KpiCard label="Total Outstanding" value={fmt(data.summary.total)} />
          </div>
          <AgingTable summary={data.summary} rows={data.rows} colLabel="Payee" />
        </>
      )}
    </ReportShell>
  )
}

// ── Report: Broker AR Aging ─────────────────────────────────────────────────

function BrokerArAgingReport() {
  const { data, isLoading, error } = useQuery<BrokerArAging>({
    queryKey: ['report', 'broker-ar-aging'],
    queryFn: getBrokerArAging,
  })

  return (
    <ReportShell title="Broker AR Aging" isLoading={isLoading} error={error as Error}>
      {data && (
        <>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 28 }}>
            <KpiCard label="Current (0–30)" value={fmt(data.summary.current)} />
            <KpiCard label="31–60 Days" value={fmt(data.summary.days31to60)} highlight={data.summary.days31to60 > 0 ? 'warn' : undefined} />
            <KpiCard label="61–90 Days" value={fmt(data.summary.days61to90)} highlight={data.summary.days61to90 > 0 ? 'warn' : undefined} />
            <KpiCard label="Over 90" value={fmt(data.summary.over90)} highlight={data.summary.over90 > 0 ? 'bad' : undefined} />
            <KpiCard label="Total Outstanding" value={fmt(data.summary.total)} />
          </div>
          <AgingTable summary={data.summary} rows={data.rows} colLabel="Agent / Broker" />
        </>
      )}
    </ReportShell>
  )
}

// ── Report: Commission Summary ──────────────────────────────────────────────

function CommissionSummaryReport() {
  const { data, isLoading, error } = useQuery<CommissionSummary>({
    queryKey: ['report', 'commission-summary'],
    queryFn: () => getCommissionSummary(12),
  })

  return (
    <ReportShell title="Commission Earned vs. Received (12 Months)" isLoading={isLoading} error={error as Error}>
      {data && (
        <>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 28 }}>
            <KpiCard label="Total Earned" value={fmt(data.totalEarned)} />
            <KpiCard label="Agent Paid Out" value={fmt(data.totalAgentPaid)} />
            <KpiCard label="Net Retained (SMM)" value={fmt(data.totalNetRetained)} />
            <KpiCard label="Cash Received" value={fmt(data.totalCashReceived)} />
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--line)' }}>
                  {['Month', 'Invoices', 'Earned', 'Agent Paid', 'Net Retained', 'Cash Received'].map(h => (
                    <th key={h} style={{ ...thStyle, textAlign: h === 'Month' ? 'left' : 'right' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {[...data.periods].reverse().map((p, i) => (
                  <tr key={i} style={{ borderBottom: '1px solid var(--line)' }}>
                    <td style={tdStyle}>{fmtMonth(p.year, p.month)}</td>
                    <td style={{ ...tdStyle, textAlign: 'right', color: 'var(--ink-3)' }}>{p.invoiceCount}</td>
                    <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(p.earned)}</td>
                    <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(p.agentPaid)}</td>
                    <td style={{ ...tdStyle, textAlign: 'right', fontWeight: 600 }}>{fmt(p.netRetained)}</td>
                    <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(p.cashReceived)}</td>
                  </tr>
                ))}
                {data.periods.length === 0 && (
                  <tr><td colSpan={6} style={{ ...tdStyle, color: 'var(--ink-4)', textAlign: 'center' }}>No invoice data</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}
    </ReportShell>
  )
}

// ── Report: Coming Soon ─────────────────────────────────────────────────────

function ComingSoon({ title }: { title: string }) {
  return (
    <ReportShell title={title}>
      <div style={{ color: 'var(--ink-4)', fontSize: 13, padding: '40px 0', textAlign: 'center' }}>
        This report is coming soon.
      </div>
    </ReportShell>
  )
}

// ── Sidebar config ──────────────────────────────────────────────────────────

const REPORT_CATEGORIES = [
  {
    label: 'Accounting',
    reports: [
      { id: 'trust-reconciliation', label: 'Trust Reconciliation' },
      { id: 'carrier-payable-aging', label: 'Carrier Payable Aging' },
      { id: 'sl-tax-aging', label: 'SL Tax Payable Aging' },
      { id: 'broker-ar-aging', label: 'Broker AR Aging' },
      { id: 'commission-summary', label: 'Commission Summary' },
      { id: 'qb-sync-health', label: 'QB Sync Health', external: '/billing/sync-health' },
    ],
  },
  {
    label: 'Production',
    reports: [
      { id: 'renewals-upcoming', label: 'Renewals Upcoming', soon: true },
      { id: 'bound-by-period', label: 'Bound by Period', soon: true },
      { id: 'hit-ratio-by-carrier', label: 'Hit Ratio by Carrier', soon: true },
    ],
  },
]

// ── Page ────────────────────────────────────────────────────────────────────

const sectionHead: React.CSSProperties = {
  fontSize: 12,
  fontWeight: 700,
  letterSpacing: '.05em',
  textTransform: 'uppercase',
  color: 'var(--ink-3)',
  margin: '0 0 10px',
}

function renderReport(id: string) {
  switch (id) {
    case 'trust-reconciliation':   return <TrustReconciliationReport />
    case 'carrier-payable-aging':  return <CarrierPayableAgingReport />
    case 'sl-tax-aging':           return <SlTaxAgingReport />
    case 'broker-ar-aging':        return <BrokerArAgingReport />
    case 'commission-summary':     return <CommissionSummaryReport />
    default:                       return null
  }
}

export function ReportsPage() {
  const [params, setParams] = useSearchParams()
  const navigate = useNavigate()
  const activeId = params.get('r') ?? 'trust-reconciliation'

  function select(id: string, external?: string) {
    if (external) { navigate(external); return }
    setParams({ r: id })
  }

  return (
    <div style={{ display: 'flex', height: '100%', overflow: 'hidden' }}>
      {/* Left nav */}
      <aside style={{
        width: 200,
        flexShrink: 0,
        borderRight: '1px solid var(--line)',
        background: 'var(--surface)',
        overflowY: 'auto',
        padding: '16px 10px',
        display: 'flex',
        flexDirection: 'column',
        gap: 18,
      }}>
        {REPORT_CATEGORIES.map(cat => (
          <div key={cat.label}>
            <div style={sectionHead}>{cat.label}</div>
            {cat.reports.map(r => (
              <button
                key={r.id}
                onClick={() => select(r.id, (r as any).external)}
                style={{
                  display: 'block',
                  width: '100%',
                  textAlign: 'left',
                  background: activeId === r.id ? 'var(--accent-soft)' : 'transparent',
                  color: activeId === r.id ? 'var(--accent-ink)' : 'var(--ink-3)',
                  border: 'none',
                  borderRadius: 'var(--r-sm)',
                  padding: '5px 10px',
                  fontSize: 12.5,
                  fontWeight: activeId === r.id ? 600 : 500,
                  cursor: 'pointer',
                  marginBottom: 1,
                }}
              >
                {r.label}
                {(r as any).soon && (
                  <span style={{ marginLeft: 6, fontSize: 9, fontWeight: 600, letterSpacing: '.05em', textTransform: 'uppercase', color: 'var(--ink-4)' }}>soon</span>
                )}
              </button>
            ))}
          </div>
        ))}
      </aside>

      {/* Main content */}
      <main style={{ flex: 1, overflowY: 'auto' }}>
        {(() => {
          const allReports = REPORT_CATEGORIES.flatMap(c => c.reports)
          const active = allReports.find(r => r.id === activeId)
          if (!active) return null
          if ((active as any).soon) return <ComingSoon title={active.label} />
          if ((active as any).external) return null
          return renderReport(activeId)
        })()}
      </main>
    </div>
  )
}
