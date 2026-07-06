import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { CheckCircle, AlertCircle, Link } from 'lucide-react'
import { toast } from 'sonner'
import { getReceipts, getOpenInvoices, applyCash } from '@/api/receipts.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ErrorState } from '@/components/common/ErrorState'
import { getApiErrorMessage } from '@/lib/apiError'
import { parseDateOnly } from '@/lib/utils'
import type { OpenInvoice, ApplicationLineRequest } from '@/types/receipt.types'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
const fmtDate = (s: string) => parseDateOnly(s).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })

// ---------- Row state ----------

interface GridRow {
  invoice: OpenInvoice
  grossApplied: number
  commissionRate: number
}

function rowCommission(row: GridRow): number {
  return Math.round(row.grossApplied * (row.commissionRate / 100) * 10000) / 10000
}

function rowNet(row: GridRow): number {
  return Math.round((row.grossApplied - rowCommission(row)) * 10000) / 10000
}

// ---------- Reconciliation Grid ----------

interface ReconciliationGridProps {
  rows: GridRow[]
  onChange: (rows: GridRow[]) => void
}

function ReconciliationGrid({ rows, onChange }: ReconciliationGridProps) {
  const updateRow = (idx: number, patch: Partial<GridRow>) => {
    const next = rows.map((r, i) => i === idx ? { ...r, ...patch } : r)
    onChange(next)
  }

  const totals = useMemo(() => ({
    gross: rows.reduce((s, r) => s + r.grossApplied, 0),
    commission: rows.reduce((s, r) => s + rowCommission(r), 0),
    net: rows.reduce((s, r) => s + rowNet(r), 0),
  }), [rows])

  if (rows.length === 0) {
    return (
      <div style={{
        border: '1px dashed var(--line)',
        borderRadius: 'var(--r-lg)',
        padding: '28px 16px',
        textAlign: 'center',
        fontSize: 13,
        color: 'var(--ink-3)',
      }}>
        No invoices added yet. Select open invoices from the list below.
      </div>
    )
  }

  const inputStyle: React.CSSProperties = {
    width: '100%',
    border: '1px solid var(--line)',
    borderRadius: 'var(--r)',
    padding: '4px 8px',
    fontSize: 12.5,
    fontFamily: 'var(--font-mono)',
    textAlign: 'right',
    background: 'var(--surface)',
    color: 'var(--ink)',
    outline: 'none',
  }

  return (
    <div className="sd-card" style={{ overflow: 'hidden' }}>
      <table className="sd-table">
        <thead>
          <tr>
            <th>Invoice #</th>
            <th>Date</th>
            <th className="num">Open Balance</th>
            <th className="num" style={{ width: 140 }}>Gross Applied</th>
            <th className="num" style={{ width: 110 }}>Comm %</th>
            <th className="num">Commission</th>
            <th className="num">Net Applied</th>
            <th style={{ width: 32 }} />
          </tr>
        </thead>
        <tbody>
          {rows.map((row, idx) => {
            const commission = rowCommission(row)
            const net = rowNet(row)
            const isOver = row.grossApplied > row.invoice.openBalance + 0.005
            return (
              <tr key={row.invoice.id} style={{ background: isOver ? 'var(--bad-bg)' : undefined, cursor: 'default' }}>
                <td className="id">{row.invoice.invoiceNumber}</td>
                <td style={{ color: 'var(--ink-3)', fontSize: 11.5 }}>{fmtDate(row.invoice.invoiceDate)}</td>
                <td className="num">{fmt.format(row.invoice.openBalance)}</td>
                <td className="num">
                  <input
                    type="number" step="0.01" min="0" max={row.invoice.openBalance}
                    style={{ ...inputStyle, borderColor: isOver ? 'var(--bad-fg)' : undefined }}
                    value={row.grossApplied}
                    onChange={e => updateRow(idx, { grossApplied: parseFloat(e.target.value) || 0 })}
                  />
                  {isOver && <p style={{ fontSize: 11, color: 'var(--bad-fg)', marginTop: 2 }}>Exceeds open balance</p>}
                </td>
                <td className="num">
                  <input
                    type="number" step="0.1" min="0" max="100"
                    style={inputStyle}
                    value={row.commissionRate}
                    onChange={e => updateRow(idx, { commissionRate: parseFloat(e.target.value) || 0 })}
                  />
                </td>
                <td className="num" style={{ color: 'var(--warn-fg)' }}>{fmt.format(commission)}</td>
                <td className="num primary-cell">{fmt.format(net)}</td>
                <td style={{ textAlign: 'center' }}>
                  <button
                    onClick={() => onChange(rows.filter((_, i) => i !== idx))}
                    style={{ color: 'var(--ink-4)', fontSize: 16, lineHeight: 1, background: 'none', border: 0, cursor: 'pointer', padding: '0 4px' }}
                  >&times;</button>
                </td>
              </tr>
            )
          })}
          <tr style={{ background: 'var(--surface-2)', fontWeight: 600, cursor: 'default', borderTop: '2px solid var(--line)' }}>
            <td colSpan={2} style={{ textAlign: 'right', color: 'var(--ink-2)', padding: '11px 14px' }}>Totals</td>
            <td />
            <td className="num">{fmt.format(totals.gross)}</td>
            <td />
            <td className="num" style={{ color: 'var(--warn-fg)' }}>{fmt.format(totals.commission)}</td>
            <td className="num">{fmt.format(totals.net)}</td>
            <td />
          </tr>
        </tbody>
      </table>
    </div>
  )
}

// ---------- Main Page ----------

export function CashApplicationPage() {
  const qc = useQueryClient()
  const [selectedReceiptId, setSelectedReceiptId] = useState<number | null>(null)
  const [gridRows, setGridRows] = useState<GridRow[]>([])

  const { data: receipts = [], isLoading: receiptsLoading, isError: receiptsError, error: receiptsErr, refetch: refetchReceipts } = useQuery({
    queryKey: ['billing', 'receipts'],
    queryFn: getReceipts,
  })

  const { data: openInvoices = [], isLoading: invoicesLoading, isError: invoicesError, error: invoicesErr, refetch: refetchInvoices } = useQuery({
    queryKey: ['billing', 'cash-application', 'open-invoices'],
    queryFn: getOpenInvoices,
  })

  const { mutate: applyMutation, isPending } = useMutation({
    mutationFn: (req: Parameters<typeof applyCash>[0]) => applyCash(req),
    onSuccess: (result) => {
      toast.success(`Applied to ${result.applications.length} invoice(s). Receipt status: ${result.receiptStatus}`)
      qc.invalidateQueries({ queryKey: ['billing', 'receipts'] })
      qc.invalidateQueries({ queryKey: ['billing', 'cash-application'] })
      qc.invalidateQueries({ queryKey: ['billing', 'invoices'] })
      // Applying cash generates distribution instructions and reduces payable balances —
      // refresh the distribution queue and disbursements aging so they aren't stale (audit).
      qc.invalidateQueries({ queryKey: ['cash-distribution-pending'] })
      qc.invalidateQueries({ queryKey: ['disbursements'] })
      qc.invalidateQueries({ queryKey: ['disbursements-aging'] })
      qc.invalidateQueries({ queryKey: ['trust-balance'] })
      setGridRows([])
      setSelectedReceiptId(null)
    },
    onError: (err) => {
      toast.error(getApiErrorMessage(err, 'Failed to apply cash'))
    },
  })

  const selectedReceipt = receipts.find(r => r.id === selectedReceiptId)
  const openReceipts = receipts.filter(r => r.status === 'Open' || r.status === 'PartiallyApplied')

  const alreadyAdded = new Set(gridRows.map(r => r.invoice.id))
  const availableInvoices = openInvoices.filter(inv => !alreadyAdded.has(inv.id))

  const addInvoice = (inv: OpenInvoice) => {
    setGridRows(rows => [...rows, { invoice: inv, grossApplied: inv.openBalance, commissionRate: 0 }])
  }

  const totals = useMemo(() => ({
    gross: gridRows.reduce((s, r) => s + r.grossApplied, 0),
    net: gridRows.reduce((s, r) => s + rowNet(r), 0),
  }), [gridRows])

  const receiptRemaining = selectedReceipt ? selectedReceipt.amount - selectedReceipt.appliedAmount : 0
  const variance = selectedReceipt ? receiptRemaining - totals.gross : 0
  const hasOverApply = gridRows.some(r => r.grossApplied > r.invoice.openBalance + 0.005)
  // Applying MORE than the receipt holds is invalid; applying LESS is a partial
  // application — allowed, with the remainder left on the receipt (audit B1).
  const isOverReceipt = variance < -0.005
  const isUnderApplied = variance > 0.005
  const isBalanced = Math.abs(variance) < 0.005
  const canPost = selectedReceiptId !== null && gridRows.length > 0 && !hasOverApply && !isOverReceipt

  const handlePost = () => {
    if (!selectedReceiptId) return
    const lines: ApplicationLineRequest[] = gridRows.map(r => ({
      invoiceId: r.invoice.id,
      grossApplied: r.grossApplied,
      commissionAmount: rowCommission(r),
    }))
    applyMutation({ receiptId: selectedReceiptId, lines })
  }

  const isLoading = receiptsLoading || invoicesLoading
  const isError = receiptsError || invoicesError
  const error = receiptsErr ?? invoicesErr
  const refetch = () => { refetchReceipts(); refetchInvoices() }

  return (
    <div className="subs-wrap">
      <div className="subs-page-head" style={{ marginBottom: 20 }}>
        <PageHeader title="Cash Application" />
      </div>

      {isLoading ? (
        <LoadingSpinner />
      ) : isError ? (
        <ErrorState error={error} onRetry={refetch} />
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {/* Step 1: Select Receipt */}
          <div className="sd-card">
            <div className="sd-card-head">
              <h3>1 — Select Receipt</h3>
            </div>
            <div className="sd-card-body">
              {openReceipts.length === 0 ? (
                <p style={{ fontSize: 13, color: 'var(--ink-3)' }}>No open receipts. Log a receipt first.</p>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                  {openReceipts.map(r => {
                    const isSelected = selectedReceiptId === r.id
                    return (
                      <button
                        key={r.id}
                        onClick={() => { setSelectedReceiptId(r.id); setGridRows([]) }}
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          padding: '10px 14px',
                          borderRadius: 'var(--r-lg)',
                          border: `1px solid ${isSelected ? 'var(--accent)' : 'var(--line)'}`,
                          background: isSelected ? 'var(--accent-soft)' : 'var(--surface)',
                          textAlign: 'left',
                          fontSize: 13,
                          cursor: 'pointer',
                          transition: 'all .12s',
                        }}
                      >
                        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                          <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 600, color: isSelected ? 'var(--accent-ink)' : 'var(--ink)' }}>{r.receiptNumber}</span>
                          <span style={{ color: 'var(--ink-2)' }}>{r.payerName}</span>
                          <span style={{ color: 'var(--ink-4)', fontSize: 11.5 }}>{fmtDate(r.receivedDate)}</span>
                        </div>
                        <div style={{ textAlign: 'right' }}>
                          <div style={{ fontFamily: 'var(--font-mono)', fontWeight: 700, color: isSelected ? 'var(--accent-ink)' : 'var(--ink)' }}>{fmt.format(r.amount - r.appliedAmount)}</div>
                          <div style={{ fontSize: 11, color: 'var(--ink-4)' }}>remaining</div>
                        </div>
                      </button>
                    )
                  })}
                </div>
              )}
            </div>
          </div>

          {/* Step 2: Match Invoices */}
          {selectedReceipt && (
            <div className="sd-card">
              <div className="sd-card-head" style={{ justifyContent: 'space-between' }}>
                <h3>2 — Match Invoices</h3>
                <div style={{ display: 'flex', alignItems: 'center', gap: 16, fontSize: 13 }}>
                  <span style={{ color: 'var(--ink-3)' }}>
                    Receipt remaining: <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--ink)' }}>{fmt.format(receiptRemaining)}</span>
                  </span>
                  <span style={{ color: 'var(--ink-3)' }}>
                    Gross applied: <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--ink)' }}>{fmt.format(totals.gross)}</span>
                  </span>
                  <span style={{
                    display: 'flex', alignItems: 'center', gap: 6, fontWeight: 600,
                    color: isBalanced && gridRows.length > 0 ? 'var(--pill-bound-fg)' : 'var(--warn-fg)',
                  }}>
                    {isBalanced && gridRows.length > 0
                      ? <><CheckCircle style={{ width: 14, height: 14 }} /> Balanced</>
                      : <><AlertCircle style={{ width: 14, height: 14 }} /> Variance: {fmt.format(Math.abs(variance))}</>
                    }
                  </span>
                </div>
              </div>
              <div className="sd-card-body" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                <ReconciliationGrid rows={gridRows} onChange={setGridRows} />

                {availableInvoices.length > 0 && (
                  <div>
                    <p style={{ fontSize: 11.5, fontWeight: 600, color: 'var(--ink-3)', marginBottom: 6, textTransform: 'uppercase', letterSpacing: '.04em' }}>Add invoices:</p>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                      {availableInvoices.map(inv => (
                        <button
                          key={inv.id}
                          onClick={() => addInvoice(inv)}
                          style={{
                            display: 'flex', alignItems: 'center', gap: 6,
                            padding: '5px 10px', fontSize: 12,
                            border: '1px solid var(--line)',
                            borderRadius: 999,
                            background: 'var(--surface)',
                            cursor: 'pointer',
                            transition: 'border-color .1s, background .1s',
                          }}
                        >
                          <Link style={{ width: 11, height: 11, color: 'var(--ink-3)' }} />
                          <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--accent-ink)', fontWeight: 600 }}>{inv.invoiceNumber}</span>
                          <span style={{ color: 'var(--ink-3)' }}>{fmt.format(inv.openBalance)}</span>
                        </button>
                      ))}
                    </div>
                  </div>
                )}

                <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 6 }}>
                  <button className="sd-btn primary" onClick={handlePost} disabled={!canPost || isPending}>
                    {isPending ? 'Posting…' : 'Post Application'}
                  </button>
                  {isOverReceipt && gridRows.length > 0 && (
                    <p style={{ fontSize: 12, color: 'var(--bad-fg)' }}>
                      Applied ({fmt.format(totals.gross)}) exceeds receipt remaining ({fmt.format(receiptRemaining)}). Reduce before posting.
                    </p>
                  )}
                  {isUnderApplied && gridRows.length > 0 && !isOverReceipt && (
                    <p style={{ fontSize: 12, color: 'var(--warn-fg)' }}>
                      Partial application — {fmt.format(variance)} will remain on the receipt as unapplied.
                    </p>
                  )}
                </div>
              </div>
            </div>
          )}

          {/* Open Invoices Reference (when no receipt selected) */}
          {!selectedReceipt && openInvoices.length > 0 && (
            <div className="sd-card" style={{ overflow: 'hidden' }}>
              <div className="sd-card-head">
                <h3>Open Invoices</h3>
              </div>
              <table className="sd-table">
                <thead>
                  <tr>
                    <th>Invoice #</th>
                    <th>Date</th>
                    <th className="num">Total</th>
                    <th className="num">Cleared</th>
                    <th className="num">Open Balance</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {openInvoices.map(inv => (
                    <tr key={inv.id} style={{ cursor: 'default' }}>
                      <td className="id">{inv.invoiceNumber}</td>
                      <td style={{ color: 'var(--ink-3)', fontSize: 11.5 }}>{fmtDate(inv.invoiceDate)}</td>
                      <td className="num">{fmt.format(inv.totalAmount)}</td>
                      <td className="num" style={{ color: 'var(--ink-3)' }}>{fmt.format(inv.clearedAmount)}</td>
                      <td className="num primary-cell">{fmt.format(inv.openBalance)}</td>
                      <td>
                        <span className={`sd-pill ${inv.status === 'PartiallyPaid' ? 'inprogress' : 'quoted'}`}>
                          {inv.status === 'PartiallyPaid' ? 'Partial' : inv.status}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
