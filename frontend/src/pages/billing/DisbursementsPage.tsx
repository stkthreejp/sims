import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { DollarSign, Send, X, ChevronDown, ChevronRight, CheckCircle2, Wallet } from 'lucide-react'
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
import { StatusBadge } from '@/components/common/StatusBadge'
import { EmptyState } from '@/components/common/EmptyState'
import { parseDateOnly, todayLocal } from '@/lib/utils'
import type { OpenPayable, AgingRow } from '@/types/disbursement.types'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
const fmtDate = (s: string) =>
  parseDateOnly(s).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
const payeeSubledgerKey = (payee: { payeeId: number | null; payeeName: string }) =>
  payee.payeeId ? `payee:${payee.payeeId}` : `name:${payee.payeeName}`

const DISB_PILL: Record<string, string> = {
  Open: 'quoted',
  PartiallyPaid: 'inprogress',
  Paid: 'bound',
  Voided: 'voided',
  Draft: 'inprogress',
  Posted: 'posted',
}
const DISB_LABEL: Record<string, string> = { PartiallyPaid: 'Partial' }

function AgeBadge({ days }: { days: number }) {
  const color = days === 0 ? 'var(--pill-bound-fg)' : days <= 30 ? 'var(--warn-fg)' : 'var(--bad-fg)'
  const bg = days === 0 ? 'var(--pill-bound-bg)' : days <= 30 ? 'var(--warn-bg)' : 'var(--bad-bg)'
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', padding: '1px 6px', borderRadius: 4, fontSize: 11.5, fontFamily: 'var(--font-mono)', color, background: bg }}>
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
  const [paymentDate, setPaymentDate] = useState(todayLocal())
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
    if (lines.length === 0) { toast.error('Enter an amount for at least one payable'); return }
    createMutation.mutate({ lines, paymentDate, paymentMethod, reference: reference || undefined, notes: notes || undefined })
  }

  const inputStyle: React.CSSProperties = {
    width: '100%', border: '1px solid var(--line)', borderRadius: 'var(--r)',
    padding: '6px 10px', fontSize: 13, background: 'var(--surface)', color: 'var(--ink)', outline: 'none',
  }

  return (
    <div className="sims-modal-backdrop">
      <div className="sims-modal" style={{ maxWidth: 680 }}>
        <div className="sims-modal-head">
          <span>Create Disbursement</span>
          <button onClick={onClose} className="sims-modal-close"><X style={{ width: 16, height: 16 }} /></button>
        </div>
        <div className="sims-modal-body" style={{ maxHeight: '70vh', overflowY: 'auto' }}>
          {/* Payable lines */}
          <div style={{ marginBottom: 20 }}>
            <p style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--ink-2)', marginBottom: 8 }}>Payables ({payables.length})</p>
            <div className="sd-card" style={{ overflow: 'hidden' }}>
              <table className="sd-table">
                <thead>
                  <tr>
                    <th>Invoice</th>
                    <th>Payee</th>
                    <th className="num">Balance</th>
                    <th className="num">Amount</th>
                  </tr>
                </thead>
                <tbody>
                  {payables.map((p) => (
                    <tr key={p.id} style={{ cursor: 'default' }}>
                      <td className="id">{p.invoiceNumber}</td>
                      <td style={{ color: 'var(--ink-2)', fontSize: 12 }}>{p.payeeName}</td>
                      <td className="num">{fmt.format(p.balance)}</td>
                      <td className="num">
                        <input
                          type="number" step="0.01" min="0" max={p.balance}
                          value={amounts[p.id] ?? ''}
                          onChange={(e) => setAmounts((prev) => ({ ...prev, [p.id]: e.target.value }))}
                          style={{ ...inputStyle, width: 110, textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 12 }}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot style={{ borderTop: '2px solid var(--line)', background: 'var(--surface-2)' }}>
                  <tr>
                    <td colSpan={3} style={{ padding: '10px 14px', fontWeight: 700, color: 'var(--ink-2)', fontSize: 13 }}>Total</td>
                    <td style={{ padding: '10px 14px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--ink)' }}>{fmt.format(totalAmount)}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </div>

          {/* Payment details */}
          <div className="sims-fields" style={{ gridTemplateColumns: '1fr 1fr' }}>
            <label className="sims-field">
              <span className="sims-field-label">Payment Date</span>
              <input type="date" className="sims-input" value={paymentDate} onChange={(e) => setPaymentDate(e.target.value)} />
            </label>
            <label className="sims-field">
              <span className="sims-field-label">Payment Method</span>
              <select className="sims-select" value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value)}>
                <option value="Check">Check</option>
                <option value="Wire">Wire</option>
                <option value="ACH">ACH</option>
              </select>
            </label>
            <label className="sims-field">
              <span className="sims-field-label">Reference <span style={{ color: 'var(--ink-4)', fontWeight: 400 }}>(check # / wire ref)</span></span>
              <input type="text" className="sims-input" value={reference} onChange={(e) => setReference(e.target.value)} placeholder="e.g. 10042" />
            </label>
            <label className="sims-field">
              <span className="sims-field-label">Notes</span>
              <input type="text" className="sims-input" value={notes} onChange={(e) => setNotes(e.target.value)} />
            </label>
          </div>
        </div>
        <div className="sims-modal-foot">
          <button className="sd-btn outline" onClick={onClose}>Cancel</button>
          <button
            className="sd-btn primary"
            disabled={createMutation.isPending || totalAmount <= 0}
            onClick={handleSubmit}
          >
            <DollarSign style={{ width: 13, height: 13 }} />
            {createMutation.isPending ? 'Creating…' : `Create — ${fmt.format(totalAmount)}`}
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

  const togglePayee = (ids: number[]) => {
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

  const byPayee = useMemo(() => {
    const map = new Map<string, OpenPayable[]>()
    for (const p of aging?.payables ?? []) {
      const key = payeeSubledgerKey(p)
      if (!map.has(key)) map.set(key, [])
      map.get(key)!.push(p)
    }
    return map
  }, [aging?.payables])

  if (isLoading || !aging) return <LoadingSpinner />

  const buckets = [
    { label: 'Current (0–30d)', value: aging.summary.current, border: 'var(--pill-bound-bg)', color: 'var(--pill-bound-fg)' },
    { label: '31–60 Days', value: aging.summary.days31to60, border: 'var(--warn-bg)', color: 'var(--warn-fg)' },
    { label: '61–90 Days', value: aging.summary.days61to90, border: 'var(--bad-bg)', color: 'var(--bad-fg)' },
    { label: 'Over 90 Days', value: aging.summary.over90, border: 'var(--bad-bg)', color: 'var(--bad-fg)' },
  ]

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {/* Bucket summary */}
      <div className="sd-metrics" style={{ gridTemplateColumns: 'repeat(4, 1fr)' }}>
        {buckets.map(({ label, value, border, color }) => (
          <div key={label} className="sd-metric" style={{ borderColor: border }}>
            <p className="k" style={{ color }}>{label}</p>
            <p className="v" style={{ color }}>{fmt.format(value)}</p>
          </div>
        ))}
      </div>

      {aging.payables.length === 0 ? (
        <EmptyState icon={Wallet} title="No open payables" description="Create invoices to generate carrier payables." />
      ) : (
        <>
          {/* Toolbar */}
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <p style={{ fontSize: 13, color: 'var(--ink-2)' }}>
              {aging.payables.length} payable{aging.payables.length !== 1 ? 's' : ''} ·{' '}
              {fmt.format(aging.summary.total)} outstanding
            </p>
            <button
              className="sd-btn primary"
              disabled={selected.size === 0}
              onClick={() => setShowModal(true)}
            >
              <Send style={{ width: 13, height: 13 }} />
              {selected.size > 0 ? `Create Disbursement (${selected.size})` : 'Select Payables'}
            </button>
          </div>

          {/* Aging table */}
          <div className="subs-table-card">
            <table className="subs-table">
              <thead>
                <tr>
                  <th className="subs-th" style={{ width: 40 }} />
                  <th className="subs-th" style={{ width: 32 }} />
                  <th className="subs-th">Payee / Invoice</th>
                  <th className="subs-th num">Current</th>
                  <th className="subs-th num">31–60d</th>
                  <th className="subs-th num">61–90d</th>
                  <th className="subs-th num">90+d</th>
                  <th className="subs-th num">Total</th>
                </tr>
              </thead>
              <tbody>
                {aging.rows.map((row: AgingRow) => {
                  const rowKey = payeeSubledgerKey(row)
                  const rowPayables = byPayee.get(rowKey) ?? []
                  const rowIds = rowPayables.map((p) => p.id)
                  const allRowSelected = rowIds.length > 0 && rowIds.every((id) => selected.has(id))
                  const someRowSelected = rowIds.some((id) => selected.has(id))
                  const isExpanded = expandedPayees.has(rowKey)

                  return (
                    <>
                      <tr key={rowKey} className="subs-row" style={{ background: 'var(--surface-2)' }}
                        onClick={() => toggleExpand(rowKey)}>
                        <td>
                          <input
                            type="checkbox"
                            checked={allRowSelected}
                            ref={(el) => { if (el) el.indeterminate = someRowSelected && !allRowSelected }}
                            onChange={(e) => { e.stopPropagation(); togglePayee(rowIds) }}
                            onClick={(e) => e.stopPropagation()}
                          />
                        </td>
                        <td style={{ color: 'var(--ink-3)' }}>
                          {isExpanded ? <ChevronDown style={{ width: 14, height: 14 }} /> : <ChevronRight style={{ width: 14, height: 14 }} />}
                        </td>
                        <td style={{ fontWeight: 600, color: 'var(--ink)' }}>{row.payeeName}</td>
                        <td style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--ink-2)' }}>{row.current > 0 ? fmt.format(row.current) : '—'}</td>
                        <td style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--warn-fg)' }}>{row.days31to60 > 0 ? fmt.format(row.days31to60) : '—'}</td>
                        <td style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--bad-fg)' }}>{row.days61to90 > 0 ? fmt.format(row.days61to90) : '—'}</td>
                        <td style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--bad-fg)' }}>{row.over90 > 0 ? fmt.format(row.over90) : '—'}</td>
                        <td style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--ink)' }}>{fmt.format(row.total)}</td>
                      </tr>

                      {isExpanded && rowPayables.map((p) => (
                        <tr key={p.id} className="subs-row"
                          style={{ fontSize: 12, background: selected.has(p.id) ? 'var(--accent-soft)' : 'var(--surface)' }}>
                          <td>
                            <input type="checkbox" checked={selected.has(p.id)} onChange={() => togglePayable(p.id)} />
                          </td>
                          <td />
                          <td style={{ paddingLeft: 28 }}>
                            <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--ink-2)' }}>{p.invoiceNumber}</span>
                            <span style={{ color: 'var(--ink-4)', marginLeft: 8 }}>{fmtDate(p.invoiceDate)}</span>
                            <span style={{ marginLeft: 8 }}><AgeBadge days={p.daysOutstanding} /></span>
                            <span style={{ marginLeft: 8 }}><StatusBadge status={DISB_PILL[p.status] ?? 'draft'} label={DISB_LABEL[p.status] ?? p.status} /></span>
                          </td>
                          <td colSpan={4} />
                          <td style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--ink-2)' }}>
                            {fmt.format(p.balance)}
                          </td>
                        </tr>
                      ))}
                    </>
                  )
                })}
              </tbody>
              <tfoot style={{ borderTop: '2px solid var(--line)', background: 'var(--surface-2)' }}>
                <tr>
                  <td colSpan={3} style={{ padding: '10px 14px', fontWeight: 700, color: 'var(--ink-2)', fontSize: 13 }}>Total Outstanding</td>
                  <td style={{ padding: '10px 14px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--ink-2)' }}>{fmt.format(aging.summary.current)}</td>
                  <td style={{ padding: '10px 14px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--warn-fg)' }}>{fmt.format(aging.summary.days31to60)}</td>
                  <td style={{ padding: '10px 14px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--bad-fg)' }}>{fmt.format(aging.summary.days61to90)}</td>
                  <td style={{ padding: '10px 14px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--bad-fg)' }}>{fmt.format(aging.summary.over90)}</td>
                  <td style={{ padding: '10px 14px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--ink)' }}>{fmt.format(aging.summary.total)}</td>
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
    return <EmptyState icon={Wallet} title="No disbursements yet" description="Select payables from the Aging tab to start a check run." />
  }

  return (
    <div style={{ display: 'flex', gap: 20 }}>
      <div style={{ flex: 1 }}>
        <div className="subs-table-card">
          <table className="subs-table">
            <thead>
              <tr>
                <th className="subs-th">Disbursement #</th>
                <th className="subs-th">Payee</th>
                <th className="subs-th">Date</th>
                <th className="subs-th">Method</th>
                <th className="subs-th num">Amount</th>
                <th className="subs-th">Status</th>
                <th className="subs-th">Actions</th>
              </tr>
            </thead>
            <tbody>
              {disbursements.map((d) => (
                <tr
                  key={d.id}
                  className="subs-row"
                  style={{ background: d.id === selectedId ? 'var(--accent-soft)' : undefined }}
                  onClick={() => setSelectedId(d.id === selectedId ? null : d.id)}
                >
                  <td className="subs-id">{d.disbursementNumber}</td>
                  <td style={{ color: 'var(--ink)' }}>{d.payeeName}</td>
                  <td style={{ color: 'var(--ink-2)' }}>{fmtDate(d.paymentDate)}</td>
                  <td style={{ color: 'var(--ink-3)', fontSize: 12 }}>{d.paymentMethod}{d.reference ? ` · ${d.reference}` : ''}</td>
                  <td style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600 }}>{fmt.format(d.totalAmount)}</td>
                  <td><StatusBadge status={DISB_PILL[d.status] ?? 'draft'} label={DISB_LABEL[d.status] ?? d.status} /></td>
                  <td onClick={(e) => e.stopPropagation()}>
                    {d.status === 'Draft' && (
                      <button
                        className="sd-btn sm"
                        disabled={postMutation.isPending}
                        onClick={() => postMutation.mutate(d.id)}
                        style={{ color: 'var(--pill-bound-fg)' }}
                      >
                        <CheckCircle2 style={{ width: 12, height: 12 }} />
                        Post
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {selectedId !== null && detail && (
        <div className="sd-card" style={{ width: 280, flexShrink: 0, alignSelf: 'flex-start', overflow: 'hidden' }}>
          <div className="sd-card-head" style={{ justifyContent: 'space-between' }}>
            <h3>{detail.disbursementNumber}</h3>
            <button onClick={() => setSelectedId(null)} style={{ fontSize: 13, color: 'var(--ink-3)', background: 'none', border: 0, cursor: 'pointer' }}>✕</button>
          </div>
          <div className="sd-card-body" style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {[
              { label: 'Payee', value: <span style={{ maxWidth: 150, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', display: 'block', textAlign: 'right' }} title={detail.payeeName}>{detail.payeeName}</span> },
              { label: 'Status', value: <StatusBadge status={DISB_PILL[detail.status] ?? 'draft'} label={DISB_LABEL[detail.status] ?? detail.status} /> },
              { label: 'Method', value: <span>{detail.paymentMethod}</span> },
              ...(detail.reference ? [{ label: 'Ref', value: <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5 }}>{detail.reference}</span> }] : []),
              ...(detail.ledgerTransactionId ? [{ label: 'JE', value: <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--ink-3)' }}>{detail.ledgerTransactionId.slice(0, 8)}…</span> }] : []),
            ].map(({ label, value }) => (
              <div key={label} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: 13 }}>
                <span style={{ color: 'var(--ink-3)' }}>{label}</span>
                {value}
              </div>
            ))}
            <div style={{ paddingTop: 8, borderTop: '1px solid var(--line-2)' }}>
              <p style={{ fontSize: 10.5, fontWeight: 700, color: 'var(--ink-4)', textTransform: 'uppercase', letterSpacing: '.05em', marginBottom: 8 }}>Lines</p>
              {detail.lines.map((l) => (
                <div key={l.id} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, marginBottom: 6 }}>
                  <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--ink-2)' }}>{l.invoiceNumber}</span>
                  <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--ink)' }}>{fmt.format(l.amount)}</span>
                </div>
              ))}
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, fontWeight: 700, borderTop: '1px solid var(--line-2)', paddingTop: 6, marginTop: 4 }}>
                <span style={{ color: 'var(--ink-2)' }}>Total</span>
                <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--ink)' }}>{fmt.format(detail.totalAmount)}</span>
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
    <div className="subs-wrap">
      <div className="subs-page-head" style={{ marginBottom: 16 }}>
        <PageHeader
          title="Carrier Disbursements"
          subtitle="Payable aging by carrier · check run selection · ledger posting"
        />
      </div>

      <div className="sd-tabs" style={{ marginBottom: 20 }}>
        <button className={`sd-tab${tab === 'aging' ? ' active' : ''}`} onClick={() => setTab('aging')}>
          Payable Aging
        </button>
        <button className={`sd-tab${tab === 'disbursements' ? ' active' : ''}`} onClick={() => setTab('disbursements')}>
          Check Run / History
        </button>
      </div>

      {tab === 'aging' ? <AgingTab /> : <DisbursementsTab />}
    </div>
  )
}
