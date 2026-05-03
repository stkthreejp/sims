import { useState, useRef } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Upload, CheckCircle2, AlertCircle, ChevronRight, FileText, X } from 'lucide-react'
import { toast } from 'sonner'
import { payeeStatementsApi } from '@/api/payeeStatements.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { PayeeStatement, PayeeStatementSummary } from '@/types/payeeStatement.types'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
const fmtDate = (s: string) =>
  new Date(s + 'T00:00:00').toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })

function StatusBadge({ status }: { status: string }) {
  const map: Record<string, string> = {
    Imported: 'bg-yellow-100 text-yellow-800',
    Reconciled: 'bg-green-100 text-green-800',
    Voided: 'bg-gray-100 text-gray-500',
  }
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${map[status] ?? 'bg-gray-100 text-gray-700'}`}>
      {status}
    </span>
  )
}

function MatchBadge({ status }: { status: string }) {
  if (status === 'AutoMatched')
    return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium bg-green-100 text-green-800"><CheckCircle2 className="h-3 w-3" />Auto</span>
  if (status === 'ManualMatched')
    return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium bg-blue-100 text-blue-800"><CheckCircle2 className="h-3 w-3" />Manual</span>
  return <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium bg-red-100 text-red-800"><AlertCircle className="h-3 w-3" />Unmatched</span>
}

const EMPTY_IMPORT = { payeeName: '', statementDate: '', referenceNumber: '', apLedgerAccountId: '' }

function ImportPanel({ onImported }: { onImported: (s: PayeeStatement) => void }) {
  const [form, setForm] = useState(EMPTY_IMPORT)
  const [file, setFile] = useState<File | null>(null)
  const fileRef = useRef<HTMLInputElement>(null)

  const { mutate, isPending } = useMutation({
    mutationFn: () => payeeStatementsApi.import(
      {
        payeeName: form.payeeName,
        statementDate: form.statementDate,
        referenceNumber: form.referenceNumber || undefined,
        apLedgerAccountId: parseInt(form.apLedgerAccountId),
      },
      file!
    ),
    onSuccess: (stmt) => {
      toast.success(`Statement imported — ${stmt.lines.length} lines, ${stmt.lines.filter(l => l.matchStatus !== 'Unmatched').length} auto-matched`)
      setForm(EMPTY_IMPORT)
      setFile(null)
      onImported(stmt)
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Import failed'),
  })

  const canSubmit = form.payeeName && form.statementDate && form.apLedgerAccountId && file && !isPending

  return (
    <div className="bg-white border border-slate-200 rounded-lg p-5 space-y-4">
      <h3 className="text-sm font-semibold text-slate-800">Upload Statement</h3>

      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">Payee Name *</label>
          <input
            value={form.payeeName}
            onChange={e => setForm(p => ({ ...p, payeeName: e.target.value }))}
            placeholder="e.g. TX Surplus Lines Stamping Office"
            className="w-full border rounded px-2.5 py-1.5 text-sm"
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">Statement Date *</label>
          <input
            type="date"
            value={form.statementDate}
            onChange={e => setForm(p => ({ ...p, statementDate: e.target.value }))}
            className="w-full border rounded px-2.5 py-1.5 text-sm"
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">Reference #</label>
          <input
            value={form.referenceNumber}
            onChange={e => setForm(p => ({ ...p, referenceNumber: e.target.value }))}
            placeholder="Optional"
            className="w-full border rounded px-2.5 py-1.5 text-sm"
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">AP Ledger Account ID *</label>
          <input
            type="number"
            value={form.apLedgerAccountId}
            onChange={e => setForm(p => ({ ...p, apLedgerAccountId: e.target.value }))}
            placeholder="e.g. 42"
            className="w-full border rounded px-2.5 py-1.5 text-sm"
          />
        </div>
      </div>

      <div>
        <label className="block text-xs font-medium text-slate-600 mb-1">CSV File *</label>
        <div
          onClick={() => fileRef.current?.click()}
          className="border-2 border-dashed border-slate-200 rounded-lg p-4 text-center cursor-pointer hover:border-blue-400 hover:bg-blue-50 transition-colors"
        >
          {file ? (
            <div className="flex items-center justify-center gap-2 text-sm text-slate-700">
              <FileText className="h-4 w-4 text-blue-600" />
              {file.name}
              <button
                onClick={e => { e.stopPropagation(); setFile(null) }}
                className="ml-1 text-slate-400 hover:text-red-500"
              >
                <X className="h-3.5 w-3.5" />
              </button>
            </div>
          ) : (
            <div className="text-sm text-slate-400">
              <Upload className="h-5 w-5 mx-auto mb-1 text-slate-300" />
              Click to select CSV
              <div className="text-xs mt-0.5">PolicyNumber, StateCode, Amount, Description</div>
            </div>
          )}
        </div>
        <input ref={fileRef} type="file" accept=".csv" className="hidden"
          onChange={e => setFile(e.target.files?.[0] ?? null)} />
      </div>

      <button
        onClick={() => mutate()}
        disabled={!canSubmit}
        className="w-full flex items-center justify-center gap-2 px-4 py-2 bg-blue-600 text-white rounded text-sm font-medium hover:bg-blue-700 disabled:opacity-50"
      >
        <Upload className="h-4 w-4" />
        {isPending ? 'Importing…' : 'Import & Auto-Match'}
      </button>
    </div>
  )
}

function StatementDetail({ statement, onClose }: { statement: PayeeStatement; onClose: () => void }) {
  const qc = useQueryClient()

  const postMutation = useMutation({
    mutationFn: () => payeeStatementsApi.post(statement.id),
    onSuccess: (updated) => {
      toast.success('Reconciliation posted')
      qc.setQueryData(['payee-statement', statement.id], updated)
      qc.invalidateQueries({ queryKey: ['payee-statements'] })
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Post failed'),
  })

  const unmatchMutation = useMutation({
    mutationFn: (lineId: number) => payeeStatementsApi.setLineMatch(statement.id, lineId, null),
    onSuccess: (updated) => {
      toast.success('Match cleared')
      qc.setQueryData(['payee-statement', statement.id], updated)
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to clear match'),
  })

  const unmatchedCount = statement.lines.filter(l => l.matchStatus === 'Unmatched').length
  const canPost = statement.status === 'Imported' && unmatchedCount === 0 && statement.lines.length > 0

  return (
    <div className="bg-white border border-slate-200 rounded-lg">
      {/* Header */}
      <div className="flex items-start justify-between px-5 py-4 border-b border-slate-100">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="font-semibold text-slate-900">{statement.payeeName}</h3>
            <StatusBadge status={statement.status} />
          </div>
          <div className="mt-0.5 text-xs text-slate-500 space-x-3">
            <span>{fmtDate(statement.statementDate)}</span>
            {statement.referenceNumber && <span>Ref: {statement.referenceNumber}</span>}
            <span>AP: {statement.apLedgerAccountName}</span>
          </div>
        </div>
        <div className="flex items-center gap-3">
          <div className="text-right">
            <div className="text-lg font-bold text-slate-900">{fmt.format(statement.statementTotal)}</div>
            <div className="text-xs text-slate-500">{statement.lines.filter(l => l.matchStatus !== 'Unmatched').length}/{statement.lines.length} matched</div>
          </div>
          <button onClick={onClose} className="p-1 text-slate-400 hover:text-slate-600">
            <X className="h-4 w-4" />
          </button>
        </div>
      </div>

      {/* Lines grid */}
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-100 bg-slate-50 text-xs text-slate-500">
              <th className="px-4 py-2.5 text-left font-medium">Policy Number</th>
              <th className="px-4 py-2.5 text-left font-medium">State</th>
              <th className="px-4 py-2.5 text-right font-medium">Amount</th>
              <th className="px-4 py-2.5 text-left font-medium">Description</th>
              <th className="px-4 py-2.5 text-left font-medium">Match</th>
              <th className="px-4 py-2.5 text-left font-medium">Matched To</th>
              {statement.status === 'Imported' && <th className="px-4 py-2.5" />}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-50">
            {statement.lines.map(line => (
              <tr key={line.id} className={line.matchStatus === 'Unmatched' ? 'bg-red-50' : ''}>
                <td className="px-4 py-2.5 font-mono text-xs text-blue-700">{line.policyNumber}</td>
                <td className="px-4 py-2.5">{line.stateCode}</td>
                <td className="px-4 py-2.5 text-right font-mono">{fmt.format(line.amount)}</td>
                <td className="px-4 py-2.5 text-slate-500 text-xs">{line.description ?? '—'}</td>
                <td className="px-4 py-2.5"><MatchBadge status={line.matchStatus} /></td>
                <td className="px-4 py-2.5 text-xs text-slate-600">
                  {line.matchedFeeDisplayName
                    ? <span title={`Invoice Line ${line.matchedInvoiceLineId}`}>{line.matchedFeeDisplayName}</span>
                    : <span className="text-slate-300">—</span>}
                </td>
                {statement.status === 'Imported' && (
                  <td className="px-4 py-2.5">
                    {line.matchStatus !== 'Unmatched' && (
                      <button
                        onClick={() => unmatchMutation.mutate(line.id)}
                        disabled={unmatchMutation.isPending}
                        className="text-xs text-slate-400 hover:text-red-600"
                        title="Clear match"
                      >
                        <X className="h-3.5 w-3.5" />
                      </button>
                    )}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-t border-slate-200 bg-slate-50 font-semibold">
              <td colSpan={2} className="px-4 py-2.5 text-xs text-slate-500">Total</td>
              <td className="px-4 py-2.5 text-right font-mono">{fmt.format(statement.statementTotal)}</td>
              <td colSpan={statement.status === 'Imported' ? 4 : 3} />
            </tr>
          </tfoot>
        </table>
      </div>

      {/* Footer actions */}
      {statement.status === 'Imported' && (
        <div className="px-5 py-3 border-t border-slate-100 flex items-center justify-between">
          {unmatchedCount > 0 ? (
            <span className="text-xs text-red-600 flex items-center gap-1">
              <AlertCircle className="h-3.5 w-3.5" />
              {unmatchedCount} unmatched line{unmatchedCount > 1 ? 's' : ''} — resolve before posting
            </span>
          ) : (
            <span className="text-xs text-green-700 flex items-center gap-1">
              <CheckCircle2 className="h-3.5 w-3.5" />
              All lines matched — ready to post
            </span>
          )}
          <button
            onClick={() => postMutation.mutate()}
            disabled={!canPost || postMutation.isPending}
            className="flex items-center gap-2 px-4 py-1.5 bg-green-600 text-white rounded text-sm font-medium hover:bg-green-700 disabled:opacity-50"
          >
            <CheckCircle2 className="h-4 w-4" />
            {postMutation.isPending ? 'Posting…' : 'Post Reconciliation'}
          </button>
        </div>
      )}
    </div>
  )
}

export function StatementReconciliationPage() {
  const qc = useQueryClient()
  const [selectedId, setSelectedId] = useState<number | null>(null)

  const { data: statements = [], isLoading } = useQuery({
    queryKey: ['payee-statements'],
    queryFn: payeeStatementsApi.getAll,
  })

  const { data: detail } = useQuery({
    queryKey: ['payee-statement', selectedId],
    queryFn: () => payeeStatementsApi.getById(selectedId!),
    enabled: selectedId !== null,
  })

  const handleImported = (stmt: PayeeStatement) => {
    qc.invalidateQueries({ queryKey: ['payee-statements'] })
    qc.setQueryData(['payee-statement', stmt.id], stmt)
    setSelectedId(stmt.id)
  }

  if (isLoading) return <LoadingSpinner />

  return (
    <div className="space-y-6">
      <PageHeader
        title="Statement Reconciliation"
        subtitle="Import payee statements · auto-match to invoice fee lines · post JEs"
      />

      <div className="grid grid-cols-3 gap-6">
        {/* Left: list + upload */}
        <div className="space-y-4">
          <ImportPanel onImported={handleImported} />

          {statements.length > 0 && (
            <div className="bg-white border border-slate-200 rounded-lg divide-y divide-slate-100">
              {statements.map(s => (
                <button
                  key={s.id}
                  onClick={() => setSelectedId(s.id)}
                  className={`w-full text-left px-4 py-3 hover:bg-slate-50 transition-colors ${selectedId === s.id ? 'bg-blue-50' : ''}`}
                >
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-medium text-slate-800 truncate">{s.payeeName}</span>
                    <StatusBadge status={s.status} />
                  </div>
                  <div className="mt-0.5 flex items-center justify-between text-xs text-slate-500">
                    <span>{fmtDate(s.statementDate)}</span>
                    <span className="font-mono">{fmt.format(s.statementTotal)}</span>
                  </div>
                  <div className="mt-0.5 flex items-center gap-1 text-xs">
                    <span className={s.matchedLines === s.totalLines ? 'text-green-600' : 'text-amber-600'}>
                      {s.matchedLines}/{s.totalLines} matched
                    </span>
                    <ChevronRight className="h-3 w-3 text-slate-300 ml-auto" />
                  </div>
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Right: detail */}
        <div className="col-span-2">
          {detail ? (
            <StatementDetail statement={detail} onClose={() => setSelectedId(null)} />
          ) : (
            <div className="bg-white border border-slate-200 rounded-lg p-12 text-center text-slate-400">
              <FileText className="h-10 w-10 mx-auto mb-2 text-slate-200" />
              <p className="text-sm">Select a statement or upload a new one</p>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
