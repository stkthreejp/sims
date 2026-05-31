import { useState, useRef } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Upload, CheckCircle2, AlertCircle, ChevronRight, FileText, X } from 'lucide-react'
import { toast } from 'sonner'
import { payeeStatementsApi } from '@/api/payeeStatements.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { StatusBadge } from '@/components/common/StatusBadge'
import type { PayeeStatement, PayeeStatementSummary } from '@/types/payeeStatement.types'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
const fmtDate = (s: string) =>
  new Date(s + 'T00:00:00').toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })

const STMT_PILL: Record<string, string> = {
  Imported: 'inprogress',
  Reconciled: 'bound',
  Voided: 'voided',
}

const MATCH_PILL: Record<string, { variant: string; label: string }> = {
  AutoMatched: { variant: 'bound', label: 'Auto' },
  ManualMatched: { variant: 'submitted', label: 'Manual' },
  Unmatched: { variant: 'cancelled', label: 'Unmatched' },
}

function MatchBadge({ status }: { status: string }) {
  const cfg = MATCH_PILL[status] ?? { variant: 'draft', label: status }
  return (
    <span className={`sd-pill ${cfg.variant}`} style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
      {status === 'Unmatched'
        ? <AlertCircle style={{ width: 10, height: 10 }} />
        : <CheckCircle2 style={{ width: 10, height: 10 }} />}
      {cfg.label}
    </span>
  )
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
    <div className="sd-card">
      <div className="sd-card-head"><h3>Upload Statement</h3></div>
      <div className="sd-card-body">
        <div className="sims-fields" style={{ gridTemplateColumns: '1fr 1fr', marginBottom: 12 }}>
          <label className="sims-field">
            <span className="sims-field-label">Payee Name *</span>
            <input
              value={form.payeeName}
              onChange={e => setForm(p => ({ ...p, payeeName: e.target.value }))}
              placeholder="e.g. TX Surplus Lines Stamping Office"
              className="sims-input"
            />
          </label>
          <label className="sims-field">
            <span className="sims-field-label">Statement Date *</span>
            <input type="date" value={form.statementDate} onChange={e => setForm(p => ({ ...p, statementDate: e.target.value }))} className="sims-input" />
          </label>
          <label className="sims-field">
            <span className="sims-field-label">Reference #</span>
            <input value={form.referenceNumber} onChange={e => setForm(p => ({ ...p, referenceNumber: e.target.value }))} placeholder="Optional" className="sims-input" />
          </label>
          <label className="sims-field">
            <span className="sims-field-label">AP Ledger Account ID *</span>
            <input type="number" value={form.apLedgerAccountId} onChange={e => setForm(p => ({ ...p, apLedgerAccountId: e.target.value }))} placeholder="e.g. 42" className="sims-input" />
          </label>
        </div>

        <div style={{ marginBottom: 12 }}>
          <p className="sims-field-label" style={{ marginBottom: 6 }}>CSV File *</p>
          <div
            onClick={() => fileRef.current?.click()}
            style={{
              border: '2px dashed var(--line)',
              borderRadius: 'var(--r-lg)',
              padding: '14px 16px',
              textAlign: 'center',
              cursor: 'pointer',
              transition: 'border-color .1s, background .1s',
            }}
          >
            {file ? (
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8, fontSize: 13, color: 'var(--ink-2)' }}>
                <FileText style={{ width: 14, height: 14, color: 'var(--accent-ink)' }} />
                {file.name}
                <button
                  onClick={e => { e.stopPropagation(); setFile(null) }}
                  style={{ marginLeft: 4, color: 'var(--ink-4)', background: 'none', border: 0, cursor: 'pointer', display: 'grid', placeItems: 'center' }}
                >
                  <X style={{ width: 13, height: 13 }} />
                </button>
              </div>
            ) : (
              <div style={{ fontSize: 13, color: 'var(--ink-3)' }}>
                <Upload style={{ width: 18, height: 18, margin: '0 auto 4px', color: 'var(--ink-4)' }} />
                Click to select CSV
                <div style={{ fontSize: 11, marginTop: 2, color: 'var(--ink-4)' }}>PolicyNumber, StateCode, Amount, Description</div>
              </div>
            )}
          </div>
          <input ref={fileRef} type="file" accept=".csv" style={{ display: 'none' }} onChange={e => setFile(e.target.files?.[0] ?? null)} />
        </div>

        <button className="sd-btn primary" style={{ width: '100%', justifyContent: 'center' }} onClick={() => mutate()} disabled={!canSubmit}>
          <Upload style={{ width: 13, height: 13 }} />
          {isPending ? 'Importing…' : 'Import & Auto-Match'}
        </button>
      </div>
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
    <div className="sd-card" style={{ overflow: 'hidden' }}>
      {/* Header */}
      <div className="sd-card-head" style={{ justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <h3>{statement.payeeName}</h3>
          <StatusBadge status={STMT_PILL[statement.status] ?? 'draft'} label={statement.status} />
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
          <div style={{ textAlign: 'right' }}>
            <div style={{ fontSize: 17, fontWeight: 700, color: 'var(--ink)' }}>{fmt.format(statement.statementTotal)}</div>
            <div style={{ fontSize: 11, color: 'var(--ink-4)' }}>{statement.lines.filter(l => l.matchStatus !== 'Unmatched').length}/{statement.lines.length} matched</div>
          </div>
          <button onClick={onClose} style={{ color: 'var(--ink-3)', background: 'none', border: 0, cursor: 'pointer', display: 'grid', placeItems: 'center' }}>
            <X style={{ width: 14, height: 14 }} />
          </button>
        </div>
      </div>

      <div style={{ padding: '6px 16px 8px', display: 'flex', gap: 16, fontSize: 12, color: 'var(--ink-3)', borderBottom: '1px solid var(--line-2)' }}>
        <span>{fmtDate(statement.statementDate)}</span>
        {statement.referenceNumber && <span>Ref: {statement.referenceNumber}</span>}
        <span>AP: {statement.apLedgerAccountName}</span>
      </div>

      {/* Lines */}
      <div style={{ overflowX: 'auto' }}>
        <table className="sd-table">
          <thead>
            <tr>
              <th>Policy Number</th>
              <th>State</th>
              <th className="num">Amount</th>
              <th>Description</th>
              <th>Match</th>
              <th>Matched To</th>
              {statement.status === 'Imported' && <th style={{ width: 32 }} />}
            </tr>
          </thead>
          <tbody>
            {statement.lines.map(line => (
              <tr key={line.id} style={{ background: line.matchStatus === 'Unmatched' ? 'var(--bad-bg)' : undefined, cursor: 'default' }}>
                <td className="id">{line.policyNumber}</td>
                <td style={{ color: 'var(--ink-2)' }}>{line.stateCode}</td>
                <td className="num">{fmt.format(line.amount)}</td>
                <td style={{ color: 'var(--ink-3)', fontSize: 12 }}>{line.description ?? '—'}</td>
                <td><MatchBadge status={line.matchStatus} /></td>
                <td style={{ fontSize: 12, color: 'var(--ink-2)' }}>
                  {line.matchedFeeDisplayName
                    ? <span title={`Invoice Line ${line.matchedInvoiceLineId}`}>{line.matchedFeeDisplayName}</span>
                    : <span style={{ color: 'var(--ink-4)' }}>—</span>}
                </td>
                {statement.status === 'Imported' && (
                  <td>
                    {line.matchStatus !== 'Unmatched' && (
                      <button
                        onClick={() => unmatchMutation.mutate(line.id)}
                        disabled={unmatchMutation.isPending}
                        title="Clear match"
                        style={{ color: 'var(--ink-4)', background: 'none', border: 0, cursor: 'pointer', display: 'grid', placeItems: 'center' }}
                      >
                        <X style={{ width: 13, height: 13 }} />
                      </button>
                    )}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
          <tfoot style={{ borderTop: '2px solid var(--line)', background: 'var(--surface-2)' }}>
            <tr>
              <td colSpan={2} style={{ padding: '10px 14px', fontWeight: 700, color: 'var(--ink-2)', fontSize: 13 }}>Total</td>
              <td style={{ padding: '10px 14px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--ink)' }}>{fmt.format(statement.statementTotal)}</td>
              <td colSpan={statement.status === 'Imported' ? 4 : 3} />
            </tr>
          </tfoot>
        </table>
      </div>

      {/* Footer */}
      {statement.status === 'Imported' && (
        <div style={{ padding: '10px 16px', borderTop: '1px solid var(--line-2)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          {unmatchedCount > 0 ? (
            <span style={{ fontSize: 12, color: 'var(--bad-fg)', display: 'flex', alignItems: 'center', gap: 5 }}>
              <AlertCircle style={{ width: 13, height: 13 }} />
              {unmatchedCount} unmatched line{unmatchedCount > 1 ? 's' : ''} — resolve before posting
            </span>
          ) : (
            <span style={{ fontSize: 12, color: 'var(--pill-bound-fg)', display: 'flex', alignItems: 'center', gap: 5 }}>
              <CheckCircle2 style={{ width: 13, height: 13 }} />
              All lines matched — ready to post
            </span>
          )}
          <button className="sd-btn primary" onClick={() => postMutation.mutate()} disabled={!canPost || postMutation.isPending}>
            <CheckCircle2 style={{ width: 13, height: 13 }} />
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
    <div className="subs-wrap">
      <div className="subs-page-head" style={{ marginBottom: 20 }}>
        <PageHeader
          title="Statement Reconciliation"
          subtitle="Import payee statements · auto-match to invoice fee lines · post JEs"
        />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: 20, alignItems: 'flex-start' }}>
        {/* Left: upload + list */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <ImportPanel onImported={handleImported} />

          {statements.length > 0 && (
            <div className="sd-card" style={{ overflow: 'hidden' }}>
              {statements.map(s => (
                <button
                  key={s.id}
                  onClick={() => setSelectedId(s.id)}
                  style={{
                    width: '100%',
                    textAlign: 'left',
                    padding: '10px 14px',
                    background: selectedId === s.id ? 'var(--accent-soft)' : 'transparent',
                    border: 0,
                    borderBottom: '1px solid var(--line-2)',
                    cursor: 'pointer',
                    transition: 'background .1s',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 3 }}>
                    <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: 160 }}>{s.payeeName}</span>
                    <StatusBadge status={STMT_PILL[s.status] ?? 'draft'} label={s.status} />
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: 11.5, color: 'var(--ink-3)' }}>
                    <span>{fmtDate(s.statementDate)}</span>
                    <span style={{ fontFamily: 'var(--font-mono)' }}>{fmt.format(s.statementTotal)}</span>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 11.5, marginTop: 2 }}>
                    <span style={{ color: s.matchedLines === s.totalLines ? 'var(--pill-bound-fg)' : 'var(--warn-fg)' }}>
                      {s.matchedLines}/{s.totalLines} matched
                    </span>
                    <ChevronRight style={{ width: 11, height: 11, color: 'var(--ink-4)', marginLeft: 'auto' }} />
                  </div>
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Right: detail */}
        <div>
          {detail ? (
            <StatementDetail statement={detail} onClose={() => setSelectedId(null)} />
          ) : (
            <div className="sd-card" style={{ padding: '40px 16px', textAlign: 'center', color: 'var(--ink-3)' }}>
              <FileText style={{ width: 36, height: 36, margin: '0 auto 10px', color: 'var(--ink-4)' }} />
              <p style={{ fontSize: 13 }}>Select a statement or upload a new one</p>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
