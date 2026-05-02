import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { DollarSign, Send, X, ChevronDown, ChevronRight, CheckCircle2 } from 'lucide-react'
import { toast } from 'sonner'
import {
  getAging,
  getDisbursements,
  getDisbursement,
  createDisbursement,
  postDisbursement,
  voidDisbursement,
} from '@/api/disbursements.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { OpenPayable, AgingRow } from '@/types/disbursement.types'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
const fmtDate = (s: string) =>
  new Date(s + 'T00:00:00').toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })

function StatusBadge({ status }: { status: string }) {
  const colors: Record<string, string> = {
    Open: 'bg-yellow-100 text-yellow-800',
    PartiallyPaid: 'bg-blue-100 text-blue-700',
    Paid: 'bg-green-100 text-green-800',
    Voided: 'bg-gray-100 text-gray-500',
    Draft: 'bg-orange-100 text-orange-700',
    Posted: 'bg-green-100 text-green-800',
  }
  const label: Record<string, string> = { PartiallyPaid: 'Partial' }
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${colors[status] ?? 'bg-gray-100 text-gray-700'}`}>
      {label[status] ?? status}
    </span>
  )
}

function AgeBadge({ days }: { days: number }) {
  const cls =
    days === 0 ? 'text-green-700 bg-green-50' :
    days <= 30 ? 'text-yellow-700 bg-yellow-50' :
    days <= 60 ? 'text-orange-700 bg-orange-50' :
    'text-red-700 bg-red-50'
  return (
    <span className={`inline-flex items-center px-1.5 py-0.5 rounded text-xs font-mono ${cls}`}>
      {days === 0 ? 'Current' : `${days}d`}
    </span>
  )
}

// ---------- Create Disbursement Modal ----------

interface CreateModalProps {
  payables: OpenPayable[]
  onClose: () => void
  onCreated: () => void
}

function CreateDisbursementModal({ payables, onClose, onCreated }: CreateModalProps) {
  const qc = useQueryClient()
  const [amounts, setAmounts] = useState<Record<number, string>>(
    Object.fromEntries(payables.map((p) => [p.id, (p.amount - p.paidAmount).toFixed(2)]))
  )
  const [paymentDate, setPaymentDate] = useState(new Date().toISOString().slice(0, 10))
  const [paymentMethod, setPaymentMethod] = useState('Check')
  const [reference, setReference] = useState('')
  const [notes, setNotes] = useState('')

  const totalAmount = Object.entries(amounts).reduce((s, [, v]) => s + (parseFloat(v) || 0), 0)

  const createMutation = useMutation({
    mutationFn: createDisbursement,
    onSuccess: () => {
      toast.success('Disbursement created')
      qc.invalidateQueries({ queryKey: ['disbursements'] })
      qc.invalidateQueries({ queryKey: ['disbursements-aging'] })
      onCreated()
    },
    onError: (e: Error) => toast.error(e.message),
  })

  const handleSubmit = () => {
    const lines = payables
      .map((p) => ({ payableId: p.id, amount: parseFloat(amounts[p.id] ?? '0') || 0 }))
      .filter((l) => l.amount > 0)

    if (lines.length === 0) {
      toast.error('Enter an amount for at least one payable')
      return
    }

    createMutation.mutate({
      lines,
      paymentDate,
      paymentMethod,
      reference: reference || undefined,
      notes: notes || undefined,
    })
  }

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-2xl max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200">
          <h2 className="text-lg font-semibold text-gray-900">Create Disbursement</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-700"><X className="h-5 w-5" /></button>
        </div>

        <div className="p-6 space-y-5">
          {/* Payable lines */}
          <div>
            <p className="text-sm font-medium text-gray-700 mb-2">Payables ({payables.length})</p>
            <div className="border border-gray-200 rounded-lg overflow-hidden">
              <table className="w-full text-sm">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-3 py-2 text-left font-semibold text-gray-600">Invoice</th>
                    <th className="px-3 py-2 text-left font-semibold text-gray-600">Payee</th>
                    <th className="px-3 py-2 text-right font-semibold text-gray-600">Balance</th>
                    <th className="px-3 py-2 text-right font-semibold text-gray-600">Amount</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {payables.map((p) => (
                    <tr key={p.id}>
                      <td className="px-3 py-2 font-mono text-xs text-gray-700">{p.invoiceNumber}</td>
                      <td className="px-3 py-2 text-gray-600 text-xs">{p.payeeName}</td>
                      <td className="px-3 py-2 text-right font-mono text-gray-700">{fmt.format(p.balance)}</td>
                      <td className="px-3 py-2 text-right">
                        <input
                          type="number"
                          step="0.01"
                          min="0"
                          max={p.balance}
                          value={amounts[p.id] ?? ''}
                          onChange={(e) => setAmounts((prev) => ({ ...prev, [p.id]: e.target.value }))}
                          className="w-28 border border-gray-300 rounded px-2 py-1 text-sm font-mono text-right focus:outline-none focus:ring-1 focus:ring-blue-400"
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot className="bg-gray-50 border-t-2 border-gray-200">
                  <tr>
                    <td colSpan={3} className="px-3 py-2 text-sm font-semibold text-gray-700">Total</td>
                    <td className="px-3 py-2 text-right font-mono font-bold text-gray-900">{fmt.format(totalAmount)}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </div>

          {/* Payment details */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Payment Date</label>
              <input
                type="date"
                value={paymentDate}
                onChange={(e) => setPaymentDate(e.target.value)}
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Payment Method</label>
              <select
                value={paymentMethod}
                onChange={(e) => setPaymentMethod(e.target.value)}
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="Check">Check</option>
                <option value="Wire">Wire</option>
                <option value="ACH">ACH</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Reference <span className="text-gray-400 font-normal">(check # / wire ref)</span>
              </label>
              <input
                type="text"
                value={reference}
                onChange={(e) => setReference(e.target.value)}
                placeholder="e.g. 10042"
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Notes</label>
              <input
                type="text"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
          </div>
        </div>

        <div className="px-6 py-4 border-t border-gray-200 flex justify-end gap-3">
          <button onClick={onClose} className="px-4 py-2 text-sm text-gray-700 border border-gray-300 rounded-md hover:bg-gray-50">
            Cancel
          </button>
          <button
            disabled={createMutation.isPending || totalAmount <= 0}
            onClick={handleSubmit}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-50 rounded-md"
          >
            <DollarSign className="h-4 w-4" />
            {createMutation.isPending ? 'Creating…' : `Create Disbursement — ${fmt.format(totalAmount)}`}
          </button>
        </div>
      </div>
    </div>
  )
}

// ---------- Aging Tab ----------

function AgingTab() {
  const { data: aging, isLoading } = useQuery({
    queryKey: ['disbursements-aging'],
    queryFn: getAging,
  })

  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [expandedPayees, setExpandedPayees] = useState<Set<string>>(new Set())
  const [showModal, setShowModal] = useState(false)

  const togglePayable = (id: number) =>
    setSelected((prev) => { const n = new Set(prev); n.has(id) ? n.delete(id) : n.add(id); return n })

  const togglePayee = (name: string, ids: number[]) => {
    const allSelected = ids.every((id) => selected.has(id))
    setSelected((prev) => {
      const n = new Set(prev)
      if (allSelected) ids.forEach((id) => n.delete(id))
      else ids.forEach((id) => n.add(id))
      return n
    })
  }

  const toggleExpand = (name: string) =>
    setExpandedPayees((prev) => { const n = new Set(prev); n.has(name) ? n.delete(name) : n.add(name); return n })

  const selectedPayables = useMemo(
    () => aging?.payables.filter((p) => selected.has(p.id)) ?? [],
    [aging, selected]
  )

  if (isLoading || !aging) return <LoadingSpinner />

  const bucketCols = ['Current', '31–60d', '61–90d', '90+d', 'Total'] as const

  // Group payables by payeeName for row expansion
  const byPayee = useMemo(() => {
    const map = new Map<string, OpenPayable[]>()
    for (const p of aging.payables) {
      if (!map.has(p.payeeName)) map.set(p.payeeName, [])
      map.get(p.payeeName)!.push(p)
    }
    return map
  }, [aging.payables])

  return (
    <div className="space-y-6">
      {/* Bucket summary cards */}
      <div className="grid grid-cols-4 gap-4">
        {[
          { label: 'Current (0–30d)', value: aging.summary.current, cls: 'border-green-200 bg-green-50' },
          { label: '31–60 Days', value: aging.summary.days31to60, cls: 'border-yellow-200 bg-yellow-50' },
          { label: '61–90 Days', value: aging.summary.days61to90, cls: 'border-orange-200 bg-orange-50' },
          { label: 'Over 90 Days', value: aging.summary.over90, cls: 'border-red-200 bg-red-50' },
        ].map(({ label, value, cls }) => (
          <div key={label} className={`border rounded-lg p-4 ${cls}`}>
            <p className="text-xs font-semibold text-gray-500 uppercase">{label}</p>
            <p className="text-2xl font-bold text-gray-900 mt-1">{fmt.format(value)}</p>
          </div>
        ))}
      </div>

      {aging.payables.length === 0 ? (
        <div className="border border-dashed border-gray-300 rounded-lg p-12 text-center text-sm text-gray-400">
          No open payables. Create invoices to generate carrier payables.
        </div>
      ) : (
        <>
          {/* Toolbar */}
          <div className="flex items-center justify-between">
            <p className="text-sm text-gray-600">
              {aging.payables.length} payable{aging.payables.length !== 1 ? 's' : ''} ·{' '}
              {fmt.format(aging.summary.total)} outstanding
            </p>
            <button
              disabled={selected.size === 0}
              onClick={() => setShowModal(true)}
              className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed text-white text-sm font-medium rounded-md"
            >
              <Send className="h-4 w-4" />
              {selected.size > 0 ? `Create Disbursement (${selected.size})` : 'Select Payables'}
            </button>
          </div>

          {/* Aging table grouped by payee */}
          <div className="border border-gray-200 rounded-lg overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 text-left">
                <tr>
                  <th className="px-4 py-2 w-8" />
                  <th className="px-4 py-2 w-8" />
                  <th className="px-4 py-2 font-semibold text-gray-700">Payee / Invoice</th>
                  <th className="px-3 py-2 font-semibold text-gray-700 text-right">Current</th>
                  <th className="px-3 py-2 font-semibold text-gray-700 text-right">31–60d</th>
                  <th className="px-3 py-2 font-semibold text-gray-700 text-right">61–90d</th>
                  <th className="px-3 py-2 font-semibold text-gray-700 text-right">90+d</th>
                  <th className="px-4 py-2 font-semibold text-gray-700 text-right">Total</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {aging.rows.map((row: AgingRow) => {
                  const rowPayables = byPayee.get(row.payeeName) ?? []
                  const rowIds = rowPayables.map((p) => p.id)
                  const allRowSelected = rowIds.length > 0 && rowIds.every((id) => selected.has(id))
                  const someRowSelected = rowIds.some((id) => selected.has(id))
                  const isExpanded = expandedPayees.has(row.payeeName)

                  return (
                    <>
                      {/* Payee group row */}
                      <tr key={row.payeeName} className="bg-gray-50 font-medium hover:bg-gray-100 cursor-pointer"
                        onClick={() => toggleExpand(row.payeeName)}>
                        <td className="px-4 py-2">
                          <input
                            type="checkbox"
                            checked={allRowSelected}
                            ref={(el) => { if (el) el.indeterminate = someRowSelected && !allRowSelected }}
                            onChange={(e) => { e.stopPropagation(); togglePayee(row.payeeName, rowIds) }}
                            onClick={(e) => e.stopPropagation()}
                            className="rounded"
                          />
                        </td>
                        <td className="px-2 py-2 text-gray-400">
                          {isExpanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
                        </td>
                        <td className="px-4 py-2 text-gray-800">{row.payeeName}</td>
                        <td className="px-3 py-2 text-right font-mono text-gray-700">{row.current > 0 ? fmt.format(row.current) : '—'}</td>
                        <td className="px-3 py-2 text-right font-mono text-orange-600">{row.days31to60 > 0 ? fmt.format(row.days31to60) : '—'}</td>
                        <td className="px-3 py-2 text-right font-mono text-orange-700">{row.days61to90 > 0 ? fmt.format(row.days61to90) : '—'}</td>
                        <td className="px-3 py-2 text-right font-mono text-red-600">{row.over90 > 0 ? fmt.format(row.over90) : '—'}</td>
                        <td className="px-4 py-2 text-right font-mono font-bold text-gray-900">{fmt.format(row.total)}</td>
                      </tr>

                      {/* Expanded payable rows */}
                      {isExpanded && rowPayables.map((p) => (
                        <tr key={p.id} className={`hover:bg-gray-50 text-xs ${selected.has(p.id) ? 'bg-blue-50' : ''}`}>
                          <td className="px-4 py-2">
                            <input
                              type="checkbox"
                              checked={selected.has(p.id)}
                              onChange={() => togglePayable(p.id)}
                              className="rounded"
                            />
                          </td>
                          <td />
                          <td className="px-4 py-2 pl-8">
                            <span className="font-mono text-gray-700">{p.invoiceNumber}</span>
                            <span className="text-gray-400 ml-2">{fmtDate(p.invoiceDate)}</span>
                            <span className="ml-2"><AgeBadge days={p.daysOutstanding} /></span>
                            <span className="ml-2"><StatusBadge status={p.status} /></span>
                          </td>
                          <td colSpan={4} />
                          <td className="px-4 py-2 text-right font-mono text-gray-700">
                            {fmt.format(p.balance)}
                          </td>
                        </tr>
                      ))}
                    </>
                  )
                })}
              </tbody>
              <tfoot className="bg-gray-50 border-t-2 border-gray-200">
                <tr>
                  <td colSpan={3} className="px-4 py-2 font-semibold text-gray-700 text-sm">Total Outstanding</td>
                  <td className="px-3 py-2 text-right font-mono font-semibold text-gray-800">{fmt.format(aging.summary.current)}</td>
                  <td className="px-3 py-2 text-right font-mono font-semibold text-orange-600">{fmt.format(aging.summary.days31to60)}</td>
                  <td className="px-3 py-2 text-right font-mono font-semibold text-orange-700">{fmt.format(aging.summary.days61to90)}</td>
                  <td className="px-3 py-2 text-right font-mono font-semibold text-red-600">{fmt.format(aging.summary.over90)}</td>
                  <td className="px-4 py-2 text-right font-mono font-bold text-gray-900">{fmt.format(aging.summary.total)}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        </>
      )}

      {showModal && (
        <CreateDisbursementModal
          payables={selectedPayables}
          onClose={() => setShowModal(false)}
          onCreated={() => { setShowModal(false); setSelected(new Set()) }}
        />
      )}
    </div>
  )
}

// ---------- Disbursements Tab ----------

function DisbursementsTab() {
  const qc = useQueryClient()
  const { data: disbursements = [], isLoading } = useQuery({
    queryKey: ['disbursements'],
    queryFn: getDisbursements,
  })
  const [selectedId, setSelectedId] = useState<number | null>(null)

  const { data: detail } = useQuery({
    queryKey: ['disbursement-detail', selectedId],
    queryFn: () => getDisbursement(selectedId!),
    enabled: selectedId !== null,
  })

  const postMutation = useMutation({
    mutationFn: postDisbursement,
    onSuccess: (d) => {
      toast.success(`${d.disbursementNumber} posted — JE ${d.ledgerTransactionId?.slice(0, 8)}…`)
      qc.invalidateQueries({ queryKey: ['disbursements'] })
      qc.invalidateQueries({ queryKey: ['disbursement-detail', d.id] })
      qc.invalidateQueries({ queryKey: ['disbursements-aging'] })
    },
    onError: (e: Error) => toast.error(e.message),
  })

  if (isLoading) return <LoadingSpinner />

  if (disbursements.length === 0) {
    return (
      <div className="border border-dashed border-gray-300 rounded-lg p-12 text-center text-sm text-gray-400">
        No disbursements yet. Select payables from the Aging tab to start a check run.
      </div>
    )
  }

  return (
    <div className="flex gap-6">
      {/* Table */}
      <div className="flex-1 border border-gray-200 rounded-lg overflow-hidden self-start">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 text-left">
            <tr>
              <th className="px-4 py-2 font-semibold text-gray-700">Disbursement #</th>
              <th className="px-4 py-2 font-semibold text-gray-700">Payee</th>
              <th className="px-4 py-2 font-semibold text-gray-700">Date</th>
              <th className="px-4 py-2 font-semibold text-gray-700">Method</th>
              <th className="px-4 py-2 font-semibold text-gray-700 text-right">Amount</th>
              <th className="px-4 py-2 font-semibold text-gray-700">Status</th>
              <th className="px-4 py-2 font-semibold text-gray-700">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {disbursements.map((d) => (
              <tr
                key={d.id}
                onClick={() => setSelectedId(d.id === selectedId ? null : d.id)}
                className={`cursor-pointer hover:bg-gray-50 transition-colors ${d.id === selectedId ? 'bg-blue-50' : ''}`}
              >
                <td className="px-4 py-3 font-mono font-medium text-gray-900">{d.disbursementNumber}</td>
                <td className="px-4 py-3 text-gray-700">{d.payeeName}</td>
                <td className="px-4 py-3 text-gray-600">{fmtDate(d.paymentDate)}</td>
                <td className="px-4 py-3 text-gray-500 text-xs">{d.paymentMethod}{d.reference ? ` · ${d.reference}` : ''}</td>
                <td className="px-4 py-3 text-right font-mono font-semibold">{fmt.format(d.totalAmount)}</td>
                <td className="px-4 py-3"><StatusBadge status={d.status} /></td>
                <td className="px-4 py-3" onClick={(e) => e.stopPropagation()}>
                  {d.status === 'Draft' && (
                    <button
                      disabled={postMutation.isPending}
                      onClick={() => postMutation.mutate(d.id)}
                      className="flex items-center gap-1 text-xs px-2 py-1 bg-green-600 hover:bg-green-700 text-white rounded disabled:opacity-50"
                    >
                      <CheckCircle2 className="h-3 w-3" />
                      Post
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Detail panel */}
      {selectedId !== null && detail && (
        <div className="w-72 shrink-0 border border-gray-200 rounded-lg overflow-hidden self-start">
          <div className="bg-gray-50 px-4 py-3 border-b border-gray-200 flex justify-between items-center">
            <span className="font-semibold text-sm">{detail.disbursementNumber}</span>
            <button onClick={() => setSelectedId(null)} className="text-gray-400 hover:text-gray-700 text-xs">✕</button>
          </div>
          <div className="p-4 space-y-3">
            <div className="flex justify-between text-sm">
              <span className="text-gray-500">Payee</span>
              <span className="font-medium text-gray-800 text-right max-w-[150px] truncate" title={detail.payeeName}>{detail.payeeName}</span>
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-gray-500">Status</span>
              <StatusBadge status={detail.status} />
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-gray-500">Method</span>
              <span>{detail.paymentMethod}</span>
            </div>
            {detail.reference && (
              <div className="flex justify-between text-sm">
                <span className="text-gray-500">Ref</span>
                <span className="font-mono text-xs">{detail.reference}</span>
              </div>
            )}
            {detail.ledgerTransactionId && (
              <div className="flex justify-between text-sm">
                <span className="text-gray-500">JE</span>
                <span className="font-mono text-xs text-gray-500">{detail.ledgerTransactionId.slice(0, 8)}…</span>
              </div>
            )}
            <div className="pt-2 border-t border-gray-100">
              <p className="text-xs font-semibold text-gray-500 uppercase mb-2">Lines</p>
              {detail.lines.map((l) => (
                <div key={l.id} className="flex justify-between text-xs mb-1.5">
                  <div>
                    <span className="font-mono text-gray-700">{l.invoiceNumber}</span>
                  </div>
                  <span className="font-mono text-gray-800">{fmt.format(l.amount)}</span>
                </div>
              ))}
              <div className="flex justify-between text-sm font-semibold border-t border-gray-100 pt-1.5 mt-1">
                <span>Total</span>
                <span className="font-mono">{fmt.format(detail.totalAmount)}</span>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

// ---------- Page ----------

type Tab = 'aging' | 'disbursements'

export function DisbursementsPage() {
  const [tab, setTab] = useState<Tab>('aging')

  return (
    <div className="p-6 space-y-6">
      <PageHeader
        title="Carrier Disbursements"
        subtitle="Payable aging by carrier · check run selection · ledger posting"
      />

      <div className="border-b border-gray-200">
        <nav className="flex gap-6">
          {([['aging', 'Payable Aging'], ['disbursements', 'Check Run / History']] as [Tab, string][]).map(([t, label]) => (
            <button
              key={t}
              onClick={() => setTab(t)}
              className={`pb-3 text-sm font-medium transition-colors border-b-2 -mb-px ${
                tab === t
                  ? 'border-blue-600 text-blue-600'
                  : 'border-transparent text-gray-500 hover:text-gray-700'
              }`}
            >
              {label}
            </button>
          ))}
        </nav>
      </div>

      {tab === 'aging' ? <AgingTab /> : <DisbursementsTab />}
    </div>
  )
}
