import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, ChevronRight, ArrowLeft, Inbox } from 'lucide-react'
import { toast } from 'sonner'
import { getReceipts, getReceipt, createReceipt } from '@/api/receipts.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { CreateReceiptRequest, ReceiptDetail } from '@/types/receipt.types'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
const fmtDate = (s: string) => new Date(s + 'T00:00:00').toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
const inputCls = 'w-full border border-gray-300 rounded px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-blue-400'

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="text-xs font-medium text-gray-600">{label}</span>
      <div className="mt-1">{children}</div>
    </label>
  )
}

function StatusBadge({ status }: { status: string }) {
  const colors: Record<string, string> = {
    Open: 'bg-blue-100 text-blue-800',
    PartiallyApplied: 'bg-yellow-100 text-yellow-800',
    Applied: 'bg-green-100 text-green-800',
    Voided: 'bg-red-100 text-red-800',
  }
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${colors[status] ?? 'bg-gray-100 text-gray-700'}`}>
      {status}
    </span>
  )
}

// ---------- New Receipt Form ----------

const EMPTY_FORM: CreateReceiptRequest = {
  receivedDate: new Date().toISOString().slice(0, 10),
  amount: 0,
  payerName: '',
}

function NewReceiptPanel({ onClose, onCreated }: { onClose: () => void; onCreated: (r: ReceiptDetail) => void }) {
  const [form, setForm] = useState<CreateReceiptRequest>(EMPTY_FORM)
  const { mutate, isPending } = useMutation({
    mutationFn: () => createReceipt(form),
    onSuccess: (r) => {
      toast.success(`Receipt ${r.receiptNumber} logged`)
      onCreated(r)
    },
    onError: () => toast.error('Failed to log receipt'),
  })

  const set = (field: keyof CreateReceiptRequest, value: unknown) =>
    setForm(f => ({ ...f, [field]: value }))

  const canSubmit = form.amount > 0 && form.payerName.trim().length > 0

  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-md mx-4">
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <h2 className="text-base font-semibold text-gray-900">Log Incoming Wire / Check</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl leading-none">&times;</button>
        </div>
        <div className="px-6 py-4 space-y-4">
          <Field label="Received Date">
            <input type="date" className={inputCls} value={form.receivedDate}
              onChange={e => set('receivedDate', e.target.value)} />
          </Field>
          <Field label="Payer Name">
            <input type="text" className={inputCls} placeholder="Agency or broker name"
              value={form.payerName} onChange={e => set('payerName', e.target.value)} />
          </Field>
          <Field label="Amount">
            <input type="number" step="0.01" min="0" className={inputCls}
              value={form.amount} onChange={e => set('amount', parseFloat(e.target.value) || 0)} />
          </Field>
          <Field label="Reference (wire ref / check #)">
            <input type="text" className={inputCls} placeholder="Optional"
              value={form.reference ?? ''} onChange={e => set('reference', e.target.value || undefined)} />
          </Field>
        </div>
        <div className="px-6 py-4 border-t border-gray-200 flex justify-end gap-3">
          <button onClick={onClose} className="px-4 py-2 text-sm border border-gray-300 rounded hover:bg-gray-50">Cancel</button>
          <button
            onClick={() => mutate()}
            disabled={isPending || !canSubmit}
            className="px-4 py-2 text-sm bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50"
          >
            {isPending ? 'Logging…' : 'Log Receipt'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ---------- Receipt Detail ----------

function ReceiptDetailView({ id, onBack }: { id: number; onBack: () => void }) {
  const { data: receipt, isLoading } = useQuery({
    queryKey: ['billing', 'receipts', id],
    queryFn: () => getReceipt(id),
  })

  if (isLoading) return <LoadingSpinner />
  if (!receipt) return null

  const remaining = receipt.amount - receipt.appliedAmount

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <button onClick={onBack} className="text-gray-500 hover:text-gray-700">
          <ArrowLeft className="h-4 w-4" />
        </button>
        <h2 className="text-lg font-semibold text-gray-900">{receipt.receiptNumber}</h2>
        <StatusBadge status={receipt.status} />
      </div>

      <div className="grid grid-cols-4 gap-4">
        {[
          { label: 'Received Date', value: fmtDate(receipt.receivedDate) },
          { label: 'Payer', value: receipt.payerName },
          { label: 'Amount', value: fmt.format(receipt.amount) },
          { label: 'Remaining', value: fmt.format(remaining) },
        ].map(({ label, value }) => (
          <div key={label} className="bg-gray-50 rounded-lg p-4">
            <p className="text-xs text-gray-500">{label}</p>
            <p className="mt-1 text-sm font-semibold text-gray-900">{value}</p>
          </div>
        ))}
      </div>

      {/* Applications */}
      <div>
        <h3 className="text-sm font-semibold text-gray-700 mb-2">Cash Applications</h3>
        {receipt.applications.length === 0 ? (
          <p className="text-sm text-gray-400 italic">No applications yet — use Cash Application to match invoices.</p>
        ) : (
          <div className="border border-gray-200 rounded-lg overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 text-left">
                <tr>
                  <th className="px-4 py-2 font-medium text-gray-600">Invoice</th>
                  <th className="px-4 py-2 font-medium text-gray-600 text-right">Gross Applied</th>
                  <th className="px-4 py-2 font-medium text-gray-600 text-right">Commission</th>
                  <th className="px-4 py-2 font-medium text-gray-600 text-right">Net Applied</th>
                  <th className="px-4 py-2 font-medium text-gray-600">Applied At</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {receipt.applications.map(a => (
                  <tr key={a.id} className="hover:bg-gray-50">
                    <td className="px-4 py-2 font-mono text-blue-700">{a.invoiceNumber}</td>
                    <td className="px-4 py-2 text-right font-mono">{fmt.format(a.grossApplied)}</td>
                    <td className="px-4 py-2 text-right font-mono text-orange-700">{fmt.format(a.commissionAmount)}</td>
                    <td className="px-4 py-2 text-right font-mono font-semibold">{fmt.format(a.netApplied)}</td>
                    <td className="px-4 py-2 text-gray-500 text-xs">{new Date(a.createdAt).toLocaleString()}</td>
                  </tr>
                ))}
                <tr className="bg-gray-50 font-semibold">
                  <td className="px-4 py-2 text-right text-gray-700">Totals</td>
                  <td className="px-4 py-2 text-right font-mono">{fmt.format(receipt.applications.reduce((s, a) => s + a.grossApplied, 0))}</td>
                  <td className="px-4 py-2 text-right font-mono text-orange-700">{fmt.format(receipt.applications.reduce((s, a) => s + a.commissionAmount, 0))}</td>
                  <td className="px-4 py-2 text-right font-mono">{fmt.format(receipt.appliedAmount)}</td>
                  <td />
                </tr>
              </tbody>
            </table>
          </div>
        )}
      </div>

      <div className="text-xs text-gray-400 font-mono">GL TXN: {receipt.ledgerTransactionId}</div>
    </div>
  )
}

// ---------- Main Page ----------

export function ReceiptsPage() {
  const qc = useQueryClient()
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [showNew, setShowNew] = useState(false)

  const { data: receipts = [], isLoading } = useQuery({
    queryKey: ['billing', 'receipts'],
    queryFn: getReceipts,
  })

  const handleCreated = (r: ReceiptDetail) => {
    qc.invalidateQueries({ queryKey: ['billing', 'receipts'] })
    qc.setQueryData(['billing', 'receipts', r.id], r)
    setShowNew(false)
    setSelectedId(r.id)
  }

  if (selectedId !== null) {
    return (
      <div className="p-6">
        <ReceiptDetailView id={selectedId} onBack={() => setSelectedId(null)} />
      </div>
    )
  }

  const totalOpen = receipts.filter(r => r.status !== 'Applied' && r.status !== 'Voided')
    .reduce((s, r) => s + r.amount - r.appliedAmount, 0)

  return (
    <div className="p-6 space-y-4">
      <PageHeader
        title="Receipts"
        actions={
          <button
            onClick={() => setShowNew(true)}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white text-sm rounded-md hover:bg-blue-700"
          >
            <Plus className="h-4 w-4" />
            Log Receipt
          </button>
        }
      />

      {!isLoading && receipts.length > 0 && (
        <div className="grid grid-cols-3 gap-4">
          <div className="bg-white rounded-lg border border-gray-200 p-4">
            <p className="text-xs text-gray-500">Total Receipts</p>
            <p className="mt-1 text-2xl font-bold text-gray-900">{receipts.length}</p>
          </div>
          <div className="bg-white rounded-lg border border-gray-200 p-4">
            <p className="text-xs text-gray-500">Total Received</p>
            <p className="mt-1 text-2xl font-bold text-gray-900">
              {fmt.format(receipts.reduce((s, r) => s + r.amount, 0))}
            </p>
          </div>
          <div className="bg-white rounded-lg border border-gray-200 p-4">
            <p className="text-xs text-gray-500">Unapplied Balance</p>
            <p className={`mt-1 text-2xl font-bold ${totalOpen > 0 ? 'text-yellow-700' : 'text-green-700'}`}>
              {fmt.format(totalOpen)}
            </p>
          </div>
        </div>
      )}

      <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
        {isLoading ? (
          <div className="p-8 flex justify-center"><LoadingSpinner /></div>
        ) : receipts.length === 0 ? (
          <div className="p-12 text-center">
            <Inbox className="h-10 w-10 text-gray-300 mx-auto mb-3" />
            <p className="text-sm text-gray-500">No receipts yet. Log an incoming wire.</p>
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-left border-b border-gray-200">
              <tr>
                <th className="px-4 py-3 font-medium text-gray-600">Receipt #</th>
                <th className="px-4 py-3 font-medium text-gray-600">Received</th>
                <th className="px-4 py-3 font-medium text-gray-600">Payer</th>
                <th className="px-4 py-3 font-medium text-gray-600 text-right">Amount</th>
                <th className="px-4 py-3 font-medium text-gray-600 text-right">Applied</th>
                <th className="px-4 py-3 font-medium text-gray-600 text-right">Remaining</th>
                <th className="px-4 py-3 font-medium text-gray-600">Status</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {receipts.map(r => (
                <tr key={r.id} className="hover:bg-gray-50 cursor-pointer" onClick={() => setSelectedId(r.id)}>
                  <td className="px-4 py-3 font-mono font-medium text-blue-700">{r.receiptNumber}</td>
                  <td className="px-4 py-3 text-gray-600">{fmtDate(r.receivedDate)}</td>
                  <td className="px-4 py-3 text-gray-700">{r.payerName}</td>
                  <td className="px-4 py-3 text-right font-mono">{fmt.format(r.amount)}</td>
                  <td className="px-4 py-3 text-right font-mono text-gray-500">{fmt.format(r.appliedAmount)}</td>
                  <td className="px-4 py-3 text-right font-mono font-semibold">{fmt.format(r.amount - r.appliedAmount)}</td>
                  <td className="px-4 py-3"><StatusBadge status={r.status} /></td>
                  <td className="px-4 py-3 text-gray-400"><ChevronRight className="h-4 w-4" /></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {showNew && (
        <NewReceiptPanel onClose={() => setShowNew(false)} onCreated={handleCreated} />
      )}
    </div>
  )
}
