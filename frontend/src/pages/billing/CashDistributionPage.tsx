import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { SendHorizontal, CheckCircle2, FileText, ChevronDown, ChevronRight } from 'lucide-react'
import { toast } from 'sonner'
import {
  getPendingInstructions,
  getBatches,
  getBatch,
  createBatch,
  markExecuted,
  getBatchPdfUrl,
} from '@/api/cashDistribution.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { NettedPayee, BatchSummary } from '@/types/cashDistribution.types'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
const fmtDate = (s: string) =>
  new Date(s).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })

// ---------- Status badge ----------

function StatusBadge({ status }: { status: string }) {
  const colors: Record<string, string> = {
    Open: 'bg-yellow-100 text-yellow-800',
    PdfGenerated: 'bg-blue-100 text-blue-800',
    Executed: 'bg-green-100 text-green-800',
    Voided: 'bg-gray-100 text-gray-600',
    Pending: 'bg-orange-100 text-orange-700',
    Batched: 'bg-blue-100 text-blue-700',
  }
  const label: Record<string, string> = { PdfGenerated: 'PDF Ready' }
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${colors[status] ?? 'bg-gray-100 text-gray-700'}`}>
      {label[status] ?? status}
    </span>
  )
}

// ---------- Pending queue ----------

function PendingQueue() {
  const qc = useQueryClient()
  const { data: payees = [], isLoading } = useQuery({
    queryKey: ['cash-distribution-pending'],
    queryFn: getPendingInstructions,
  })
  const [selected, setSelected] = useState<Set<number>>(new Set())
  const [expanded, setExpanded] = useState<Set<number>>(new Set())

  const batchMutation = useMutation({
    mutationFn: createBatch,
    onSuccess: () => {
      toast.success('Batch created and PDF generated')
      setSelected(new Set())
      qc.invalidateQueries({ queryKey: ['cash-distribution-pending'] })
      qc.invalidateQueries({ queryKey: ['cash-distribution-batches'] })
    },
    onError: (e: Error) => toast.error(e.message),
  })

  const toggleAll = () => {
    if (selected.size === payees.length) setSelected(new Set())
    else setSelected(new Set(payees.map((p) => p.payeeId)))
  }

  const togglePayee = (id: number) => {
    setSelected((prev) => {
      const next = new Set(prev)
      next.has(id) ? next.delete(id) : next.add(id)
      return next
    })
  }

  const toggleExpand = (id: number) => {
    setExpanded((prev) => {
      const next = new Set(prev)
      next.has(id) ? next.delete(id) : next.add(id)
      return next
    })
  }

  const selectedPayees = payees.filter((p) => selected.has(p.payeeId))
  const selectedTotal = selectedPayees.reduce((s, p) => s + p.totalAmount, 0)

  if (isLoading) return <LoadingSpinner />

  if (payees.length === 0) {
    return (
      <div className="border border-dashed border-gray-300 rounded-lg p-12 text-center text-sm text-gray-400">
        No pending distribution instructions. Apply cash receipts to generate instructions.
      </div>
    )
  }

  return (
    <div className="space-y-4">
      {/* Toolbar */}
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-600">
          {payees.length} payee{payees.length !== 1 ? 's' : ''} ·{' '}
          {payees.reduce((s, p) => s + p.instructionCount, 0)} instructions ·{' '}
          {fmt.format(payees.reduce((s, p) => s + p.totalAmount, 0))} pending
        </p>
        <button
          disabled={selected.size === 0 || batchMutation.isPending}
          onClick={() => batchMutation.mutate({ payeeIds: [...selected] })}
          className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed text-white text-sm font-medium rounded-md transition-colors"
        >
          <SendHorizontal className="h-4 w-4" />
          {batchMutation.isPending
            ? 'Creating batch…'
            : `Execute as Batch${selected.size > 0 ? ` (${selected.size})` : ''}`}
        </button>
      </div>

      {/* Selection summary */}
      {selected.size > 0 && (
        <div className="bg-blue-50 border border-blue-200 rounded-lg px-4 py-2 text-sm text-blue-800 flex items-center justify-between">
          <span>
            {selected.size} payee{selected.size !== 1 ? 's' : ''} selected ·{' '}
            {selectedPayees.reduce((s, p) => s + p.instructionCount, 0)} instructions ·{' '}
            {fmt.format(selectedTotal)}
          </span>
          <button onClick={() => setSelected(new Set())} className="text-blue-600 hover:underline text-xs">
            Clear
          </button>
        </div>
      )}

      {/* Payee table */}
      <div className="border border-gray-200 rounded-lg overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 text-left">
            <tr>
              <th className="px-4 py-2 w-8">
                <input
                  type="checkbox"
                  checked={selected.size === payees.length && payees.length > 0}
                  onChange={toggleAll}
                  className="rounded"
                />
              </th>
              <th className="px-4 py-2 w-8" />
              <th className="px-4 py-2 font-semibold text-gray-700">Payee</th>
              <th className="px-4 py-2 font-semibold text-gray-700">Type</th>
              <th className="px-4 py-2 font-semibold text-gray-700 text-right">Instructions</th>
              <th className="px-4 py-2 font-semibold text-gray-700 text-right">Net Wire Amount</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {payees.map((payee) => (
              <>
                <tr
                  key={payee.payeeId}
                  className={`hover:bg-gray-50 transition-colors ${selected.has(payee.payeeId) ? 'bg-blue-50' : ''}`}
                >
                  <td className="px-4 py-3">
                    <input
                      type="checkbox"
                      checked={selected.has(payee.payeeId)}
                      onChange={() => togglePayee(payee.payeeId)}
                      className="rounded"
                    />
                  </td>
                  <td className="px-2 py-3">
                    <button onClick={() => toggleExpand(payee.payeeId)} className="text-gray-400 hover:text-gray-700">
                      {expanded.has(payee.payeeId)
                        ? <ChevronDown className="h-4 w-4" />
                        : <ChevronRight className="h-4 w-4" />}
                    </button>
                  </td>
                  <td className="px-4 py-3 font-medium text-gray-900">{payee.payeeName}</td>
                  <td className="px-4 py-3 text-gray-500 text-xs">{payee.payeeType}</td>
                  <td className="px-4 py-3 text-right text-gray-700">{payee.instructionCount}</td>
                  <td className="px-4 py-3 text-right font-mono font-semibold text-gray-900">
                    {fmt.format(payee.totalAmount)}
                  </td>
                </tr>
                {expanded.has(payee.payeeId) && (
                  <tr key={`${payee.payeeId}-detail`} className="bg-gray-50">
                    <td colSpan={6} className="px-8 py-2">
                      <table className="w-full text-xs text-gray-600">
                        <thead>
                          <tr className="text-gray-500 font-semibold">
                            <td className="py-1 pr-4">Receipt</td>
                            <td className="py-1 pr-4">Fee</td>
                            <td className="py-1 text-right">Amount</td>
                          </tr>
                        </thead>
                        <tbody>
                          {payee.instructions.map((inst) => (
                            <tr key={inst.id} className="border-t border-gray-100">
                              <td className="py-1 pr-4 font-mono">{inst.receiptNumber}</td>
                              <td className="py-1 pr-4">{inst.feeDisplayName}</td>
                              <td className="py-1 text-right font-mono">{fmt.format(inst.amount)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </td>
                  </tr>
                )}
              </>
            ))}
          </tbody>
          <tfoot className="bg-gray-50 border-t-2 border-gray-200">
            <tr>
              <td colSpan={4} className="px-4 py-2 text-sm font-semibold text-gray-700">Total</td>
              <td className="px-4 py-2 text-right text-sm font-semibold text-gray-700">
                {payees.reduce((s, p) => s + p.instructionCount, 0)}
              </td>
              <td className="px-4 py-2 text-right font-mono font-bold text-gray-900">
                {fmt.format(payees.reduce((s, p) => s + p.totalAmount, 0))}
              </td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  )
}

// ---------- Batch list ----------

function BatchList() {
  const qc = useQueryClient()
  const { data: batches = [], isLoading } = useQuery({
    queryKey: ['cash-distribution-batches'],
    queryFn: getBatches,
  })
  const [selectedBatch, setSelectedBatch] = useState<number | null>(null)
  const [bankRef, setBankRef] = useState('')
  const [showExecuteModal, setShowExecuteModal] = useState<number | null>(null)

  const { data: batchDetail } = useQuery({
    queryKey: ['cash-distribution-batch', selectedBatch],
    queryFn: () => getBatch(selectedBatch!),
    enabled: selectedBatch !== null,
  })

  const executeMutation = useMutation({
    mutationFn: ({ id, ref }: { id: number; ref: string }) =>
      markExecuted(id, { bankReference: ref || undefined }),
    onSuccess: () => {
      toast.success('Batch marked as executed — sweep JEs posted')
      setShowExecuteModal(null)
      setBankRef('')
      qc.invalidateQueries({ queryKey: ['cash-distribution-batches'] })
      qc.invalidateQueries({ queryKey: ['cash-distribution-batch', showExecuteModal] })
    },
    onError: (e: Error) => toast.error(e.message),
  })

  const openPdf = async (id: number) => {
    try {
      const { url } = await getBatchPdfUrl(id)
      window.open(url, '_blank')
    } catch {
      toast.error('Could not retrieve PDF download link')
    }
  }

  if (isLoading) return <LoadingSpinner />

  if (batches.length === 0) {
    return (
      <div className="border border-dashed border-gray-300 rounded-lg p-12 text-center text-sm text-gray-400">
        No batches yet. Select payees from the Pending Queue and click "Execute as Batch".
      </div>
    )
  }

  return (
    <div className="flex gap-6">
      {/* Batch table */}
      <div className="flex-1 border border-gray-200 rounded-lg overflow-hidden self-start">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 text-left">
            <tr>
              <th className="px-4 py-2 font-semibold text-gray-700">Batch #</th>
              <th className="px-4 py-2 font-semibold text-gray-700">Date</th>
              <th className="px-4 py-2 font-semibold text-gray-700 text-right">Wires</th>
              <th className="px-4 py-2 font-semibold text-gray-700 text-right">Total</th>
              <th className="px-4 py-2 font-semibold text-gray-700">Status</th>
              <th className="px-4 py-2 font-semibold text-gray-700">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {batches.map((b) => (
              <tr
                key={b.id}
                onClick={() => setSelectedBatch(b.id === selectedBatch ? null : b.id)}
                className={`cursor-pointer hover:bg-gray-50 transition-colors ${b.id === selectedBatch ? 'bg-blue-50' : ''}`}
              >
                <td className="px-4 py-3 font-mono font-medium text-gray-900">{b.batchNumber}</td>
                <td className="px-4 py-3 text-gray-600">{fmtDate(b.createdAt)}</td>
                <td className="px-4 py-3 text-right text-gray-700">{b.totalWires}</td>
                <td className="px-4 py-3 text-right font-mono font-semibold">{fmt.format(b.totalAmount)}</td>
                <td className="px-4 py-3"><StatusBadge status={b.status} /></td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2" onClick={(e) => e.stopPropagation()}>
                    {b.pdfBlobPath && (
                      <button
                        onClick={() => openPdf(b.id)}
                        title="Download wire sheet PDF"
                        className="text-blue-600 hover:text-blue-800"
                      >
                        <FileText className="h-4 w-4" />
                      </button>
                    )}
                    {(b.status === 'Open' || b.status === 'PdfGenerated') && (
                      <button
                        onClick={() => setShowExecuteModal(b.id)}
                        className="flex items-center gap-1 text-xs px-2 py-1 bg-green-600 hover:bg-green-700 text-white rounded transition-colors"
                      >
                        <CheckCircle2 className="h-3 w-3" />
                        Mark Executed
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Batch detail panel */}
      {selectedBatch !== null && batchDetail && (
        <div className="w-80 shrink-0 border border-gray-200 rounded-lg overflow-hidden self-start">
          <div className="bg-gray-50 px-4 py-3 border-b border-gray-200 flex justify-between items-center">
            <span className="font-semibold text-sm text-gray-800">{batchDetail.batchNumber}</span>
            <button onClick={() => setSelectedBatch(null)} className="text-gray-400 hover:text-gray-700 text-xs">✕</button>
          </div>
          <div className="p-4 space-y-3">
            <div className="flex justify-between text-sm">
              <span className="text-gray-500">Status</span>
              <StatusBadge status={batchDetail.status} />
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-gray-500">Total</span>
              <span className="font-mono font-semibold">{fmt.format(batchDetail.totalAmount)}</span>
            </div>
            {batchDetail.bankReference && (
              <div className="flex justify-between text-sm">
                <span className="text-gray-500">Bank Ref</span>
                <span className="font-mono text-xs">{batchDetail.bankReference}</span>
              </div>
            )}
            {batchDetail.executedAt && (
              <div className="flex justify-between text-sm">
                <span className="text-gray-500">Executed</span>
                <span>{fmtDate(batchDetail.executedAt)}</span>
              </div>
            )}
            <div className="pt-2 border-t border-gray-100">
              <p className="text-xs font-semibold text-gray-500 uppercase mb-2">Wires</p>
              {batchDetail.wires.map((w) => (
                <div key={w.payeeId} className="mb-3">
                  <div className="flex justify-between text-sm font-medium text-gray-800">
                    <span>{w.payeeName}</span>
                    <span className="font-mono">{fmt.format(w.netAmount)}</span>
                  </div>
                  {w.instructions.map((inst) => (
                    <div key={inst.id} className="flex justify-between text-xs text-gray-500 pl-2 mt-0.5">
                      <span>{inst.receiptNumber} · {inst.feeDisplayName}</span>
                      <span className="font-mono">{fmt.format(inst.amount)}</span>
                    </div>
                  ))}
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Mark Executed modal */}
      {showExecuteModal !== null && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg shadow-xl w-full max-w-md p-6">
            <h2 className="text-lg font-semibold text-gray-900 mb-1">Mark Batch Executed</h2>
            <p className="text-sm text-gray-500 mb-4">
              This will post a sweep journal entry to the ledger for each instruction in the batch,
              reducing the Trust account and clearing the payable liabilities.
            </p>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Bank Confirmation Reference <span className="text-gray-400 font-normal">(optional)</span>
            </label>
            <input
              type="text"
              value={bankRef}
              onChange={(e) => setBankRef(e.target.value)}
              placeholder="e.g. FEDREF-20260502-001"
              className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            <div className="flex justify-end gap-3 mt-6">
              <button
                onClick={() => { setShowExecuteModal(null); setBankRef('') }}
                className="px-4 py-2 text-sm text-gray-700 border border-gray-300 rounded-md hover:bg-gray-50"
              >
                Cancel
              </button>
              <button
                disabled={executeMutation.isPending}
                onClick={() => executeMutation.mutate({ id: showExecuteModal, ref: bankRef })}
                className="px-4 py-2 text-sm font-medium text-white bg-green-600 hover:bg-green-700 disabled:opacity-50 rounded-md flex items-center gap-2"
              >
                <CheckCircle2 className="h-4 w-4" />
                {executeMutation.isPending ? 'Posting JEs…' : 'Confirm Executed'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

// ---------- Page ----------

type Tab = 'pending' | 'batches'

export function CashDistributionPage() {
  const [tab, setTab] = useState<Tab>('pending')

  return (
    <div className="p-6 space-y-6">
      <PageHeader
        title="Cash Distribution"
        subtitle="Pending wire instructions netted by destination · Execute as batch · Mark bank confirmations"
      />

      {/* Tabs */}
      <div className="border-b border-gray-200">
        <nav className="flex gap-6">
          {(['pending', 'batches'] as Tab[]).map((t) => (
            <button
              key={t}
              onClick={() => setTab(t)}
              className={`pb-3 text-sm font-medium capitalize transition-colors border-b-2 -mb-px ${
                tab === t
                  ? 'border-blue-600 text-blue-600'
                  : 'border-transparent text-gray-500 hover:text-gray-700'
              }`}
            >
              {t === 'pending' ? 'Pending Queue' : 'Batch History'}
            </button>
          ))}
        </nav>
      </div>

      {tab === 'pending' ? <PendingQueue /> : <BatchList />}
    </div>
  )
}
