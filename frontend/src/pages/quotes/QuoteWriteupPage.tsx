import { useState, useEffect } from 'react'
import axios from 'axios'
import { useParams, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ArrowLeft, FlaskConical, CheckCircle2, AlertTriangle, ChevronDown, ChevronRight, Save, Send, ThumbsUp, Plus, Trash2 } from 'lucide-react'
import { uwWriteupApi } from '@/api/uwWriteup.api'
import type { IMWriteupPayload, WriteupCondition, UWWriteupDto } from '@/types/uwWriteup.types'
import { EMPTY_PAYLOAD } from '@/types/uwWriteup.types'
import { LOB_LABELS, type PolicyLineOfBusiness } from '@/types/quote.types'
import { formatCurrency } from '@/lib/utils'
import { usePermissions } from '@/hooks/usePermissions'
import { useUnsavedChangesGuard } from '@/hooks/useUnsavedChangesGuard'

// ── Small shared components ──────────────────────────────────────────────────

function Section({ title, defaultOpen = true, children }: { title: string; defaultOpen?: boolean; children: React.ReactNode }) {
  const [open, setOpen] = useState(defaultOpen)
  return (
    <div className="sd-card overflow-hidden">
      <button
        type="button"
        onClick={() => setOpen(!open)}
        className="flex w-full items-center justify-between px-5 py-3 text-left text-sm font-semibold"
        style={{ color: 'var(--ink-2)' }}
      >
        {title}
        {open ? <ChevronDown className="h-4 w-4" style={{ color: 'var(--ink-4)' }} /> : <ChevronRight className="h-4 w-4" style={{ color: 'var(--ink-4)' }} />}
      </button>
      {open && <div className="space-y-4 border-t px-5 py-4" style={{ borderColor: 'var(--line-2)', background: 'var(--surface-2)' }}>{children}</div>}
    </div>
  )
}

function FieldRow({ label, value }: { label: string; value?: string | number | null }) {
  return (
    <div className="min-w-0">
      <div className="sims-field-label">{label}</div>
      <div className="break-words text-sm font-medium" style={{ color: 'var(--ink-2)' }}>{value ?? '-'}</div>
    </div>
  )
}

function NarrativeBlock({
  label,
  prompt,
  value,
  onChange,
  readOnly,
}: {
  label: string
  prompt: string
  value?: string
  onChange: (v: string) => void
  readOnly: boolean
}) {
  return (
    <div>
      <div className="sims-field-label">{label}</div>
      <div className="mb-1 text-xs" style={{ color: 'var(--ink-3)' }}>{prompt}</div>
      <textarea
        rows={4}
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value)}
        readOnly={readOnly}
        placeholder={readOnly ? '' : 'Enter notes...'}
        className="sims-textarea"
      />
    </div>
  )
}

function ReferralCheckbox({
  label,
  autoChecked,
  value,
  onChange,
  readOnly,
}: {
  label: string
  autoChecked?: boolean
  value: boolean
  onChange: (v: boolean) => void
  readOnly: boolean
}) {
  return (
    <label className="flex cursor-pointer items-center gap-2 text-sm" style={{ color: 'var(--ink-2)' }}>
      <input
        type="checkbox"
        checked={value}
        onChange={(e) => onChange(e.target.checked)}
        disabled={readOnly}
        className="h-4 w-4 rounded"
        style={{ borderColor: 'var(--line)' }}
      />
      <span style={value ? { color: 'var(--ink-2)', fontWeight: 500 } : { color: 'var(--ink-3)' }}>{label}</span>
      {autoChecked && (
        <span className="rounded px-1.5 py-0.5 text-[10.5px] font-semibold" style={{ background: 'var(--warn-bg)', color: 'var(--warn-fg)' }}>auto</span>
      )}
    </label>
  )
}

// ── Main page ────────────────────────────────────────────────────────────────

function ShortText({
  label,
  value,
  onChange,
  readOnly,
}: {
  label: string
  value?: string
  onChange: (value: string) => void
  readOnly: boolean
}) {
  return (
    <label className="block">
      <span className="text-xs font-medium" style={{ color: 'var(--ink-3)' }}>{label}</span>
      <input
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value)}
        readOnly={readOnly}
        className="sims-input mt-1"
      />
    </label>
  )
}

function NumericField({
  label,
  value,
  onChange,
  readOnly,
  suffix,
}: {
  label: string
  value?: number | null
  onChange: (value: number | null) => void
  readOnly: boolean
  suffix?: string
}) {
  return (
    <label className="block">
      <span className="text-xs font-medium" style={{ color: 'var(--ink-3)' }}>{label}</span>
      <div className="mt-1 flex items-center gap-2">
        <input
          type="number"
          value={value ?? ''}
          onChange={(e) => onChange(e.target.value === '' ? null : Number(e.target.value))}
          readOnly={readOnly}
          className="sims-input"
        />
        {suffix && <span className="text-sm" style={{ color: 'var(--ink-3)' }}>{suffix}</span>}
      </div>
    </label>
  )
}

export default function QuoteWriteupPage() {
  const { quoteId } = useParams<{ quoteId: string }>()
  const qc = useQueryClient()
  const { canManageUnderwriting } = usePermissions()

  const { data: writeup, error, isError, isLoading } = useQuery({
    queryKey: ['uw-writeup', quoteId],
    queryFn: () => uwWriteupApi.get(quoteId!),
    enabled: !!quoteId,
    retry: false,
    // Seeds the full editable UW writeup form; a focus refetch would wipe unsaved narratives.
    refetchOnWindowFocus: false,
  })

  const [payload, setPayload] = useState<IMWriteupPayload>(EMPTY_PAYLOAD)
  const [conditions, setConditions] = useState<WriteupCondition[]>([])
  const [newConditionText, setNewConditionText] = useState('')
  const [submitDecision, setSubmitDecision] = useState('')
  const [showSubmitPanel, setShowSubmitPanel] = useState(false)

  // Sync state when data arrives
  useEffect(() => {
    if (writeup) {
      setPayload({ ...EMPTY_PAYLOAD, ...(writeup.payload ?? {}) })
      setConditions(writeup.conditions ?? [])
    }
  }, [writeup])

  const isReadOnly = writeup?.status !== 'Draft'

  // Warn before a tab close/refresh drops unsaved writeup edits (audit U7). Dirty =
  // local editable state diverges from what the loaded draft would seed.
  const isDirty = !!writeup && writeup.status === 'Draft' && (
    JSON.stringify(payload) !== JSON.stringify({ ...EMPTY_PAYLOAD, ...(writeup.payload ?? {}) }) ||
    JSON.stringify(conditions) !== JSON.stringify(writeup.conditions ?? [])
  )
  useUnsavedChangesGuard(isDirty)

  const saveMutation = useMutation({
    mutationFn: () => uwWriteupApi.save(quoteId!, { payload, conditions }),
    onSuccess: (data) => {
      qc.setQueryData(['uw-writeup', quoteId], data)
      toast.success('Writeup saved')
    },
    onError: () => toast.error('Failed to save'),
  })

  const submitMutation = useMutation({
    mutationFn: () => uwWriteupApi.submit(quoteId!, { decision: submitDecision }),
    onSuccess: (data) => {
      qc.setQueryData(['uw-writeup', quoteId], data)
      setShowSubmitPanel(false)
      toast.success('Writeup submitted')
    },
    onError: () => toast.error('Failed to submit'),
  })

  const approveMutation = useMutation({
    mutationFn: () => uwWriteupApi.approve(quoteId!),
    onSuccess: (data) => {
      qc.setQueryData(['uw-writeup', quoteId], data)
      toast.success('Writeup approved')
    },
    onError: () => toast.error('Failed to approve'),
  })

  const patchPayload = (patch: Partial<IMWriteupPayload>) =>
    setPayload((p) => ({ ...p, ...patch }))

  function addCondition() {
    if (!newConditionText.trim()) return
    setConditions((cs) => [
      ...cs,
      { id: crypto.randomUUID(), text: newConditionText.trim(), required: true, satisfied: false, sortOrder: cs.length },
    ])
    setNewConditionText('')
  }

  function removeCondition(id: string) {
    setConditions((cs) => cs.filter((c) => c.id !== id))
  }

  if (isError) {
    return (
      <div className="max-w-3xl mx-auto px-6 py-6">
        <Link to="/submissions" className="inline-flex items-center gap-2 text-sm" style={{ color: 'var(--ink-3)' }}>
          <ArrowLeft className="h-4 w-4" /> Back to submissions
        </Link>
        <div className="mt-5 rounded-lg border p-4 text-sm" style={{ borderColor: 'var(--bad-fg)', background: 'var(--bad-bg)', color: 'var(--bad-fg)' }}>
          <div className="font-semibold">Quote writeup could not be loaded.</div>
          <div className="mt-1">{getWriteupErrorMessage(error)}</div>
        </div>
      </div>
    )
  }

  if (isLoading || !writeup) {
    return (
      <div className="p-8 text-sm" style={{ color: 'var(--ink-4)' }}>Loading writeup…</div>
    )
  }

  const { equipment: eq } = writeup
  const lob = writeup.lob as PolicyLineOfBusiness
  const lobLabel = LOB_LABELS[lob] ?? writeup.lob
  const isInlandMarine = writeup.lob === 'InlandMarine'
  const isAutoLiability = writeup.lob === 'AutoLiability'
  const isAutoPhysicalDamage = writeup.lob === 'AutoPhysicalDamage'
  const isGeneralLiability = writeup.lob === 'GeneralLiability'
  const isAuto = isAutoLiability || isAutoPhysicalDamage || writeup.lob === 'CommercialAuto'

  return (
    <div className="mx-auto max-w-5xl space-y-5 px-6 py-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link to={`/submissions`} style={{ color: 'var(--ink-4)' }}>
            <ArrowLeft className="h-4 w-4" />
          </Link>
          <div>
            <h1 className="text-lg font-semibold flex items-center gap-2" style={{ color: 'var(--ink)' }}>
              <FlaskConical className="h-4 w-4" style={{ color: 'var(--ink-4)' }} />
              {lobLabel} Underwriting Writeup
            </h1>
            <p className="text-xs mt-0.5" style={{ color: 'var(--ink-4)' }}>
              {writeup.insuredName} · {writeup.effectiveDate}
            </p>
          </div>
        </div>
        <StatusBadge status={writeup.status} decision={writeup.decision} />
      </div>

      {/* Header info */}
      <Section title="Header" defaultOpen={false}>
        <div className="grid grid-cols-2 gap-x-8 gap-y-1.5">
          <FieldRow label="Underwriter" value={writeup.uwName} />
          <FieldRow label="Assistant UW" value={writeup.assistantUWName} />
          <FieldRow label="Agent" value={writeup.agentName} />
          <FieldRow label="Named Insured" value={writeup.insuredName} />
          <FieldRow label="Line of Business" value={lobLabel} />
          <FieldRow label="Policy Type" value={writeup.policyType} />
          <FieldRow label="Effective Date" value={writeup.effectiveDate} />
          <FieldRow label="Operation Type" value={writeup.operationType} />
          <FieldRow label="New Venture?" value={writeup.newVenture ? 'Yes' : 'No'} />
          <FieldRow label="Years in Business" value={writeup.yearsInBusiness} />
          <FieldRow label="Credit Score" value={writeup.creditScore} />
          <FieldRow label="Website" value={writeup.website} />
          <div className="col-span-2">
            <FieldRow label="Address" value={writeup.address} />
          </div>
          {writeup.priorCarriers.length > 0 && (
            <div className="col-span-2 space-y-1 pt-1 border-t" style={{ borderColor: 'var(--line-2)' }}>
              <div className="text-xs font-medium" style={{ color: 'var(--ink-4)' }}>Prior Carriers</div>
              {writeup.priorCarriers.map((pc, i) => (
                <div key={i} className="text-sm" style={{ color: 'var(--ink-2)' }}>
                  {pc.carrierName}
                  {pc.policyNumber && <span style={{ color: 'var(--ink-4)' }}> · {pc.policyNumber}</span>}
                  {pc.expirationDate && <span style={{ color: 'var(--ink-4)' }}> · exp {pc.expirationDate}</span>}
                  {pc.premiumAmount && <span style={{ color: 'var(--ink-4)' }}> · {formatCurrency(pc.premiumAmount, { cents: false })}</span>}
                </div>
              ))}
            </div>
          )}
          {writeup.newVenture && (
            <div className="col-span-2">
              <label className="flex items-center gap-2 text-sm cursor-pointer">
                <input
                  type="checkbox"
                  checked={payload.newVentureDocsOk ?? false}
                  onChange={(e) => patchPayload({ newVentureDocsOk: e.target.checked })}
                  disabled={isReadOnly}
                />
                <span style={{ color: 'var(--ink-3)' }}>Additional new venture documents received and OK?</span>
              </label>
            </div>
          )}
        </div>
      </Section>

      {(isAutoPhysicalDamage || isGeneralLiability) && (
        <Section title="Program / Market">
          <NarrativeBlock
            label="Program / market"
            prompt={isAutoPhysicalDamage ? "Lloyd's / Brace selection and any placement notes." : "Lloyd's / Brace GL selection and any placement notes."}
            value={payload.programMarket}
            onChange={(v) => patchPayload({ programMarket: v })}
            readOnly={isReadOnly}
          />
        </Section>
      )}

      {/* Referral triggers */}
      <Section title={isInlandMarine ? 'Referral Triggers' : 'Reason(s) for Referral'}>
        <div className="space-y-2">
          <ReferralCheckbox
            label={isInlandMarine ? 'Loss Ratio > 55%' : '4-year loss ratio > 50%'}
            value={payload.referralLossRatioOver55}
            onChange={(v) => patchPayload({ referralLossRatioOver55: v })}
            readOnly={isReadOnly}
          />
          {isInlandMarine ? (
            <>
              <ReferralCheckbox label="Single piece > $500,000" autoChecked={writeup.autoReferralPieceOver500k} value={payload.referralPieceOver500k || writeup.autoReferralPieceOver500k} onChange={(v) => patchPayload({ referralPieceOver500k: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="Total TIV > $2,000,000" autoChecked={writeup.autoReferralTivOver2mil} value={payload.referralTivOver2mil || writeup.autoReferralTivOver2mil} onChange={(v) => patchPayload({ referralTivOver2mil: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="Loss > $400,000" value={payload.referralLossOver400k} onChange={(v) => patchPayload({ referralLossOver400k: v })} readOnly={isReadOnly} />
            </>
          ) : (
            <ReferralCheckbox label="Any loss > $50,000" value={!!payload.referralLossOver50k} onChange={(v) => patchPayload({ referralLossOver50k: v })} readOnly={isReadOnly} />
          )}
          {isAuto && (
            <>
              <ReferralCheckbox label="FMCSA conditional / unsatisfactory" value={!!payload.referralFmcsaConditional} onChange={(v) => patchPayload({ referralFmcsaConditional: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="BASIC over threshold" value={!!payload.referralBasicOverThreshold} onChange={(v) => patchPayload({ referralBasicOverThreshold: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="Schedule credit > 20%" value={!!payload.referralScheduleCreditOver20} onChange={(v) => patchPayload({ referralScheduleCreditOver20: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="Premium > $100K" value={!!payload.referralPremiumOver100k} onChange={(v) => patchPayload({ referralPremiumOver100k: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="Owner-operator > 30%" value={!!payload.referralOwnerOperatorOver30} onChange={(v) => patchPayload({ referralOwnerOperatorOver30: v })} readOnly={isReadOnly} />
            </>
          )}
          {isAutoPhysicalDamage && (
            <>
              <ReferralCheckbox label="Rate reduction > 5%" value={!!payload.referralRateReduction} onChange={(v) => patchPayload({ referralRateReduction: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="Unit ACV / stated amount over cap" value={!!payload.referralUnitOverCap} onChange={(v) => patchPayload({ referralUnitOverCap: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="TIV one location over threshold" value={!!payload.referralTivLocationThreshold} onChange={(v) => patchPayload({ referralTivLocationThreshold: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="Tornado / hail exposure" value={!!payload.referralTornadoHail} onChange={(v) => patchPayload({ referralTornadoHail: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="Coastal APD exposure" value={!!payload.referralCoastalApd} onChange={(v) => patchPayload({ referralCoastalApd: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="Credit score below threshold" value={!!payload.referralCreditScoreLow} onChange={(v) => patchPayload({ referralCreditScoreLow: v })} readOnly={isReadOnly} />
            </>
          )}
          {isGeneralLiability && (
            <>
              <ReferralCheckbox label="UW credit > 20%" autoChecked={writeup.scheduleCreditPercent > 20} value={!!payload.referralGlUwCreditOver20 || writeup.scheduleCreditPercent > 20} onChange={(v) => patchPayload({ referralGlUwCreditOver20: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="Logging revenue below threshold" value={!!payload.referralGlRevenueBelowThreshold} onChange={(v) => patchPayload({ referralGlRevenueBelowThreshold: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="Sawmill / lumberyard operations" value={!!payload.referralSawmillOps} onChange={(v) => patchPayload({ referralSawmillOps: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="Residential work" value={!!payload.referralResidentialWork} onChange={(v) => patchPayload({ referralResidentialWork: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="Burning exposure" value={!!payload.referralBurningExposure} onChange={(v) => patchPayload({ referralBurningExposure: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="Payroll change > 25%" value={!!payload.referralPayrollChangeOver25} onChange={(v) => patchPayload({ referralPayrollChangeOver25: v })} readOnly={isReadOnly} />
              <ReferralCheckbox label="Subcontractors without COI / hold harmless" value={!!payload.referralSubcontractorControls} onChange={(v) => patchPayload({ referralSubcontractorControls: v })} readOnly={isReadOnly} />
            </>
          )}
          <div className="flex items-center gap-2 pt-1">
            <span className="text-sm shrink-0" style={{ color: 'var(--ink-3)' }}>Other:</span>
            <input
              type="text"
              value={payload.referralOtherText ?? ''}
              onChange={(e) => patchPayload({ referralOtherText: e.target.value })}
              readOnly={isReadOnly}
              placeholder="Describe…"
              className="sd-input flex-1 text-sm px-2.5 py-1.5"
            />
          </div>
        </div>
      </Section>

      {isGeneralLiability && (
        <Section title="GL Eligibility & Referral Facts">
          <div className="rounded-md px-3 py-2 text-sm" style={{ background: 'var(--surface-2)', color: 'var(--ink-2)' }}>
            Schedule credit from rater: <span className="font-semibold">{writeup.scheduleCreditPercent.toFixed(2)}%</span>
            {writeup.scheduleModifier != null && <span style={{ color: 'var(--ink-4)' }}> (modifier {writeup.scheduleModifier.toFixed(2)})</span>}
          </div>
          <div className="grid grid-cols-3 gap-3 max-[900px]:grid-cols-1">
            <NumericField label="Logging revenue" value={payload.glLoggingRevenuePercent} onChange={(v) => patchPayload({ glLoggingRevenuePercent: v })} readOnly={isReadOnly} suffix="%" />
            <NumericField label="Management experience" value={payload.glManagementExperienceYears} onChange={(v) => patchPayload({ glManagementExperienceYears: v })} readOnly={isReadOnly} suffix="years" />
            <NumericField label="Largest single loss" value={payload.glLargestSingleLossAmount} onChange={(v) => patchPayload({ glLargestSingleLossAmount: v })} readOnly={isReadOnly} />
          </div>
          <div className="grid grid-cols-2 gap-2 max-[700px]:grid-cols-1">
            <ReferralCheckbox label="Fuel storage over max allowable" value={!!payload.glFuelStorageOverMax} onChange={(v) => patchPayload({ glFuelStorageOverMax: v })} readOnly={isReadOnly} />
            <ReferralCheckbox label="Log road building exceeds allowed percent" value={!!payload.glLogRoadBuildingOverAllowed} onChange={(v) => patchPayload({ glLogRoadBuildingOverAllowed: v })} readOnly={isReadOnly} />
            <ReferralCheckbox label="Grading/excavation exceeds allowed percent" value={!!payload.glGradingExcavationOverAllowed} onChange={(v) => patchPayload({ glGradingExcavationOverAllowed: v })} readOnly={isReadOnly} />
            <ReferralCheckbox label="Aircraft/drone operations" value={!!payload.glAircraftOrDroneOps} onChange={(v) => patchPayload({ glAircraftOrDroneOps: v })} readOnly={isReadOnly} />
            <ReferralCheckbox label="Explosives used" value={!!payload.glExplosivesUsed} onChange={(v) => patchPayload({ glExplosivesUsed: v })} readOnly={isReadOnly} />
            <ReferralCheckbox label="Non-mechanized logging" value={!!payload.glNonMechanizedLogging} onChange={(v) => patchPayload({ glNonMechanizedLogging: v })} readOnly={isReadOnly} />
            <ReferralCheckbox label="Bankruptcy or receivership" value={!!payload.glBankruptcyOrReceivership} onChange={(v) => patchPayload({ glBankruptcyOrReceivership: v })} readOnly={isReadOnly} />
            <ReferralCheckbox label="Herbicide/pesticide application" value={!!payload.glHerbicidePesticideApplication} onChange={(v) => patchPayload({ glHerbicidePesticideApplication: v })} readOnly={isReadOnly} />
            <ReferralCheckbox label="Crane use outside allowed operations" value={!!payload.glCraneUseOutsideAllowed} onChange={(v) => patchPayload({ glCraneUseOutsideAllowed: v })} readOnly={isReadOnly} />
            <ReferralCheckbox label="Equipment rental/leasing to others" value={!!payload.glEquipmentRentalToOthers} onChange={(v) => patchPayload({ glEquipmentRentalToOthers: v })} readOnly={isReadOnly} />
            <ReferralCheckbox label="Third-party equipment repair/service" value={!!payload.glThirdPartyEquipmentRepair} onChange={(v) => patchPayload({ glThirdPartyEquipmentRepair: v })} readOnly={isReadOnly} />
            <ReferralCheckbox label="Right-of-way clearing/maintenance" value={!!payload.glRightOfWayClearing} onChange={(v) => patchPayload({ glRightOfWayClearing: v })} readOnly={isReadOnly} />
          </div>
        </Section>
      )}

      {/* Losses */}
      <Section title="Losses">
        {!isInlandMarine && (
          <NarrativeBlock
            label="Loss synopsis"
            prompt="Trends, repeat drivers or units, severity drivers, attorney involvement, and shock loss concerns."
            value={payload.lossSynopsis}
            onChange={(v) => patchPayload({ lossSynopsis: v })}
            readOnly={isReadOnly}
          />
        )}
        <NarrativeBlock
          label="Mitigation actions"
          prompt="Describe any action taken by the insured to prevent future losses."
          value={payload.lossMitigationActions}
          onChange={(v) => patchPayload({ lossMitigationActions: v })}
          readOnly={isReadOnly}
        />
        <NarrativeBlock
          label={isInlandMarine ? 'Losses over $25,000' : isGeneralLiability ? 'GL BI / attorney / losses over $50,000' : 'Losses over $50,000'}
          prompt="Describe each loss exceeding $25,000 — date, cause, amount, status."
          value={isInlandMarine ? payload.lossesOver25kDescription : payload.lossesOver50kDescription}
          onChange={(v) => isInlandMarine ? patchPayload({ lossesOver25kDescription: v }) : patchPayload({ lossesOver50kDescription: v })}
          readOnly={isReadOnly}
        />
      </Section>

      {/* Equipment & Values */}
      {(isInlandMarine || isAutoPhysicalDamage) && (
      <Section title={isAutoPhysicalDamage ? 'Vehicles, Values & CAB' : 'Equipment & Values'}>
        <div className="grid grid-cols-4 gap-3 text-center">
          {[
            { label: 'Total TIV', value: formatCurrency(eq.totalTiv, { cents: false }) },
            { label: 'Largest Unit', value: formatCurrency(eq.largestUnitTiv, { cents: false }) },
            { label: 'Cutters', value: eq.countCutter },
            { label: 'Skidders', value: eq.countSkidder },
            { label: 'Loaders', value: eq.countLoader },
            { label: 'Dozers', value: eq.countDozer },
            { label: 'Other', value: eq.countOther },
            { label: 'Total Units', value: eq.totalCount },
          ].map(({ label, value }) => (
            <div key={label} className="rounded-lg px-3 py-2" style={{ background: 'var(--surface-2)' }}>
              <div className="text-xs" style={{ color: 'var(--ink-4)' }}>{label}</div>
              <div className="text-sm font-semibold mt-0.5" style={{ color: 'var(--ink-2)' }}>{value}</div>
            </div>
          ))}
        </div>
        <label className="flex items-center gap-2 text-sm cursor-pointer pt-1">
          <input
            type="checkbox"
            checked={payload.eqValueChecked}
            onChange={(e) => patchPayload({ eqValueChecked: e.target.checked })}
            disabled={isReadOnly}
          />
          <span style={{ color: 'var(--ink-3)' }}>Equipment values verified against appraisals / invoices</span>
        </label>
        {isAutoPhysicalDamage && (
          <>
            <NarrativeBlock label="Max concentration at one location" prompt="Note highest location concentration and flood / tornado / hail concern." value={payload.maxConcentrationOneLocation} onChange={(v) => patchPayload({ maxConcentrationOneLocation: v })} readOnly={isReadOnly} />
            <NarrativeBlock label="CAB alerts / FMCSA / ISS rating notes" prompt="Vehicle maintenance, driver, unsafe, crash, hours, FMCSA rating, ISS/CAB rating." value={payload.cabAlertsNotes} onChange={(v) => patchPayload({ cabAlertsNotes: v })} readOnly={isReadOnly} />
          </>
        )}
      </Section>
      )}

      {isAutoLiability && (
        <Section title="Vehicles, FMCSA & CAB">
          <NarrativeBlock label="Vehicle / power unit summary" prompt="Tractors, trucks, trailers, total power units, age profile, maintenance, and telematics." value={payload.narrativeEquipment} onChange={(v) => patchPayload({ narrativeEquipment: v })} readOnly={isReadOnly} />
          <NarrativeBlock label="CAB alerts" prompt="Vehicle maintenance, driver, unsafe, crash, or hours alerts." value={payload.cabAlertsNotes} onChange={(v) => patchPayload({ cabAlertsNotes: v })} readOnly={isReadOnly} />
          <div className="grid grid-cols-2 gap-3 max-[700px]:grid-cols-1">
            <ShortText label="FMCSA safety rating" value={payload.fmcsaSafetyRating} onChange={(v) => patchPayload({ fmcsaSafetyRating: v })} readOnly={isReadOnly} />
            <ShortText label="ISS / CAB rating" value={payload.issCabRating} onChange={(v) => patchPayload({ issCabRating: v })} readOnly={isReadOnly} />
          </div>
        </Section>
      )}

      {isGeneralLiability && (
        <Section title="Exposures & ISO Class Codes">
          <NarrativeBlock label="Class code exposure notes" prompt="Payroll, sales, receipts, class mix, expiring to proposed change, and any referral class concerns." value={payload.glClassExposureNotes} onChange={(v) => patchPayload({ glClassExposureNotes: v })} readOnly={isReadOnly} />
        </Section>
      )}

      {/* Operations & Metrics */}
      <Section title={isAuto ? 'Operations & Fleet Metrics' : isGeneralLiability ? 'GL Operations Review' : 'Operations & Metrics'}>
        <div className="grid grid-cols-2 gap-4">
          {isInlandMarine && (
            <label className="flex items-center gap-2 text-sm cursor-pointer col-span-2">
              <input
                type="checkbox"
                checked={payload.waterborneExposure}
                onChange={(e) => patchPayload({ waterborneExposure: e.target.checked })}
                disabled={isReadOnly}
              />
              <span style={{ color: 'var(--ink-3)' }}>Any waterborne exposure?</span>
            </label>
          )}
          {isGeneralLiability && (
            <>
              <div className="col-span-2">
                <NarrativeBlock label="Risk characteristics" prompt="Mechanized operations, site/public controls, fire controls, residential/burning/sawmill concerns." value={payload.glRiskCharacteristics} onChange={(v) => patchPayload({ glRiskCharacteristics: v })} readOnly={isReadOnly} />
              </div>
              <div className="col-span-2">
                <NarrativeBlock label="Subcontractor and contract controls" prompt="Sub counts, COI/AI, hold-harmless, timber contracts, line verification, and woods employees." value={payload.glSubcontractorControls} onChange={(v) => patchPayload({ glSubcontractorControls: v })} readOnly={isReadOnly} />
              </div>
            </>
          )}
          <div className="space-y-1">
            <label className="text-xs font-medium" style={{ color: 'var(--ink-3)' }}>Last Inspection Date</label>
            <input
              type="date"
              value={payload.lastInspectionDate ?? ''}
              onChange={(e) => patchPayload({ lastInspectionDate: e.target.value })}
              readOnly={isReadOnly}
              className="sd-input w-full text-sm px-2.5 py-1.5"
            />
          </div>
          <div className="space-y-1">
            <label className="text-xs font-medium" style={{ color: 'var(--ink-3)' }}>Website reviewed?</label>
            <select
              value={payload.websiteReviewed === true ? 'yes' : payload.websiteReviewed === false ? 'no' : ''}
              onChange={(e) => patchPayload({ websiteReviewed: e.target.value === 'yes' ? true : e.target.value === 'no' ? false : null })}
              disabled={isReadOnly}
              className="sd-input w-full text-sm px-2.5 py-1.5"
            >
              <option value="">—</option>
              <option value="yes">Yes — reviewed</option>
              <option value="no">Not found</option>
            </select>
          </div>
        </div>
        <label className="flex items-start gap-2 text-sm cursor-pointer">
          <input
            type="checkbox"
            checked={payload.recommendationsOutstanding}
            onChange={(e) => patchPayload({ recommendationsOutstanding: e.target.checked })}
            disabled={isReadOnly}
            className="mt-0.5"
          />
          <span style={{ color: 'var(--ink-3)' }}>Recommendations outstanding from prior inspection?</span>
        </label>
        {payload.recommendationsOutstanding && (
          <textarea
            rows={2}
            value={payload.recommendationsDetail ?? ''}
            onChange={(e) => patchPayload({ recommendationsDetail: e.target.value })}
            readOnly={isReadOnly}
            placeholder="Describe recommendations…"
            className="sd-input w-full px-3 py-2 text-sm resize-y"
          />
        )}
        {payload.websiteReviewed && payload.websiteIssues !== undefined && (
          <textarea
            rows={2}
            value={payload.websiteIssues ?? ''}
            onChange={(e) => patchPayload({ websiteIssues: e.target.value })}
            readOnly={isReadOnly}
            placeholder="Note any website issues…"
            className="sd-input w-full px-3 py-2 text-sm resize-y"
          />
        )}
      </Section>

      {(isAutoLiability || isAutoPhysicalDamage) && (
        <Section title="Drivers">
          <div className="grid grid-cols-2 gap-3 max-[700px]:grid-cols-1">
            <ShortText label="# of drivers" value={payload.driverCount} onChange={(v) => patchPayload({ driverCount: v })} readOnly={isReadOnly} />
            <ShortText label="Driver age span" value={payload.driverAgeSpan} onChange={(v) => patchPayload({ driverAgeSpan: v })} readOnly={isReadOnly} />
            <ShortText label="Driver turnover %" value={payload.driverTurnoverPercent} onChange={(v) => patchPayload({ driverTurnoverPercent: v })} readOnly={isReadOnly} />
            <ShortText label="Owner-op %" value={payload.ownerOperatorPercent} onChange={(v) => patchPayload({ ownerOperatorPercent: v })} readOnly={isReadOnly} />
          </div>
          <ReferralCheckbox label="MVRs in file within 90 days" value={payload.mvrInFile === true} onChange={(v) => patchPayload({ mvrInFile: v })} readOnly={isReadOnly} />
          <NarrativeBlock label="Drivers to exclude or watch" prompt="Name, reason, MVR/CAB detail, and required action." value={payload.driversWatchNotes} onChange={(v) => patchPayload({ driversWatchNotes: v })} readOnly={isReadOnly} />
        </Section>
      )}

      {/* UW Narratives */}
      <Section title="Underwriting Notes">
        <NarrativeBlock
          label={isGeneralLiability ? 'Operation' : 'Operators / Operation'}
          prompt={isGeneralLiability ? 'What they do, where, contracts, wood types, logging type, and landowner type.' : 'Insured employees, operation mix, hauls for whom, training/certs, and concerns.'}
          value={payload.narrativeOperators}
          onChange={(v) => patchPayload({ narrativeOperators: v })}
          readOnly={isReadOnly}
        />
        {isInlandMarine && (
          <>
            <NarrativeBlock
              label="Equipment"
              prompt="Age, maintenance records, cool-down procedure, average age, deductible reasoning, and usage patterns."
              value={payload.narrativeEquipment}
              onChange={(v) => patchPayload({ narrativeEquipment: v })}
              readOnly={isReadOnly}
            />
            <NarrativeBlock
              label="Fire Suppression"
              prompt="Type of fire suppression system installed, maintenance schedule, last service date."
              value={payload.narrativeFireSuppression}
              onChange={(v) => patchPayload({ narrativeFireSuppression: v })}
              readOnly={isReadOnly}
            />
          </>
        )}
        {(isAutoLiability || isAutoPhysicalDamage) && (
          <>
            <NarrativeBlock label="Drivers" prompt="Average age, turnover, DOT documentation, experience, date of hire, MVR violations, fatigue/hours management." value={payload.narrativeDrivers} onChange={(v) => patchPayload({ narrativeDrivers: v })} readOnly={isReadOnly} />
            <NarrativeBlock label="CAB / FMCSA" prompt="BASICs, severe violations, accidents not on loss runs, radius, overall concerns and trends." value={payload.narrativeCabFmcsa} onChange={(v) => patchPayload({ narrativeCabFmcsa: v })} readOnly={isReadOnly} />
            <NarrativeBlock label="Additional interests and contracts" prompt="Who, how many, hold-harmless, AI blanket vs scheduled, and certificate requirements." value={payload.narrativeAdditionalInterests} onChange={(v) => patchPayload({ narrativeAdditionalInterests: v })} readOnly={isReadOnly} />
          </>
        )}
        {isGeneralLiability && (
          <>
            <NarrativeBlock label="Exposure changes" prompt="Payroll movement, class code mix, subcontractor costs, and drivers of any swing over 25%." value={payload.glExposureChanges} onChange={(v) => patchPayload({ glExposureChanges: v })} readOnly={isReadOnly} />
            <NarrativeBlock label="Subcontractors" prompt="Count, types, COI and hold-harmless discipline, who they haul for, and AI requirements." value={payload.glSubcontractorsNarrative} onChange={(v) => patchPayload({ glSubcontractorsNarrative: v })} readOnly={isReadOnly} />
            <NarrativeBlock label="Endorsements and additional interests" prompt="Logging endorsement limit, blanket vs scheduled AI/WOS/PNC, and certificates issued." value={payload.glEndorsementsNarrative} onChange={(v) => patchPayload({ glEndorsementsNarrative: v })} readOnly={isReadOnly} />
          </>
        )}
        <NarrativeBlock
          label="Other Concerns"
          prompt="Any additional risk concerns, positive attributes, or notable observations."
          value={payload.narrativeOtherConcerns}
          onChange={(v) => patchPayload({ narrativeOtherConcerns: v })}
          readOnly={isReadOnly}
        />
      </Section>

      {!isInlandMarine && (
        <Section title="Requested Terms / Pricing">
          <NarrativeBlock
            label="Pricing rationale"
            prompt={isGeneralLiability ? 'Payroll change, class code mix, sub cost, credit drivers, and target rate comparison.' : 'Exposure change, mix shift, credits/debits, telematics/safety credits, and rate adequacy.'}
            value={payload.pricingRationale}
            onChange={(v) => patchPayload({ pricingRationale: v })}
            readOnly={isReadOnly}
          />
          <NarrativeBlock
            label="Special terms / endorsements"
            prompt="Requested endorsements, sublimits, deductibles, AI/WOS/PNC counts, and any file-specific terms."
            value={payload.specialTerms}
            onChange={(v) => patchPayload({ specialTerms: v })}
            readOnly={isReadOnly}
          />
        </Section>
      )}

      {/* Conditions */}
      <Section title="Conditions (if approving with conditions)">
        <div className="space-y-2">
          {conditions.length === 0 && (
            <p className="text-sm italic" style={{ color: 'var(--ink-4)' }}>No conditions added.</p>
          )}
          {conditions.map((c) => (
            <div key={c.id} className="flex items-start gap-2">
              {!isReadOnly ? (
                <>
                  <input
                    type="text"
                    value={c.text}
                    onChange={(e) => setConditions((cs) => cs.map((x) => x.id === c.id ? { ...x, text: e.target.value } : x))}
                    className="sd-input flex-1 text-sm px-2.5 py-1.5"
                  />
                  <button
                    type="button"
                    onClick={() => removeCondition(c.id)}
                    className="mt-1.5"
                    style={{ color: 'var(--ink-4)' }}
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>
                </>
              ) : (
                <div className="flex items-center gap-2 text-sm">
                  {c.satisfied
                    ? <CheckCircle2 className="h-4 w-4 shrink-0" style={{ color: 'var(--good-fg)' }} />
                    : <AlertTriangle className="h-4 w-4 shrink-0" style={{ color: 'var(--warn-fg)' }} />}
                  <span style={c.satisfied ? { color: 'var(--ink-4)', textDecoration: 'line-through' } : { color: 'var(--ink-2)' }}>{c.text}</span>
                </div>
              )}
            </div>
          ))}
          {!isReadOnly && (
            <div className="flex gap-2 pt-1">
              <input
                type="text"
                value={newConditionText}
                onChange={(e) => setNewConditionText(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && addCondition()}
                placeholder="Add a condition…"
                className="sd-input flex-1 text-sm px-2.5 py-1.5"
              />
              <button
                type="button"
                onClick={addCondition}
                className="flex items-center gap-1 px-3 py-1.5 text-sm rounded"
                style={{ background: 'var(--surface-2)', color: 'var(--ink-3)' }}
              >
                <Plus className="h-3.5 w-3.5" />
                Add
              </button>
            </div>
          )}
        </div>
      </Section>

      {/* Recommendation / Rationale */}
      <Section title={isInlandMarine ? 'Recommendation' : 'Loss Control & Recommendation'}>
        {!isInlandMarine && (
          <NarrativeBlock
            label="Loss control analysis"
            prompt="Underwriter loss control analysis and any action taken as a result."
            value={payload.lossControlAnalysis}
            onChange={(v) => patchPayload({ lossControlAnalysis: v })}
            readOnly={isReadOnly}
          />
        )}
        <NarrativeBlock
          label="Rationale"
          prompt="Summarize the risk, explain your decision, and note any concerns for the file."
          value={payload.decisionRationale}
          onChange={(v) => patchPayload({ decisionRationale: v })}
          readOnly={isReadOnly}
        />
        {writeup.submittedAt && (
          <div className="text-xs pt-1" style={{ color: 'var(--ink-4)' }}>
            Submitted by {writeup.submittedByName} on {new Date(writeup.submittedAt).toLocaleDateString()}
            {writeup.approvedAt && ` · Approved by ${writeup.approvedByName} on ${new Date(writeup.approvedAt).toLocaleDateString()}`}
          </div>
        )}
      </Section>

      {/* Action bar */}
      {writeup.status === 'Draft' && (
        <div className="sticky bottom-0 -mx-6 flex items-center justify-between gap-3 border-t px-6 py-3 shadow-sm backdrop-blur" style={{ borderColor: 'var(--line)', background: 'var(--surface)' }}>
          <button
            type="button"
            onClick={() => saveMutation.mutate()}
            disabled={saveMutation.isPending}
            className="sd-btn outline"
          >
            <Save className="h-4 w-4" />
            {saveMutation.isPending ? 'Saving…' : 'Save Draft'}
          </button>
          <button
            type="button"
            onClick={() => setShowSubmitPanel(!showSubmitPanel)}
            className="sd-btn primary"
          >
            <Send className="h-4 w-4" />
            Submit Decision
          </button>
        </div>
      )}

      {writeup.status === 'Submitted' && (
        <div className="sticky bottom-0 -mx-6 flex items-center justify-end gap-3 border-t px-6 py-3 shadow-sm backdrop-blur" style={{ borderColor: 'var(--line)', background: 'var(--surface)' }}>
          {canManageUnderwriting ? (
            <button
              type="button"
              onClick={() => approveMutation.mutate()}
              disabled={approveMutation.isPending}
              className="sd-btn primary"
            >
              <ThumbsUp className="h-4 w-4" />
              {approveMutation.isPending ? 'Approving…' : 'Approve'}
            </button>
          ) : (
            <span className="text-xs" style={{ color: 'var(--ink-4)' }}>
              Underwriting approval permission required to approve this writeup.
            </span>
          )}
        </div>
      )}

      {/* Submit decision panel */}
      {showSubmitPanel && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ background: 'rgba(0,0,0,0.3)' }}>
          <div className="sims-modal w-full max-w-sm space-y-4 p-6">
            <h2 className="text-base font-semibold" style={{ color: 'var(--ink-2)' }}>Submit Decision</h2>
            <div className="space-y-2">
              {(['Approve', 'ApproveWithConditions', 'ReferUp', 'Decline'] as const).map((d) => (
                <label key={d} className="flex items-center gap-2 text-sm cursor-pointer">
                  <input
                    type="radio"
                    name="decision"
                    value={d}
                    checked={submitDecision === d}
                    onChange={() => setSubmitDecision(d)}
                    className="h-4 w-4"
                    style={{ borderColor: 'var(--line)' }}
                  />
                  <span>{DECISION_LABELS[d]}</span>
                </label>
              ))}
            </div>
            <div className="flex gap-2 pt-2">
              <button
                type="button"
                onClick={() => setShowSubmitPanel(false)}
                className="sd-btn outline flex-1"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={() => submitMutation.mutate()}
                disabled={!submitDecision || submitMutation.isPending}
                className="sd-btn primary flex-1"
              >
                {submitMutation.isPending ? 'Submitting…' : 'Confirm'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

const DECISION_LABELS: Record<string, string> = {
  Approve: 'Approve',
  ApproveWithConditions: 'Approve with Conditions',
  ReferUp: 'Refer Up',
  Decline: 'Decline',
}

function getWriteupErrorMessage(error: unknown) {
  if (!axios.isAxiosError(error)) return 'Please try again.'
  if (error.response?.status === 403) return 'You do not have permission to open this quote writeup.'
  if (error.response?.status === 404) return 'This quote writeup was not found.'

  const data = error.response?.data as { errorMessage?: string; detail?: string; title?: string } | undefined
  return data?.errorMessage ?? data?.detail ?? data?.title ?? 'Please try again.'
}

function StatusBadge({ status, decision }: { status: UWWriteupDto['status']; decision?: string }) {
  const config = {
    Draft:     { style: { background: 'var(--surface-2)', color: 'var(--ink-3)' }, label: 'Draft' },
    Submitted: { style: { background: 'var(--surface-2)', color: 'var(--accent-ink)' }, label: decision ? `Submitted · ${DECISION_LABELS[decision] ?? decision}` : 'Submitted' },
    Approved:  { style: { background: 'var(--good-bg)', color: 'var(--good-fg)' }, label: 'Approved' },
    Declined:  { style: { background: 'var(--bad-bg)', color: 'var(--bad-fg)' }, label: 'Declined' },
  }[status]

  return (
    <span className="px-2.5 py-1 rounded-full text-xs font-semibold" style={config.style}>
      {config.label}
    </span>
  )
}
