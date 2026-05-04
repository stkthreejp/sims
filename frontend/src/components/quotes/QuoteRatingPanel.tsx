import { useEffect, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Calculator, Check, AlertTriangle, X, Lock, FlaskConical } from 'lucide-react'
import { toast } from 'sonner'
import { quotesApi } from '@/api/quotes.api'
import { submissionIMApi, imLookupsApi } from '@/api/submissionLob.api'
import { ratingApi } from '@/api/rating.api'
import { formatCurrency } from '@/lib/utils'

import type { PolicyLineOfBusiness } from '@/types/quote.types'

const LOB_SHADOW_KEY: Partial<Record<PolicyLineOfBusiness, 'gl' | 'im' | 'al' | 'apd'>> = {
  GeneralLiability:  'gl',
  InlandMarine:      'im',
  AutoLiability:     'al',
  AutoPhysicalDamage:'apd',
}

type Props = {
  quoteId: string
  submissionId: string
  lineOfBusiness: PolicyLineOfBusiness
  isBound: boolean
}

// Decode the JSON blobs the engine writes per line. They're stored as strings so
// the engine doesn't have to share a typed shape with the UI; we tolerate any keys.
function safeParse(raw: string): Record<string, unknown> {
  try { return JSON.parse(raw) as Record<string, unknown> } catch { return {} }
}

export function QuoteRatingPanel({ quoteId, submissionId, lineOfBusiness, isBound }: Props) {
  const qc = useQueryClient()

  const { data: snapshot, isLoading: snapshotLoading } = useQuery({
    queryKey: ['rating-snapshot', quoteId],
    queryFn: () => quotesApi.getRatingSnapshot(quoteId),
  })

  const { data: equipment = [] } = useQuery({
    queryKey: ['submission-equipment', submissionId],
    queryFn: () => submissionIMApi.getEquipment(submissionId),
  })

  const { data: equipmentTypes = [] } = useQuery({
    queryKey: ['im-equipment-types'],
    queryFn: () => imLookupsApi.getEquipmentTypes(),
    staleTime: 5 * 60 * 1000,
  })

  // Form state — seeded from snapshot when one exists.
  const [modifier, setModifier] = useState(1.0)
  const [reason, setReason] = useState('')

  useEffect(() => {
    if (snapshot) {
      setModifier(snapshot.scheduleModifier)
      setReason(snapshot.scheduleModifierReason ?? '')
    }
  }, [snapshot])

  // Plan bounds: if we have a snapshot, use its bounds; otherwise default to a
  // wide range. The server clamps anyway, so this is just UI guidance.
  const scheduleMin = snapshot?.scheduleMin ?? 0.5
  const scheduleMax = snapshot?.scheduleMax ?? 1.5

  const reasonRequired = modifier !== 1.0
  const reasonInvalid = reasonRequired && !reason.trim()

  const { data: shadowStatus } = useQuery({
    queryKey: ['shadow-status'],
    queryFn: () => ratingApi.getShadowStatus(),
    staleTime: 60 * 1000,
  })

  const rateMutation = useMutation({
    mutationFn: () => quotesApi.rate(quoteId, {
      scheduleModifier: modifier,
      scheduleModifierReason: reason.trim() || undefined,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['rating-snapshot', quoteId] })
      qc.invalidateQueries({ queryKey: ['quotes', 'by-submission', submissionId] })
      toast.success('Premium calculated')
    },
    onError: (err: any) => {
      const code = err?.response?.data?.errorCode
      const msg = err?.response?.data?.errorMessage ?? 'Failed to calculate premium'
      toast.error(msg, { description: code ? `Code: ${code}` : undefined })
    },
  })

  const shadowMutation = useMutation({
    mutationFn: () => quotesApi.shadowRate(quoteId, {
      scheduleModifier: modifier,
      scheduleModifierReason: reason.trim() || undefined,
    }),
    onSuccess: (result: any) => {
      const shadow = result?.shadowPremium
      const actual = result?.actualPremium
      const deltaPct = result?.deltaPct
      const deltaStr = deltaPct != null ? ` (${deltaPct >= 0 ? '+' : ''}${Number(deltaPct).toFixed(2)}% vs spreadsheet)` : ''
      toast.success(
        `Shadow: ${formatCurrency(shadow)}${deltaStr}`,
        { description: actual != null ? `Spreadsheet: ${formatCurrency(actual)}` : undefined, duration: 6000 }
      )
      qc.invalidateQueries({ queryKey: ['shadow-results'] })
    },
    onError: (err: any) => {
      const code = err?.response?.data?.errorCode
      const msg = err?.response?.data?.errorMessage ?? 'Shadow rate failed'
      toast.error(msg, { description: code ? `Code: ${code}` : undefined })
    },
  })

  const lastError = rateMutation.error as any
  const lastErrorCode: string | undefined = lastError?.response?.data?.errorCode
  const lastErrorMessage: string | undefined = lastError?.response?.data?.errorMessage

  // Rated lines indexed by exposure ref so we can join them onto the equipment list.
  const linesByRef = new Map(snapshot?.lines?.map((l) => [l.exposureRef, l]) ?? [])

  // Items missing required fields can't be rated. The user spec asked us to highlight these
  // so the user knows what to fix before clicking Calculate.
  const itemsMissingType = equipment.filter((e) => !e.equipmentTypeId)
  const itemsMissingValue = equipment.filter((e) => !e.value)
  const blockedByMissingFields = itemsMissingType.length > 0 || itemsMissingValue.length > 0

  return (
    <div className="px-5 py-4 bg-slate-50 border-t border-slate-200 space-y-4">
      <div className="flex items-center gap-2">
        <Calculator className="h-4 w-4 text-slate-700" />
        <h3 className="text-sm font-semibold text-slate-800">Rating</h3>
        {isBound && (
          <span className="inline-flex items-center gap-1 text-xs text-slate-600 bg-slate-100 border border-slate-200 rounded px-1.5 py-0.5">
            <Lock className="h-3 w-3" /> Locked at bind
          </span>
        )}
      </div>

      {/* Equipment summary */}
      <div className="bg-white border rounded">
        <div className="px-4 py-2 border-b text-xs font-semibold text-slate-600 uppercase">
          Equipment ({equipment.length})
        </div>
        {equipment.length === 0 ? (
          <p className="px-4 py-3 text-sm text-slate-400">No equipment scheduled. Add equipment items to the submission before rating.</p>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-xs text-slate-500 uppercase">
              <tr>
                <th className="px-4 py-2 text-left w-10">#</th>
                <th className="px-4 py-2 text-left">Type</th>
                <th className="px-4 py-2 text-left">Year/Make/Model</th>
                <th className="px-4 py-2 text-right">Value</th>
                <th className="px-4 py-2 text-left">Deductible</th>
                <th className="px-4 py-2 text-left">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {equipment.map((e) => {
                const type = equipmentTypes.find((t) => t.id === e.equipmentTypeId)
                const dedLabel = e.deductible === null
                  ? '10% ACV'
                  : e.deductible !== undefined && e.deductible !== null
                    ? `$${e.deductible.toLocaleString()}`
                    : '—'
                const ref = `EQ-${String(e.itemNumber).padStart(3, '0')}`
                const ratedLine = linesByRef.get(ref)
                const missing: string[] = []
                if (!e.equipmentTypeId) missing.push('type')
                if (!e.value) missing.push('value')
                return (
                  <tr key={e.id} className={missing.length ? 'bg-amber-50' : ''}>
                    <td className="px-4 py-2 text-slate-500">{e.itemNumber}</td>
                    <td className="px-4 py-2">{type?.name ?? <span className="text-amber-700">— missing —</span>}</td>
                    <td className="px-4 py-2 text-slate-600">{[e.year, e.make, e.model].filter(Boolean).join(' ') || '—'}</td>
                    <td className="px-4 py-2 text-right">{e.value != null ? formatCurrency(e.value) : <span className="text-amber-700">— missing —</span>}</td>
                    <td className="px-4 py-2 text-slate-600">{dedLabel}</td>
                    <td className="px-4 py-2">
                      {missing.length > 0 ? (
                        <span className="inline-flex items-center gap-1 text-xs text-amber-700">
                          <AlertTriangle className="h-3 w-3" /> Missing {missing.join(' & ')}
                        </span>
                      ) : ratedLine ? (
                        <span className="inline-flex items-center gap-1 text-xs text-green-700">
                          <Check className="h-3 w-3" /> Rated {formatCurrency(ratedLine.linePremium)}
                        </span>
                      ) : (
                        <span className="text-xs text-slate-400">Not yet rated</span>
                      )}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        )}
      </div>

      {/* Schedule modifier */}
      <div className="bg-white border rounded p-4 space-y-3">
        <div className="flex items-center justify-between">
          <h4 className="text-xs font-semibold text-slate-700 uppercase">Schedule Modifier (IRPM)</h4>
          <span className="text-xs text-slate-500">Allowed range: {scheduleMin.toFixed(2)}–{scheduleMax.toFixed(2)}</span>
        </div>
        <div className="grid grid-cols-3 gap-3">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Modifier</label>
            <input
              type="number"
              step="0.01"
              min={scheduleMin}
              max={scheduleMax}
              value={modifier}
              onChange={(e) => setModifier(parseFloat(e.target.value) || 1.0)}
              disabled={isBound}
              className="w-full border rounded px-2 py-1.5 text-sm disabled:bg-slate-50"
            />
          </div>
          <div className="col-span-2">
            <label className="block text-xs font-medium text-slate-600 mb-1">
              Reason {reasonRequired && <span className="text-red-600">*</span>}
            </label>
            <input
              type="text"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              disabled={isBound}
              placeholder={reasonRequired ? 'Required when modifier ≠ 1.00' : 'Optional'}
              className={`w-full border rounded px-2 py-1.5 text-sm disabled:bg-slate-50 ${reasonInvalid ? 'border-red-300' : ''}`}
            />
          </div>
        </div>
      </div>

      {/* Calculate button + errors */}
      {!isBound && (
        <div className="space-y-2">
          <div className="flex items-center gap-3 flex-wrap">
            <button
              onClick={() => rateMutation.mutate()}
              disabled={rateMutation.isPending || shadowMutation.isPending || reasonInvalid || equipment.length === 0 || blockedByMissingFields}
              className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <Calculator className="h-4 w-4" />
              {rateMutation.isPending ? 'Calculating…' : snapshot ? 'Recalculate Premium' : 'Calculate Premium'}
            </button>
            {shadowStatus?.[LOB_SHADOW_KEY[lineOfBusiness]] && (
              <button
                onClick={() => shadowMutation.mutate()}
                disabled={shadowMutation.isPending || rateMutation.isPending || reasonInvalid || equipment.length === 0 || blockedByMissingFields}
                className="inline-flex items-center gap-2 px-4 py-2 bg-white border border-slate-300 text-slate-700 text-sm font-medium rounded hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed"
                title="Run engine without changing the quote premium — compare to spreadsheet"
              >
                <FlaskConical className="h-4 w-4 text-slate-500" />
                {shadowMutation.isPending ? 'Running…' : 'Shadow Rate'}
              </button>
            )}
            {blockedByMissingFields && (
              <span className="text-xs text-amber-700">
                Fix missing fields on equipment items above before rating.
              </span>
            )}
          </div>

          {lastErrorCode && (
            <div className="flex items-start gap-2 px-3 py-2 bg-red-50 border border-red-200 rounded text-sm">
              <X className="h-4 w-4 text-red-600 flex-shrink-0 mt-0.5" />
              <div>
                <p className="font-medium text-red-800">{describeRatingError(lastErrorCode)}</p>
                {lastErrorMessage && <p className="text-xs text-red-700 mt-0.5">{lastErrorMessage}</p>}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Results */}
      {snapshotLoading ? (
        <p className="text-sm text-slate-400">Loading…</p>
      ) : snapshot ? (
        <div className="bg-white border rounded">
          <div className="px-4 py-2 border-b text-xs font-semibold text-slate-600 uppercase flex items-center justify-between">
            <span>Calculation Detail</span>
            <span className="text-xs font-normal text-slate-500 normal-case">
              Rated {new Date(snapshot.ratedAt).toLocaleString()}
              {snapshot.ratedByName ? ` by ${snapshot.ratedByName}` : ''}
            </span>
          </div>
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-xs text-slate-500 uppercase">
              <tr>
                <th className="px-4 py-2 text-left">Item</th>
                <th className="px-4 py-2 text-left">Type</th>
                <th className="px-4 py-2 text-right">Stated Value</th>
                <th className="px-4 py-2 text-left">Age Band</th>
                <th className="px-4 py-2 text-right">Base Rate</th>
                <th className="px-4 py-2 text-right">Ded Factor</th>
                <th className="px-4 py-2 text-right">Line Premium</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {snapshot.lines.map((l) => {
                const inputs = safeParse(l.inputs)
                const factors = safeParse(l.factorsApplied)
                return (
                  <tr key={l.exposureRef}>
                    <td className="px-4 py-2 font-mono text-xs text-slate-500">{l.exposureRef}</td>
                    <td className="px-4 py-2">{String(inputs.type ?? '—')}</td>
                    <td className="px-4 py-2 text-right">{inputs.value != null ? formatCurrency(Number(inputs.value)) : '—'}</td>
                    <td className="px-4 py-2 text-slate-600">{String(factors.age_band ?? '—')}</td>
                    <td className="px-4 py-2 text-right text-slate-600">{factors.base_rate != null ? Number(factors.base_rate).toFixed(4) : '—'}</td>
                    <td className="px-4 py-2 text-right text-slate-600">{factors.deductible_factor != null ? Number(factors.deductible_factor).toFixed(4) : '—'}</td>
                    <td className="px-4 py-2 text-right font-medium">{formatCurrency(l.linePremium)}</td>
                  </tr>
                )
              })}
            </tbody>
            <tfoot className="bg-slate-50 text-sm">
              <tr>
                <td colSpan={6} className="px-4 py-2 text-right text-slate-600">Manual Premium (sum of lines × modifier)</td>
                <td className="px-4 py-2 text-right font-medium">{formatCurrency(snapshot.manualPremium)}</td>
              </tr>
              <tr>
                <td colSpan={6} className="px-4 py-2 text-right text-slate-600">
                  Schedule Modifier
                  {snapshot.scheduleModifier !== 1.0 && snapshot.scheduleModifierReason && (
                    <span className="block text-xs text-slate-500 italic">"{snapshot.scheduleModifierReason}"</span>
                  )}
                </td>
                <td className="px-4 py-2 text-right text-slate-600">× {snapshot.scheduleModifier.toFixed(2)}</td>
              </tr>
              {snapshot.minimumPremium != null && snapshot.grandTotalPremium === snapshot.minimumPremium && (
                <tr>
                  <td colSpan={6} className="px-4 py-2 text-right text-amber-700 text-xs">
                    Minimum premium floor applied ({formatCurrency(snapshot.minimumPremium)})
                  </td>
                  <td />
                </tr>
              )}
              <tr className="bg-blue-50 border-t-2 border-blue-200">
                <td colSpan={6} className="px-4 py-3 text-right font-semibold text-blue-900">Grand Total Premium</td>
                <td className="px-4 py-3 text-right font-bold text-blue-900 text-base">{formatCurrency(snapshot.grandTotalPremium)}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      ) : null}
    </div>
  )
}

function describeRatingError(code: string): string {
  switch (code) {
    case 'NO_RATING_PLAN': return 'No rating plan assigned'
    case 'NO_EQUIPMENT': return 'No equipment on submission'
    case 'MISSING_TYPE': return 'Equipment item missing type'
    case 'MISSING_VALUE': return 'Equipment item missing stated value'
    case 'INELIGIBLE': return 'Equipment type not eligible'
    case 'LOOKUP_FAIL': return 'Missing factor in rating plan'
    case 'MISSING_FACTORS': return 'Rating plan misconfigured'
    case 'REASON_REQUIRED': return 'Schedule modifier reason required'
    case 'NOT_FOUND': return 'Quote not found'
    default: return `Rating failed (${code})`
  }
}
