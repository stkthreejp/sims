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

const IM_OPTIONAL_ENDORSEMENTS = [
  { key: 'debrisRemoval', label: 'Debris Removal', premium: 250 },
  { key: 'rentalReimbursement', label: 'Rental Reimbursement', premium: 500 },
  { key: 'towingStorageRecovery', label: 'Towing, Storage & Recovery', premium: 175 },
  { key: 'newlyAcquiredEquipment', label: 'Newly Acquired Equipment', premium: 0 },
] as const

type IMEndorsementKey = typeof IM_OPTIONAL_ENDORSEMENTS[number]['key']

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
  const [imEndorsements, setImEndorsements] = useState<Record<IMEndorsementKey, boolean>>({
    debrisRemoval: true,
    rentalReimbursement: true,
    towingStorageRecovery: true,
    newlyAcquiredEquipment: false,
  })

  useEffect(() => {
    if (snapshot) {
      setModifier(snapshot.scheduleModifier)
      setReason(snapshot.scheduleModifierReason ?? '')
      setImEndorsements({
        debrisRemoval: snapshot.debrisRemoval,
        rentalReimbursement: snapshot.rentalReimbursement,
        towingStorageRecovery: snapshot.towingStorageRecovery,
        newlyAcquiredEquipment: snapshot.newlyAcquiredEquipment,
      })
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
      ...(lineOfBusiness === 'InlandMarine' ? imEndorsements : {}),
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['rating-snapshot', quoteId] })
      qc.invalidateQueries({ queryKey: ['quotes', quoteId] })
      qc.invalidateQueries({ queryKey: ['quote-invoice-preview', quoteId] })
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
      ...(lineOfBusiness === 'InlandMarine' ? imEndorsements : {}),
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
  const selectedEndorsementPremium = IM_OPTIONAL_ENDORSEMENTS
    .filter((e) => imEndorsements[e.key])
    .reduce((sum, e) => sum + e.premium, 0)

  return (
    <div className="space-y-4" style={{ padding: '14px 16px', borderTop: '1px solid var(--line-2)', background: 'var(--surface-2)' }}>
      <div className="flex items-center gap-2">
        <Calculator size={16} strokeWidth={1.7} style={{ color: 'var(--ink-3)' }} />
        <h3 style={{ margin: 0, color: 'var(--ink)', fontSize: 'var(--fs-body)', fontWeight: 600 }}>Rating</h3>
        {isBound && (
          <span className="sd-lob">
            <Lock size={12} /> Locked at bind
          </span>
        )}
      </div>

      {/* Equipment summary */}
      <div className="sd-card">
        <div className="sd-card-head">
          <h3>Equipment <span className="cnt">{equipment.length}</span></h3>
        </div>
        {equipment.length === 0 ? (
          <p style={{ margin: 0, padding: '14px 16px', color: 'var(--ink-3)', fontSize: 'var(--fs-body)' }}>No equipment scheduled. Add equipment items to the submission before rating.</p>
        ) : (
          <table className="sd-table">
            <thead>
              <tr>
                <th>#</th>
                <th>Type</th>
                <th>Year/Make/Model</th>
                <th className="num">Value</th>
                <th>Deductible</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
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

      {lineOfBusiness === 'InlandMarine' && (
        <div className="sd-card">
          <div className="sd-card-body space-y-3">
          <div className="flex items-center justify-between">
            <h4 className="sims-field-label" style={{ margin: 0 }}>Optional Endorsements</h4>
            <span style={{ color: 'var(--ink-3)', fontSize: 'var(--fs-sm)' }}>Selected premium: {formatCurrency(selectedEndorsementPremium)}</span>
          </div>
          <div className="grid grid-cols-2 gap-2">
            {IM_OPTIONAL_ENDORSEMENTS.map((endorsement) => (
              <label key={endorsement.key} className="flex items-center justify-between gap-3 cursor-pointer" style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-md)', padding: '8px 10px', color: 'var(--ink-2)', fontSize: 'var(--fs-body)' }}>
                <span className="flex items-center gap-2">
                  <input
                    type="checkbox"
                    checked={imEndorsements[endorsement.key]}
                    disabled={isBound}
                    onChange={(e) => setImEndorsements((current) => ({ ...current, [endorsement.key]: e.target.checked }))}
                  />
                  {endorsement.label}
                </span>
                <span style={{ color: 'var(--ink-3)', fontSize: 'var(--fs-sm)' }}>{endorsement.premium > 0 ? formatCurrency(endorsement.premium) : 'No charge'}</span>
              </label>
            ))}
          </div>
          </div>
        </div>
      )}

      {/* Schedule modifier */}
      <div className="sd-card">
        <div className="sd-card-body space-y-3">
        <div className="flex items-center justify-between">
          <h4 className="text-xs font-semibold text-slate-700 uppercase">Schedule Modifier (IRPM)</h4>
          <span className="text-xs text-slate-500">Allowed range: {scheduleMin.toFixed(2)}–{scheduleMax.toFixed(2)}</span>
        </div>
        <div className="grid grid-cols-3 gap-3">
          <div>
            <label className="sims-field-label">Modifier</label>
            <input
              type="number"
              step="0.01"
              min={scheduleMin}
              max={scheduleMax}
              value={modifier}
              onChange={(e) => setModifier(parseFloat(e.target.value) || 1.0)}
              disabled={isBound}
              className="sims-input"
            />
          </div>
          <div className="col-span-2">
            <label className="sims-field-label">
              Reason {reasonRequired && <span className="text-red-600">*</span>}
            </label>
            <input
              type="text"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              disabled={isBound}
              placeholder={reasonRequired ? 'Required when modifier ≠ 1.00' : 'Optional'}
              className="sims-input"
              style={reasonInvalid ? { borderColor: 'var(--bad-fg)' } : undefined}
            />
          </div>
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
              className="sd-btn primary sm"
            >
              <Calculator size={14} />
              {rateMutation.isPending ? 'Calculating…' : snapshot ? 'Recalculate Premium' : 'Calculate Premium'}
            </button>
            {shadowStatus?.[LOB_SHADOW_KEY[lineOfBusiness]!] && (
              <button
                onClick={() => shadowMutation.mutate()}
                disabled={shadowMutation.isPending || rateMutation.isPending || reasonInvalid || equipment.length === 0 || blockedByMissingFields}
                className="sd-btn outline sm"
                title="Run engine without changing the quote premium — compare to spreadsheet"
              >
                <FlaskConical size={14} />
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
            <div className="flex items-start gap-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm">
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
        <div className="sd-card">
          <div className="sd-card-head">
            <h3>Calculation Detail</h3>
            <span style={{ color: 'var(--ink-3)', fontSize: 'var(--fs-sm)' }}>
              Rated {new Date(snapshot.ratedAt).toLocaleString()}
              {snapshot.ratedByName ? ` by ${snapshot.ratedByName}` : ''}
            </span>
          </div>
          <table className="sd-table">
            <thead>
              <tr>
                <th>Item</th>
                <th>Type</th>
                <th className="num">Stated Value</th>
                <th>Age Band</th>
                <th className="num">Base Rate</th>
                <th className="num">Ded Factor</th>
                <th className="num">Line Premium</th>
              </tr>
            </thead>
            <tbody>
              {snapshot.lines.filter((l) => !l.exposureRef.startsWith('IM-END-')).map((l) => {
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
              {lineOfBusiness === 'InlandMarine' && snapshot.endorsementPremium > 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-2 text-right text-slate-600">Optional Endorsements</td>
                  <td className="px-4 py-2 text-right font-medium">{formatCurrency(snapshot.endorsementPremium)}</td>
                </tr>
              )}
              <tr style={{ background: 'var(--info-bg)' }}>
                <td colSpan={6} className="px-4 py-3 text-right font-semibold" style={{ color: 'var(--info)' }}>Grand Total Premium</td>
                <td className="px-4 py-3 text-right text-base font-bold" style={{ color: 'var(--info)' }}>{formatCurrency(snapshot.grandTotalPremium)}</td>
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
