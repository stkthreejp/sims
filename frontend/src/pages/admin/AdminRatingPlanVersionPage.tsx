import { useState, useMemo, useRef } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useParams, Link } from 'react-router-dom'
import {
  ArrowLeft, CheckCircle, XCircle, Search, ChevronDown, ChevronRight,
  Pencil, Save, X, Upload, BarChart2, RefreshCw, AlertTriangle,
} from 'lucide-react'
import { toast } from 'sonner'
import { ratingApi } from '@/api/rating.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { useAuthStore } from '@/store/authStore'
import type {
  PlanStatus, FactorTable, FactorRow, RatingImpactPreview, UpdateVersionMetaDto,
} from '@/types/rating.types'

type Tab = 'schedule' | 'factors' | 'eligibility' | 'impact' | 'audit'

function StatusBadge({ status }: { status: PlanStatus }) {
  const map: Record<PlanStatus, { label: string; style: React.CSSProperties }> = {
    Active:  { label: 'Active',  style: { background: 'var(--good-bg)', color: 'var(--good-fg)' } },
    Draft:   { label: 'Draft',   style: { background: 'var(--warn-bg)', color: 'var(--warn-fg)' } },
    Retired: { label: 'Retired', style: { background: 'var(--surface-2)', color: 'var(--ink-3)' } },
  }
  const { label, style } = map[status]
  return <span className="px-2 py-0.5 rounded-full text-xs font-medium" style={style}>{label}</span>
}

// ─── Editable factor table panel ─────────────────────────────────────────────

function FactorTablePanel({
  table,
  isDraft,
  versionId,
  onSaved,
}: {
  table: FactorTable
  isDraft: boolean
  versionId: string
  onSaved: () => void
}) {
  const [open, setOpen] = useState(true)
  const [editMode, setEditMode] = useState(false)
  const [search, setSearch] = useState('')
  const [editedFactors, setEditedFactors] = useState<Record<string, string>>({})
  const [pasteMode, setPasteMode] = useState(false)
  const [pasteText, setPasteText] = useState('')
  const [parsedPaste, setParsedPaste] = useState<{ dims: Record<string, string>; factor: string }[] | null>(null)

  const saveMutation = useMutation({
    mutationFn: () => {
      const rows = table.rows.map((r) => ({
        dimensionValues: r.dimensionValues,
        factor: parseFloat(editedFactors[r.id] ?? String(r.factor)),
      }))
      return ratingApi.updateFactorTable(versionId, table.code, { rows })
    },
    onSuccess: () => {
      toast.success(`${table.code} saved`)
      setEditMode(false)
      setEditedFactors({})
      onSaved()
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Save failed'),
  })

  // A9(a): block save when any factor cell is non-numeric/NaN.
  function handleSave() {
    const bad = table.rows.find((r) => !Number.isFinite(parseFloat(editedFactors[r.id] ?? String(r.factor))))
    if (bad) {
      const label = table.dimensionNames.map((d) => bad.dimensionValues[d] ?? '—').join(' / ')
      toast.error(`Non-numeric factor for row "${label}". Fix all factor cells before saving.`)
      return
    }
    saveMutation.mutate()
  }

  const confirmPasteMutation = useMutation({
    mutationFn: () => {
      if (!parsedPaste) throw new Error('No data')
      return ratingApi.updateFactorTable(versionId, table.code, { rows: parsedPaste.map((r) => ({ dimensionValues: r.dims, factor: parseFloat(r.factor) })) })
    },
    onSuccess: () => {
      toast.success(`${table.code} updated from paste`)
      setPasteMode(false)
      setPasteText('')
      setParsedPaste(null)
      onSaved()
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Save failed'),
  })

  function handleParsePaste() {
    const lines = pasteText.trim().split('\n').filter(Boolean)
    if (lines.length < 2) { toast.error('Paste at least a header row and one data row.'); return }
    const sep = lines[0].includes('\t') ? '\t' : ','
    const headers = lines[0].split(sep).map((h) => h.trim())
    const factorIdx = headers.findIndex((h) => h.toLowerCase() === 'factor')
    if (factorIdx < 0) { toast.error("Paste data must have a 'factor' column."); return }
    const dimHeaders = headers.filter((_, i) => i !== factorIdx)
    // A9(b): reject when pasted dimension columns don't match this table's dimensions —
    // otherwise the rows are stored with keys the rater will never match against.
    const expected = [...table.dimensionNames].sort()
    const got = [...dimHeaders].sort()
    if (expected.length !== got.length || expected.some((d, i) => d !== got[i])) {
      toast.error(`Paste columns must match this table's dimensions: ${table.dimensionNames.join(', ')} (plus factor). Got: ${dimHeaders.join(', ') || '(none)'}.`)
      return
    }
    const parsed = lines.slice(1).map((line) => {
      const cols = line.split(sep).map((c) => c.trim())
      const dims: Record<string, string> = {}
      dimHeaders.forEach((h, i) => { dims[h] = cols[i] ?? '' })
      return { dims, factor: cols[factorIdx] ?? '0' }
    })
    setParsedPaste(parsed)
  }

  const filteredRows = useMemo(() => {
    if (!search.trim()) return table.rows
    const q = search.toLowerCase()
    return table.rows.filter((r) =>
      Object.values(r.dimensionValues).some((v) => v.toLowerCase().includes(q)) ||
      String(r.factor).includes(q)
    )
  }, [table.rows, search])

  const currentFactor = (row: FactorRow) =>
    editedFactors[row.id] !== undefined ? editedFactors[row.id] : row.factor.toFixed(4)

  return (
    <div className="border rounded-lg overflow-hidden">
      <div className="w-full flex items-center justify-between px-4 py-3" style={{ background: 'var(--surface-2)' }}>
        <button
          onClick={() => setOpen((o) => !o)}
          className="flex items-center gap-2 text-left flex-1"
        >
          {open ? <ChevronDown className="h-4 w-4" style={{ color: 'var(--ink-4)' }} /> : <ChevronRight className="h-4 w-4" style={{ color: 'var(--ink-4)' }} />}
          <span className="text-sm font-semibold font-mono" style={{ color: 'var(--ink-2)' }}>{table.code}</span>
          <span className="text-xs" style={{ color: 'var(--ink-4)' }}>{table.rows.length} rows · {table.dimensionNames.join(', ')}</span>
          <span className="text-xs px-1.5 py-0.5 rounded" style={{ background: 'var(--surface-2)', color: 'var(--ink-3)' }}>{table.valueSemantics}</span>
        </button>

        {isDraft && open && (
          <div className="flex items-center gap-1.5 shrink-0">
            {editMode ? (
              <>
                <button
                  onClick={() => { setEditMode(false); setEditedFactors({}) }}
                  className="flex items-center gap-1 px-2 py-1 text-xs border rounded" style={{ color: 'var(--ink-3)' }}
                >
                  <X className="h-3 w-3" /> Cancel
                </button>
                <button
                  onClick={handleSave}
                  disabled={saveMutation.isPending}
                  className="sd-btn primary flex items-center gap-1 px-2 py-1 text-xs rounded disabled:opacity-50"
                >
                  <Save className="h-3 w-3" /> {saveMutation.isPending ? 'Saving…' : 'Save'}
                </button>
              </>
            ) : (
              <>
                <button
                  onClick={() => { setPasteMode((p) => !p); setEditMode(false) }}
                  className="flex items-center gap-1 px-2 py-1 text-xs border rounded" style={{ color: 'var(--ink-3)' }}
                >
                  <Upload className="h-3 w-3" /> Paste
                </button>
                <button
                  onClick={() => { setEditMode(true); setPasteMode(false) }}
                  className="flex items-center gap-1 px-2 py-1 text-xs border rounded" style={{ color: 'var(--ink-3)' }}
                >
                  <Pencil className="h-3 w-3" /> Edit
                </button>
              </>
            )}
          </div>
        )}
      </div>

      {open && (
        <div>
          {/* Paste mode */}
          {pasteMode && (
            <div className="px-4 py-3 border-b space-y-2" style={{ background: 'var(--warn-bg)' }}>
              <p className="text-xs font-medium" style={{ color: 'var(--warn-fg)' }}>
                Paste tab-separated or CSV data (header row required, include a <code className="px-1 rounded" style={{ background: 'var(--warn-bg)' }}>factor</code> column).
              </p>
              <textarea
                value={pasteText}
                onChange={(e) => { setPasteText(e.target.value); setParsedPaste(null) }}
                rows={5}
                placeholder="equipment_type&#9;age_band&#9;factor&#10;1&#9;1-3&#9;0.8500&#10;1&#9;4-7&#9;0.5000"
                className="w-full px-3 py-2 text-xs font-mono border rounded resize-y"
              />
              <div className="flex gap-2">
                <button
                  onClick={handleParsePaste}
                  disabled={!pasteText.trim()}
                  className="px-3 py-1 text-xs rounded disabled:opacity-50" style={{ background: 'var(--warn-fg)', color: 'white' }}
                >
                  Preview Changes
                </button>
                <button
                  onClick={() => { setPasteMode(false); setPasteText(''); setParsedPaste(null) }}
                  className="px-3 py-1 text-xs border rounded" style={{ color: 'var(--ink-3)' }}
                >
                  Cancel
                </button>
              </div>

              {parsedPaste && (
                <div className="mt-2 space-y-1">
                  <p className="text-xs font-medium" style={{ color: 'var(--ink-2)' }}>{parsedPaste.length} rows parsed. Current: {table.rows.length} rows. <span style={{ color: 'var(--warn-fg)' }}>This will replace all existing rows.</span></p>
                  <div className="max-h-40 overflow-y-auto border rounded" style={{ background: 'var(--surface)' }}>
                    <table className="w-full text-xs">
                      <thead>
                        <tr className="border-b" style={{ background: 'var(--surface-2)', color: 'var(--ink-3)' }}>
                          {Object.keys(parsedPaste[0]?.dims ?? {}).map((d) => (
                            <th key={d} className="px-3 py-1.5 text-left font-medium">{d}</th>
                          ))}
                          <th className="px-3 py-1.5 text-right font-medium">factor</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y">
                        {parsedPaste.slice(0, 20).map((r, i) => (
                          <tr key={i}>
                            {Object.values(r.dims).map((v, j) => (
                              <td key={j} className="px-3 py-1">{v}</td>
                            ))}
                            <td className="px-3 py-1 text-right font-mono">{r.factor}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                  {parsedPaste.length > 20 && <p className="text-xs" style={{ color: 'var(--ink-4)' }}>…and {parsedPaste.length - 20} more rows</p>}
                  <button
                    onClick={() => confirmPasteMutation.mutate()}
                    disabled={confirmPasteMutation.isPending}
                    className="sd-btn primary px-3 py-1 text-xs rounded disabled:opacity-50"
                  >
                    {confirmPasteMutation.isPending ? 'Applying…' : 'Apply Changes'}
                  </button>
                </div>
              )}
            </div>
          )}

          <div className="px-4 py-2 border-b" style={{ background: 'var(--surface)' }}>
            <div className="relative">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5" style={{ color: 'var(--ink-4)' }} />
              <input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Filter rows…"
                className="w-full pl-8 pr-3 py-1.5 text-xs border rounded"
              />
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-xs">
              <thead>
                <tr className="text-left border-b" style={{ color: 'var(--ink-3)', background: 'var(--surface-2)' }}>
                  {table.dimensionNames.map((d) => (
                    <th key={d} className="px-4 py-2 font-medium">{d}</th>
                  ))}
                  <th className="px-4 py-2 font-medium text-right">Factor</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {filteredRows.map((row) => (
                  <tr key={row.id} className="hover:bg-slate-50">
                    {table.dimensionNames.map((d) => (
                      <td key={d} className="px-4 py-1.5" style={{ color: 'var(--ink-2)' }}>{row.dimensionValues[d] ?? '—'}</td>
                    ))}
                    <td className="px-4 py-1.5 text-right">
                      {editMode ? (
                        <input
                          type="number"
                          step="0.0001"
                          value={currentFactor(row)}
                          onChange={(e) => setEditedFactors((prev) => ({ ...prev, [row.id]: e.target.value }))}
                          className="w-24 px-1.5 py-0.5 border rounded text-right font-mono outline-none" style={{ color: 'var(--ink-2)' }}
                        />
                      ) : (
                        <span className="font-mono font-medium" style={{ color: 'var(--ink-2)' }}>{row.factor.toFixed(4)}</span>
                      )}
                    </td>
                  </tr>
                ))}
                {filteredRows.length === 0 && (
                  <tr>
                    <td colSpan={table.dimensionNames.length + 1} className="px-4 py-4 text-center" style={{ color: 'var(--ink-4)' }}>
                      No rows match filter.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  )
}

// ─── Impact preview panel ─────────────────────────────────────────────────────

function ImpactPreviewPanel({
  versionId,
  isDraft,
}: {
  versionId: string
  isDraft: boolean
}) {
  const qc = useQueryClient()

  const { data: preview, isLoading } = useQuery({
    queryKey: ['rating-impact-preview', versionId],
    queryFn: () => ratingApi.getImpactPreview(versionId),
    retry: false,
  })

  const computeMutation = useMutation({
    mutationFn: () => ratingApi.computeImpactPreview(versionId),
    onSuccess: (data) => {
      qc.setQueryData(['rating-impact-preview', versionId], data)
      qc.invalidateQueries({ queryKey: ['rating-plan-version', versionId] })
      toast.success('Impact preview computed')
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to compute preview'),
  })

  const fmtCurrency = (n: number) =>
    n.toLocaleString('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 })

  const fmtPct = (n: number) => {
    const sign = n > 0 ? '+' : ''
    return `${sign}${n.toFixed(1)}%`
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-sm font-semibold" style={{ color: 'var(--ink-2)' }}>Impact Preview</h3>
          {preview && (
            <p className="text-xs mt-0.5" style={{ color: 'var(--ink-4)' }}>
              Computed {new Date(preview.computedAt).toLocaleString()} — {preview.quoteCount} open rated quote{preview.quoteCount !== 1 ? 's' : ''}
            </p>
          )}
        </div>
        {isDraft && (
          <button
            onClick={() => computeMutation.mutate()}
            disabled={computeMutation.isPending}
            className="sd-btn primary flex items-center gap-1.5 px-3 py-1.5 text-sm rounded disabled:opacity-50"
          >
            <RefreshCw className={`h-3.5 w-3.5 ${computeMutation.isPending ? 'animate-spin' : ''}`} />
            {computeMutation.isPending ? 'Computing…' : preview ? 'Recompute' : 'Run Preview'}
          </button>
        )}
      </div>

      {isLoading && <LoadingSpinner />}

      {!isLoading && !preview && (
        <div className="text-center py-10 border border-dashed rounded-lg">
          <BarChart2 className="h-8 w-8 mx-auto mb-2" style={{ color: 'var(--ink-4)' }} />
          <p className="text-sm" style={{ color: 'var(--ink-4)' }}>No impact preview yet.</p>
          {isDraft && (
            <p className="text-xs mt-1" style={{ color: 'var(--ink-4)' }}>Run the preview to see how this version affects open quotes.</p>
          )}
        </div>
      )}

      {preview && (
        <>
          {/* Summary cards */}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <div className="border rounded-lg p-3 text-center" style={{ background: 'var(--surface)' }}>
              <div className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Open Quotes</div>
              <div className="text-xl font-semibold" style={{ color: 'var(--ink-2)' }}>{preview.quoteCount}</div>
            </div>
            <div className="border rounded-lg p-3 text-center" style={{ background: 'var(--surface)' }}>
              <div className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Current Premium</div>
              <div className="text-lg font-semibold" style={{ color: 'var(--ink-2)' }}>{fmtCurrency(preview.totalCurrentPremium)}</div>
            </div>
            <div className="border rounded-lg p-3 text-center" style={{ background: 'var(--surface)' }}>
              <div className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>New Premium</div>
              <div className="text-lg font-semibold" style={{ color: 'var(--ink-2)' }}>{fmtCurrency(preview.totalNewPremium)}</div>
            </div>
            <div className="border rounded-lg p-3 text-center" style={{ background: 'var(--surface)', ...(preview.totalDeltaPct > 0 ? { borderColor: 'var(--line)' } : preview.totalDeltaPct < 0 ? { borderColor: 'var(--bad-fg)' } : {}) }}>
              <div className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Total Change</div>
              <div className="text-xl font-semibold" style={{ color: preview.totalDeltaPct > 0 ? 'var(--good-fg)' : preview.totalDeltaPct < 0 ? 'var(--bad-fg)' : 'var(--ink-2)' }}>
                {fmtPct(preview.totalDeltaPct)}
              </div>
            </div>
          </div>

          {/* Up / Down / Flat */}
          <div className="flex gap-3">
            <div className="flex-1 border rounded-lg p-3 text-center" style={{ background: 'var(--good-bg)', borderColor: 'var(--line)' }}>
              <div className="text-xs" style={{ color: 'var(--good-fg)' }}>Quotes Up</div>
              <div className="text-xl font-semibold" style={{ color: 'var(--good-fg)' }}>{preview.quotesUp}</div>
            </div>
            <div className="flex-1 border rounded-lg p-3 text-center" style={{ background: 'var(--surface-2)' }}>
              <div className="text-xs" style={{ color: 'var(--ink-3)' }}>Flat</div>
              <div className="text-xl font-semibold" style={{ color: 'var(--ink-2)' }}>{preview.quotesFlat}</div>
            </div>
            <div className="flex-1 border rounded-lg p-3 text-center" style={{ background: 'var(--bad-bg)', borderColor: 'var(--bad-fg)' }}>
              <div className="text-xs" style={{ color: 'var(--bad-fg)' }}>Quotes Down</div>
              <div className="text-xl font-semibold" style={{ color: 'var(--bad-fg)' }}>{preview.quotesDown}</div>
            </div>
          </div>

          {/* Distribution */}
          {preview.distributionBuckets.length > 0 && (
            <div className="border rounded-lg p-4 space-y-2" style={{ background: 'var(--surface)' }}>
              <h4 className="text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Premium Change Distribution</h4>
              <div className="space-y-1.5">
                {preview.distributionBuckets.map((b) => {
                  const maxCount = Math.max(...preview.distributionBuckets.map((x) => x.count), 1)
                  const pct = (b.count / maxCount) * 100
                  return (
                    <div key={b.rangeLabel} className="flex items-center gap-2 text-xs">
                      <span className="w-28 text-right shrink-0" style={{ color: 'var(--ink-3)' }}>{b.rangeLabel}</span>
                      <div className="flex-1 rounded-full h-3 relative" style={{ background: 'var(--surface-2)' }}>
                        <div
                          className="h-3 rounded-full"
                          style={{ width: `${pct}%`, background: 'var(--accent)' }}
                        />
                      </div>
                      <span className="w-6 font-medium" style={{ color: 'var(--ink-2)' }}>{b.count}</span>
                    </div>
                  )
                })}
              </div>
            </div>
          )}

          {/* Top movers */}
          {preview.topMovers.length > 0 && (
            <div className="border rounded-lg overflow-hidden" style={{ background: 'var(--surface)' }}>
              <div className="px-4 py-3 border-b" style={{ background: 'var(--surface-2)' }}>
                <h4 className="text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Top Movers (by % change)</h4>
              </div>
              <table className="w-full text-xs">
                <thead>
                  <tr className="text-left border-b" style={{ color: 'var(--ink-3)', background: 'var(--surface-2)' }}>
                    <th className="px-4 py-2 font-medium">Quote</th>
                    <th className="px-4 py-2 font-medium">Insured</th>
                    <th className="px-4 py-2 font-medium text-right">Current</th>
                    <th className="px-4 py-2 font-medium text-right">New</th>
                    <th className="px-4 py-2 font-medium text-right">Change</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {preview.topMovers.map((m) => (
                    <tr key={m.quoteNumber} className="hover:bg-slate-50">
                      <td className="px-4 py-2 font-mono" style={{ color: 'var(--ink-3)' }}>{m.quoteNumber}</td>
                      <td className="px-4 py-2" style={{ color: 'var(--ink-2)' }}>{m.insuredName}</td>
                      <td className="px-4 py-2 text-right" style={{ color: 'var(--ink-3)' }}>{fmtCurrency(m.currentPremium)}</td>
                      <td className="px-4 py-2 text-right" style={{ color: 'var(--ink-3)' }}>{fmtCurrency(m.newPremium)}</td>
                      <td className="px-4 py-2 text-right font-medium" style={{ color: m.deltaPct > 0 ? 'var(--good-fg)' : m.deltaPct < 0 ? 'var(--bad-fg)' : 'var(--ink-3)' }}>
                        {fmtPct(m.deltaPct)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </div>
  )
}

// ─── Meta edit form ───────────────────────────────────────────────────────────

function MetaEditForm({
  versionId,
  initial,
  onCancel,
  onSaved,
}: {
  versionId: string
  initial: UpdateVersionMetaDto
  onCancel: () => void
  onSaved: () => void
}) {
  const [form, setForm] = useState(initial)

  const saveMutation = useMutation({
    mutationFn: () => ratingApi.updateVersionMeta(versionId, form),
    onSuccess: () => { toast.success('Version metadata saved'); onSaved() },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Save failed'),
  })

  const set = (key: keyof UpdateVersionMetaDto, val: string | number | null) =>
    setForm((prev) => ({ ...prev, [key]: val }))

  return (
    <div className="border rounded-lg p-4 space-y-3" style={{ background: 'var(--warn-bg)', borderColor: 'var(--warn-fg)' }}>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="block text-xs font-medium mb-1" style={{ color: 'var(--ink-3)' }}>Effective Date</label>
          <input
            type="date"
            value={form.effectiveDate}
            onChange={(e) => set('effectiveDate', e.target.value)}
            className="sd-input w-full px-3 py-1.5 text-sm"
          />
        </div>
        <div>
          <label className="block text-xs font-medium mb-1" style={{ color: 'var(--ink-3)' }}>Minimum Premium</label>
          <input
            type="number"
            min={0}
            value={form.minimumPremium ?? ''}
            onChange={(e) => set('minimumPremium', e.target.value ? parseFloat(e.target.value) : null)}
            placeholder="None"
            className="sd-input w-full px-3 py-1.5 text-sm"
          />
        </div>
        <div>
          <label className="block text-xs font-medium mb-1" style={{ color: 'var(--ink-3)' }}>Schedule Min (%)</label>
          <input
            type="number"
            step="0.01"
            value={(form.scheduleMin * 100).toFixed(0)}
            onChange={(e) => set('scheduleMin', parseFloat(e.target.value) / 100)}
            className="sd-input w-full px-3 py-1.5 text-sm"
          />
        </div>
        <div>
          <label className="block text-xs font-medium mb-1" style={{ color: 'var(--ink-3)' }}>Schedule Max (%)</label>
          <input
            type="number"
            step="0.01"
            value={(form.scheduleMax * 100).toFixed(0)}
            onChange={(e) => set('scheduleMax', parseFloat(e.target.value) / 100)}
            className="sd-input w-full px-3 py-1.5 text-sm"
          />
        </div>
      </div>
      <div>
        <label className="block text-xs font-medium mb-1" style={{ color: 'var(--ink-3)' }}>Notes</label>
        <textarea
          rows={2}
          value={form.notes ?? ''}
          onChange={(e) => set('notes', e.target.value || null)}
          className="sd-input w-full px-3 py-1.5 text-sm resize-none"
        />
      </div>
      <div className="flex gap-2 justify-end">
        <button onClick={onCancel} className="sd-btn outline px-3 py-1.5 text-sm border rounded">
          Cancel
        </button>
        <button
          onClick={() => saveMutation.mutate()}
          disabled={saveMutation.isPending}
          className="sd-btn primary flex items-center gap-1.5 px-3 py-1.5 text-sm rounded disabled:opacity-50"
        >
          <Save className="h-3.5 w-3.5" /> {saveMutation.isPending ? 'Saving…' : 'Save'}
        </button>
      </div>
    </div>
  )
}

// ─── CSV upload section ───────────────────────────────────────────────────────

function CsvUploadSection({ versionId, onDone }: { versionId: string; onDone: () => void }) {
  const [file, setFile] = useState<File | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  const uploadMutation = useMutation({
    mutationFn: () => {
      if (!file) throw new Error('No file')
      return ratingApi.importCsv(versionId, file)
    },
    onSuccess: (data) => {
      const summary = data.tablesUpdated.map((t) => `${t}: ${data.rowCountByTable[t]} rows`).join(', ')
      toast.success(`Imported: ${summary || 'no tables updated'}`)
      if (data.warnings.length > 0) toast.warning(`Warnings: ${data.warnings.join('; ')}`)
      setFile(null)
      onDone()
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Import failed'),
  })

  return (
    <div className="border rounded-lg p-4 space-y-2" style={{ background: 'var(--surface-2)' }}>
      <h4 className="text-xs font-semibold uppercase tracking-wide flex items-center gap-1.5" style={{ color: 'var(--ink-3)' }}>
        <Upload className="h-3.5 w-3.5" /> Bulk CSV Import
      </h4>
      <p className="text-xs" style={{ color: 'var(--ink-3)' }}>
        CSV must have columns: <code className="px-1 rounded" style={{ background: 'var(--surface-2)' }}>table_code</code>, dimension columns matching each table, and <code className="px-1 rounded" style={{ background: 'var(--surface-2)' }}>factor</code>. Multiple tables can be in one file.
      </p>
      <div className="flex items-center gap-2">
        <input
          ref={inputRef}
          type="file"
          accept=".csv"
          onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          className="hidden"
        />
        <button
          onClick={() => inputRef.current?.click()}
          className="px-3 py-1.5 text-xs border rounded" style={{ color: 'var(--ink-3)' }}
        >
          {file ? file.name : 'Choose CSV file…'}
        </button>
        {file && (
          <button
            onClick={() => uploadMutation.mutate()}
            disabled={uploadMutation.isPending}
            className="sd-btn primary px-3 py-1.5 text-xs rounded disabled:opacity-50"
          >
            {uploadMutation.isPending ? 'Importing…' : 'Import'}
          </button>
        )}
        {file && (
          <button onClick={() => setFile(null)} style={{ color: 'var(--ink-4)' }}>
            <X className="h-4 w-4" />
          </button>
        )}
      </div>
    </div>
  )
}

// ─── Main page ────────────────────────────────────────────────────────────────

export function AdminRatingPlanVersionPage() {
  const { versionId } = useParams<{ versionId: string }>()
  const qc = useQueryClient()
  const currentUserId = useAuthStore((s) => s.user?.id)
  const [activeTab, setActiveTab] = useState<Tab>('schedule')
  const [editingMeta, setEditingMeta] = useState(false)

  const { data: version, isLoading: vLoading } = useQuery({
    queryKey: ['rating-plan-version', versionId],
    queryFn: () => ratingApi.getVersion(versionId!),
    enabled: !!versionId,
  })

  const { data: factors = [], isLoading: fLoading } = useQuery({
    queryKey: ['rating-plan-version-factors', versionId],
    queryFn: () => ratingApi.getVersionFactors(versionId!),
    enabled: !!versionId && activeTab === 'factors',
  })

  const { data: eligibility = [], isLoading: eLoading } = useQuery({
    queryKey: ['rating-plan-version-eligibility', versionId],
    queryFn: () => ratingApi.getVersionEligibilityRules(versionId!),
    enabled: !!versionId && activeTab === 'eligibility',
  })

  const promoteMutation = useMutation({
    mutationFn: () => ratingApi.promoteVersion(versionId!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['rating-plan-version', versionId] })
      qc.invalidateQueries({ queryKey: ['rating-plan', version?.ratingPlanId] })
      qc.invalidateQueries({ queryKey: ['rating-plans'] })
      toast.success('Version promoted to Active')
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to promote version'),
  })

  const retireMutation = useMutation({
    mutationFn: () => ratingApi.retireVersion(versionId!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['rating-plan-version', versionId] })
      qc.invalidateQueries({ queryKey: ['rating-plan', version?.ratingPlanId] })
      qc.invalidateQueries({ queryKey: ['rating-plans'] })
      toast.success('Version retired')
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to retire version'),
  })

  if (vLoading) return <LoadingSpinner />
  if (!version) return <div className="p-6 text-sm" style={{ color: 'var(--ink-3)' }}>Version not found.</div>

  const isDraft = version.status === 'Draft'

  const effectiveRange = version.expirationDate
    ? `${version.effectiveDate} — ${version.expirationDate}`
    : version.status === 'Active'
      ? `${version.effectiveDate} onward`
      : version.effectiveDate

  const blockedByMakerChecker =
    (version.createdById && version.createdById === currentUserId) ||
    (version.lastEditedById && version.lastEditedById === currentUserId)

  const tabs: { id: Tab; label: string }[] = [
    { id: 'schedule', label: 'Schedule & Limits' },
    { id: 'factors', label: 'Factor Tables' },
    { id: 'eligibility', label: 'Eligibility Rules' },
    { id: 'impact', label: 'Impact Preview' },
    { id: 'audit', label: 'Audit' },
  ]

  return (
    <div className="p-6 space-y-5 max-w-5xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm" style={{ color: 'var(--ink-3)' }}>
        <Link to="/admin/rating" className="flex items-center gap-1" style={{ color: 'var(--ink-3)' }}>
          <ArrowLeft className="h-4 w-4" /> Rating Engine
        </Link>
        <span>/</span>
        <Link to={`/admin/rating/plans/${version.ratingPlanId}`} style={{ color: 'var(--ink-3)' }}>
          {version.planName}
        </Link>
        <span>/</span>
        <span className="font-medium" style={{ color: 'var(--ink-2)' }}>v{version.versionNumber}</span>
      </div>

      {/* Header */}
      <div className="border rounded-lg p-5" style={{ background: 'var(--surface)' }}>
        <div className="flex items-start justify-between">
          <div className="space-y-1.5">
            <div className="flex items-center gap-2">
              <h1 className="text-xl font-semibold" style={{ color: 'var(--ink)' }}>{version.planName}</h1>
              <span className="font-mono text-sm" style={{ color: 'var(--ink-4)' }}>v{version.versionNumber}</span>
              <StatusBadge status={version.status} />
            </div>
            <div className="flex items-center gap-3 text-sm" style={{ color: 'var(--ink-3)' }}>
              <span className="px-2 py-0.5 rounded text-xs" style={{ background: 'var(--surface-2)', color: 'var(--accent-ink)', borderColor: 'var(--line-2)', border: '1px solid' }}>{version.lobLabel}</span>
              <span className="text-xs" style={{ color: 'var(--ink-4)' }}>{effectiveRange}</span>
            </div>
          </div>

          <div className="flex items-center gap-2">
            {isDraft && (
              <button
                onClick={() => setEditingMeta((v) => !v)}
                className="sd-btn outline flex items-center gap-1.5 px-3 py-1.5 text-sm border rounded"
              >
                <Pencil className="h-3.5 w-3.5" /> Edit Meta
              </button>
            )}

            {isDraft && (() => {
              const noPreview = !version.impactPreviewComputedAt
              const promoteTitle = blockedByMakerChecker
                ? 'You edited this draft — a different admin must promote it.'
                : noPreview
                  ? 'Run an impact preview before promoting.'
                  : undefined
              return (
                <div title={promoteTitle}>
                  <button
                    onClick={() => {
                      if (confirm(`Promote v${version.versionNumber} to Active for ${version.planName}?`))
                        promoteMutation.mutate()
                    }}
                    disabled={promoteMutation.isPending || !!blockedByMakerChecker || noPreview}
                    className="flex items-center gap-1.5 px-3 py-1.5 rounded text-sm disabled:opacity-50 disabled:cursor-not-allowed" style={{ background: 'var(--good-fg)', color: 'white' }}
                  >
                    <CheckCircle className="h-3.5 w-3.5" /> Promote
                  </button>
                </div>
              )
            })()}

            {(isDraft || version.status === 'Active') && (
              <button
                onClick={() => {
                  const msg = version.status === 'Active'
                    ? `Retire the ACTIVE version v${version.versionNumber} of ${version.planName}?\n\nThis plan will have NO active version. New quotes for ${version.lobLabel} on carriers assigned to this plan will STOP rating until another version is promoted. This cannot be undone.`
                    : `Retire v${version.versionNumber} of ${version.planName}? This cannot be undone.`
                  if (confirm(msg))
                    retireMutation.mutate()
                }}
                disabled={retireMutation.isPending}
                className="flex items-center gap-1.5 px-3 py-1.5 border rounded text-sm disabled:opacity-50" style={{ borderColor: 'var(--bad-fg)', color: 'var(--bad-fg)' }}
              >
                <XCircle className="h-3.5 w-3.5" /> Retire
              </button>
            )}
          </div>
        </div>

        {/* Promote gate notice */}
        {isDraft && !version.impactPreviewComputedAt && (
          <div className="mt-3 flex items-center gap-1.5 text-xs border rounded px-3 py-1.5" style={{ color: 'var(--warn-fg)', background: 'var(--warn-bg)', borderColor: 'var(--warn-fg)' }}>
            <AlertTriangle className="h-3.5 w-3.5 shrink-0" />
            Impact preview required before promoting. Go to the <button onClick={() => setActiveTab('impact')} className="underline font-medium">Impact Preview</button> tab.
          </div>
        )}

        {/* Meta edit form */}
        {editingMeta && isDraft && (
          <div className="mt-4">
            <MetaEditForm
              versionId={versionId!}
              initial={{
                effectiveDate: version.effectiveDate,
                notes: version.notes,
                scheduleMin: version.scheduleMin,
                scheduleMax: version.scheduleMax,
                minimumPremium: version.minimumPremium,
              }}
              onCancel={() => setEditingMeta(false)}
              onSaved={() => {
                setEditingMeta(false)
                qc.invalidateQueries({ queryKey: ['rating-plan-version', versionId] })
              }}
            />
          </div>
        )}
      </div>

      {/* Tabs */}
      <div className="border-b flex gap-1">
        {tabs.map((t) => (
          <button
            key={t.id}
            onClick={() => setActiveTab(t.id)}
            className="px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors border-transparent"
            style={activeTab === t.id ? { borderColor: 'var(--accent-ink)', color: 'var(--accent-ink)' } : { color: 'var(--ink-3)' }}
          >
            {t.label}
            {t.id === 'impact' && !version.impactPreviewComputedAt && isDraft && (
              <span className="ml-1.5 inline-block w-1.5 h-1.5 rounded-full align-middle" style={{ background: 'var(--warn-fg)' }} />
            )}
          </button>
        ))}
      </div>

      {/* Tab content */}
      {activeTab === 'schedule' && (
        <div className="border rounded-lg p-5" style={{ background: 'var(--surface)' }}>
          <dl className="grid grid-cols-2 gap-x-8 gap-y-4 text-sm">
            <div>
              <dt className="text-xs font-medium mb-0.5" style={{ color: 'var(--ink-3)' }}>Schedule Rating Min</dt>
              <dd className="font-mono" style={{ color: 'var(--ink-2)' }}>{(version.scheduleMin * 100).toFixed(0)}%</dd>
            </div>
            <div>
              <dt className="text-xs font-medium mb-0.5" style={{ color: 'var(--ink-3)' }}>Schedule Rating Max</dt>
              <dd className="font-mono" style={{ color: 'var(--ink-2)' }}>{(version.scheduleMax * 100).toFixed(0)}%</dd>
            </div>
            <div>
              <dt className="text-xs font-medium mb-0.5" style={{ color: 'var(--ink-3)' }}>Minimum Premium</dt>
              <dd style={{ color: 'var(--ink-2)' }}>{version.minimumPremium != null ? `$${version.minimumPremium.toLocaleString()}` : '—'}</dd>
            </div>
            <div>
              <dt className="text-xs font-medium mb-0.5" style={{ color: 'var(--ink-3)' }}>Effective Date</dt>
              <dd style={{ color: 'var(--ink-2)' }}>{version.effectiveDate}</dd>
            </div>
            {version.notes && (
              <div className="col-span-2">
                <dt className="text-xs font-medium mb-0.5" style={{ color: 'var(--ink-3)' }}>Notes</dt>
                <dd className="whitespace-pre-wrap" style={{ color: 'var(--ink-2)' }}>{version.notes}</dd>
              </div>
            )}
          </dl>
        </div>
      )}

      {activeTab === 'factors' && (
        <div className="space-y-3">
          {isDraft && (
            <CsvUploadSection
              versionId={versionId!}
              onDone={() => qc.invalidateQueries({ queryKey: ['rating-plan-version-factors', versionId] })}
            />
          )}
          {fLoading ? (
            <LoadingSpinner />
          ) : factors.length === 0 ? (
            <div className="text-center py-8 border border-dashed rounded-lg">
              <p className="text-sm" style={{ color: 'var(--ink-4)' }}>No factor tables in this version.</p>
              {isDraft && <p className="text-xs mt-1" style={{ color: 'var(--ink-4)' }}>Use CSV import above or paste data into individual tables.</p>}
            </div>
          ) : (
            factors.map((t) => (
              <FactorTablePanel
                key={t.id}
                table={t}
                isDraft={isDraft}
                versionId={versionId!}
                onSaved={() => qc.invalidateQueries({ queryKey: ['rating-plan-version-factors', versionId] })}
              />
            ))
          )}
        </div>
      )}

      {activeTab === 'eligibility' && (
        <div className="border rounded-lg overflow-hidden" style={{ background: 'var(--surface)' }}>
          {eLoading ? (
            <LoadingSpinner />
          ) : eligibility.length === 0 ? (
            <div className="text-center py-8">
              <p className="text-sm" style={{ color: 'var(--ink-4)' }}>No eligibility rules in this version.</p>
            </div>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs border-b" style={{ color: 'var(--ink-3)', background: 'var(--surface-2)' }}>
                  <th className="px-4 py-2 font-medium">#</th>
                  <th className="px-4 py-2 font-medium">Equipment Type</th>
                  <th className="px-4 py-2 font-medium">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {eligibility.map((r) => (
                  <tr key={r.id} className="hover:bg-slate-50">
                    <td className="px-4 py-2.5 font-mono text-xs" style={{ color: 'var(--ink-4)' }}>{r.typeNumber}</td>
                    <td className="px-4 py-2.5" style={{ color: 'var(--ink-2)' }}>{r.equipmentTypeName}</td>
                    <td className="px-4 py-2.5">
                      {r.accepted ? (
                        <span className="flex items-center gap-1 text-xs" style={{ color: 'var(--good-fg)' }}>
                          <CheckCircle className="h-3.5 w-3.5" /> Accepted
                        </span>
                      ) : (
                        <span className="flex items-center gap-1 text-xs" style={{ color: 'var(--bad-fg)' }}>
                          <XCircle className="h-3.5 w-3.5" /> Excluded
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {activeTab === 'impact' && (
        <ImpactPreviewPanel versionId={versionId!} isDraft={isDraft} />
      )}

      {activeTab === 'audit' && (
        <div className="border rounded-lg p-5" style={{ background: 'var(--surface)' }}>
          <dl className="space-y-4 text-sm">
            <div>
              <dt className="text-xs font-medium mb-0.5" style={{ color: 'var(--ink-3)' }}>Status</dt>
              <dd><StatusBadge status={version.status} /></dd>
            </div>
            {version.promotedAt && (
              <>
                <div>
                  <dt className="text-xs font-medium mb-0.5" style={{ color: 'var(--ink-3)' }}>Promoted at</dt>
                  <dd style={{ color: 'var(--ink-2)' }}>{new Date(version.promotedAt).toLocaleString()}</dd>
                </div>
                {version.promotedByName && (
                  <div>
                    <dt className="text-xs font-medium mb-0.5" style={{ color: 'var(--ink-3)' }}>Promoted by</dt>
                    <dd style={{ color: 'var(--ink-2)' }}>{version.promotedByName}</dd>
                  </div>
                )}
              </>
            )}
            {version.impactPreviewComputedAt && (
              <div>
                <dt className="text-xs font-medium mb-0.5" style={{ color: 'var(--ink-3)' }}>Impact preview computed</dt>
                <dd style={{ color: 'var(--ink-2)' }}>{new Date(version.impactPreviewComputedAt).toLocaleString()}</dd>
              </div>
            )}
            {!version.promotedAt && (
              <p className="text-xs italic" style={{ color: 'var(--ink-4)' }}>No promotion history yet.</p>
            )}
          </dl>
        </div>
      )}
    </div>
  )
}
