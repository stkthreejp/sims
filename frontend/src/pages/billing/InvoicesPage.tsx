import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, ArrowLeft, Receipt } from 'lucide-react'
import { toast } from 'sonner'
import { getInvoices, getInvoice, createInvoice } from '@/api/invoices.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { EmptyState } from '@/components/common/EmptyState'
import { StatusBadge } from '@/components/common/StatusBadge'
import { ErrorState } from '@/components/common/ErrorState'
import { getApiErrorMessage } from '@/lib/apiError'
import { parseDateOnly, todayLocal } from '@/lib/utils'
import type { CreateInvoiceRequest, InvoiceDetail } from '@/types/invoice.types'

const US_STATES = ['AL','AK','AZ','AR','CA','CO','CT','DE','FL','GA','HI','ID','IL','IN','IA','KS','KY','LA','ME','MD','MA','MI','MN','MS','MO','MT','NE','NV','NH','NJ','NM','NY','NC','ND','OH','OK','OR','PA','RI','SC','SD','TN','TX','UT','VT','VA','WA','WV','WI','WY','DC']

const EMPTY_FORM: CreateInvoiceRequest = {
  effectiveDate: todayLocal(),
  grossPremium: 0,
  stateCode: 'TX',
  isEndorsement: false,
  isFilingState: true,
  locationCount: 1,
  vehicleCount: 1,
}

const CATEGORY_PILL: Record<string, string> = {
  Tax: 'warning',
  StampingFee: 'inprogress',
  PolicyFee: 'quoted',
  BrokerFee: 'bound',
  Inspection: 'submitted',
  Other: 'draft',
}

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
const fmtDate = (s: string) => parseDateOnly(s).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })

// ---------- New Invoice Modal ----------

function NewInvoiceModal({ onClose, onCreated }: { onClose: () => void; onCreated: (inv: InvoiceDetail) => void }) {
  const [form, setForm] = useState<CreateInvoiceRequest>(EMPTY_FORM)
  const { mutate, isPending } = useMutation({
    mutationFn: () => createInvoice(form),
    onSuccess: (inv) => { toast.success(`Invoice ${inv.invoiceNumber} posted`); onCreated(inv) },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed to create invoice')),
  })
  const set = (field: keyof CreateInvoiceRequest, value: unknown) => setForm(f => ({ ...f, [field]: value }))

  return (
    <div className="sims-modal-backdrop">
      <div className="sims-modal">
        <div className="sims-modal-head">
          <h2 className="sims-modal-title">New Invoice</h2>
          <button onClick={onClose} className="sims-icon-btn">&times;</button>
        </div>
        <div className="sims-modal-body" style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <div>
              <label className="sims-field-label">Effective Date</label>
              <input type="date" className="sims-input" value={form.effectiveDate}
                onChange={e => set('effectiveDate', e.target.value)} />
            </div>
            <div>
              <label className="sims-field-label">State</label>
              <select className="sims-select" value={form.stateCode}
                onChange={e => set('stateCode', e.target.value)}>
                {US_STATES.map(s => <option key={s}>{s}</option>)}
              </select>
            </div>
          </div>
          <div>
            <label className="sims-field-label">Gross Premium</label>
            <input type="number" step="0.01" min="0" className="sims-input"
              value={form.grossPremium} onChange={e => set('grossPremium', parseFloat(e.target.value) || 0)} />
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <div>
              <label className="sims-field-label">Line of Business</label>
              <input type="text" className="sims-input" placeholder="e.g. GL, Commercial Auto"
                value={form.lineOfBusiness ?? ''} onChange={e => set('lineOfBusiness', e.target.value || undefined)} />
            </div>
            <div>
              <label className="sims-field-label">License Type</label>
              <select className="sims-select" value={form.licenseType ?? ''}
                onChange={e => set('licenseType', e.target.value || undefined)}>
                <option value="">— any —</option>
                <option value="Admitted">Admitted</option>
                <option value="Non-Admitted">Non-Admitted</option>
              </select>
            </div>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <div>
              <label className="sims-field-label">Locations</label>
              <input type="number" min="1" className="sims-input"
                value={form.locationCount} onChange={e => set('locationCount', parseInt(e.target.value) || 1)} />
            </div>
            <div>
              <label className="sims-field-label">Vehicles</label>
              <input type="number" min="1" className="sims-input"
                value={form.vehicleCount} onChange={e => set('vehicleCount', parseInt(e.target.value) || 1)} />
            </div>
          </div>
          <div style={{ display: 'flex', gap: 20, fontSize: 13 }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <input type="checkbox" checked={form.isFilingState}
                onChange={e => set('isFilingState', e.target.checked)} />
              Filing state (surplus lines)
            </label>
            <label style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <input type="checkbox" checked={form.isEndorsement}
                onChange={e => set('isEndorsement', e.target.checked)} />
              Endorsement
            </label>
          </div>
        </div>
        <div className="sims-modal-foot">
          <button onClick={onClose} className="sd-btn outline">Cancel</button>
          <button onClick={() => mutate()} disabled={isPending || form.grossPremium <= 0} className="sd-btn primary">
            {isPending ? 'Posting…' : 'Post Invoice'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ---------- Invoice Detail ----------

function InvoiceDetailView({ id, onBack }: { id: number; onBack: () => void }) {
  const { data: inv, isLoading } = useQuery({
    queryKey: ['billing', 'invoices', id],
    queryFn: () => getInvoice(id),
  })

  if (isLoading) return <LoadingSpinner />
  if (!inv) return null

  const totalDebit = inv.ledgerEntries.reduce((s, r) => s + r.debit, 0)
  const totalCredit = inv.ledgerEntries.reduce((s, r) => s + r.credit, 0)

  return (
    <div className="subs-wrap">
      <header className="subs-page-head">
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <button onClick={onBack} className="sims-icon-btn">
            <ArrowLeft style={{ width: 14, height: 14 }} />
          </button>
          <div>
            <h1 className="subs-h1">{inv.invoiceNumber}</h1>
            <div className="subs-sub">Invoice detail</div>
          </div>
        </div>
        <StatusBadge status={inv.status} />
      </header>

      {/* Summary metrics */}
      <div className="sd-metrics" style={{ marginBottom: 18 }}>
        <div className="sd-metric">
          <div className="k">Invoice Date</div>
          <div className="v">{fmtDate(inv.invoiceDate)}</div>
        </div>
        <div className="sd-metric">
          <div className="k">Effective Date</div>
          <div className="v">{fmtDate(inv.effectiveDate)}</div>
        </div>
        <div className="sd-metric">
          <div className="k">Gross Premium</div>
          <div className="v" style={{ fontVariantNumeric: 'tabular-nums' }}>{fmt.format(inv.grossPremium)}</div>
        </div>
        <div className="sd-metric accent">
          <div className="k">Invoice Total</div>
          <div className="v" style={{ fontVariantNumeric: 'tabular-nums' }}>{fmt.format(inv.totalAmount)}</div>
        </div>
      </div>

      {/* Transaction links */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14, marginBottom: 18 }}>
        <div className="sd-card" style={{ padding: '13px 16px' }}>
          <div className="k" style={{ fontSize: 10, letterSpacing: '.06em', textTransform: 'uppercase', color: 'var(--ink-4)', fontWeight: 600, marginBottom: 4 }}>Policy Transaction</div>
          <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)', fontFamily: 'var(--font-mono)' }}>
            {inv.policyTransactionNumber ?? 'Unlinked'}
          </div>
          {inv.policyTransactionType && (
            <div style={{ fontSize: 11.5, color: 'var(--ink-3)', marginTop: 2 }}>{inv.policyTransactionType}</div>
          )}
        </div>
        <div className="sd-card" style={{ padding: '13px 16px' }}>
          <div className="k" style={{ fontSize: 10, letterSpacing: '.06em', textTransform: 'uppercase', color: 'var(--ink-4)', fontWeight: 600, marginBottom: 4 }}>Policy Version</div>
          <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)', fontFamily: 'var(--font-mono)' }}>
            {inv.policyVersionNumber != null ? `v${inv.policyVersionNumber}` : 'Unlinked'}
          </div>
          {inv.policyVersionId && (
            <div style={{ fontSize: 10.5, color: 'var(--ink-4)', marginTop: 2, fontFamily: 'var(--font-mono)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {inv.policyVersionId}
            </div>
          )}
        </div>
      </div>

      {/* Fee Lines */}
      <div className="sd-card" style={{ marginBottom: 18, overflow: 'hidden' }}>
        <div className="sd-card-head">
          <h3 style={{ margin: 0, fontSize: 13, fontWeight: 600, color: 'var(--ink)' }}>Fee Lines</h3>
          <span style={{ fontSize: 12, color: 'var(--ink-3)' }}>{fmt.format(inv.totalFees)} total fees</span>
        </div>
        <table className="sd-table">
          <thead>
            <tr>
              <th>Fee</th>
              <th>Category</th>
              <th>GL Account</th>
              <th className="num">Amount</th>
              <th style={{ textAlign: 'center' }}>Taxable</th>
            </tr>
          </thead>
          <tbody>
            {inv.lines.map(l => (
              <tr key={l.id}>
                <td>
                  <div style={{ fontWeight: 600, color: 'var(--ink)' }}>{l.feeDisplayName}</div>
                  <div style={{ fontSize: 10.5, color: 'var(--ink-4)', fontFamily: 'var(--font-mono)' }}>{l.feeCode}</div>
                </td>
                <td>
                  <span className={`sd-pill ${CATEGORY_PILL[l.feeCategory] ?? 'draft'}`}>{l.feeCategory}</span>
                </td>
                <td style={{ color: 'var(--ink-3)' }}>{l.accountCode} — {l.accountLabel}</td>
                <td className="num" style={{ fontFamily: 'var(--font-mono)' }}>{fmt.format(l.amount)}</td>
                <td style={{ textAlign: 'center', fontSize: 11 }}>{l.isTaxable ? '✓' : '—'}</td>
              </tr>
            ))}
            <tr style={{ background: 'var(--surface-2)', fontWeight: 600 }}>
              <td colSpan={3} style={{ textAlign: 'right', padding: '11px 14px', color: 'var(--ink-3)' }}>Total Fees</td>
              <td className="num" style={{ fontFamily: 'var(--font-mono)' }}>{fmt.format(inv.totalFees)}</td>
              <td />
            </tr>
          </tbody>
        </table>
      </div>

      {/* Ledger Postings */}
      <div className="sd-card" style={{ overflow: 'hidden' }}>
        <div className="sd-card-head">
          <h3 style={{ margin: 0, fontSize: 13, fontWeight: 600, color: 'var(--ink)' }}>Ledger Postings</h3>
          <span style={{ fontSize: 11, color: 'var(--ink-4)', fontFamily: 'var(--font-mono)' }}>TXN {inv.ledgerTransactionId}</span>
        </div>
        <table className="sd-table">
          <thead>
            <tr>
              <th>Account</th>
              <th>Memo</th>
              <th className="num">Debit</th>
              <th className="num">Credit</th>
            </tr>
          </thead>
          <tbody>
            {inv.ledgerEntries.map(e => (
              <tr key={e.id} style={e.debit > 0 ? { background: 'var(--accent-soft)' } : undefined}>
                <td>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--ink-3)', marginRight: 8 }}>{e.accountCode}</span>
                  <span style={{ color: 'var(--ink-2)' }}>{e.accountLabel}</span>
                </td>
                <td style={{ color: 'var(--ink-3)', fontSize: 12 }}>{e.memo}</td>
                <td className="num" style={{ fontFamily: 'var(--font-mono)' }}>{e.debit > 0 ? fmt.format(e.debit) : '—'}</td>
                <td className="num" style={{ fontFamily: 'var(--font-mono)' }}>{e.credit > 0 ? fmt.format(e.credit) : '—'}</td>
              </tr>
            ))}
            <tr style={{ background: 'var(--surface-2)', fontWeight: 600, borderTop: '2px solid var(--line)' }}>
              <td colSpan={2} style={{ textAlign: 'right', padding: '11px 14px', color: 'var(--ink-3)' }}>Totals</td>
              <td className="num" style={{ fontFamily: 'var(--font-mono)' }}>{fmt.format(totalDebit)}</td>
              <td className="num" style={{ fontFamily: 'var(--font-mono)' }}>{fmt.format(totalCredit)}</td>
            </tr>
            <tr>
              <td colSpan={4} style={{ textAlign: 'right', padding: '6px 14px' }}>
                {Math.abs(totalDebit - totalCredit) < 0.001
                  ? <span style={{ fontSize: 11, color: 'var(--pill-bound-fg)', fontWeight: 500 }}>✓ Balanced</span>
                  : <span style={{ fontSize: 11, color: 'var(--bad-fg)', fontWeight: 500 }}>✗ Out of balance by {fmt.format(Math.abs(totalDebit - totalCredit))}</span>
                }
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  )
}

// ---------- Main Page ----------

export function InvoicesPage() {
  const qc = useQueryClient()
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [showNew, setShowNew] = useState(false)

  const { data: invoices = [], isLoading, isError, error, refetch } = useQuery({
    queryKey: ['billing', 'invoices'],
    queryFn: getInvoices,
  })

  const handleCreated = (inv: InvoiceDetail) => {
    qc.invalidateQueries({ queryKey: ['billing', 'invoices'] })
    qc.setQueryData(['billing', 'invoices', inv.id], inv)
    // A posted invoice is a new open receivable + carrier payable + GL posting —
    // refresh cash application, disbursements aging, activity, and trust (audit).
    qc.invalidateQueries({ queryKey: ['billing', 'cash-application'] })
    qc.invalidateQueries({ queryKey: ['disbursements'] })
    qc.invalidateQueries({ queryKey: ['disbursements-aging'] })
    qc.invalidateQueries({ queryKey: ['activity'] })
    qc.invalidateQueries({ queryKey: ['trust-balance'] })
    setShowNew(false)
    setSelectedId(inv.id)
  }

  if (selectedId !== null) {
    return <InvoiceDetailView id={selectedId} onBack={() => setSelectedId(null)} />
  }

  if (isError) {
    return (
      <div className="subs-wrap">
        <header className="subs-page-head">
          <div>
            <h1 className="subs-h1">Invoices</h1>
            <div className="subs-sub">Couldn't load</div>
          </div>
          <button onClick={() => setShowNew(true)} className="sd-btn primary">
            <Plus size={14} /> New Invoice
          </button>
        </header>
        <ErrorState error={error} onRetry={refetch} />
        {showNew && (
          <NewInvoiceModal onClose={() => setShowNew(false)} onCreated={handleCreated} />
        )}
      </div>
    )
  }

  const today = new Date(); today.setHours(0, 0, 0, 0)
  const daysOld = (dateStr: string) => Math.floor((today.getTime() - parseDateOnly(dateStr).getTime()) / 86400000)

  const open = invoices.filter(i => i.status === 'Posted' || i.status === 'PartiallyPaid')
  const outstanding = open.reduce((s, i) => s + i.openBalance, 0)
  const pastDue = open.filter(i => i.openBalance > 0 && daysOld(i.dueDate) > 0)
  const pastDueAmount = pastDue.reduce((s, i) => s + i.openBalance, 0)
  const totalBilled = invoices.reduce((s, i) => s + i.totalAmount, 0)

  return (
    <div className="subs-wrap">
      <header className="subs-page-head">
        <div>
          <h1 className="subs-h1">Invoices</h1>
          <div className="subs-sub">{isLoading ? 'Loading…' : `${invoices.length} records`}</div>
        </div>
        <button onClick={() => setShowNew(true)} className="sd-btn primary">
          <Plus size={14} /> New Invoice
        </button>
      </header>

      {/* Metrics strip skeleton */}
      {isLoading && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 10, marginBottom: 20 }}>
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} style={{ height: 72, borderRadius: 'var(--r-lg)', background: 'var(--surface-2)', border: '1px solid var(--line)' }} />
          ))}
        </div>
      )}

      {/* Metrics strip */}
      {!isLoading && (
        <div className="sd-metrics" style={{ gridTemplateColumns: 'repeat(3, 1fr)' }}>
          <div className="sd-metric accent">
            <div className="k">Outstanding</div>
            <div className="v" style={{ fontVariantNumeric: 'tabular-nums' }}>{fmt.format(outstanding)}</div>
            <div className="s">{open.length} open invoice{open.length !== 1 ? 's' : ''}</div>
          </div>
          <div className="sd-metric" style={pastDue.length > 0 ? { background: 'var(--bad-bg)', borderColor: 'var(--bad-fg)' } : {}}>
            <div className="k" style={pastDue.length > 0 ? { color: 'var(--bad-fg)' } : {}}>Past Due</div>
            <div className="v" style={{ fontVariantNumeric: 'tabular-nums', ...(pastDue.length > 0 ? { color: 'var(--bad-fg)' } : {}) }}>
              {pastDue.length > 0 ? fmt.format(pastDueAmount) : '—'}
            </div>
            <div className="s">{pastDue.length > 0 ? `${pastDue.length} invoice${pastDue.length !== 1 ? 's' : ''}` : 'None'}</div>
          </div>
          <div className="sd-metric">
            <div className="k">Total Billed (all time)</div>
            <div className="v" style={{ fontVariantNumeric: 'tabular-nums' }}>{fmt.format(totalBilled)}</div>
            <div className="s">{invoices.length} invoice{invoices.length !== 1 ? 's' : ''}</div>
          </div>
        </div>
      )}

      {/* Table skeleton */}
      {isLoading && (
        <div className="subs-table-card">
          <table className="subs-table">
            <tbody>
              {Array.from({ length: 8 }).map((_, i) => (
                <tr key={i} className="subs-row" style={{ pointerEvents: 'none' }}>
                  <td colSpan={8} style={{ padding: '12px 14px' }}>
                    <div style={{ height: 14, borderRadius: 4, background: 'var(--surface-2)', width: `${55 + (i % 4) * 12}%` }} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Table */}
      {!isLoading && (
        <div className="subs-table-card">
          <table className="subs-table">
            <thead>
              <tr>
                <th className="subs-th">Invoice #</th>
                <th className="subs-th">Transaction</th>
                <th className="subs-th">Invoice Date</th>
                <th className="subs-th">Effective Date</th>
                <th className="subs-th num">Gross Premium</th>
                <th className="subs-th num">Fees</th>
                <th className="subs-th num">Total</th>
                <th className="subs-th">Status</th>
              </tr>
            </thead>
            <tbody>
              {invoices.length === 0 && (
                <tr>
                  <td colSpan={8}>
                    <EmptyState
                      icon={Receipt}
                      title="No invoices yet"
                      description="Post the first invoice to get started."
                      action={
                        <button className="sd-btn primary sm" onClick={() => setShowNew(true)}>
                          New Invoice
                        </button>
                      }
                    />
                  </td>
                </tr>
              )}
              {invoices.map(inv => {
                const overdueDays = daysOld(inv.dueDate)
                const isOpen = inv.openBalance > 0 && inv.status !== 'Paid'
                const rowBg = isOpen && overdueDays > 0
                  ? 'var(--bad-bg)'
                  : isOpen && overdueDays > -15
                    ? 'var(--warn-bg)'
                    : undefined
                return (
                <tr key={inv.id} className="subs-row" style={rowBg ? { background: rowBg } : undefined} onClick={() => setSelectedId(inv.id)}>
                  <td className="subs-id">{inv.invoiceNumber}</td>
                  <td>
                    <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--ink-2)' }}>
                      {inv.policyTransactionNumber ?? '—'}
                    </div>
                    {inv.policyVersionNumber != null && (
                      <div style={{ fontSize: 10.5, color: 'var(--ink-4)' }}>v{inv.policyVersionNumber}</div>
                    )}
                  </td>
                  <td className="subs-eff">{fmtDate(inv.invoiceDate)}</td>
                  <td className="subs-eff">{fmtDate(inv.effectiveDate)}</td>
                  <td className="subs-eff num">{fmt.format(inv.grossPremium)}</td>
                  <td className="subs-eff num subs-muted">{fmt.format(inv.totalFees)}</td>
                  <td className="subs-eff num" style={{ fontWeight: 600 }}>{fmt.format(inv.totalAmount)}</td>
                  <td><StatusBadge status={inv.status} /></td>
                </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      {showNew && (
        <NewInvoiceModal onClose={() => setShowNew(false)} onCreated={handleCreated} />
      )}
    </div>
  )
}
