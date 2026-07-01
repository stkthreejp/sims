import { useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { FileUp } from 'lucide-react'
import { toast } from 'sonner'
import { PageHeader } from '@/components/common/PageHeader'
import { usePermissions } from '@/hooks/usePermissions'
import { getClaims, getImportBatches, importClaims } from '@/api/claims.api'
import type { ClaimStatus, ImportClaimsRequest, UnifiedClaimImportRow } from '@/types/claim.types'

const STATUSES: ClaimStatus[] = ['Open', 'Closed', 'Reopened', 'Denied', 'Subrogation', 'Withdrawn']

function money(value: number) {
  return value.toLocaleString('en-US', { style: 'currency', currency: 'USD' })
}

function fmtDate(value?: string | null) {
  if (!value) return '—'
  return new Date(`${value}T00:00:00`).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

// ── CSV parsing (Unified_Claims_Import layout) ───────────────────────────────

function parseCsv(text: string): string[][] {
  const rows: string[][] = []
  let row: string[] = []
  let field = ''
  let inQuotes = false
  for (let i = 0; i < text.length; i++) {
    const ch = text[i]
    if (inQuotes) {
      if (ch === '"' && text[i + 1] === '"') { field += '"'; i++ }
      else if (ch === '"') inQuotes = false
      else field += ch
    } else if (ch === '"') {
      inQuotes = true
    } else if (ch === ',') {
      row.push(field); field = ''
    } else if (ch === '\n' || ch === '\r') {
      if (ch === '\r' && text[i + 1] === '\n') i++
      row.push(field); field = ''
      if (row.some((f) => f.length > 0)) rows.push(row)
      row = []
    } else {
      field += ch
    }
  }
  row.push(field)
  if (row.some((f) => f.length > 0)) rows.push(row)
  return rows
}

const HEADER_MAP: Record<string, keyof UnifiedClaimImportRow> = {
  claimnumber: 'claimNumber',
  account: 'account',
  claimstatusdesc: 'claimStatusDesc',
  adjustername: 'adjusterName',
  claimtypedesc: 'claimTypeDesc',
  claimantname: 'claimantName',
  dateofclaim: 'dateOfClaim',
  datereported: 'dateReported',
  carriername: 'carrierName',
  carrierpolicynum: 'carrierPolicyNum',
  carriereffectivedate: 'carrierEffectiveDate',
  namedinsured: 'namedInsured',
  accidentcausedesc: 'accidentCauseDesc',
  accidentdescription: 'accidentDescription',
  riskstate: 'riskState',
  accidentstate: 'accidentState',
  totallosspaid: 'totalLossPaid',
  totalexppaid: 'totalExpPaid',
  totalosloss: 'totalOsLoss',
  totalosexp: 'totalOsExp',
  totalrecovery: 'totalRecovery',
  totalincurred: 'totalIncurred',
  lob: 'lob',
  valuedate: 'valueDate',
}

const NUMERIC_FIELDS = new Set<keyof UnifiedClaimImportRow>([
  'totalLossPaid', 'totalExpPaid', 'totalOsLoss', 'totalOsExp', 'totalRecovery', 'totalIncurred',
])

function rowsFromCsv(text: string): UnifiedClaimImportRow[] {
  const grid = parseCsv(text)
  if (grid.length < 2) return []
  const headers = grid[0].map((h) => HEADER_MAP[h.replace(/[\s_]/g, '').toLowerCase()] ?? null)
  return grid.slice(1).map((cells) => {
    const row: Record<string, unknown> = {}
    headers.forEach((key, idx) => {
      if (!key) return
      const raw = cells[idx]?.trim()
      if (!raw) return
      if (NUMERIC_FIELDS.has(key)) {
        const n = Number(raw.replace(/[$,()]/g, (m) => (m === '(' ? '-' : m === ')' ? '' : '')))
        if (!Number.isNaN(n)) row[key] = n
      } else {
        row[key] = raw
      }
    })
    return row as UnifiedClaimImportRow
  })
}

// ── status pill ──────────────────────────────────────────────────────────────

function StatusPill({ status }: { status: ClaimStatus }) {
  const color =
    status === 'Open' || status === 'Reopened' ? 'var(--warn-fg)'
    : status === 'Closed' ? 'var(--ink-3)'
    : 'var(--danger)'
  return (
    <span style={{
      fontSize: 11, fontWeight: 600, padding: '2px 8px', borderRadius: 999,
      border: '1px solid var(--line)', color,
    }}>
      {status}
    </span>
  )
}

// ── page ─────────────────────────────────────────────────────────────────────

type Tab = 'claims' | 'imports'

export function ClaimsPage() {
  const qc = useQueryClient()
  const { canManageClaims } = usePermissions()
  const [tab, setTab] = useState<Tab>('claims')
  const [status, setStatus] = useState<ClaimStatus | ''>('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const fileInput = useRef<HTMLInputElement>(null)
  const [importMeta, setImportMeta] = useState({ carrierName: '', tpaName: '', valuationDate: '' })

  const { data: claims = [], isLoading, isError } = useQuery({
    queryKey: ['claims', status, fromDate, toDate],
    queryFn: () => getClaims({
      status: status || undefined,
      fromDate: fromDate || undefined,
      toDate: toDate || undefined,
    }),
  })

  const { data: batches = [] } = useQuery({
    queryKey: ['claims', 'import-batches'],
    queryFn: getImportBatches,
    enabled: tab === 'imports',
  })

  const importMutation = useMutation({
    mutationFn: (req: ImportClaimsRequest) => importClaims(req),
    onSuccess: (batch) => {
      qc.invalidateQueries({ queryKey: ['claims'] })
      const note = batch.errorCount > 0 ? ` (${batch.errorCount} errors — see batch detail)` : ''
      toast.success(`Imported ${batch.fileName}: ${batch.createdCount} created, ${batch.updatedCount} updated, ${batch.skippedCount} skipped${note}`)
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Import failed'),
  })

  const totals = useMemo(() => ({
    paid: claims.reduce((s, c) => s + c.paid, 0),
    reserved: claims.reduce((s, c) => s + c.reserved, 0),
    expense: claims.reduce((s, c) => s + c.expense, 0),
    incurred: claims.reduce((s, c) => s + c.incurred, 0),
  }), [claims])

  async function onFileChosen(file: File) {
    if (!importMeta.valuationDate) {
      toast.error('Set the valuation date before importing')
      return
    }
    const text = await file.text()
    const rows = rowsFromCsv(text)
    if (rows.length === 0) {
      toast.error('No data rows found — check the file uses the Unified_Claims_Import headers')
      return
    }
    importMutation.mutate({
      fileName: file.name,
      carrierName: importMeta.carrierName || undefined,
      tpaName: importMeta.tpaName || undefined,
      valuationDate: importMeta.valuationDate,
      rows,
    })
  }

  return (
    <div>
      <PageHeader
        title="Claims"
        subtitle="Imported and manually entered claims across the book. Loss runs are generated from policy or insured detail pages."
      />

      <div style={{ display: 'flex', gap: 6, marginBottom: 14 }}>
        {(['claims', 'imports'] as Tab[]).map((t) => (
          <button
            key={t}
            className={`sd-btn ${tab === t ? '' : 'outline'}`}
            onClick={() => setTab(t)}
          >
            {t === 'claims' ? 'Claims' : 'Import History'}
          </button>
        ))}
      </div>

      {tab === 'claims' && (
        <div className="admin-panel">
          <div style={{ display: 'flex', gap: 10, alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: 14 }}>
            <label style={{ fontSize: 12 }}>
              Status<br />
              <select className="sd-input" value={status} onChange={(e) => setStatus(e.target.value as ClaimStatus | '')}>
                <option value="">All</option>
                {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
              </select>
            </label>
            <label style={{ fontSize: 12 }}>
              Loss date from<br />
              <input type="date" className="sd-input" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
            </label>
            <label style={{ fontSize: 12 }}>
              Loss date to<br />
              <input type="date" className="sd-input" value={toDate} onChange={(e) => setToDate(e.target.value)} />
            </label>
            <div style={{ flex: 1 }} />
            <div style={{ display: 'flex', gap: 14, fontSize: 12.5, color: 'var(--ink-3)' }}>
              <span>Paid <strong style={{ color: 'var(--ink)' }}>{money(totals.paid)}</strong></span>
              <span>Reserved <strong style={{ color: 'var(--ink)' }}>{money(totals.reserved)}</strong></span>
              <span>Expense <strong style={{ color: 'var(--ink)' }}>{money(totals.expense)}</strong></span>
              <span>Incurred <strong style={{ color: 'var(--ink)' }}>{money(totals.incurred)}</strong></span>
            </div>
          </div>

          {isError ? (
            <div className="sd-form-error" style={{ padding: 12 }}>Could not load claims. Refresh to retry.</div>
          ) : isLoading ? (
            <div style={{ padding: 24, color: 'var(--ink-4)', fontSize: 13 }}>Loading…</div>
          ) : claims.length === 0 ? (
            <div style={{ padding: 24, color: 'var(--ink-4)', fontSize: 13 }}>No claims match the current filters.</div>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table className="sd-table">
                <thead>
                  <tr>
                    <th>Claim #</th>
                    <th>Policy</th>
                    <th>Insured</th>
                    <th>Date of Loss</th>
                    <th>Status</th>
                    <th>Cause</th>
                    <th style={{ textAlign: 'right' }}>Paid</th>
                    <th style={{ textAlign: 'right' }}>Reserved</th>
                    <th style={{ textAlign: 'right' }}>Expense</th>
                    <th style={{ textAlign: 'right' }}>Incurred</th>
                    <th>Valued</th>
                  </tr>
                </thead>
                <tbody>
                  {claims.map((c) => (
                    <tr key={c.id}>
                      <td>
                        {c.claimNumber}
                        {c.isManualEntry && <span style={{ marginLeft: 6, fontSize: 9.5, color: 'var(--ink-4)', textTransform: 'uppercase', fontWeight: 700 }}>manual</span>}
                      </td>
                      <td>{c.policyNumber ?? c.sourcePolicyReference ?? '—'}</td>
                      <td>{c.insuredName ?? '—'}</td>
                      <td>{fmtDate(c.dateOfLoss)}</td>
                      <td><StatusPill status={c.status} /></td>
                      <td style={{ maxWidth: 220, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{c.lossCause ?? '—'}</td>
                      <td style={{ textAlign: 'right' }}>{money(c.paid)}</td>
                      <td style={{ textAlign: 'right' }}>{money(c.reserved)}</td>
                      <td style={{ textAlign: 'right' }}>{money(c.expense)}</td>
                      <td style={{ textAlign: 'right' }}>{money(c.incurred)}</td>
                      <td>{fmtDate(c.lastValuationDate)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {tab === 'imports' && (
        <div className="admin-panel">
          {canManageClaims && (
            <div style={{ display: 'flex', gap: 10, alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: 16, paddingBottom: 14, borderBottom: '1px solid var(--line)' }}>
              <label style={{ fontSize: 12 }}>
                Valuation date *<br />
                <input type="date" className="sd-input" value={importMeta.valuationDate}
                  onChange={(e) => setImportMeta((m) => ({ ...m, valuationDate: e.target.value }))} />
              </label>
              <label style={{ fontSize: 12 }}>
                Carrier<br />
                <input className="sd-input" placeholder="e.g. Brace" value={importMeta.carrierName}
                  onChange={(e) => setImportMeta((m) => ({ ...m, carrierName: e.target.value }))} />
              </label>
              <label style={{ fontSize: 12 }}>
                TPA<br />
                <input className="sd-input" placeholder="e.g. Sedgwick" value={importMeta.tpaName}
                  onChange={(e) => setImportMeta((m) => ({ ...m, tpaName: e.target.value }))} />
              </label>
              <button
                className="sd-btn"
                disabled={importMutation.isPending}
                onClick={() => fileInput.current?.click()}
              >
                <FileUp size={14} /> {importMutation.isPending ? 'Importing…' : 'Import CSV'}
              </button>
              <input
                ref={fileInput}
                type="file"
                accept=".csv,text/csv"
                style={{ display: 'none' }}
                onChange={(e) => {
                  const f = e.target.files?.[0]
                  if (f) onFileChosen(f)
                  e.target.value = ''
                }}
              />
              <span style={{ fontSize: 11.5, color: 'var(--ink-4)' }}>
                Unified_Claims_Import column layout. Rows colliding with manual claims are skipped.
              </span>
            </div>
          )}

          {batches.length === 0 ? (
            <div style={{ padding: 24, color: 'var(--ink-4)', fontSize: 13 }}>No import batches yet.</div>
          ) : (
            <table className="sd-table">
              <thead>
                <tr>
                  <th>File</th>
                  <th>Carrier / TPA</th>
                  <th>Valuation</th>
                  <th style={{ textAlign: 'right' }}>Rows</th>
                  <th style={{ textAlign: 'right' }}>Created</th>
                  <th style={{ textAlign: 'right' }}>Updated</th>
                  <th style={{ textAlign: 'right' }}>Skipped</th>
                  <th style={{ textAlign: 'right' }}>Errors</th>
                  <th>Status</th>
                  <th>By</th>
                  <th>When</th>
                </tr>
              </thead>
              <tbody>
                {batches.map((b) => (
                  <tr key={b.id} title={b.errorSummaryJson ? JSON.parse(b.errorSummaryJson).join('\n') : undefined}>
                    <td>{b.fileName}</td>
                    <td>{[b.carrierName, b.tpaName].filter(Boolean).join(' / ') || '—'}</td>
                    <td>{fmtDate(b.valuationDate)}</td>
                    <td style={{ textAlign: 'right' }}>{b.recordCount}</td>
                    <td style={{ textAlign: 'right' }}>{b.createdCount}</td>
                    <td style={{ textAlign: 'right' }}>{b.updatedCount}</td>
                    <td style={{ textAlign: 'right' }}>{b.skippedCount}</td>
                    <td style={{ textAlign: 'right', color: b.errorCount > 0 ? 'var(--danger)' : undefined }}>{b.errorCount}</td>
                    <td>{b.status}</td>
                    <td>{b.importedByName || '—'}</td>
                    <td>{new Date(b.createdAt).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  )
}
