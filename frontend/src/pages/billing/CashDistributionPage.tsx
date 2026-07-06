import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { SendHorizontal, CheckCircle2, FileText, ChevronDown, ChevronRight, Landmark } from 'lucide-react'
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
import { StatusBadge } from '@/components/common/StatusBadge'
import { EmptyState } from '@/components/common/EmptyState'
import { ErrorState } from '@/components/common/ErrorState'
import { getApiErrorMessage } from '@/lib/apiError'
import { parseDateOnly } from '@/lib/utils'
import { CashBalanceBadge } from '@/components/accounting/CashBalanceBadge'
import type { NettedPayee, BatchSummary } from '@/types/cashDistribution.types'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
const fmtDate = (s: string) =>
  parseDateOnly(s).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })

const BATCH_PILL: Record<string, string> = {
  Open: 'submitted',
  PdfGenerated: 'inprogress',
  Executed: 'bound',
  Voided: 'voided',
  Pending: 'inprogress',
  Batched: 'submitted',
}
const BATCH_LABEL: Record<string, string> = { PdfGenerated: 'PDF Ready' }

// ---------- Pending Queue ----------

function PendingQueue() {
  const qc = useQueryClient()
  const { data: payees = [], isLoading, isError, error, refetch } = useQuery({
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
    onError: (e) => toast.error(getApiErrorMessage(e)),
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

  if (isError) return <ErrorState error={error} onRetry={refetch} />
  if (isLoading) return <LoadingSpinner />

  if (payees.length === 0) {
    return (
      <EmptyState
        icon={Landmark}
        title="No pending instructions"
        description="Apply cash receipts to invoices to generate distribution instructions."
      />
    )
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      {/* Toolbar */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <p style={{ fontSize: 13, color: 'var(--ink-2)' }}>
          {payees.length} payee{payees.length !== 1 ? 's' : ''} ·{' '}
          {payees.reduce((s, p) => s + p.instructionCount, 0)} instructions ·{' '}
          {fmt.format(payees.reduce((s, p) => s + p.totalAmount, 0))} pending
        </p>
        <button
          className="sd-btn primary"
          disabled={selected.size === 0 || batchMutation.isPending}
          onClick={() => batchMutation.mutate({ payeeIds: [...selected] })}
        >
          <SendHorizontal style={{ width: 13, height: 13 }} />
          {batchMutation.isPending
            ? 'Creating batch…'
            : `Execute as Batch${selected.size > 0 ? ` (${selected.size})` : ''}`}
        </button>
      </div>

      {/* Selection summary */}
      {selected.size > 0 && (
        <div style={{
          background: 'var(--accent-soft)',
          border: '1px solid var(--line)',
          borderRadius: 'var(--r-lg)',
          padding: '8px 14px',
          fontSize: 13,
          color: 'var(--accent-ink)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
        }}>
          <span>
            {selected.size} payee{selected.size !== 1 ? 's' : ''} selected ·{' '}
            {selectedPayees.reduce((s, p) => s + p.instructionCount, 0)} instructions ·{' '}
            {fmt.format(selectedTotal)}
          </span>
          <button
            onClick={() => setSelected(new Set())}
            style={{ fontSize: 12, color: 'var(--accent-ink)', background: 'none', border: 0, cursor: 'pointer', textDecoration: 'underline' }}
          >
            Clear
          </button>
        </div>
      )}

      {/* Payee table */}
      <div className="subs-table-card">
        <table className="subs-table">
          <thead>
            <tr>
              <th className="subs-th" style={{ width: 40 }}>
                <input
                  type="checkbox"
                  checked={selected.size === payees.length && payees.length > 0}
                  onChange={toggleAll}
                />
              </th>
              <th className="subs-th" style={{ width: 32 }} />
              <th className="subs-th">Payee</th>
              <th className="subs-th">Type</th>
              <th className="subs-th num">Instructions</th>
              <th className="subs-th num">Net Wire Amount</th>
            </tr>
          </thead>
          <tbody>
            {payees.map((payee) => (
              <>
                <tr
                  key={payee.payeeId}
                  className="subs-row"
                  style={{ background: selected.has(payee.payeeId) ? 'var(--accent-soft)' : undefined }}
                >
                  <td>
                    <input
                      type="checkbox"
                      checked={selected.has(payee.payeeId)}
                      onChange={() => togglePayee(payee.payeeId)}
                    />
                  </td>
                  <td>
                    <button
                      onClick={() => toggleExpand(payee.payeeId)}
                      style={{ color: 'var(--ink-3)', background: 'none', border: 0, cursor: 'pointer', display: 'grid', placeItems: 'center' }}
                    >
                      {expanded.has(payee.payeeId)
                        ? <ChevronDown style={{ width: 14, height: 14 }} />
                        : <ChevronRight style={{ width: 14, height: 14 }} />}
                    </button>
                  </td>
                  <td style={{ fontWeight: 600, color: 'var(--ink)' }}>{payee.payeeName}</td>
                  <td style={{ color: 'var(--ink-3)', fontSize: 12 }}>{payee.payeeType}</td>
                  <td style={{ textAlign: 'right', color: 'var(--ink-2)' }}>{payee.instructionCount}</td>
                  <td style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--ink)' }}>
                    {fmt.format(payee.totalAmount)}
                  </td>
                </tr>
                {expanded.has(payee.payeeId) && (
                  <tr key={`${payee.payeeId}-detail`} style={{ background: 'var(--surface-2)', cursor: 'default' }}>
                    <td colSpan={6} style={{ padding: '8px 28px' }}>
                      <table style={{ width: '100%', fontSize: 12, color: 'var(--ink-3)' }}>
                        <thead>
                          <tr>
                            <td style={{ paddingRight: 16, paddingBottom: 4, fontWeight: 600, color: 'var(--ink-4)', textTransform: 'uppercase', fontSize: 10.5, letterSpacing: '.04em' }}>Receipt</td>
                            <td style={{ paddingRight: 16, paddingBottom: 4, fontWeight: 600, color: 'var(--ink-4)', textTransform: 'uppercase', fontSize: 10.5, letterSpacing: '.04em' }}>Fee</td>
                            <td style={{ paddingBottom: 4, textAlign: 'right', fontWeight: 600, color: 'var(--ink-4)', textTransform: 'uppercase', fontSize: 10.5, letterSpacing: '.04em' }}>Amount</td>
                          </tr>
                        </thead>
                        <tbody>
                          {payee.instructions.map((inst) => (
                            <tr key={inst.id} style={{ borderTop: '1px solid var(--line-2)' }}>
                              <td style={{ padding: '4px 16px 4px 0', fontFamily: 'var(--font-mono)' }}>{inst.receiptNumber}</td>
                              <td style={{ padding: '4px 16px 4px 0' }}>{inst.feeDisplayName}</td>
                              <td style={{ padding: '4px 0', textAlign: 'right', fontFamily: 'var(--font-mono)' }}>{fmt.format(inst.amount)}</td>
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
          <tfoot style={{ borderTop: '2px solid var(--line)', background: 'var(--surface-2)' }}>
            <tr>
              <td colSpan={4} style={{ padding: '10px 14px', fontSize: 13, fontWeight: 700, color: 'var(--ink-2)' }}>Total</td>
              <td style={{ padding: '10px 14px', textAlign: 'right', fontSize: 13, fontWeight: 700, color: 'var(--ink-2)' }}>
                {payees.reduce((s, p) => s + p.instructionCount, 0)}
              </td>
              <td style={{ padding: '10px 14px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--ink)' }}>
                {fmt.format(payees.reduce((s, p) => s + p.totalAmount, 0))}
              </td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  )
}

// ---------- Batch List ----------

function BatchList() {
  const qc = useQueryClient()
  const { data: batches = [], isLoading, isError, error, refetch } = useQuery({
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
      // Executing a batch posts sweep JEs from trust — refresh activity + trust (audit).
      qc.invalidateQueries({ queryKey: ['activity'] })
      qc.invalidateQueries({ queryKey: ['trust-balance'] })
    },
    onError: (e) => toast.error(getApiErrorMessage(e)),
  })

  const openPdf = async (id: number) => {
    try {
      const { url } = await getBatchPdfUrl(id)
      window.open(url, '_blank')
    } catch {
      toast.error('Could not retrieve PDF download link')
    }
  }

  if (isError) return <ErrorState error={error} onRetry={refetch} />
  if (isLoading) return <LoadingSpinner />

  if (batches.length === 0) {
    return (
      <EmptyState
        icon={Landmark}
        title="No batches yet"
        description='Select payees from the Pending Queue and click "Execute as Batch".'
      />
    )
  }

  return (
    <div style={{ display: 'flex', gap: 20 }}>
      {/* Batch table */}
      <div style={{ flex: 1 }}>
        <div className="subs-table-card">
          <table className="subs-table">
            <thead>
              <tr>
                <th className="subs-th">Batch #</th>
                <th className="subs-th">Date</th>
                <th className="subs-th num">Wires</th>
                <th className="subs-th num">Total</th>
                <th className="subs-th">Status</th>
                <th className="subs-th">Actions</th>
              </tr>
            </thead>
            <tbody>
              {batches.map((b) => (
                <tr
                  key={b.id}
                  className="subs-row"
                  style={{ background: b.id === selectedBatch ? 'var(--accent-soft)' : undefined }}
                  onClick={() => setSelectedBatch(b.id === selectedBatch ? null : b.id)}
                >
                  <td className="subs-id">{b.batchNumber}</td>
                  <td style={{ color: 'var(--ink-2)' }}>{fmtDate(b.createdAt)}</td>
                  <td style={{ textAlign: 'right', color: 'var(--ink-2)' }}>{b.totalWires}</td>
                  <td style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600 }}>{fmt.format(b.totalAmount)}</td>
                  <td><StatusBadge status={BATCH_PILL[b.status] ?? 'draft'} label={BATCH_LABEL[b.status] ?? b.status} /></td>
                  <td onClick={(e) => e.stopPropagation()}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      {b.pdfBlobPath && (
                        <button
                          onClick={() => openPdf(b.id)}
                          title="Download wire sheet PDF"
                          style={{ color: 'var(--accent-ink)', background: 'none', border: 0, cursor: 'pointer', display: 'grid', placeItems: 'center' }}
                        >
                          <FileText style={{ width: 14, height: 14 }} />
                        </button>
                      )}
                      {(b.status === 'Open' || b.status === 'PdfGenerated') && (
                        <button
                          className="sd-btn sm"
                          onClick={() => setShowExecuteModal(b.id)}
                          style={{ color: 'var(--pill-bound-fg)' }}
                        >
                          <CheckCircle2 style={{ width: 12, height: 12 }} />
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
      </div>

      {/* Batch detail panel */}
      {selectedBatch !== null && batchDetail && (
        <div className="sd-card" style={{ width: 280, flexShrink: 0, alignSelf: 'flex-start', overflow: 'hidden' }}>
          <div className="sd-card-head" style={{ justifyContent: 'space-between' }}>
            <h3>{batchDetail.batchNumber}</h3>
            <button
              onClick={() => setSelectedBatch(null)}
              style={{ fontSize: 13, color: 'var(--ink-3)', background: 'none', border: 0, cursor: 'pointer' }}
            >✕</button>
          </div>
          <div className="sd-card-body" style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13 }}>
              <span style={{ color: 'var(--ink-3)' }}>Status</span>
              <StatusBadge status={BATCH_PILL[batchDetail.status] ?? 'draft'} label={BATCH_LABEL[batchDetail.status] ?? batchDetail.status} />
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13 }}>
              <span style={{ color: 'var(--ink-3)' }}>Total</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 700 }}>{fmt.format(batchDetail.totalAmount)}</span>
            </div>
            {batchDetail.bankReference && (
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13 }}>
                <span style={{ color: 'var(--ink-3)' }}>Bank Ref</span>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5 }}>{batchDetail.bankReference}</span>
              </div>
            )}
            {batchDetail.executedAt && (
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13 }}>
                <span style={{ color: 'var(--ink-3)' }}>Executed</span>
                <span>{fmtDate(batchDetail.executedAt)}</span>
              </div>
            )}
            <div style={{ paddingTop: 8, borderTop: '1px solid var(--line-2)' }}>
              <p style={{ fontSize: 10.5, fontWeight: 700, color: 'var(--ink-4)', textTransform: 'uppercase', letterSpacing: '.05em', marginBottom: 8 }}>Wires</p>
              {batchDetail.wires.map((w) => (
                <div key={w.payeeId} style={{ marginBottom: 10 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, fontWeight: 600, color: 'var(--ink)' }}>
                    <span>{w.payeeName}</span>
                    <span style={{ fontFamily: 'var(--font-mono)' }}>{fmt.format(w.netAmount)}</span>
                  </div>
                  {w.instructions.map((inst) => (
                    <div key={inst.id} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11.5, color: 'var(--ink-3)', paddingLeft: 8, marginTop: 2 }}>
                      <span>{inst.receiptNumber} · {inst.feeDisplayName}</span>
                      <span style={{ fontFamily: 'var(--font-mono)' }}>{fmt.format(inst.amount)}</span>
                    </div>
                  ))}
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Mark Executed modal */}
      {showExecuteModal !== null && (() => {
        const executeBatch = batches.find((b) => b.id === showExecuteModal)
        return (
        <div className="sims-modal-backdrop">
          <div className="sims-modal">
            <div className="sims-modal-head">
              <span>Mark Batch Executed</span>
              <button onClick={() => { setShowExecuteModal(null); setBankRef('') }} className="sims-modal-close">&times;</button>
            </div>
            <div className="sims-modal-body">
              {executeBatch && (
                <div style={{
                  background: 'var(--surface-2)',
                  border: '1px solid var(--line-2)',
                  borderRadius: 'var(--r-lg)',
                  padding: '10px 14px',
                  marginBottom: 14,
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 6,
                  fontSize: 13,
                }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: 'var(--ink-3)' }}>Batch</span>
                    <span style={{ fontWeight: 600, color: 'var(--ink)' }}>{executeBatch.batchNumber}</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: 'var(--ink-3)' }}>Wires · Instructions</span>
                    <span style={{ color: 'var(--ink-2)' }}>{executeBatch.totalWires} · {executeBatch.totalInstructions}</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: 'var(--ink-3)' }}>Total</span>
                    <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--ink)' }}>{fmt.format(executeBatch.totalAmount)}</span>
                  </div>
                </div>
              )}
              <p style={{ fontSize: 13, color: 'var(--ink-2)', marginBottom: 14 }}>
                This will post a sweep journal entry to the ledger for each instruction in the batch,
                reducing the Trust account and clearing the payable liabilities.
              </p>
              <label className="sims-field">
                <span className="sims-field-label">
                  Bank Confirmation Reference <span style={{ color: 'var(--ink-4)', fontWeight: 400 }}>(optional)</span>
                </span>
                <input
                  type="text"
                  className="sims-input"
                  value={bankRef}
                  onChange={(e) => setBankRef(e.target.value)}
                  placeholder="e.g. FEDREF-20260502-001"
                />
              </label>
            </div>
            <div className="sims-modal-foot">
              <button className="sd-btn outline" onClick={() => { setShowExecuteModal(null); setBankRef('') }}>
                Cancel
              </button>
              <button
                className="sd-btn primary"
                disabled={executeMutation.isPending}
                onClick={() => executeMutation.mutate({ id: showExecuteModal, ref: bankRef })}
              >
                <CheckCircle2 style={{ width: 13, height: 13 }} />
                {executeMutation.isPending ? 'Posting JEs…' : 'Confirm Executed'}
              </button>
            </div>
          </div>
        </div>
        )
      })()}
    </div>
  )
}

// ---------- Page ----------

type Tab = 'pending' | 'batches'

export function CashDistributionPage() {
  const [tab, setTab] = useState<Tab>('pending')

  return (
    <div className="subs-wrap">
      <div className="subs-page-head" style={{ marginBottom: 16 }}>
        <PageHeader
          title="Cash Distribution"
          subtitle="Pending wire instructions netted by destination · Execute as batch · Mark bank confirmations"
          action={<CashBalanceBadge />}
        />
      </div>

      <div className="sd-tabs" style={{ marginBottom: 20 }}>
        <button
          className={`sd-tab${tab === 'pending' ? ' active' : ''}`}
          onClick={() => setTab('pending')}
        >
          Pending Queue
        </button>
        <button
          className={`sd-tab${tab === 'batches' ? ' active' : ''}`}
          onClick={() => setTab('batches')}
        >
          Batch History
        </button>
      </div>

      {tab === 'pending' ? <PendingQueue /> : <BatchList />}
    </div>
  )
}
