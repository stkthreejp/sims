import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, ChevronRight, ArrowLeft, Receipt, TrendingUp, FileText } from 'lucide-react'
import { toast } from 'sonner'
import { getInvoices, getInvoice, createInvoice } from '@/api/invoices.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { CreateInvoiceRequest, InvoiceDetail } from '@/types/invoice.types'

const US_STATES = ['AL','AK','AZ','AR','CA','CO','CT','DE','FL','GA','HI','ID','IL','IN','IA','KS','KY','LA','ME','MD','MA','MI','MN','MS','MO','MT','NE','NV','NH','NJ','NM','NY','NC','ND','OH','OK','OR','PA','RI','SC','SD','TN','TX','UT','VT','VA','WA','WV','WI','WY','DC']

const EMPTY_FORM: CreateInvoiceRequest = {
  effectiveDate: new Date().toISOString().slice(0, 10),
  grossPremium: 0,
  stateCode: 'TX',
  isEndorsement: false,
  isFilingState: true,
  locationCount: 1,
  vehicleCount: 1,
}

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
  const cls = status === 'Posted'
    ? 'bg-green-100 text-green-800'
    : 'bg-red-100 text-red-800'
  return <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${cls}`}>{status}</span>
}

function CategoryBadge({ category }: { category: string }) {
  const colors: Record<string, string> = {
    Tax: 'bg-orange-100 text-orange-800',
    StampingFee: 'bg-purple-100 text-purple-800',
    PolicyFee: 'bg-blue-100 text-blue-800',
    BrokerFee: 'bg-teal-100 text-teal-800',
    Inspection: 'bg-yellow-100 text-yellow-800',
    Other: 'bg-gray-100 text-gray-700',
  }
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${colors[category] ?? colors.Other}`}>
      {category}
    </span>
  )
}

// ---------- New Invoice Form ----------

function NewInvoicePanel({ onClose, onCreated }: { onClose: () => void; onCreated: (inv: InvoiceDetail) => void }) {
  const [form, setForm] = useState<CreateInvoiceRequest>(EMPTY_FORM)
  const { mutate, isPending } = useMutation({
    mutationFn: () => createInvoice(form),
    onSuccess: (inv) => {
      toast.success(`Invoice ${inv.invoiceNumber} posted`)
      onCreated(inv)
    },
    onError: () => toast.error('Failed to create invoice'),
  })

  const set = (field: keyof CreateInvoiceRequest, value: unknown) =>
    setForm(f => ({ ...f, [field]: value }))

  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-lg mx-4">
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <h2 className="text-base font-semibold text-gray-900">New Invoice</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl leading-none">&times;</button>
        </div>
        <div className="px-6 py-4 space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <Field label="Effective Date">
              <input type="date" className={inputCls} value={form.effectiveDate}
                onChange={e => set('effectiveDate', e.target.value)} />
            </Field>
            <Field label="State">
              <select className={inputCls} value={form.stateCode}
                onChange={e => set('stateCode', e.target.value)}>
                {US_STATES.map(s => <option key={s}>{s}</option>)}
              </select>
            </Field>
          </div>
          <Field label="Gross Premium">
            <input type="number" step="0.01" min="0" className={inputCls}
              value={form.grossPremium} onChange={e => set('grossPremium', parseFloat(e.target.value) || 0)} />
          </Field>
          <div className="grid grid-cols-2 gap-4">
            <Field label="Line of Business">
              <input type="text" className={inputCls} placeholder="e.g. GL, Commercial Auto"
                value={form.lineOfBusiness ?? ''} onChange={e => set('lineOfBusiness', e.target.value || undefined)} />
            </Field>
            <Field label="License Type">
              <select className={inputCls} value={form.licenseType ?? ''}
                onChange={e => set('licenseType', e.target.value || undefined)}>
                <option value="">— any —</option>
                <option value="Admitted">Admitted</option>
                <option value="Non-Admitted">Non-Admitted</option>
              </select>
            </Field>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <Field label="Locations">
              <input type="number" min="1" className={inputCls}
                value={form.locationCount} onChange={e => set('locationCount', parseInt(e.target.value) || 1)} />
            </Field>
            <Field label="Vehicles">
              <input type="number" min="1" className={inputCls}
                value={form.vehicleCount} onChange={e => set('vehicleCount', parseInt(e.target.value) || 1)} />
            </Field>
          </div>
          <div className="flex items-center gap-6">
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={form.isFilingState}
                onChange={e => set('isFilingState', e.target.checked)} />
              Filing state (surplus lines)
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={form.isEndorsement}
                onChange={e => set('isEndorsement', e.target.checked)} />
              Endorsement
            </label>
          </div>
        </div>
        <div className="px-6 py-4 border-t border-gray-200 flex justify-end gap-3">
          <button onClick={onClose} className="px-4 py-2 text-sm border border-gray-300 rounded hover:bg-gray-50">Cancel</button>
          <button
            onClick={() => mutate()}
            disabled={isPending || form.grossPremium <= 0}
            className="px-4 py-2 text-sm bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50"
          >
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
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <button onClick={onBack} className="text-gray-500 hover:text-gray-700">
          <ArrowLeft className="h-4 w-4" />
        </button>
        <h2 className="text-lg font-semibold text-gray-900">{inv.invoiceNumber}</h2>
        <StatusBadge status={inv.status} />
      </div>

      {/* Summary */}
      <div className="grid grid-cols-4 gap-4">
        {[
          { label: 'Invoice Date', value: fmtDate(inv.invoiceDate) },
          { label: 'Effective Date', value: fmtDate(inv.effectiveDate) },
          { label: 'Gross Premium', value: fmt.format(inv.grossPremium) },
          { label: 'Invoice Total', value: fmt.format(inv.totalAmount) },
        ].map(({ label, value }) => (
          <div key={label} className="bg-gray-50 rounded-lg p-4">
            <p className="text-xs text-gray-500">{label}</p>
            <p className="mt-1 text-sm font-semibold text-gray-900">{value}</p>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="bg-white rounded-lg border border-gray-200 p-4">
          <p className="text-xs text-gray-500">Policy Transaction</p>
          <p className="mt-1 text-sm font-semibold text-gray-900">
            {inv.policyTransactionNumber ?? 'Unlinked'}
          </p>
          {inv.policyTransactionType && (
            <p className="mt-1 text-xs text-gray-500">{inv.policyTransactionType}</p>
          )}
        </div>
        <div className="bg-white rounded-lg border border-gray-200 p-4">
          <p className="text-xs text-gray-500">Policy Version</p>
          <p className="mt-1 text-sm font-semibold text-gray-900">
            {inv.policyVersionNumber != null ? `v${inv.policyVersionNumber}` : 'Unlinked'}
          </p>
          {inv.policyVersionId && (
            <p className="mt-1 truncate font-mono text-xs text-gray-400">{inv.policyVersionId}</p>
          )}
        </div>
      </div>

      {/* Fee Lines */}
      <div>
        <h3 className="text-sm font-semibold text-gray-700 mb-2">Fee Lines</h3>
        <div className="border border-gray-200 rounded-lg overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-left">
              <tr>
                <th className="px-4 py-2 font-medium text-gray-600">Fee</th>
                <th className="px-4 py-2 font-medium text-gray-600">Category</th>
                <th className="px-4 py-2 font-medium text-gray-600">GL Account</th>
                <th className="px-4 py-2 font-medium text-gray-600 text-right">Amount</th>
                <th className="px-4 py-2 font-medium text-gray-600 text-center">Taxable</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {inv.lines.map(l => (
                <tr key={l.id} className="hover:bg-gray-50">
                  <td className="px-4 py-2">
                    <div className="font-medium text-gray-900">{l.feeDisplayName}</div>
                    <div className="text-xs text-gray-400">{l.feeCode}</div>
                  </td>
                  <td className="px-4 py-2"><CategoryBadge category={l.feeCategory} /></td>
                  <td className="px-4 py-2 text-gray-600">{l.accountCode} — {l.accountLabel}</td>
                  <td className="px-4 py-2 text-right font-mono text-gray-900">{fmt.format(l.amount)}</td>
                  <td className="px-4 py-2 text-center text-xs">{l.isTaxable ? '✓' : '—'}</td>
                </tr>
              ))}
              <tr className="bg-gray-50 font-medium">
                <td colSpan={3} className="px-4 py-2 text-right text-gray-700">Total Fees</td>
                <td className="px-4 py-2 text-right font-mono">{fmt.format(inv.totalFees)}</td>
                <td />
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      {/* Ledger Postings */}
      <div>
        <div className="flex items-center justify-between mb-2">
          <h3 className="text-sm font-semibold text-gray-700">Ledger Postings</h3>
          <span className="text-xs text-gray-400 font-mono">TXN {inv.ledgerTransactionId}</span>
        </div>
        <div className="border border-gray-200 rounded-lg overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-left">
              <tr>
                <th className="px-4 py-2 font-medium text-gray-600">Account</th>
                <th className="px-4 py-2 font-medium text-gray-600">Memo</th>
                <th className="px-4 py-2 font-medium text-gray-600 text-right">Debit</th>
                <th className="px-4 py-2 font-medium text-gray-600 text-right">Credit</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {inv.ledgerEntries.map(e => (
                <tr key={e.id} className={e.debit > 0 ? 'bg-blue-50/40' : ''}>
                  <td className="px-4 py-2">
                    <span className="font-mono text-xs text-gray-500 mr-2">{e.accountCode}</span>
                    <span className="text-gray-700">{e.accountLabel}</span>
                  </td>
                  <td className="px-4 py-2 text-gray-500 text-xs">{e.memo}</td>
                  <td className="px-4 py-2 text-right font-mono">{e.debit > 0 ? fmt.format(e.debit) : '—'}</td>
                  <td className="px-4 py-2 text-right font-mono">{e.credit > 0 ? fmt.format(e.credit) : '—'}</td>
                </tr>
              ))}
              <tr className="bg-gray-50 font-semibold border-t-2 border-gray-300">
                <td colSpan={2} className="px-4 py-2 text-right text-gray-700">Totals</td>
                <td className="px-4 py-2 text-right font-mono">{fmt.format(totalDebit)}</td>
                <td className="px-4 py-2 text-right font-mono">{fmt.format(totalCredit)}</td>
              </tr>
              <tr>
                <td colSpan={4} className="px-4 py-1 text-right">
                  {Math.abs(totalDebit - totalCredit) < 0.001
                    ? <span className="text-xs text-green-600 font-medium">✓ Balanced</span>
                    : <span className="text-xs text-red-600 font-medium">✗ Out of balance by {fmt.format(Math.abs(totalDebit - totalCredit))}</span>
                  }
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}

// ---------- Main Page ----------

export function InvoicesPage() {
  const qc = useQueryClient()
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [showNew, setShowNew] = useState(false)

  const { data: invoices = [], isLoading } = useQuery({
    queryKey: ['billing', 'invoices'],
    queryFn: getInvoices,
  })

  const handleCreated = (inv: InvoiceDetail) => {
    qc.invalidateQueries({ queryKey: ['billing', 'invoices'] })
    qc.setQueryData(['billing', 'invoices', inv.id], inv)
    setShowNew(false)
    setSelectedId(inv.id)
  }

  if (selectedId !== null) {
    return (
      <div className="p-6">
        <InvoiceDetailView id={selectedId} onBack={() => setSelectedId(null)} />
      </div>
    )
  }

  return (
    <div className="p-6 space-y-4">
      <PageHeader
        title="Invoices"
        actions={
          <button
            onClick={() => setShowNew(true)}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white text-sm rounded-md hover:bg-blue-700"
          >
            <Plus className="h-4 w-4" />
            New Invoice
          </button>
        }
      />

      {/* Summary cards */}
      {!isLoading && invoices.length > 0 && (
        <div className="grid grid-cols-3 gap-4">
          <div className="bg-white rounded-lg border border-gray-200 p-4">
            <p className="text-xs text-gray-500">Total Invoices</p>
            <p className="mt-1 text-2xl font-bold text-gray-900">{invoices.length}</p>
          </div>
          <div className="bg-white rounded-lg border border-gray-200 p-4">
            <p className="text-xs text-gray-500">Total Premiums</p>
            <p className="mt-1 text-2xl font-bold text-gray-900">
              {fmt.format(invoices.reduce((s, i) => s + i.grossPremium, 0))}
            </p>
          </div>
          <div className="bg-white rounded-lg border border-gray-200 p-4 flex items-start gap-3">
            <TrendingUp className="h-5 w-5 text-green-600 mt-1 shrink-0" />
            <div>
              <p className="text-xs text-gray-500">Total Billed</p>
              <p className="mt-1 text-2xl font-bold text-gray-900">
                {fmt.format(invoices.reduce((s, i) => s + i.totalAmount, 0))}
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Invoice list */}
      <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
        {isLoading ? (
          <div className="p-8 flex justify-center"><LoadingSpinner /></div>
        ) : invoices.length === 0 ? (
          <div className="p-12 text-center">
            <Receipt className="h-10 w-10 text-gray-300 mx-auto mb-3" />
            <p className="text-sm text-gray-500">No invoices yet. Post the first one.</p>
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-left border-b border-gray-200">
              <tr>
                <th className="px-4 py-3 font-medium text-gray-600">Invoice #</th>
                <th className="px-4 py-3 font-medium text-gray-600">Transaction</th>
                <th className="px-4 py-3 font-medium text-gray-600">Invoice Date</th>
                <th className="px-4 py-3 font-medium text-gray-600">Effective Date</th>
                <th className="px-4 py-3 font-medium text-gray-600 text-right">Gross Premium</th>
                <th className="px-4 py-3 font-medium text-gray-600 text-right">Total Fees</th>
                <th className="px-4 py-3 font-medium text-gray-600 text-right">Total Amount</th>
                <th className="px-4 py-3 font-medium text-gray-600">Status</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {invoices.map(inv => (
                <tr
                  key={inv.id}
                  className="hover:bg-gray-50 cursor-pointer"
                  onClick={() => setSelectedId(inv.id)}
                >
                  <td className="px-4 py-3 font-mono font-medium text-blue-700">{inv.invoiceNumber}</td>
                  <td className="px-4 py-3 text-gray-600">
                    <div className="font-mono text-xs text-gray-800">{inv.policyTransactionNumber ?? 'Unlinked'}</div>
                    {inv.policyVersionNumber != null && <div className="text-xs text-gray-400">v{inv.policyVersionNumber}</div>}
                  </td>
                  <td className="px-4 py-3 text-gray-600">{fmtDate(inv.invoiceDate)}</td>
                  <td className="px-4 py-3 text-gray-600">{fmtDate(inv.effectiveDate)}</td>
                  <td className="px-4 py-3 text-right font-mono">{fmt.format(inv.grossPremium)}</td>
                  <td className="px-4 py-3 text-right font-mono text-gray-500">{fmt.format(inv.totalFees)}</td>
                  <td className="px-4 py-3 text-right font-mono font-semibold">{fmt.format(inv.totalAmount)}</td>
                  <td className="px-4 py-3"><StatusBadge status={inv.status} /></td>
                  <td className="px-4 py-3 text-gray-400"><ChevronRight className="h-4 w-4" /></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {showNew && (
        <NewInvoicePanel
          onClose={() => setShowNew(false)}
          onCreated={handleCreated}
        />
      )}
    </div>
  )
}
