import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, ArrowLeft, ChevronRight, Banknote } from 'lucide-react'
import { toast } from 'sonner'
import { getReceipts, getReceipt, createReceipt } from '@/api/receipts.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { StatusBadge } from '@/components/common/StatusBadge'
import { EmptyState } from '@/components/common/EmptyState'
import { ErrorState } from '@/components/common/ErrorState'
import { getApiErrorMessage } from '@/lib/apiError'
import { parseDateOnly, todayLocal } from '@/lib/utils'
import type { CreateReceiptRequest, ReceiptDetail } from '@/types/receipt.types'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
const fmtDate = (s: string) => parseDateOnly(s).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })

const RECEIPT_PILL: Record<string, string> = {
  Open: 'quoted',
  PartiallyApplied: 'inprogress',
  Applied: 'bound',
  Voided: 'voided',
}
const RECEIPT_LABEL: Record<string, string> = { PartiallyApplied: 'Partial' }

// ---------- New Receipt Modal ----------

const EMPTY_FORM: CreateReceiptRequest = {
  receivedDate: todayLocal(),
  amount: 0,
  payerName: '',
}

function NewReceiptModal({ onClose, onCreated }: { onClose: () => void; onCreated: (r: ReceiptDetail) => void }) {
  const [form, setForm] = useState<CreateReceiptRequest>(EMPTY_FORM)
  const { mutate, isPending } = useMutation({
    mutationFn: () => createReceipt(form),
    onSuccess: (r) => {
      toast.success(`Receipt ${r.receiptNumber} logged`)
      onCreated(r)
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed to log receipt')),
  })

  const set = (field: keyof CreateReceiptRequest, value: unknown) =>
    setForm(f => ({ ...f, [field]: value }))

  const canSubmit = form.amount > 0 && form.payerName.trim().length > 0

  return (
    <div className="sims-modal-backdrop">
      <div className="sims-modal">
        <div className="sims-modal-head">
          <span>Log Incoming Wire / Check</span>
          <button onClick={onClose} className="sims-modal-close">&times;</button>
        </div>
        <div className="sims-modal-body">
          <div className="sims-fields">
            <label className="sims-field">
              <span className="sims-field-label">Received Date</span>
              <input type="date" className="sims-input" value={form.receivedDate}
                onChange={e => set('receivedDate', e.target.value)} />
            </label>
            <label className="sims-field">
              <span className="sims-field-label">Payer Name</span>
              <input type="text" className="sims-input" placeholder="Agency or broker name"
                value={form.payerName} onChange={e => set('payerName', e.target.value)} />
            </label>
            <label className="sims-field">
              <span className="sims-field-label">Amount</span>
              <input type="number" step="0.01" min="0" className="sims-input"
                value={form.amount} onChange={e => set('amount', parseFloat(e.target.value) || 0)} />
            </label>
            <label className="sims-field">
              <span className="sims-field-label">Reference (wire ref / check #)</span>
              <input type="text" className="sims-input" placeholder="Optional"
                value={form.reference ?? ''} onChange={e => set('reference', e.target.value || undefined)} />
            </label>
          </div>
        </div>
        <div className="sims-modal-foot">
          <button className="sd-btn outline" onClick={onClose}>Cancel</button>
          <button className="sd-btn primary" onClick={() => mutate()} disabled={isPending || !canSubmit}>
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
    <div className="subs-wrap">
      <div className="subs-page-head">
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <button className="sd-btn outline sm" onClick={onBack}>
            <ArrowLeft style={{ width: 13, height: 13 }} />
            Back
          </button>
          <h2 style={{ margin: 0, fontSize: 17, fontWeight: 700, color: 'var(--ink)' }}>{receipt.receiptNumber}</h2>
          <StatusBadge status={RECEIPT_PILL[receipt.status] ?? 'draft'} label={RECEIPT_LABEL[receipt.status] ?? receipt.status} />
        </div>
      </div>

      <div className="sd-metrics" style={{ gridTemplateColumns: 'repeat(4, 1fr)', marginBottom: 20 }}>
        {[
          { label: 'Received Date', value: fmtDate(receipt.receivedDate) },
          { label: 'Payer', value: receipt.payerName },
          { label: 'Amount', value: fmt.format(receipt.amount) },
          { label: 'Remaining', value: fmt.format(remaining) },
        ].map(({ label, value }) => (
          <div key={label} className="sd-metric">
            <p className="k">{label}</p>
            <p className="v">{value}</p>
          </div>
        ))}
      </div>

      <div className="sd-card">
        <div className="sd-card-head">
          <h3>Cash Applications</h3>
        </div>
        {receipt.applications.length === 0 ? (
          <EmptyState
            icon={Banknote}
            title="No applications yet"
            description="Use Cash Application to match invoices to this receipt."
          />
        ) : (
          <table className="sd-table">
            <thead>
              <tr>
                <th>Invoice</th>
                <th className="num">Gross Applied</th>
                <th className="num">Commission</th>
                <th className="num">Net Applied</th>
                <th>Applied At</th>
              </tr>
            </thead>
            <tbody>
              {receipt.applications.map(a => (
                <tr key={a.id} style={{ cursor: 'default' }}>
                  <td className="id">{a.invoiceNumber}</td>
                  <td className="num">{fmt.format(a.grossApplied)}</td>
                  <td className="num" style={{ color: 'var(--warn-fg)' }}>{fmt.format(a.commissionAmount)}</td>
                  <td className="num primary-cell">{fmt.format(a.netApplied)}</td>
                  <td style={{ color: 'var(--ink-3)', fontSize: 11.5 }}>{new Date(a.createdAt).toLocaleString()}</td>
                </tr>
              ))}
              <tr style={{ background: 'var(--surface-2)', fontWeight: 600, cursor: 'default' }}>
                <td style={{ textAlign: 'right', color: 'var(--ink-2)', padding: '11px 14px' }}>Totals</td>
                <td className="num">{fmt.format(receipt.applications.reduce((s, a) => s + a.grossApplied, 0))}</td>
                <td className="num" style={{ color: 'var(--warn-fg)' }}>{fmt.format(receipt.applications.reduce((s, a) => s + a.commissionAmount, 0))}</td>
                <td className="num">{fmt.format(receipt.appliedAmount)}</td>
                <td />
              </tr>
            </tbody>
          </table>
        )}
      </div>

      <p style={{ marginTop: 12, fontSize: 11, color: 'var(--ink-4)', fontFamily: 'var(--font-mono)' }}>
        GL TXN: {receipt.ledgerTransactionId}
      </p>
    </div>
  )
}

// ---------- Skeleton ----------

function SkeletonRows() {
  return (
    <>
      {Array.from({ length: 6 }).map((_, i) => (
        <tr key={i} className="subs-row" style={{ pointerEvents: 'none' }}>
          {[80, 90, 150, 80, 80, 80, 70, 20].map((w, j) => (
            <td key={j}>
              <div style={{ height: 12, width: w, borderRadius: 4, background: 'var(--surface-2)' }} />
            </td>
          ))}
        </tr>
      ))}
    </>
  )
}

// ---------- Main Page ----------

export function ReceiptsPage() {
  const qc = useQueryClient()
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [showNew, setShowNew] = useState(false)

  const { data: receipts = [], isLoading, isError, error, refetch } = useQuery({
    queryKey: ['billing', 'receipts'],
    queryFn: getReceipts,
  })

  const handleCreated = (r: ReceiptDetail) => {
    qc.invalidateQueries({ queryKey: ['billing', 'receipts'] })
    qc.setQueryData(['billing', 'receipts', r.id], r)
    setShowNew(false)
    setSelectedId(r.id)
  }

  if (selectedId !== null) return <ReceiptDetailView id={selectedId} onBack={() => setSelectedId(null)} />

  if (isError) {
    return (
      <div className="subs-wrap">
        <div className="subs-page-head">
          <PageHeader title="Receipts" />
          <button className="sd-btn primary" onClick={() => setShowNew(true)}>
            <Plus style={{ width: 13, height: 13 }} />
            Log Receipt
          </button>
        </div>
        <ErrorState error={error} onRetry={refetch} />
        {showNew && <NewReceiptModal onClose={() => setShowNew(false)} onCreated={handleCreated} />}
      </div>
    )
  }

  const totalOpen = receipts
    .filter(r => r.status !== 'Applied' && r.status !== 'Voided')
    .reduce((s, r) => s + r.amount - r.appliedAmount, 0)

  return (
    <div className="subs-wrap">
      <div className="subs-page-head">
        <PageHeader title="Receipts" />
        <button className="sd-btn primary" onClick={() => setShowNew(true)}>
          <Plus style={{ width: 13, height: 13 }} />
          Log Receipt
        </button>
      </div>

      {!isLoading && receipts.length > 0 && (
        <div className="sd-metrics" style={{ gridTemplateColumns: 'repeat(3, 1fr)', marginBottom: 18 }}>
          <div className="sd-metric">
            <p className="k">Total Receipts</p>
            <p className="v">{receipts.length}</p>
          </div>
          <div className="sd-metric">
            <p className="k">Total Received</p>
            <p className="v">{fmt.format(receipts.reduce((s, r) => s + r.amount, 0))}</p>
          </div>
          <div className="sd-metric">
            <p className="k">Unapplied Balance</p>
            <p className="v" style={{ color: totalOpen > 0 ? 'var(--warn-fg)' : 'var(--pill-bound-fg)' }}>
              {fmt.format(totalOpen)}
            </p>
          </div>
        </div>
      )}

      <div className="subs-table-card">
        <table className="subs-table">
          <thead>
            <tr>
              <th className="subs-th">Receipt #</th>
              <th className="subs-th">Received</th>
              <th className="subs-th">Payer</th>
              <th className="subs-th num">Amount</th>
              <th className="subs-th num">Applied</th>
              <th className="subs-th num">Remaining</th>
              <th className="subs-th">Status</th>
              <th className="subs-th" style={{ width: 24 }} />
            </tr>
          </thead>
          <tbody>
            {isLoading ? (
              <SkeletonRows />
            ) : receipts.length === 0 ? (
              <tr>
                <td colSpan={8}>
                  <EmptyState icon={Banknote} title="No receipts" description="Log an incoming wire or check to get started." />
                </td>
              </tr>
            ) : (
              receipts.map(r => (
                <tr key={r.id} className="subs-row" onClick={() => setSelectedId(r.id)}>
                  <td className="subs-id">{r.receiptNumber}</td>
                  <td style={{ color: 'var(--ink-2)' }}>{fmtDate(r.receivedDate)}</td>
                  <td style={{ color: 'var(--ink)' }}>{r.payerName}</td>
                  <td style={{ textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>{fmt.format(r.amount)}</td>
                  <td style={{ textAlign: 'right', fontVariantNumeric: 'tabular-nums', color: 'var(--ink-3)' }}>{fmt.format(r.appliedAmount)}</td>
                  <td style={{ textAlign: 'right', fontVariantNumeric: 'tabular-nums', fontWeight: 600 }}>{fmt.format(r.amount - r.appliedAmount)}</td>
                  <td><StatusBadge status={RECEIPT_PILL[r.status] ?? 'draft'} label={RECEIPT_LABEL[r.status] ?? r.status} /></td>
                  <td style={{ color: 'var(--ink-4)' }}><ChevronRight style={{ width: 14, height: 14 }} /></td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {showNew && <NewReceiptModal onClose={() => setShowNew(false)} onCreated={handleCreated} />}
    </div>
  )
}
