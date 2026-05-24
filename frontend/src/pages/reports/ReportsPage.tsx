import { Link, useSearchParams, useNavigate } from 'react-router-dom'
import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  getTrustReconciliation,
  getCarrierPayableAging,
  getSlTaxAging,
  getBrokerArAging,
  getCommissionSummary,
  getInvoiceTotalsByPolicyTransaction,
  getInvoiceTotalsByProgram,
  getPostBindFollowUp,
  getManagerQueue,
  getUnassignedProgramCleanup,
  getAuthorityApprovalActivity,
  getDeclineReasonReport,
  getClearanceOverrideReport,
} from '@/api/reports.api'
import type {
  TrustReconciliation,
  PayableAging,
  BrokerArAging,
  CommissionSummary,
  InvoiceTotalsByPolicyTransaction,
  InvoiceTotalsByProgram,
  PostBindFollowUp,
  ManagerQueue,
  UnassignedProgramCleanup,
  AuthorityApprovalActivity,
  DeclineReasonReport,
  ClearanceOverrideReport,
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

function fmtDate(value?: string | null) {
  if (!value) return '-'
  return new Date(`${value}T00:00:00`).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

function fmtDateTime(value?: string | null) {
  if (!value) return '-'
  return new Date(value).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' })
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

function InvoiceTotalsByPolicyTransactionReport() {
  const { data, isLoading, error } = useQuery<InvoiceTotalsByPolicyTransaction>({
    queryKey: ['report', 'invoice-totals-by-policy-transaction'],
    queryFn: getInvoiceTotalsByPolicyTransaction,
  })

  const totalAmount = data?.rows.reduce((sum, row) => sum + row.totalAmount, 0) ?? 0
  const totalInvoices = data?.rows.reduce((sum, row) => sum + row.invoiceCount, 0) ?? 0

  return (
    <ReportShell title="Invoice Totals by Policy Transaction" isLoading={isLoading} error={error as Error}>
      {data && (
        <>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 28 }}>
            <KpiCard label="Transactions" value={data.rows.length.toLocaleString()} />
            <KpiCard label="Invoices" value={totalInvoices.toLocaleString()} />
            <KpiCard label="Total Amount" value={fmt(totalAmount)} />
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--line)' }}>
                  {['Transaction', 'Type', 'Version', 'Invoices', 'Gross Premium', 'Fees', 'Total'].map(h => (
                    <th key={h} style={{ ...thStyle, textAlign: ['Transaction', 'Type', 'Version'].includes(h) ? 'left' : 'right' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {data.rows.map(row => (
                  <tr key={row.policyTransactionId ?? 'unlinked'} style={{ borderBottom: '1px solid var(--line)' }}>
                    <td style={tdStyle}>{row.policyTransactionNumber}</td>
                    <td style={{ ...tdStyle, color: 'var(--ink-3)' }}>{row.policyTransactionType ?? '-'}</td>
                    <td style={{ ...tdStyle, color: 'var(--ink-3)' }}>{row.policyVersionNumber ? `v${row.policyVersionNumber}` : '-'}</td>
                    <td style={{ ...tdStyle, textAlign: 'right', color: 'var(--ink-3)' }}>{row.invoiceCount}</td>
                    <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(row.grossPremium)}</td>
                    <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(row.totalFees)}</td>
                    <td style={{ ...tdStyle, textAlign: 'right', fontWeight: 600 }}>{fmt(row.totalAmount)}</td>
                  </tr>
                ))}
                {data.rows.length === 0 && (
                  <tr><td colSpan={7} style={{ ...tdStyle, color: 'var(--ink-4)', textAlign: 'center' }}>No invoice data</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}
    </ReportShell>
  )
}

function InvoiceTotalsByProgramReport() {
  const [params, setParams] = useSearchParams()
  const selectedProgramId = params.get('programId') ?? ''
  const { data, isLoading, error } = useQuery<InvoiceTotalsByProgram>({
    queryKey: ['report', 'invoice-totals-by-program', selectedProgramId || null],
    queryFn: () => getInvoiceTotalsByProgram(selectedProgramId || null),
  })

  const totalAmount = data?.rows.reduce((sum, row) => sum + row.totalAmount, 0) ?? 0
  const totalNetRetained = data?.rows.reduce((sum, row) => sum + row.netRetained, 0) ?? 0
  const totalInvoices = data?.rows.reduce((sum, row) => sum + row.invoiceCount, 0) ?? 0

  function selectProgram(programId: string) {
    const next = new URLSearchParams(params)
    if (programId) next.set('programId', programId)
    else next.delete('programId')
    setParams(next)
  }

  return (
    <ReportShell title="Invoice Totals by Program" isLoading={isLoading} error={error as Error}>
      {data && (
        <>
          <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 16 }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12.5, color: 'var(--ink-3)' }}>
              Program
              <select
                value={selectedProgramId}
                onChange={(e) => selectProgram(e.target.value)}
                style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-sm)', padding: '6px 9px', minWidth: 180, color: 'var(--ink)', background: 'var(--surface)' }}
              >
                <option value="">All programs</option>
                {(data.availablePrograms ?? []).map(program => (
                  <option key={program.id} value={program.id}>{program.name}</option>
                ))}
              </select>
            </label>
          </div>

          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 28 }}>
            <KpiCard label="Programs" value={data.rows.length.toLocaleString()} />
            <KpiCard label="Invoices" value={totalInvoices.toLocaleString()} />
            <KpiCard label="Total Amount" value={fmt(totalAmount)} />
            <KpiCard label="Net Retained" value={fmt(totalNetRetained)} />
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--line)' }}>
                  {['Program', 'Invoices', 'Gross Premium', 'Fees', 'Total', 'Commission', 'Agent Paid', 'Net Retained'].map(h => (
                    <th key={h} style={{ ...thStyle, textAlign: h === 'Program' ? 'left' : 'right' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {data.rows.map(row => (
                  <tr key={row.programId ?? 'unassigned'} style={{ borderBottom: '1px solid var(--line)' }}>
                    <td style={tdStyle}>
                      <div style={{ fontWeight: 600 }}>{row.programName}</div>
                      {row.programCode && <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.programCode}</div>}
                    </td>
                    <td style={{ ...tdStyle, textAlign: 'right', color: 'var(--ink-3)' }}>{row.invoiceCount}</td>
                    <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(row.grossPremium)}</td>
                    <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(row.totalFees)}</td>
                    <td style={{ ...tdStyle, textAlign: 'right', fontWeight: 600 }}>{fmt(row.totalAmount)}</td>
                    <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(row.commissionAmount)}</td>
                    <td style={{ ...tdStyle, textAlign: 'right' }}>{fmt(row.agentCommissionAmount)}</td>
                    <td style={{ ...tdStyle, textAlign: 'right', fontWeight: 600 }}>{fmt(row.netRetained)}</td>
                  </tr>
                ))}
                {data.rows.length === 0 && (
                  <tr><td colSpan={8} style={{ ...tdStyle, color: 'var(--ink-4)', textAlign: 'center' }}>No invoice data</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}
    </ReportShell>
  )
}

function ManagerQueueReport() {
  const [typeFilter, setTypeFilter] = useState('')
  const [slaFilter, setSlaFilter] = useState('')
  const [search, setSearch] = useState('')
  const { data, isLoading, error } = useQuery<ManagerQueue>({
    queryKey: ['report', 'manager-queue'],
    queryFn: getManagerQueue,
  })

  const rows = data?.rows ?? []
  const filteredRows = rows.filter(row => {
    if (typeFilter && row.workType !== typeFilter) return false
    if (slaFilter && row.slaStatus !== slaFilter) return false
    if (search) {
      const query = search.toLowerCase()
      const target = `${row.title} ${row.detail} ${row.referenceNumber} ${row.insuredName ?? ''} ${row.ownerName ?? ''} ${row.priority} ${row.workType}`.toLowerCase()
      if (!target.includes(query)) return false
    }
    return true
  })
  const hasFilters = typeFilter || slaFilter || search
  const overdueCount = filteredRows.filter(row => row.slaStatus === 'Overdue').length

  return (
    <ReportShell title="Manager Queue" isLoading={isLoading} error={error as Error}>
      {data && (
        <>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 28 }}>
            <KpiCard label="Visible Items" value={filteredRows.length.toLocaleString()} sub={`${rows.length.toLocaleString()} total`} />
            <KpiCard label="Referrals" value={data.pendingReferralCount.toLocaleString()} highlight={data.pendingReferralCount > 0 ? 'warn' : undefined} />
            <KpiCard label="Authority" value={data.pendingAuthorityApprovalCount.toLocaleString()} highlight={data.pendingAuthorityApprovalCount > 0 ? 'warn' : undefined} />
            <KpiCard label="Post-Bind" value={data.postBindFollowUpCount.toLocaleString()} highlight={data.postBindFollowUpCount > 0 ? 'warn' : undefined} />
            <KpiCard label="Overdue" value={overdueCount.toLocaleString()} highlight={overdueCount > 0 ? 'bad' : undefined} />
          </div>

          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center', marginBottom: 16 }}>
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search queue..."
              style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-sm)', padding: '7px 10px', minWidth: 240, fontSize: 12.5, color: 'var(--ink)', background: 'var(--surface)' }}
            />
            <select value={typeFilter} onChange={(e) => setTypeFilter(e.target.value)} style={filterStyle}>
              <option value="">All work</option>
              <option value="Referral">Referrals</option>
              <option value="AuthorityApproval">Authority approvals</option>
              <option value="PostBind">Post-bind follow-up</option>
            </select>
            <select value={slaFilter} onChange={(e) => setSlaFilter(e.target.value)} style={filterStyle}>
              <option value="">All SLA</option>
              <option value="Overdue">Overdue</option>
              <option value="DueToday">Due today</option>
              <option value="DueSoon">Due soon</option>
              <option value="OnTrack">On track</option>
            </select>
            {hasFilters && (
              <button
                onClick={() => { setTypeFilter(''); setSlaFilter(''); setSearch('') }}
                style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-sm)', padding: '7px 10px', fontSize: 12.5, color: 'var(--ink-3)', background: 'var(--surface)', cursor: 'pointer' }}
              >
                Clear filters
              </button>
            )}
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--line)' }}>
                  {['Work', 'Reference', 'Insured', 'Owner', 'Opened', 'Due', 'SLA', 'Detail'].map(h => (
                    <th key={h} style={thStyle}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {filteredRows.map(row => (
                  <tr key={`${row.workType}-${row.id}`} style={{ borderBottom: '1px solid var(--line)', verticalAlign: 'top' }}>
                    <td style={tdStyle}>
                      <WorkTypeBadge type={row.workType} />
                      <div style={{ fontWeight: 600, marginTop: 6 }}>{row.title}</div>
                      <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.priority}</div>
                    </td>
                    <td style={tdStyle}>
                      <Link to={row.actionUrl} style={{ color: 'var(--accent-ink)', fontWeight: 600, textDecoration: 'none' }}>
                        {row.referenceNumber || 'Open'}
                      </Link>
                    </td>
                    <td style={tdStyle}>{row.insuredName ?? '-'}</td>
                    <td style={tdStyle}>{row.ownerName ?? 'Unassigned'}</td>
                    <td style={tdStyle}>
                      <div>{new Date(row.createdAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}</div>
                      <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.daysOpen}d open</div>
                    </td>
                    <td style={tdStyle}>{fmtDate(row.dueDate)}</td>
                    <td style={tdStyle}><SlaBadge status={row.slaStatus} /></td>
                    <td style={{ ...tdStyle, minWidth: 260, color: 'var(--ink-3)' }}>{row.detail || '-'}</td>
                  </tr>
                ))}
                {filteredRows.length === 0 && (
                  <tr><td colSpan={8} style={{ ...tdStyle, color: 'var(--ink-4)', textAlign: 'center' }}>No manager queue items</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}
    </ReportShell>
  )
}

function AuthorityApprovalActivityReport() {
  const [statusFilter, setStatusFilter] = useState('')
  const [overrideFilter, setOverrideFilter] = useState('')
  const [programFilter, setProgramFilter] = useState('')
  const [search, setSearch] = useState('')
  const { data, isLoading, error } = useQuery<AuthorityApprovalActivity>({
    queryKey: ['report', 'authority-approvals'],
    queryFn: getAuthorityApprovalActivity,
  })

  const rows = data?.rows ?? []
  const programs = Array.from(
    new Map(rows.filter(row => row.programId).map(row => [row.programId!, row.programName ?? 'Unassigned'])).entries()
  ).sort((a, b) => a[1].localeCompare(b[1]))
  const filteredRows = rows.filter(row => {
    if (statusFilter && row.status !== statusFilter) return false
    if (overrideFilter === 'override' && !row.isOverride) return false
    if (overrideFilter === 'standard' && row.isOverride) return false
    if (programFilter && row.programId !== programFilter) return false
    if (search) {
      const query = search.toLowerCase()
      const target = `${row.actionLabel} ${row.approvalType} ${row.reason} ${row.referenceNumber} ${row.insuredName ?? ''} ${row.programName ?? ''} ${row.programCode ?? ''} ${row.lineOfBusiness ?? ''} ${row.state ?? ''} ${row.requestedByName ?? ''} ${row.ownerName ?? ''} ${row.decisionByName ?? ''}`.toLowerCase()
      if (!target.includes(query)) return false
    }
    return true
  })
  const hasFilters = statusFilter || overrideFilter || programFilter || search

  return (
    <ReportShell title="Authority Approvals" isLoading={isLoading} error={error as Error}>
      {data && (
        <>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 28 }}>
            <KpiCard label="Visible Items" value={filteredRows.length.toLocaleString()} sub={`${rows.length.toLocaleString()} total`} />
            <KpiCard label="Pending" value={data.pendingCount.toLocaleString()} highlight={data.pendingCount > 0 ? 'warn' : undefined} />
            <KpiCard label="Overdue" value={data.overduePendingCount.toLocaleString()} highlight={data.overduePendingCount > 0 ? 'bad' : undefined} />
            <KpiCard label="Overrides" value={data.overrideCount.toLocaleString()} highlight={data.overrideCount > 0 ? 'warn' : undefined} />
            <KpiCard label="Avg Decision" value={data.averageDecisionHours == null ? '-' : `${data.averageDecisionHours}h`} />
          </div>

          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center', marginBottom: 16 }}>
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search approvals..."
              style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-sm)', padding: '7px 10px', minWidth: 240, fontSize: 12.5, color: 'var(--ink)', background: 'var(--surface)' }}
            />
            <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)} style={filterStyle}>
              <option value="">All status</option>
              <option value="Pending">Pending</option>
              <option value="Approved">Approved</option>
              <option value="Declined">Declined</option>
              <option value="Cancelled">Cancelled</option>
            </select>
            <select value={overrideFilter} onChange={(e) => setOverrideFilter(e.target.value)} style={filterStyle}>
              <option value="">All approvals</option>
              <option value="override">Overrides</option>
              <option value="standard">Standard</option>
            </select>
            <select value={programFilter} onChange={(e) => setProgramFilter(e.target.value)} style={filterStyle}>
              <option value="">All programs</option>
              {programs.map(([id, name]) => <option key={id} value={id}>{name}</option>)}
            </select>
            {hasFilters && (
              <button
                onClick={() => { setStatusFilter(''); setOverrideFilter(''); setProgramFilter(''); setSearch('') }}
                style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-sm)', padding: '7px 10px', fontSize: 12.5, color: 'var(--ink-3)', background: 'var(--surface)', cursor: 'pointer' }}
              >
                Clear filters
              </button>
            )}
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--line)' }}>
                  {['Action', 'Reference', 'Insured', 'Program', 'Requested', 'Owner', 'Due', 'Decision', 'SLA', 'Reason'].map(h => (
                    <th key={h} style={thStyle}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {filteredRows.map(row => (
                  <tr key={row.id} style={{ borderBottom: '1px solid var(--line)', verticalAlign: 'top' }}>
                    <td style={tdStyle}>
                      <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexWrap: 'wrap' }}>
                        <StatusBadge status={row.status} />
                        {row.isOverride && <OverrideBadge />}
                      </div>
                      <div style={{ fontWeight: 600, marginTop: 6 }}>{row.actionLabel}</div>
                      <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.approvalType}</div>
                    </td>
                    <td style={tdStyle}>
                      <Link to={row.actionUrl} style={{ color: 'var(--accent-ink)', fontWeight: 600, textDecoration: 'none' }}>
                        {row.referenceNumber || row.targetType}
                      </Link>
                    </td>
                    <td style={tdStyle}>{row.insuredName ?? '-'}</td>
                    <td style={tdStyle}>
                      <div>{row.programName ?? 'Unassigned'}</div>
                      {row.programCode && <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.programCode}</div>}
                      {(row.lineOfBusiness || row.state) && <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{[row.lineOfBusiness, row.state].filter(Boolean).join(' / ')}</div>}
                    </td>
                    <td style={tdStyle}>
                      <div>{fmtDateTime(row.requestedAt)}</div>
                      <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.requestedByName ?? 'Unknown'}</div>
                    </td>
                    <td style={tdStyle}>{row.ownerName ?? 'Unassigned'}</td>
                    <td style={tdStyle}>
                      <div>{fmtDateTime(row.dueAt)}</div>
                      {row.hoursUntilDue != null && (
                        <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>
                          {row.hoursUntilDue < 0 ? `${Math.abs(row.hoursUntilDue)}h late` : `${row.hoursUntilDue}h left`}
                        </div>
                      )}
                    </td>
                    <td style={tdStyle}>
                      <div>{fmtDateTime(row.decisionAt)}</div>
                      {row.decisionHours != null && <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.decisionHours}h turnaround</div>}
                      {row.decisionByName && <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.decisionByName}</div>}
                    </td>
                    <td style={tdStyle}><SlaBadge status={row.slaStatus} /></td>
                    <td style={{ ...tdStyle, minWidth: 260, color: 'var(--ink-3)' }}>{row.reason || '-'}</td>
                  </tr>
                ))}
                {filteredRows.length === 0 && (
                  <tr><td colSpan={10} style={{ ...tdStyle, color: 'var(--ink-4)', textAlign: 'center' }}>No authority approval items</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}
    </ReportShell>
  )
}

function DeclineReasonReportView() {
  const [reasonFilter, setReasonFilter] = useState('')
  const [search, setSearch] = useState('')
  const { data, isLoading, error } = useQuery<DeclineReasonReport>({
    queryKey: ['report', 'decline-reasons'],
    queryFn: getDeclineReasonReport,
  })

  const rows = data?.rows ?? []
  const filteredRows = rows.filter(row => {
    if (reasonFilter && row.reason !== reasonFilter) return false
    if (search) {
      const query = search.toLowerCase()
      const target = `${row.quoteNumber} ${row.submissionNumber} ${row.insuredName} ${row.carrierName} ${row.programName ?? ''} ${row.lineOfBusiness} ${row.state ?? ''} ${row.reason}`.toLowerCase()
      if (!target.includes(query)) return false
    }
    return true
  })
  const hasFilters = reasonFilter || search

  return (
    <ReportShell title="Decline Reasons" isLoading={isLoading} error={error as Error}>
      {data && (
        <>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 28 }}>
            <KpiCard label="Visible Declines" value={filteredRows.length.toLocaleString()} sub={`${rows.length.toLocaleString()} total`} />
            <KpiCard label="With Reason" value={data.withReasonCount.toLocaleString()} />
            <KpiCard label="Unspecified" value={data.unspecifiedCount.toLocaleString()} highlight={data.unspecifiedCount > 0 ? 'warn' : undefined} />
            <KpiCard label="Reason Buckets" value={data.reasons.length.toLocaleString()} />
          </div>

          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center', marginBottom: 16 }}>
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search declined quotes..."
              style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-sm)', padding: '7px 10px', minWidth: 240, fontSize: 12.5, color: 'var(--ink)', background: 'var(--surface)' }}
            />
            <select value={reasonFilter} onChange={(e) => setReasonFilter(e.target.value)} style={filterStyle}>
              <option value="">All reasons</option>
              {data.reasons.map(reason => (
                <option key={reason.reason} value={reason.reason}>{reason.reason}</option>
              ))}
            </select>
            {hasFilters && (
              <button
                onClick={() => { setReasonFilter(''); setSearch('') }}
                style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-sm)', padding: '7px 10px', fontSize: 12.5, color: 'var(--ink-3)', background: 'var(--surface)', cursor: 'pointer' }}
              >
                Clear filters
              </button>
            )}
          </div>

          <h3 style={sectionHead}>Reason Summary</h3>
          <div style={{ overflowX: 'auto', marginBottom: 24 }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--line)' }}>
                  {['Reason', 'Count', 'Share'].map(h => (
                    <th key={h} style={{ ...thStyle, textAlign: h === 'Reason' ? 'left' : 'right' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {data.reasons.map(reason => (
                  <tr key={reason.reason} style={{ borderBottom: '1px solid var(--line)' }}>
                    <td style={tdStyle}>{reason.reason}</td>
                    <td style={{ ...tdStyle, textAlign: 'right', color: 'var(--ink-3)' }}>{reason.count}</td>
                    <td style={{ ...tdStyle, textAlign: 'right', fontWeight: 600 }}>{Math.round(reason.share * 100)}%</td>
                  </tr>
                ))}
                {data.reasons.length === 0 && (
                  <tr><td colSpan={3} style={{ ...tdStyle, color: 'var(--ink-4)', textAlign: 'center' }}>No decline reasons</td></tr>
                )}
              </tbody>
            </table>
          </div>

          <h3 style={sectionHead}>Declined Quotes</h3>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--line)' }}>
                  {['Quote', 'Submission', 'Insured', 'Program', 'Carrier / LOB', 'State', 'Declined', 'Reason'].map(h => (
                    <th key={h} style={thStyle}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {filteredRows.map(row => (
                  <tr key={row.quoteId} style={{ borderBottom: '1px solid var(--line)', verticalAlign: 'top' }}>
                    <td style={tdStyle}>
                      <Link to={row.actionUrl} style={{ color: 'var(--accent-ink)', fontWeight: 600, textDecoration: 'none' }}>
                        {row.quoteNumber}
                      </Link>
                    </td>
                    <td style={tdStyle}>{row.submissionNumber}</td>
                    <td style={tdStyle}>{row.insuredName}</td>
                    <td style={tdStyle}>
                      <div>{row.programName ?? 'Unassigned'}</div>
                      {row.programCode && <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.programCode}</div>}
                    </td>
                    <td style={tdStyle}>
                      <div>{row.carrierName}</div>
                      <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.lineOfBusiness}</div>
                    </td>
                    <td style={tdStyle}>{row.state ?? '-'}</td>
                    <td style={tdStyle}>{fmtDateTime(row.declinedAt)}</td>
                    <td style={{ ...tdStyle, minWidth: 260, color: row.reason === 'Unspecified' ? 'var(--red, #b91c1c)' : 'var(--ink-3)' }}>{row.reason}</td>
                  </tr>
                ))}
                {filteredRows.length === 0 && (
                  <tr><td colSpan={8} style={{ ...tdStyle, color: 'var(--ink-4)', textAlign: 'center' }}>No declined quotes</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}
    </ReportShell>
  )
}

function ClearanceOverrideReportView() {
  const [checkFilter, setCheckFilter] = useState('')
  const [search, setSearch] = useState('')
  const { data, isLoading, error } = useQuery<ClearanceOverrideReport>({
    queryKey: ['report', 'clearance-overrides'],
    queryFn: getClearanceOverrideReport,
  })

  const rows = data?.rows ?? []
  const filteredRows = rows.filter(row => {
    if (checkFilter && row.checkType !== checkFilter) return false
    if (search) {
      const query = search.toLowerCase()
      const target = `${row.submissionNumber} ${row.insuredName} ${row.programName ?? ''} ${row.lineOfBusiness ?? ''} ${row.state ?? ''} ${row.checkType} ${row.matchedRecordLabel ?? ''} ${row.overrideReason} ${row.overriddenByName ?? ''}`.toLowerCase()
      if (!target.includes(query)) return false
    }
    return true
  })
  const hasFilters = checkFilter || search

  return (
    <ReportShell title="Clearance Overrides" isLoading={isLoading} error={error as Error}>
      {data && (
        <>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 28 }}>
            <KpiCard label="Visible Overrides" value={filteredRows.length.toLocaleString()} sub={`${rows.length.toLocaleString()} total`} />
            <KpiCard label="Blocked Checks" value={data.blockedOverrideCount.toLocaleString()} highlight={data.blockedOverrideCount > 0 ? 'warn' : undefined} />
            <KpiCard label="Warning Checks" value={data.warningOverrideCount.toLocaleString()} />
            <KpiCard label="Check Types" value={data.checkTypes.length.toLocaleString()} />
          </div>

          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center', marginBottom: 16 }}>
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search overrides..."
              style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-sm)', padding: '7px 10px', minWidth: 240, fontSize: 12.5, color: 'var(--ink)', background: 'var(--surface)' }}
            />
            <select value={checkFilter} onChange={(e) => setCheckFilter(e.target.value)} style={filterStyle}>
              <option value="">All checks</option>
              {data.checkTypes.map(check => (
                <option key={check.checkType} value={check.checkType}>{check.checkType}</option>
              ))}
            </select>
            {hasFilters && (
              <button
                onClick={() => { setCheckFilter(''); setSearch('') }}
                style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-sm)', padding: '7px 10px', fontSize: 12.5, color: 'var(--ink-3)', background: 'var(--surface)', cursor: 'pointer' }}
              >
                Clear filters
              </button>
            )}
          </div>

          <h3 style={sectionHead}>Check Summary</h3>
          <div style={{ overflowX: 'auto', marginBottom: 24 }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--line)' }}>
                  {['Check', 'Overrides'].map(h => (
                    <th key={h} style={{ ...thStyle, textAlign: h === 'Check' ? 'left' : 'right' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {data.checkTypes.map(check => (
                  <tr key={check.checkType} style={{ borderBottom: '1px solid var(--line)' }}>
                    <td style={tdStyle}>{check.checkType}</td>
                    <td style={{ ...tdStyle, textAlign: 'right', fontWeight: 600 }}>{check.count}</td>
                  </tr>
                ))}
                {data.checkTypes.length === 0 && (
                  <tr><td colSpan={2} style={{ ...tdStyle, color: 'var(--ink-4)', textAlign: 'center' }}>No clearance overrides</td></tr>
                )}
              </tbody>
            </table>
          </div>

          <h3 style={sectionHead}>Override Details</h3>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--line)' }}>
                  {['Submission', 'Insured', 'Program', 'LOB / State', 'Check', 'Matched', 'Overridden', 'Reason'].map(h => (
                    <th key={h} style={thStyle}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {filteredRows.map(row => (
                  <tr key={row.id} style={{ borderBottom: '1px solid var(--line)', verticalAlign: 'top' }}>
                    <td style={tdStyle}>
                      <Link to={row.actionUrl} style={{ color: 'var(--accent-ink)', fontWeight: 600, textDecoration: 'none' }}>
                        {row.submissionNumber}
                      </Link>
                    </td>
                    <td style={tdStyle}>{row.insuredName}</td>
                    <td style={tdStyle}>
                      <div>{row.programName ?? 'Unassigned'}</div>
                      {row.programCode && <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.programCode}</div>}
                    </td>
                    <td style={tdStyle}>
                      <div>{row.lineOfBusiness ?? '-'}</div>
                      <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.state ?? '-'}</div>
                    </td>
                    <td style={tdStyle}>
                      <div style={{ fontWeight: 600 }}>{row.checkType}</div>
                      <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.status}</div>
                    </td>
                    <td style={tdStyle}>{row.matchedRecordLabel ?? '-'}</td>
                    <td style={tdStyle}>
                      <div>{fmtDateTime(row.overriddenAt)}</div>
                      <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.overriddenByName ?? 'Unknown'}</div>
                    </td>
                    <td style={{ ...tdStyle, minWidth: 280 }}>
                      <div>{row.overrideReason || '-'}</div>
                      <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 4 }}>{row.explanation}</div>
                    </td>
                  </tr>
                ))}
                {filteredRows.length === 0 && (
                  <tr><td colSpan={8} style={{ ...tdStyle, color: 'var(--ink-4)', textAlign: 'center' }}>No clearance overrides</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}
    </ReportShell>
  )
}

function UnassignedProgramCleanupReport() {
  const [typeFilter, setTypeFilter] = useState('')
  const [search, setSearch] = useState('')
  const { data, isLoading, error } = useQuery<UnassignedProgramCleanup>({
    queryKey: ['report', 'unassigned-program-cleanup'],
    queryFn: getUnassignedProgramCleanup,
  })

  const rows = data?.rows ?? []
  const filteredRows = rows.filter(row => {
    if (typeFilter && row.recordType !== typeFilter) return false
    if (search) {
      const query = search.toLowerCase()
      const target = `${row.referenceNumber} ${row.insuredName} ${row.carrierName} ${row.lineOfBusiness} ${row.state ?? ''} ${row.status}`.toLowerCase()
      if (!target.includes(query)) return false
    }
    return true
  })
  const hasFilters = typeFilter || search

  return (
    <ReportShell title="Unassigned Program Cleanup" isLoading={isLoading} error={error as Error}>
      {data && (
        <>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 28 }}>
            <KpiCard label="Visible Items" value={filteredRows.length.toLocaleString()} sub={`${rows.length.toLocaleString()} total`} />
            <KpiCard label="Open Quotes" value={data.openQuoteCount.toLocaleString()} highlight={data.openQuoteCount > 0 ? 'warn' : undefined} />
            <KpiCard label="Active Policies" value={data.activePolicyCount.toLocaleString()} highlight={data.activePolicyCount > 0 ? 'warn' : undefined} />
          </div>

          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center', marginBottom: 16 }}>
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search records..."
              style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-sm)', padding: '7px 10px', minWidth: 240, fontSize: 12.5, color: 'var(--ink)', background: 'var(--surface)' }}
            />
            <select value={typeFilter} onChange={(e) => setTypeFilter(e.target.value)} style={filterStyle}>
              <option value="">All records</option>
              <option value="Quote">Quotes</option>
              <option value="Policy">Policies</option>
            </select>
            {hasFilters && (
              <button
                onClick={() => { setTypeFilter(''); setSearch('') }}
                style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-sm)', padding: '7px 10px', fontSize: 12.5, color: 'var(--ink-3)', background: 'var(--surface)', cursor: 'pointer' }}
              >
                Clear filters
              </button>
            )}
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--line)' }}>
                  {['Type', 'Reference', 'Insured', 'Carrier / LOB', 'State', 'Status', 'Effective', 'Expiration'].map(h => (
                    <th key={h} style={thStyle}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {filteredRows.map(row => (
                  <tr key={`${row.recordType}-${row.id}`} style={{ borderBottom: '1px solid var(--line)', verticalAlign: 'top' }}>
                    <td style={tdStyle}><WorkTypeBadge type={row.recordType} /></td>
                    <td style={tdStyle}>
                      <Link to={row.actionUrl} style={{ color: 'var(--accent-ink)', fontWeight: 600, textDecoration: 'none' }}>
                        {row.referenceNumber || 'Open'}
                      </Link>
                    </td>
                    <td style={tdStyle}>{row.insuredName || '-'}</td>
                    <td style={tdStyle}>
                      <div>{row.carrierName}</div>
                      <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.lineOfBusiness}</div>
                    </td>
                    <td style={tdStyle}>{row.state ?? '-'}</td>
                    <td style={tdStyle}>{row.status}</td>
                    <td style={tdStyle}>{fmtDate(row.effectiveDate)}</td>
                    <td style={tdStyle}>{fmtDate(row.expirationDate)}</td>
                  </tr>
                ))}
                {filteredRows.length === 0 && (
                  <tr><td colSpan={8} style={{ ...tdStyle, color: 'var(--ink-4)', textAlign: 'center' }}>No unassigned program items</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}
    </ReportShell>
  )
}

function PostBindFollowUpReport() {
  const [ownerFilter, setOwnerFilter] = useState('')
  const [slaFilter, setSlaFilter] = useState('')
  const [dueFilter, setDueFilter] = useState('')
  const [search, setSearch] = useState('')
  const { data, isLoading, error } = useQuery<PostBindFollowUp>({
    queryKey: ['report', 'post-bind-follow-up'],
    queryFn: getPostBindFollowUp,
  })

  const rows = data?.rows ?? []
  const owners = Array.from(
    new Map(rows.filter(row => row.ownerId).map(row => [row.ownerId!, row.ownerName ?? 'Unassigned'])).entries()
  ).sort((a, b) => a[1].localeCompare(b[1]))
  const filteredRows = rows.filter(row => {
    if (ownerFilter && row.ownerId !== ownerFilter) return false
    if (slaFilter && row.slaStatus !== slaFilter) return false
    if (dueFilter === 'overdue' && row.daysUntilDue >= 0) return false
    if (dueFilter === 'next7' && (row.daysUntilDue < 0 || row.daysUntilDue > 7)) return false
    if (dueFilter === 'later' && row.daysUntilDue <= 7) return false
    if (search) {
      const query = search.toLowerCase()
      const target = `${row.policyNumber} ${row.insuredName} ${row.programName ?? ''} ${row.carrierName} ${row.lineOfBusiness} ${row.state ?? ''} ${row.ownerName ?? ''} ${row.openRequiredItems.join(' ')}`.toLowerCase()
      if (!target.includes(query)) return false
    }
    return true
  })

  const openItems = filteredRows.reduce((sum, row) => sum + row.openRequiredItemCount, 0)
  const oldestAge = filteredRows.reduce((max, row) => Math.max(max, row.daysSinceBind), 0)
  const hasFilters = ownerFilter || slaFilter || dueFilter || search

  return (
    <ReportShell title="Post-Bind Follow-Up" isLoading={isLoading} error={error as Error}>
      {data && (
        <>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 28 }}>
            <KpiCard label="Policies" value={filteredRows.length.toLocaleString()} sub={`${rows.length.toLocaleString()} total`} />
            <KpiCard label="Open Items" value={openItems.toLocaleString()} highlight={openItems > 0 ? 'warn' : undefined} />
            <KpiCard label="Oldest Bind Age" value={`${oldestAge} days`} highlight={oldestAge > 14 ? 'bad' : oldestAge > 7 ? 'warn' : undefined} />
          </div>

          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center', marginBottom: 16 }}>
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search policies, insureds, items..."
              style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-sm)', padding: '7px 10px', minWidth: 240, fontSize: 12.5, color: 'var(--ink)', background: 'var(--surface)' }}
            />
            <select value={ownerFilter} onChange={(e) => setOwnerFilter(e.target.value)} style={filterStyle}>
              <option value="">All owners</option>
              {owners.map(([id, name]) => <option key={id} value={id}>{name}</option>)}
            </select>
            <select value={slaFilter} onChange={(e) => setSlaFilter(e.target.value)} style={filterStyle}>
              <option value="">All SLA</option>
              <option value="Overdue">Overdue</option>
              <option value="DueToday">Due today</option>
              <option value="DueSoon">Due soon</option>
              <option value="OnTrack">On track</option>
            </select>
            <select value={dueFilter} onChange={(e) => setDueFilter(e.target.value)} style={filterStyle}>
              <option value="">All due dates</option>
              <option value="overdue">Overdue</option>
              <option value="next7">Next 7 days</option>
              <option value="later">Later</option>
            </select>
            {hasFilters && (
              <button
                onClick={() => { setOwnerFilter(''); setSlaFilter(''); setDueFilter(''); setSearch('') }}
                style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-sm)', padding: '7px 10px', fontSize: 12.5, color: 'var(--ink-3)', background: 'var(--surface)', cursor: 'pointer' }}
              >
                Clear filters
              </button>
            )}
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--line)' }}>
                  {['Policy', 'Insured', 'Owner', 'Program', 'Carrier / LOB', 'State', 'Bound', 'Due', 'SLA', 'Open Items'].map(h => (
                    <th key={h} style={{ ...thStyle, textAlign: h === 'Age' ? 'right' : 'left' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {filteredRows.map(row => (
                  <tr key={row.policyId} style={{ borderBottom: '1px solid var(--line)', verticalAlign: 'top' }}>
                    <td style={tdStyle}>
                      <Link to={`/policies/${row.policyId}`} style={{ color: 'var(--accent-ink)', fontWeight: 600, textDecoration: 'none' }}>
                        {row.policyNumber}
                      </Link>
                      <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.daysSinceBind}d since bind</div>
                    </td>
                    <td style={tdStyle}>{row.insuredName || '-'}</td>
                    <td style={tdStyle}>{row.ownerName ?? 'Unassigned'}</td>
                    <td style={tdStyle}>
                      <div>{row.programName ?? 'Unassigned'}</div>
                      {row.programCode && <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.programCode}</div>}
                    </td>
                    <td style={tdStyle}>
                      <div>{row.carrierName}</div>
                      <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.lineOfBusiness}</div>
                    </td>
                    <td style={tdStyle}>{row.state ?? '-'}</td>
                    <td style={tdStyle}>{fmtDate(row.boundDate)}</td>
                    <td style={tdStyle}>
                      <div>{fmtDate(row.dueDate)}</div>
                      <div style={{ fontSize: 11, color: 'var(--ink-4)', marginTop: 2 }}>{row.daysUntilDue < 0 ? `${Math.abs(row.daysUntilDue)}d late` : row.daysUntilDue === 0 ? 'Today' : `${row.daysUntilDue}d left`}</div>
                    </td>
                    <td style={tdStyle}>
                      <SlaBadge status={row.slaStatus} />
                    </td>
                    <td style={{ ...tdStyle, minWidth: 220 }}>
                      <div style={{ fontWeight: 600, marginBottom: 4 }}>{row.openRequiredItemCount} required</div>
                      {row.openRequiredItems.map(item => (
                        <div key={item} style={{ color: 'var(--ink-3)', marginBottom: 2 }}>{item}</div>
                      ))}
                    </td>
                  </tr>
                ))}
                {filteredRows.length === 0 && (
                  <tr><td colSpan={10} style={{ ...tdStyle, color: 'var(--ink-4)', textAlign: 'center' }}>No post-bind follow-up items</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}
    </ReportShell>
  )
}

const filterStyle: React.CSSProperties = {
  border: '1px solid var(--line)',
  borderRadius: 'var(--r-sm)',
  padding: '7px 9px',
  minWidth: 140,
  fontSize: 12.5,
  color: 'var(--ink)',
  background: 'var(--surface)',
}

function SlaBadge({ status }: { status: string }) {
  const label = status === 'DueToday' ? 'Due today' : status === 'DueSoon' ? 'Due soon' : status === 'OnTrack' ? 'On track' : status
  const bg = status === 'Overdue' ? 'var(--red-soft, #fef2f2)' : status === 'DueToday' || status === 'DueSoon' ? 'var(--yellow-soft, #fefce8)' : 'var(--green-soft, #f0fdf4)'
  const color = status === 'Overdue' ? 'var(--red, #b91c1c)' : status === 'DueToday' || status === 'DueSoon' ? '#8a5a00' : '#166534'
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', borderRadius: 'var(--r-sm)', padding: '3px 7px', fontSize: 11, fontWeight: 700, background: bg, color }}>
      {label}
    </span>
  )
}

function WorkTypeBadge({ type }: { type: string }) {
  const label = type === 'AuthorityApproval' ? 'Authority' : type === 'PostBind' ? 'Post-bind' : type
  const bg = type === 'AuthorityApproval' ? 'var(--yellow-soft, #fefce8)' : type === 'Referral' ? 'var(--red-soft, #fef2f2)' : 'var(--green-soft, #f0fdf4)'
  const color = type === 'AuthorityApproval' ? '#8a5a00' : type === 'Referral' ? 'var(--red, #b91c1c)' : '#166534'
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', borderRadius: 'var(--r-sm)', padding: '3px 7px', fontSize: 11, fontWeight: 700, background: bg, color }}>
      {label}
    </span>
  )
}

function StatusBadge({ status }: { status: string }) {
  const bg = status === 'Pending' ? 'var(--yellow-soft, #fefce8)' : status === 'Declined' || status === 'Cancelled' ? 'var(--red-soft, #fef2f2)' : 'var(--green-soft, #f0fdf4)'
  const color = status === 'Pending' ? '#8a5a00' : status === 'Declined' || status === 'Cancelled' ? 'var(--red, #b91c1c)' : '#166534'
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', borderRadius: 'var(--r-sm)', padding: '3px 7px', fontSize: 11, fontWeight: 700, background: bg, color }}>
      {status}
    </span>
  )
}

function OverrideBadge() {
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', borderRadius: 'var(--r-sm)', padding: '3px 7px', fontSize: 11, fontWeight: 700, background: 'var(--surface-2, #f8f9fa)', color: 'var(--ink-3)' }}>
      Override
    </span>
  )
}

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
    label: 'Operations',
    reports: [
      { id: 'manager-queue', label: 'Manager Queue' },
      { id: 'authority-approvals', label: 'Authority Approvals' },
      { id: 'decline-reasons', label: 'Decline Reasons' },
      { id: 'clearance-overrides', label: 'Clearance Overrides' },
      { id: 'post-bind-follow-up', label: 'Post-Bind Follow-Up' },
      { id: 'unassigned-program-cleanup', label: 'Unassigned Program Cleanup' },
    ],
  },
  {
    label: 'Accounting',
    reports: [
      { id: 'trust-reconciliation', label: 'Trust Reconciliation' },
      { id: 'carrier-payable-aging', label: 'Carrier Payable Aging' },
      { id: 'sl-tax-aging', label: 'SL Tax Payable Aging' },
      { id: 'broker-ar-aging', label: 'Broker AR Aging' },
      { id: 'commission-summary', label: 'Commission Summary' },
      { id: 'invoice-totals-by-program', label: 'Invoice Totals by Program' },
      { id: 'invoice-totals-by-transaction', label: 'Invoice Totals by Transaction' },
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
    case 'invoice-totals-by-program': return <InvoiceTotalsByProgramReport />
    case 'invoice-totals-by-transaction': return <InvoiceTotalsByPolicyTransactionReport />
    case 'manager-queue':          return <ManagerQueueReport />
    case 'authority-approvals':    return <AuthorityApprovalActivityReport />
    case 'decline-reasons':        return <DeclineReasonReportView />
    case 'clearance-overrides':    return <ClearanceOverrideReportView />
    case 'post-bind-follow-up':    return <PostBindFollowUpReport />
    case 'unassigned-program-cleanup': return <UnassignedProgramCleanupReport />
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
