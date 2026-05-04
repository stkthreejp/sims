import { useState, useEffect } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ArrowLeft, FlaskConical, CheckCircle2, AlertTriangle, ChevronDown, ChevronRight, Save, Send, ThumbsUp, Plus, Trash2 } from 'lucide-react'
import { uwWriteupApi } from '@/api/uwWriteup.api'
import type { IMWriteupPayload, WriteupCondition, UWWriteupDto } from '@/types/uwWriteup.types'
import { EMPTY_PAYLOAD } from '@/types/uwWriteup.types'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 })

// ── Small shared components ──────────────────────────────────────────────────

function Section({ title, defaultOpen = true, children }: { title: string; defaultOpen?: boolean; children: React.ReactNode }) {
  const [open, setOpen] = useState(defaultOpen)
  return (
    <div className="border border-slate-200 rounded-lg overflow-hidden">
      <button
        type="button"
        onClick={() => setOpen(!open)}
        className="w-full flex items-center justify-between px-4 py-3 bg-slate-50 hover:bg-slate-100 text-sm font-semibold text-slate-700 transition-colors"
      >
        {title}
        {open ? <ChevronDown className="h-4 w-4 text-slate-400" /> : <ChevronRight className="h-4 w-4 text-slate-400" />}
      </button>
      {open && <div className="px-4 py-4 space-y-4">{children}</div>}
    </div>
  )
}

function FieldRow({ label, value }: { label: string; value?: string | number | null }) {
  return (
    <div className="flex justify-between text-sm">
      <span className="text-slate-400">{label}</span>
      <span className="text-slate-700 font-medium text-right max-w-xs">{value ?? '—'}</span>
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
    <div className="space-y-1.5">
      <div className="text-xs font-semibold text-slate-600 uppercase tracking-wide">{label}</div>
      <div className="text-xs text-slate-400 italic">{prompt}</div>
      <textarea
        rows={4}
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value)}
        readOnly={readOnly}
        placeholder={readOnly ? '' : 'Enter notes…'}
        className="w-full rounded border border-slate-200 px-3 py-2 text-sm text-slate-700 placeholder-slate-300 focus:outline-none focus:ring-1 focus:ring-blue-400 resize-y disabled:bg-slate-50"
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
    <label className="flex items-center gap-2 text-sm cursor-pointer">
      <input
        type="checkbox"
        checked={value}
        onChange={(e) => onChange(e.target.checked)}
        disabled={readOnly}
        className="rounded border-slate-300 text-blue-600 focus:ring-blue-400"
      />
      <span className={value ? 'text-slate-800 font-medium' : 'text-slate-600'}>{label}</span>
      {autoChecked && (
        <span className="text-xs px-1.5 py-0.5 rounded bg-amber-100 text-amber-700 font-medium">auto</span>
      )}
    </label>
  )
}

// ── Main page ────────────────────────────────────────────────────────────────

export default function QuoteWriteupPage() {
  const { quoteId } = useParams<{ quoteId: string }>()
  const qc = useQueryClient()

  const { data: writeup, isLoading } = useQuery({
    queryKey: ['uw-writeup', quoteId],
    queryFn: () => uwWriteupApi.get(quoteId!),
    enabled: !!quoteId,
  })

  const [payload, setPayload] = useState<IMWriteupPayload>(EMPTY_PAYLOAD)
  const [conditions, setConditions] = useState<WriteupCondition[]>([])
  const [newConditionText, setNewConditionText] = useState('')
  const [submitDecision, setSubmitDecision] = useState('')
  const [showSubmitPanel, setShowSubmitPanel] = useState(false)

  // Sync state when data arrives
  useEffect(() => {
    if (writeup) {
      setPayload(writeup.payload ?? EMPTY_PAYLOAD)
      setConditions(writeup.conditions ?? [])
    }
  }, [writeup])

  const isReadOnly = writeup?.status !== 'Draft'

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

  if (isLoading || !writeup) {
    return (
      <div className="p-8 text-sm text-slate-400">Loading writeup…</div>
    )
  }

  const { equipment: eq } = writeup

  return (
    <div className="max-w-3xl mx-auto px-6 py-6 space-y-5">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link to={`/submissions`} className="text-slate-400 hover:text-slate-600">
            <ArrowLeft className="h-4 w-4" />
          </Link>
          <div>
            <h1 className="text-lg font-semibold text-slate-900 flex items-center gap-2">
              <FlaskConical className="h-4 w-4 text-slate-400" />
              IM Underwriting Writeup
            </h1>
            <p className="text-xs text-slate-400 mt-0.5">
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
          <FieldRow label="Line of Business" value={writeup.lob} />
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
            <div className="col-span-2 space-y-1 pt-1 border-t border-slate-100">
              <div className="text-xs text-slate-400 font-medium">Prior Carriers</div>
              {writeup.priorCarriers.map((pc, i) => (
                <div key={i} className="text-sm text-slate-700">
                  {pc.carrierName}
                  {pc.policyNumber && <span className="text-slate-400"> · {pc.policyNumber}</span>}
                  {pc.expirationDate && <span className="text-slate-400"> · exp {pc.expirationDate}</span>}
                  {pc.premiumAmount && <span className="text-slate-400"> · {fmt.format(pc.premiumAmount)}</span>}
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
                <span className="text-slate-600">Additional new venture documents received and OK?</span>
              </label>
            </div>
          )}
        </div>
      </Section>

      {/* Referral triggers */}
      <Section title="Referral Triggers">
        <div className="space-y-2">
          <ReferralCheckbox
            label="Loss Ratio > 55%"
            value={payload.referralLossRatioOver55}
            onChange={(v) => patchPayload({ referralLossRatioOver55: v })}
            readOnly={isReadOnly}
          />
          <ReferralCheckbox
            label="Single piece > $500,000"
            autoChecked={writeup.autoReferralPieceOver500k}
            value={payload.referralPieceOver500k || writeup.autoReferralPieceOver500k}
            onChange={(v) => patchPayload({ referralPieceOver500k: v })}
            readOnly={isReadOnly}
          />
          <ReferralCheckbox
            label="Total TIV > $2,000,000"
            autoChecked={writeup.autoReferralTivOver2mil}
            value={payload.referralTivOver2mil || writeup.autoReferralTivOver2mil}
            onChange={(v) => patchPayload({ referralTivOver2mil: v })}
            readOnly={isReadOnly}
          />
          <ReferralCheckbox
            label="Loss > $400,000"
            value={payload.referralLossOver400k}
            onChange={(v) => patchPayload({ referralLossOver400k: v })}
            readOnly={isReadOnly}
          />
          <div className="flex items-center gap-2 pt-1">
            <span className="text-sm text-slate-500 shrink-0">Other:</span>
            <input
              type="text"
              value={payload.referralOtherText ?? ''}
              onChange={(e) => patchPayload({ referralOtherText: e.target.value })}
              readOnly={isReadOnly}
              placeholder="Describe…"
              className="flex-1 text-sm border border-slate-200 rounded px-2.5 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-400"
            />
          </div>
        </div>
      </Section>

      {/* Losses */}
      <Section title="Losses">
        <NarrativeBlock
          label="Mitigation actions"
          prompt="Describe any action taken by the insured to prevent future losses."
          value={payload.lossMitigationActions}
          onChange={(v) => patchPayload({ lossMitigationActions: v })}
          readOnly={isReadOnly}
        />
        <NarrativeBlock
          label="Losses over $25,000"
          prompt="Describe each loss exceeding $25,000 — date, cause, amount, status."
          value={payload.lossesOver25kDescription}
          onChange={(v) => patchPayload({ lossesOver25kDescription: v })}
          readOnly={isReadOnly}
        />
      </Section>

      {/* Equipment & Values */}
      <Section title="Equipment & Values">
        <div className="grid grid-cols-4 gap-3 text-center">
          {[
            { label: 'Total TIV', value: fmt.format(eq.totalTiv) },
            { label: 'Largest Unit', value: fmt.format(eq.largestUnitTiv) },
            { label: 'Cutters', value: eq.countCutter },
            { label: 'Skidders', value: eq.countSkidder },
            { label: 'Loaders', value: eq.countLoader },
            { label: 'Dozers', value: eq.countDozer },
            { label: 'Other', value: eq.countOther },
            { label: 'Total Units', value: eq.totalCount },
          ].map(({ label, value }) => (
            <div key={label} className="bg-slate-50 rounded-lg px-3 py-2">
              <div className="text-xs text-slate-400">{label}</div>
              <div className="text-sm font-semibold text-slate-800 mt-0.5">{value}</div>
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
          <span className="text-slate-600">Equipment values verified against appraisals / invoices</span>
        </label>
      </Section>

      {/* Operations & Metrics */}
      <Section title="Operations & Metrics">
        <div className="grid grid-cols-2 gap-4">
          <label className="flex items-center gap-2 text-sm cursor-pointer col-span-2">
            <input
              type="checkbox"
              checked={payload.waterborneExposure}
              onChange={(e) => patchPayload({ waterborneExposure: e.target.checked })}
              disabled={isReadOnly}
            />
            <span className="text-slate-600">Any waterborne exposure?</span>
          </label>
          <div className="space-y-1">
            <label className="text-xs font-medium text-slate-500">Last Inspection Date</label>
            <input
              type="date"
              value={payload.lastInspectionDate ?? ''}
              onChange={(e) => patchPayload({ lastInspectionDate: e.target.value })}
              readOnly={isReadOnly}
              className="w-full text-sm border border-slate-200 rounded px-2.5 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-400"
            />
          </div>
          <div className="space-y-1">
            <label className="text-xs font-medium text-slate-500">Website reviewed?</label>
            <select
              value={payload.websiteReviewed === true ? 'yes' : payload.websiteReviewed === false ? 'no' : ''}
              onChange={(e) => patchPayload({ websiteReviewed: e.target.value === 'yes' ? true : e.target.value === 'no' ? false : null })}
              disabled={isReadOnly}
              className="w-full text-sm border border-slate-200 rounded px-2.5 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-400 bg-white"
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
          <span className="text-slate-600">Recommendations outstanding from prior inspection?</span>
        </label>
        {payload.recommendationsOutstanding && (
          <textarea
            rows={2}
            value={payload.recommendationsDetail ?? ''}
            onChange={(e) => patchPayload({ recommendationsDetail: e.target.value })}
            readOnly={isReadOnly}
            placeholder="Describe recommendations…"
            className="w-full rounded border border-slate-200 px-3 py-2 text-sm text-slate-700 placeholder-slate-300 focus:outline-none focus:ring-1 focus:ring-blue-400 resize-y"
          />
        )}
        {payload.websiteReviewed && payload.websiteIssues !== undefined && (
          <textarea
            rows={2}
            value={payload.websiteIssues ?? ''}
            onChange={(e) => patchPayload({ websiteIssues: e.target.value })}
            readOnly={isReadOnly}
            placeholder="Note any website issues…"
            className="w-full rounded border border-slate-200 px-3 py-2 text-sm text-slate-700 placeholder-slate-300 focus:outline-none focus:ring-1 focus:ring-blue-400 resize-y"
          />
        )}
      </Section>

      {/* UW Narratives */}
      <Section title="Underwriting Notes">
        <NarrativeBlock
          label="Operators"
          prompt="Insured employees: avg years experience, training/certs, any concerns with operators?"
          value={payload.narrativeOperators}
          onChange={(v) => patchPayload({ narrativeOperators: v })}
          readOnly={isReadOnly}
        />
        <NarrativeBlock
          label="Equipment"
          prompt="Age, maintenance records, cool-down procedure followed? Mortgage on units >75% value? Avg equipment age, deductible reasoning, usage patterns."
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
        <NarrativeBlock
          label="Other Concerns"
          prompt="Any additional risk concerns, positive attributes, or notable observations."
          value={payload.narrativeOtherConcerns}
          onChange={(v) => patchPayload({ narrativeOtherConcerns: v })}
          readOnly={isReadOnly}
        />
      </Section>

      {/* Conditions */}
      <Section title="Conditions (if approving with conditions)">
        <div className="space-y-2">
          {conditions.length === 0 && (
            <p className="text-sm text-slate-400 italic">No conditions added.</p>
          )}
          {conditions.map((c) => (
            <div key={c.id} className="flex items-start gap-2">
              {!isReadOnly ? (
                <>
                  <input
                    type="text"
                    value={c.text}
                    onChange={(e) => setConditions((cs) => cs.map((x) => x.id === c.id ? { ...x, text: e.target.value } : x))}
                    className="flex-1 text-sm border border-slate-200 rounded px-2.5 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-400"
                  />
                  <button
                    type="button"
                    onClick={() => removeCondition(c.id)}
                    className="text-slate-300 hover:text-red-400 mt-1.5"
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>
                </>
              ) : (
                <div className="flex items-center gap-2 text-sm">
                  {c.satisfied
                    ? <CheckCircle2 className="h-4 w-4 text-emerald-500 shrink-0" />
                    : <AlertTriangle className="h-4 w-4 text-amber-400 shrink-0" />}
                  <span className={c.satisfied ? 'text-slate-400 line-through' : 'text-slate-700'}>{c.text}</span>
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
                className="flex-1 text-sm border border-slate-200 rounded px-2.5 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-400"
              />
              <button
                type="button"
                onClick={addCondition}
                className="flex items-center gap-1 px-3 py-1.5 text-sm bg-slate-100 hover:bg-slate-200 rounded text-slate-600"
              >
                <Plus className="h-3.5 w-3.5" />
                Add
              </button>
            </div>
          )}
        </div>
      </Section>

      {/* Recommendation / Rationale */}
      <Section title="Recommendation">
        <NarrativeBlock
          label="Rationale"
          prompt="Summarize the risk, explain your decision, and note any concerns for the file."
          value={payload.decisionRationale}
          onChange={(v) => patchPayload({ decisionRationale: v })}
          readOnly={isReadOnly}
        />
        {writeup.submittedAt && (
          <div className="text-xs text-slate-400 pt-1">
            Submitted by {writeup.submittedByName} on {new Date(writeup.submittedAt).toLocaleDateString()}
            {writeup.approvedAt && ` · Approved by ${writeup.approvedByName} on ${new Date(writeup.approvedAt).toLocaleDateString()}`}
          </div>
        )}
      </Section>

      {/* Action bar */}
      {writeup.status === 'Draft' && (
        <div className="sticky bottom-0 bg-white border-t border-slate-200 -mx-6 px-6 py-3 flex items-center justify-between gap-3">
          <button
            type="button"
            onClick={() => saveMutation.mutate()}
            disabled={saveMutation.isPending}
            className="flex items-center gap-1.5 px-4 py-2 text-sm border border-slate-200 rounded-lg text-slate-600 hover:bg-slate-50 disabled:opacity-50"
          >
            <Save className="h-4 w-4" />
            {saveMutation.isPending ? 'Saving…' : 'Save Draft'}
          </button>
          <button
            type="button"
            onClick={() => setShowSubmitPanel(!showSubmitPanel)}
            className="flex items-center gap-1.5 px-4 py-2 text-sm bg-blue-600 hover:bg-blue-700 rounded-lg text-white"
          >
            <Send className="h-4 w-4" />
            Submit Decision
          </button>
        </div>
      )}

      {writeup.status === 'Submitted' && (
        <div className="sticky bottom-0 bg-white border-t border-slate-200 -mx-6 px-6 py-3 flex items-center justify-end gap-3">
          <button
            type="button"
            onClick={() => approveMutation.mutate()}
            disabled={approveMutation.isPending}
            className="flex items-center gap-1.5 px-4 py-2 text-sm bg-emerald-600 hover:bg-emerald-700 rounded-lg text-white disabled:opacity-50"
          >
            <ThumbsUp className="h-4 w-4" />
            {approveMutation.isPending ? 'Approving…' : 'Approve'}
          </button>
        </div>
      )}

      {/* Submit decision panel */}
      {showSubmitPanel && (
        <div className="fixed inset-0 bg-black/30 flex items-center justify-center z-50">
          <div className="bg-white rounded-xl shadow-xl p-6 w-full max-w-sm space-y-4">
            <h2 className="text-base font-semibold text-slate-800">Submit Decision</h2>
            <div className="space-y-2">
              {(['Approve', 'ApproveWithConditions', 'ReferUp', 'Decline'] as const).map((d) => (
                <label key={d} className="flex items-center gap-2 text-sm cursor-pointer">
                  <input
                    type="radio"
                    name="decision"
                    value={d}
                    checked={submitDecision === d}
                    onChange={() => setSubmitDecision(d)}
                    className="text-blue-600"
                  />
                  <span>{DECISION_LABELS[d]}</span>
                </label>
              ))}
            </div>
            <div className="flex gap-2 pt-2">
              <button
                type="button"
                onClick={() => setShowSubmitPanel(false)}
                className="flex-1 py-2 text-sm border border-slate-200 rounded-lg text-slate-600 hover:bg-slate-50"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={() => submitMutation.mutate()}
                disabled={!submitDecision || submitMutation.isPending}
                className="flex-1 py-2 text-sm bg-blue-600 hover:bg-blue-700 text-white rounded-lg disabled:opacity-50"
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

function StatusBadge({ status, decision }: { status: UWWriteupDto['status']; decision?: string }) {
  const config = {
    Draft: { bg: 'bg-slate-100', text: 'text-slate-600', label: 'Draft' },
    Submitted: { bg: 'bg-blue-100', text: 'text-blue-700', label: decision ? `Submitted · ${DECISION_LABELS[decision] ?? decision}` : 'Submitted' },
    Approved: { bg: 'bg-emerald-100', text: 'text-emerald-700', label: 'Approved' },
    Declined: { bg: 'bg-red-100', text: 'text-red-700', label: 'Declined' },
  }[status]

  return (
    <span className={`px-2.5 py-1 rounded-full text-xs font-semibold ${config.bg} ${config.text}`}>
      {config.label}
    </span>
  )
}
