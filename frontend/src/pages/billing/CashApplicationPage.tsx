import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { CheckCircle, AlertCircle, Link } from 'lucide-react'
import { toast } from 'sonner'
import { getReceipts, getOpenInvoices, applyCash } from '@/api/receipts.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { OpenInvoice, ApplicationLineRequest } from '@/types/receipt.types'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
const fmtDate = (s: string) => new Date(s + 'T00:00:00').toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
const inputCls = 'w-full border border-gray-300 rounded px-2 py-1 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-blue-400 text-right'

// ---------- Row state ----------

interface GridRow {
  invoice: OpenInvoice
  grossApplied: number
  commissionRate: number  // % entered by user
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
      <div className="border border-dashed border-gray-300 rounded-lg p-8 text-center text-sm text-gray-400">
        No invoices added yet. Select open invoices from the list below.
      </div>
    )
  }

  return (
    <div className="border border-gray-200 rounded-lg overflow-hidden">
      <table className="w-full text-sm">
        <thead className="bg-gray-50 text-left">
          <tr>
            <th className="px-4 py-2 font-medium text-gray-600">Invoice #</th>
            <th className="px-4 py-2 font-medium text-gray-600">Invoice Date</th>
            <th className="px-4 py-2 font-medium text-gray-600 text-right">Open Balance</th>
            <th className="px-4 py-2 font-medium text-gray-600 text-right w-36">Gross Applied</th>
            <th className="px-4 py-2 font-medium text-gray-600 text-right w-28">Comm %</th>
            <th className="px-4 py-2 font-medium text-gray-600 text-right">Commission</th>
            <th className="px-4 py-2 font-medium text-gray-600 text-right">Net Applied</th>
            <th className="px-4 py-2 w-8" />
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {rows.map((row, idx) => {
            const commission = rowCommission(row)
            const net = rowNet(row)
            const isOver = row.grossApplied > row.invoice.openBalance + 0.005
            return (
              <tr key={row.invoice.id} className={isOver ? 'bg-red-50' : 'hover:bg-gray-50'}>
                <td className="px-4 py-2 font-mono text-blue-700">{row.invoice.invoiceNumber}</td>
                <td className="px-4 py-2 text-gray-500 text-xs">{fmtDate(row.invoice.invoiceDate)}</td>
                <td className="px-4 py-2 text-right font-mono text-gray-700">{fmt.format(row.invoice.openBalance)}</td>
                <td className="px-4 py-2">
                  <input
                    type="number" step="0.01" min="0"
                    max={row.invoice.openBalance}
                    className={`${inputCls} ${isOver ? 'border-red-400' : ''}`}
                    value={row.grossApplied}
                    onChange={e => updateRow(idx, { grossApplied: parseFloat(e.target.value) || 0 })}
                  />
                  {isOver && <p className="text-xs text-red-600 mt-0.5">Exceeds open balance</p>}
                </td>
                <td className="px-4 py-2">
                  <input
                    type="number" step="0.1" min="0" max="100"
                    className={inputCls}
                    value={row.commissionRate}
                    onChange={e => updateRow(idx, { commissionRate: parseFloat(e.target.value) || 0 })}
                  />
                </td>
                <td className="px-4 py-2 text-right font-mono text-orange-700">{fmt.format(commission)}</td>
                <td className="px-4 py-2 text-right font-mono font-semibold text-gray-900">{fmt.format(net)}</td>
                <td className="px-4 py-2 text-center">
                  <button
                    onClick={() => onChange(rows.filter((_, i) => i !== idx))}
                    className="text-gray-300 hover:text-red-500 text-lg leading-none"
                  >&times;</button>
                </td>
              </tr>
            )
          })}
          <tr className="bg-gray-50 font-semibold border-t-2 border-gray-300">
            <td colSpan={2} className="px-4 py-2 text-right text-gray-700">Totals</td>
            <td />
            <td className="px-4 py-2 text-right font-mono">{fmt.format(totals.gross)}</td>
            <td />
            <td className="px-4 py-2 text-right font-mono text-orange-700">{fmt.format(totals.commission)}</td>
            <td className="px-4 py-2 text-right font-mono">{fmt.format(totals.net)}</td>
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

  const { data: receipts = [], isLoading: receiptsLoading } = useQuery({
    queryKey: ['billing', 'receipts'],
    queryFn: getReceipts,
  })

  const { data: openInvoices = [], isLoading: invoicesLoading } = useQuery({
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
      setGridRows([])
      setSelectedReceiptId(null)
    },
    onError: (err: { response?: { data?: { errorMessage?: string } } }) => {
      toast.error(err?.response?.data?.errorMessage ?? 'Failed to apply cash')
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

  const receiptRemaining = selectedReceipt
    ? selectedReceipt.amount - selectedReceipt.appliedAmount
    : 0

  const variance = selectedReceipt ? receiptRemaining - totals.gross : 0
  const isBalanced = Math.abs(variance) < 0.005
  const hasOverApply = gridRows.some(r => r.grossApplied > r.invoice.openBalance + 0.005)
  const canPost = selectedReceiptId !== null && gridRows.length > 0 && isBalanced && !hasOverApply

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

  return (
    <div className="p-6 space-y-6">
      <PageHeader title="Cash Application" />

      {isLoading ? (
        <div className="flex justify-center py-12"><LoadingSpinner /></div>
      ) : (
        <>
          {/* Step 1: Select Receipt */}
          <div className="bg-white rounded-lg border border-gray-200 p-5">
            <h3 className="text-sm font-semibold text-gray-700 mb-3">1 — Select Receipt</h3>
            {openReceipts.length === 0 ? (
              <p className="text-sm text-gray-400">No open receipts. Log a receipt first.</p>
            ) : (
              <div className="grid grid-cols-1 gap-2">
                {openReceipts.map(r => (
                  <button
                    key={r.id}
                    onClick={() => { setSelectedReceiptId(r.id); setGridRows([]) }}
                    className={`flex items-center justify-between px-4 py-3 rounded-lg border text-left text-sm transition-colors ${
                      selectedReceiptId === r.id
                        ? 'border-blue-500 bg-blue-50'
                        : 'border-gray-200 hover:border-gray-300 hover:bg-gray-50'
                    }`}
                  >
                    <div>
                      <span className="font-mono font-medium text-blue-700 mr-3">{r.receiptNumber}</span>
                      <span className="text-gray-600">{r.payerName}</span>
                      <span className="text-gray-400 text-xs ml-2">{fmtDate(r.receivedDate)}</span>
                    </div>
                    <div className="text-right">
                      <div className="font-mono font-semibold text-gray-900">{fmt.format(r.amount - r.appliedAmount)}</div>
                      <div className="text-xs text-gray-400">remaining</div>
                    </div>
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Step 2: Build Reconciliation Grid */}
          {selectedReceipt && (
            <div className="bg-white rounded-lg border border-gray-200 p-5 space-y-4">
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-semibold text-gray-700">2 — Match Invoices</h3>
                <div className="flex items-center gap-4 text-sm">
                  <span className="text-gray-500">Receipt remaining: <span className="font-mono font-semibold text-gray-900">{fmt.format(receiptRemaining)}</span></span>
                  <span className="text-gray-500">Gross applied: <span className="font-mono font-semibold text-gray-900">{fmt.format(totals.gross)}</span></span>
                  <span className={`flex items-center gap-1.5 font-medium ${isBalanced && gridRows.length > 0 ? 'text-green-600' : 'text-amber-600'}`}>
                    {isBalanced && gridRows.length > 0
                      ? <><CheckCircle className="h-4 w-4" /> Variance: {fmt.format(0)}</>
                      : <><AlertCircle className="h-4 w-4" /> Variance: {fmt.format(Math.abs(variance))}</>
                    }
                  </span>
                </div>
              </div>

              <ReconciliationGrid rows={gridRows} onChange={setGridRows} />

              {/* Available invoices to add */}
              {availableInvoices.length > 0 && (
                <div>
                  <p className="text-xs font-medium text-gray-500 mb-2">Add invoices:</p>
                  <div className="flex flex-wrap gap-2">
                    {availableInvoices.map(inv => (
                      <button
                        key={inv.id}
                        onClick={() => addInvoice(inv)}
                        className="flex items-center gap-1.5 px-3 py-1.5 text-xs border border-gray-200 rounded-full hover:border-blue-400 hover:bg-blue-50 transition-colors"
                      >
                        <Link className="h-3 w-3 text-gray-400" />
                        <span className="font-mono text-blue-700">{inv.invoiceNumber}</span>
                        <span className="text-gray-500">{fmt.format(inv.openBalance)}</span>
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {/* Post button */}
              <div className="pt-2 flex justify-end">
                <button
                  onClick={handlePost}
                  disabled={!canPost || isPending}
                  className="px-6 py-2 text-sm bg-green-600 text-white rounded-md hover:bg-green-700 disabled:opacity-50 disabled:cursor-not-allowed font-medium"
                >
                  {isPending ? 'Posting…' : 'Post Application'}
                </button>
              </div>
              {!isBalanced && gridRows.length > 0 && (
                <p className="text-xs text-amber-700 text-right">
                  Gross applied must equal receipt remaining ({fmt.format(receiptRemaining)}) before posting.
                </p>
              )}
            </div>
          )}

          {/* Open Invoices Reference */}
          {!selectedReceipt && openInvoices.length > 0 && (
            <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
              <div className="px-4 py-3 border-b border-gray-100">
                <h3 className="text-sm font-semibold text-gray-700">Open Invoices</h3>
              </div>
              <table className="w-full text-sm">
                <thead className="bg-gray-50 text-left">
                  <tr>
                    <th className="px-4 py-2 font-medium text-gray-600">Invoice #</th>
                    <th className="px-4 py-2 font-medium text-gray-600">Date</th>
                    <th className="px-4 py-2 font-medium text-gray-600 text-right">Total</th>
                    <th className="px-4 py-2 font-medium text-gray-600 text-right">Cleared</th>
                    <th className="px-4 py-2 font-medium text-gray-600 text-right">Open Balance</th>
                    <th className="px-4 py-2 font-medium text-gray-600">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {openInvoices.map(inv => (
                    <tr key={inv.id} className="hover:bg-gray-50">
                      <td className="px-4 py-2 font-mono text-blue-700">{inv.invoiceNumber}</td>
                      <td className="px-4 py-2 text-gray-500 text-xs">{fmtDate(inv.invoiceDate)}</td>
                      <td className="px-4 py-2 text-right font-mono">{fmt.format(inv.totalAmount)}</td>
                      <td className="px-4 py-2 text-right font-mono text-gray-500">{fmt.format(inv.clearedAmount)}</td>
                      <td className="px-4 py-2 text-right font-mono font-semibold">{fmt.format(inv.openBalance)}</td>
                      <td className="px-4 py-2">
                        <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${
                          inv.status === 'PartiallyPaid' ? 'bg-yellow-100 text-yellow-800' : 'bg-blue-100 text-blue-800'
                        }`}>{inv.status}</span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </div>
  )
}
