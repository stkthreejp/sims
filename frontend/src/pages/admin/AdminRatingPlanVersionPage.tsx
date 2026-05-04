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
  const map: Record<PlanStatus, { label: string; cls: string }> = {
    Active:  { label: 'Active',  cls: 'bg-emerald-100 text-emerald-700' },
    Draft:   { label: 'Draft',   cls: 'bg-amber-100 text-amber-700' },
    Retired: { label: 'Retired', cls: 'bg-slate-100 text-slate-500' },
  }
  const { label, cls } = map[status]
  return <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${cls}`}>{label}</span>
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
  const fileInputRef = useRef<HTMLInputElement>(null)

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
      <div className="w-full flex items-center justify-between px-4 py-3 bg-slate-50">
        <button
          onClick={() => setOpen((o) => !o)}
          className="flex items-center gap-2 text-left flex-1"
        >
          {open ? <ChevronDown className="h-4 w-4 text-slate-400" /> : <ChevronRight className="h-4 w-4 text-slate-400" />}
          <span className="text-sm font-semibold text-slate-700 font-mono">{table.code}</span>
          <span className="text-xs text-slate-400">{table.rows.length} rows · {table.dimensionNames.join(', ')}</span>
          <span className="text-xs px-1.5 py-0.5 rounded bg-slate-200 text-slate-600">{table.valueSemantics}</span>
        </button>

        {isDraft && open && (
          <div className="flex items-center gap-1.5 shrink-0">
            {editMode ? (
              <>
                <button
                  onClick={() => { setEditMode(false); setEditedFactors({}) }}
                  className="flex items-center gap-1 px-2 py-1 text-xs border rounded text-slate-600 hover:bg-slate-100"
                >
                  <X className="h-3 w-3" /> Cancel
                </button>
                <button
                  onClick={() => saveMutation.mutate()}
                  disabled={saveMutation.isPending}
                  className="flex items-center gap-1 px-2 py-1 text-xs bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50"
                >
                  <Save className="h-3 w-3" /> {saveMutation.isPending ? 'Saving…' : 'Save'}
                </button>
              </>
            ) : (
              <>
                <button
                  onClick={() => { setPasteMode((p) => !p); setEditMode(false) }}
                  className="flex items-center gap-1 px-2 py-1 text-xs border rounded text-slate-600 hover:bg-slate-100"
                >
                  <Upload className="h-3 w-3" /> Paste
                </button>
                <button
                  onClick={() => { setEditMode(true); setPasteMode(false) }}
                  className="flex items-center gap-1 px-2 py-1 text-xs border rounded text-slate-600 hover:bg-slate-100"
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
            <div className="px-4 py-3 border-b bg-amber-50 space-y-2">
              <p className="text-xs text-amber-700 font-medium">
                Paste tab-separated or CSV data (header row required, include a <code className="bg-amber-100 px-1 rounded">factor</code> column).
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
                  className="px-3 py-1 text-xs bg-amber-600 text-white rounded hover:bg-amber-700 disabled:opacity-50"
                >
                  Preview Changes
                </button>
                <button
                  onClick={() => { setPasteMode(false); setPasteText(''); setParsedPaste(null) }}
                  className="px-3 py-1 text-xs border rounded text-slate-600 hover:bg-slate-50"
                >
                  Cancel
                </button>
              </div>

              {parsedPaste && (
                <div className="mt-2 space-y-1">
                  <p className="text-xs font-medium text-slate-700">{parsedPaste.length} rows parsed. Current: {table.rows.length} rows. <span className="text-amber-700">This will replace all existing rows.</span></p>
                  <div className="max-h-40 overflow-y-auto border rounded bg-white">
                    <table className="w-full text-xs">
                      <thead>
                        <tr className="bg-slate-50 text-slate-500 border-b">
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
                  {parsedPaste.length > 20 && <p className="text-xs text-slate-400">…and {parsedPaste.length - 20} more rows</p>}
                  <button
                    onClick={() => confirmPasteMutation.mutate()}
                    disabled={confirmPasteMutation.isPending}
                    className="px-3 py-1 text-xs bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50"
                  >
                    {confirmPasteMutation.isPending ? 'Applying…' : 'Apply Changes'}
                  </button>
                </div>
              )}
            </div>
          )}

          <div className="px-4 py-2 border-b bg-white">
            <div className="relative">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-400" />
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
                <tr className="text-left text-slate-500 border-b bg-slate-50">
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
                      <td key={d} className="px-4 py-1.5 text-slate-700">{row.dimensionValues[d] ?? '—'}</td>
                    ))}
                    <td className="px-4 py-1.5 text-right">
                      {editMode ? (
                        <input
                          type="number"
                          step="0.0001"
                          value={currentFactor(row)}
                          onChange={(e) => setEditedFactors((prev) => ({ ...prev, [row.id]: e.target.value }))}
                          className="w-24 px-1.5 py-0.5 border rounded text-right font-mono text-slate-800 focus:ring-1 focus:ring-blue-500 outline-none"
                        />
                      ) : (
                        <span className="font-mono font-medium text-slate-800">{row.factor.toFixed(4)}</span>
                      )}
                    </td>
                  </tr>
                ))}
                {filteredRows.length === 0 && (
                  <tr>
                    <td colSpan={table.dimensionNames.length + 1} className="px-4 py-4 text-center text-slate-400">
                      No rows match filter.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}
      <input ref={fileInputRef} type="file" accept=".csv" className="hidden" />
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
          <h3 className="text-sm font-semibold text-slate-800">Impact Preview</h3>
          {preview && (
            <p className="text-xs text-slate-400 mt-0.5">
              Computed {new Date(preview.computedAt).toLocaleString()} — {preview.quoteCount} open rated quote{preview.quoteCount !== 1 ? 's' : ''}
            </p>
          )}
        </div>
        {isDraft && (
          <button
            onClick={() => computeMutation.mutate()}
            disabled={computeMutation.isPending}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50"
          >
            <RefreshCw className={`h-3.5 w-3.5 ${computeMutation.isPending ? 'animate-spin' : ''}`} />
            {computeMutation.isPending ? 'Computing…' : preview ? 'Recompute' : 'Run Preview'}
          </button>
        )}
      </div>

      {isLoading && <LoadingSpinner />}

      {!isLoading && !preview && (
        <div className="text-center py-10 border border-dashed rounded-lg">
          <BarChart2 className="h-8 w-8 text-slate-300 mx-auto mb-2" />
          <p className="text-sm text-slate-400">No impact preview yet.</p>
          {isDraft && (
            <p className="text-xs text-slate-300 mt-1">Run the preview to see how this version affects open quotes.</p>
          )}
        </div>
      )}

      {preview && (
        <>
          {/* Summary cards */}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <div className="bg-white border rounded-lg p-3 text-center">
              <div className="text-xs text-slate-500 mb-0.5">Open Quotes</div>
              <div className="text-xl font-semibold text-slate-800">{preview.quoteCount}</div>
            </div>
            <div className="bg-white border rounded-lg p-3 text-center">
              <div className="text-xs text-slate-500 mb-0.5">Current Premium</div>
              <div className="text-lg font-semibold text-slate-800">{fmtCurrency(preview.totalCurrentPremium)}</div>
            </div>
            <div className="bg-white border rounded-lg p-3 text-center">
              <div className="text-xs text-slate-500 mb-0.5">New Premium</div>
              <div className="text-lg font-semibold text-slate-800">{fmtCurrency(preview.totalNewPremium)}</div>
            </div>
            <div className={`bg-white border rounded-lg p-3 text-center ${preview.totalDeltaPct > 0 ? 'border-emerald-200 bg-emerald-50/30' : preview.totalDeltaPct < 0 ? 'border-red-200 bg-red-50/30' : ''}`}>
              <div className="text-xs text-slate-500 mb-0.5">Total Change</div>
              <div className={`text-xl font-semibold ${preview.totalDeltaPct > 0 ? 'text-emerald-700' : preview.totalDeltaPct < 0 ? 'text-red-700' : 'text-slate-700'}`}>
                {fmtPct(preview.totalDeltaPct)}
              </div>
            </div>
          </div>

          {/* Up / Down / Flat */}
          <div className="flex gap-3">
            <div className="flex-1 bg-emerald-50 border border-emerald-200 rounded-lg p-3 text-center">
              <div className="text-xs text-emerald-600">Quotes Up</div>
              <div className="text-xl font-semibold text-emerald-700">{preview.quotesUp}</div>
            </div>
            <div className="flex-1 bg-slate-50 border rounded-lg p-3 text-center">
              <div className="text-xs text-slate-500">Flat</div>
              <div className="text-xl font-semibold text-slate-700">{preview.quotesFlat}</div>
            </div>
            <div className="flex-1 bg-red-50 border border-red-200 rounded-lg p-3 text-center">
              <div className="text-xs text-red-600">Quotes Down</div>
              <div className="text-xl font-semibold text-red-700">{preview.quotesDown}</div>
            </div>
          </div>

          {/* Distribution */}
          {preview.distributionBuckets.length > 0 && (
            <div className="bg-white border rounded-lg p-4 space-y-2">
              <h4 className="text-xs font-semibold text-slate-600 uppercase tracking-wide">Premium Change Distribution</h4>
              <div className="space-y-1.5">
                {preview.distributionBuckets.map((b) => {
                  const maxCount = Math.max(...preview.distributionBuckets.map((x) => x.count), 1)
                  const pct = (b.count / maxCount) * 100
                  return (
                    <div key={b.rangeLabel} className="flex items-center gap-2 text-xs">
                      <span className="w-28 text-right text-slate-500 shrink-0">{b.rangeLabel}</span>
                      <div className="flex-1 bg-slate-100 rounded-full h-3 relative">
                        <div
                          className="h-3 rounded-full bg-blue-500"
                          style={{ width: `${pct}%` }}
                        />
                      </div>
                      <span className="w-6 text-slate-700 font-medium">{b.count}</span>
                    </div>
                  )
                })}
              </div>
            </div>
          )}

          {/* Top movers */}
          {preview.topMovers.length > 0 && (
            <div className="bg-white border rounded-lg overflow-hidden">
              <div className="px-4 py-3 border-b bg-slate-50">
                <h4 className="text-xs font-semibold text-slate-600 uppercase tracking-wide">Top Movers (by % change)</h4>
              </div>
              <table className="w-full text-xs">
                <thead>
                  <tr className="text-left text-slate-500 border-b bg-slate-50/50">
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
                      <td className="px-4 py-2 font-mono text-slate-600">{m.quoteNumber}</td>
                      <td className="px-4 py-2 text-slate-700">{m.insuredName}</td>
                      <td className="px-4 py-2 text-right text-slate-600">{fmtCurrency(m.currentPremium)}</td>
                      <td className="px-4 py-2 text-right text-slate-600">{fmtCurrency(m.newPremium)}</td>
                      <td className={`px-4 py-2 text-right font-medium ${m.deltaPct > 0 ? 'text-emerald-700' : m.deltaPct < 0 ? 'text-red-700' : 'text-slate-500'}`}>
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
    <div className="bg-amber-50 border border-amber-200 rounded-lg p-4 space-y-3">
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">Effective Date</label>
          <input
            type="date"
            value={form.effectiveDate}
            onChange={(e) => set('effectiveDate', e.target.value)}
            className="w-full px-3 py-1.5 text-sm border rounded focus:ring-1 focus:ring-blue-500 outline-none"
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">Minimum Premium</label>
          <input
            type="number"
            min={0}
            value={form.minimumPremium ?? ''}
            onChange={(e) => set('minimumPremium', e.target.value ? parseFloat(e.target.value) : null)}
            placeholder="None"
            className="w-full px-3 py-1.5 text-sm border rounded focus:ring-1 focus:ring-blue-500 outline-none"
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">Schedule Min (%)</label>
          <input
            type="number"
            step="0.01"
            value={(form.scheduleMin * 100).toFixed(0)}
            onChange={(e) => set('scheduleMin', parseFloat(e.target.value) / 100)}
            className="w-full px-3 py-1.5 text-sm border rounded focus:ring-1 focus:ring-blue-500 outline-none"
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">Schedule Max (%)</label>
          <input
            type="number"
            step="0.01"
            value={(form.scheduleMax * 100).toFixed(0)}
            onChange={(e) => set('scheduleMax', parseFloat(e.target.value) / 100)}
            className="w-full px-3 py-1.5 text-sm border rounded focus:ring-1 focus:ring-blue-500 outline-none"
          />
        </div>
      </div>
      <div>
        <label className="block text-xs font-medium text-slate-600 mb-1">Notes</label>
        <textarea
          rows={2}
          value={form.notes ?? ''}
          onChange={(e) => set('notes', e.target.value || null)}
          className="w-full px-3 py-1.5 text-sm border rounded focus:ring-1 focus:ring-blue-500 outline-none resize-none"
        />
      </div>
      <div className="flex gap-2 justify-end">
        <button onClick={onCancel} className="px-3 py-1.5 text-sm border rounded hover:bg-slate-50">
          Cancel
        </button>
        <button
          onClick={() => saveMutation.mutate()}
          disabled={saveMutation.isPending}
          className="flex items-center gap-1.5 px-3 py-1.5 text-sm bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50"
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
    <div className="bg-slate-50 border rounded-lg p-4 space-y-2">
      <h4 className="text-xs font-semibold text-slate-600 uppercase tracking-wide flex items-center gap-1.5">
        <Upload className="h-3.5 w-3.5" /> Bulk CSV Import
      </h4>
      <p className="text-xs text-slate-500">
        CSV must have columns: <code className="bg-slate-100 px-1 rounded">table_code</code>, dimension columns matching each table, and <code className="bg-slate-100 px-1 rounded">factor</code>. Multiple tables can be in one file.
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
          className="px-3 py-1.5 text-xs border rounded hover:bg-white text-slate-600"
        >
          {file ? file.name : 'Choose CSV file…'}
        </button>
        {file && (
          <button
            onClick={() => uploadMutation.mutate()}
            disabled={uploadMutation.isPending}
            className="px-3 py-1.5 text-xs bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50"
          >
            {uploadMutation.isPending ? 'Importing…' : 'Import'}
          </button>
        )}
        {file && (
          <button onClick={() => setFile(null)} className="text-slate-400 hover:text-slate-600">
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
  if (!version) return <div className="p-6 text-sm text-slate-500">Version not found.</div>

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
      <div className="flex items-center gap-2 text-sm text-slate-500">
        <Link to="/admin/rating" className="hover:text-slate-700 flex items-center gap-1">
          <ArrowLeft className="h-4 w-4" /> Rating Engine
        </Link>
        <span>/</span>
        <Link to={`/admin/rating/plans/${version.ratingPlanId}`} className="hover:text-slate-700">
          {version.planName}
        </Link>
        <span>/</span>
        <span className="text-slate-800 font-medium">v{version.versionNumber}</span>
      </div>

      {/* Header */}
      <div className="bg-white border rounded-lg p-5">
        <div className="flex items-start justify-between">
          <div className="space-y-1.5">
            <div className="flex items-center gap-2">
              <h1 className="text-xl font-semibold text-slate-900">{version.planName}</h1>
              <span className="text-slate-400 font-mono text-sm">v{version.versionNumber}</span>
              <StatusBadge status={version.status} />
            </div>
            <div className="flex items-center gap-3 text-sm text-slate-500">
              <span className="px-2 py-0.5 bg-blue-50 text-blue-700 rounded text-xs border border-blue-100">{version.lobLabel}</span>
              <span className="text-xs text-slate-400">{effectiveRange}</span>
            </div>
          </div>

          <div className="flex items-center gap-2">
            {isDraft && (
              <button
                onClick={() => setEditingMeta((v) => !v)}
                className="flex items-center gap-1.5 px-3 py-1.5 text-sm border rounded hover:bg-slate-50 text-slate-600"
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
                    className="flex items-center gap-1.5 px-3 py-1.5 bg-emerald-600 text-white rounded text-sm hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    <CheckCircle className="h-3.5 w-3.5" /> Promote
                  </button>
                </div>
              )
            })()}

            {(isDraft || version.status === 'Active') && (
              <button
                onClick={() => {
                  if (confirm(`Retire v${version.versionNumber} of ${version.planName}? This cannot be undone.`))
                    retireMutation.mutate()
                }}
                disabled={retireMutation.isPending}
                className="flex items-center gap-1.5 px-3 py-1.5 border border-red-200 text-red-600 rounded text-sm hover:bg-red-50 disabled:opacity-50"
              >
                <XCircle className="h-3.5 w-3.5" /> Retire
              </button>
            )}
          </div>
        </div>

        {/* Promote gate notice */}
        {isDraft && !version.impactPreviewComputedAt && (
          <div className="mt-3 flex items-center gap-1.5 text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded px-3 py-1.5">
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
            className={`px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors ${
              activeTab === t.id
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-slate-500 hover:text-slate-700'
            }`}
          >
            {t.label}
            {t.id === 'impact' && !version.impactPreviewComputedAt && isDraft && (
              <span className="ml-1.5 inline-block w-1.5 h-1.5 rounded-full bg-amber-400 align-middle" />
            )}
          </button>
        ))}
      </div>

      {/* Tab content */}
      {activeTab === 'schedule' && (
        <div className="bg-white border rounded-lg p-5">
          <dl className="grid grid-cols-2 gap-x-8 gap-y-4 text-sm">
            <div>
              <dt className="text-xs font-medium text-slate-500 mb-0.5">Schedule Rating Min</dt>
              <dd className="text-slate-800 font-mono">{(version.scheduleMin * 100).toFixed(0)}%</dd>
            </div>
            <div>
              <dt className="text-xs font-medium text-slate-500 mb-0.5">Schedule Rating Max</dt>
              <dd className="text-slate-800 font-mono">{(version.scheduleMax * 100).toFixed(0)}%</dd>
            </div>
            <div>
              <dt className="text-xs font-medium text-slate-500 mb-0.5">Minimum Premium</dt>
              <dd className="text-slate-800">{version.minimumPremium != null ? `$${version.minimumPremium.toLocaleString()}` : '—'}</dd>
            </div>
            <div>
              <dt className="text-xs font-medium text-slate-500 mb-0.5">Effective Date</dt>
              <dd className="text-slate-800">{version.effectiveDate}</dd>
            </div>
            {version.notes && (
              <div className="col-span-2">
                <dt className="text-xs font-medium text-slate-500 mb-0.5">Notes</dt>
                <dd className="text-slate-700 whitespace-pre-wrap">{version.notes}</dd>
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
              <p className="text-sm text-slate-400">No factor tables in this version.</p>
              {isDraft && <p className="text-xs text-slate-300 mt-1">Use CSV import above or paste data into individual tables.</p>}
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
        <div className="bg-white border rounded-lg overflow-hidden">
          {eLoading ? (
            <LoadingSpinner />
          ) : eligibility.length === 0 ? (
            <div className="text-center py-8">
              <p className="text-sm text-slate-400">No eligibility rules in this version.</p>
            </div>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-slate-500 border-b bg-slate-50">
                  <th className="px-4 py-2 font-medium">#</th>
                  <th className="px-4 py-2 font-medium">Equipment Type</th>
                  <th className="px-4 py-2 font-medium">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {eligibility.map((r) => (
                  <tr key={r.id} className="hover:bg-slate-50">
                    <td className="px-4 py-2.5 text-slate-400 font-mono text-xs">{r.typeNumber}</td>
                    <td className="px-4 py-2.5 text-slate-800">{r.equipmentTypeName}</td>
                    <td className="px-4 py-2.5">
                      {r.accepted ? (
                        <span className="flex items-center gap-1 text-emerald-700 text-xs">
                          <CheckCircle className="h-3.5 w-3.5" /> Accepted
                        </span>
                      ) : (
                        <span className="flex items-center gap-1 text-red-600 text-xs">
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
        <div className="bg-white border rounded-lg p-5">
          <dl className="space-y-4 text-sm">
            <div>
              <dt className="text-xs font-medium text-slate-500 mb-0.5">Status</dt>
              <dd><StatusBadge status={version.status} /></dd>
            </div>
            {version.promotedAt && (
              <>
                <div>
                  <dt className="text-xs font-medium text-slate-500 mb-0.5">Promoted at</dt>
                  <dd className="text-slate-800">{new Date(version.promotedAt).toLocaleString()}</dd>
                </div>
                {version.promotedByName && (
                  <div>
                    <dt className="text-xs font-medium text-slate-500 mb-0.5">Promoted by</dt>
                    <dd className="text-slate-800">{version.promotedByName}</dd>
                  </div>
                )}
              </>
            )}
            {version.impactPreviewComputedAt && (
              <div>
                <dt className="text-xs font-medium text-slate-500 mb-0.5">Impact preview computed</dt>
                <dd className="text-slate-800">{new Date(version.impactPreviewComputedAt).toLocaleString()}</dd>
              </div>
            )}
            {!version.promotedAt && (
              <p className="text-slate-400 text-xs italic">No promotion history yet.</p>
            )}
          </dl>
        </div>
      )}
    </div>
  )
}
