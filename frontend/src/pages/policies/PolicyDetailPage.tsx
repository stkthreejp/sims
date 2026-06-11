import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, Ban, Download, FileSignature, FileX2, Loader2, Pin, PinOff, Pencil, RotateCcw, Trash2, Plus, X, Check, FileText, Send } from 'lucide-react'
import { toast } from 'sonner'
import { policiesApi } from '@/api/policies.api'
import { quotesApi } from '@/api/quotes.api'
import { submissionsApi } from '@/api/submissions.api'
import { underwritingGuidelinesApi } from '@/api/underwritingGuidelines.api'
import { attachmentsApi } from '@/api/attachments.api'
import { downloadLossRunCsv } from '@/api/claims.api'
import { documentTemplatesApi } from '@/api/documentTemplates.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { LOB_LABELS } from '@/types/quote.types'
import type { QuoteChecklistItem } from '@/types/quote.types'
import { POLICY_STATUS_LABELS, POLICY_TRANSACTION_STATUS_LABELS, POLICY_TRANSACTION_STATUS_PILL } from '@/types/policy.types'
import type { CancellationComplianceChecklistItem, CancellationReason, IssueCancellationNotice, LegalComplianceGuidance, LegalComplianceRequirement, LegalRequirementSnapshot, MarkNonRenewal, NonRenewPolicy, Policy, PolicyIssuancePacket, PolicyTransaction, ReinstatePolicy, StartRewritePolicy } from '@/types/policy.types'
import { DOCUMENT_TYPE_LABELS } from '@/types/attachment.types'
import type { Attachment, DocumentType } from '@/types/attachment.types'
import { formatCurrency } from '@/lib/utils'
import type { Note } from '@/types/quote.types'
import type { DocumentTemplateListItem } from '@/types/documentTemplate.types'
import type { UnderwritingReferralSummary } from '@/types/submission.types'
import { DocumentsSection } from '@/components/documents/DocumentsSection'
import { UnderwritingControlEnforcementPanel } from '@/components/underwriting/UnderwritingControlEnforcementPanel'
import { usePermissions } from '@/hooks/usePermissions'

const POLICY_STATUS_PILL: Record<Policy['status'], string> = {
  Active: 'bound',
  Renewed: 'quoted',
  NonRenewed: 'expired',
  Expired: 'expired',
  Cancelled: 'cancelled',
}

const POLICY_STAGES = ['Bound', 'In Force', 'Renewal', 'Closed']

export function PolicyDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()

  const [noteSubject, setNoteSubject] = useState('')
  const [noteBody, setNoteBody] = useState('')
  const [showNoteForm, setShowNoteForm] = useState(false)
  const [editingNote, setEditingNote] = useState<Note | null>(null)
  const [editSubject, setEditSubject] = useState('')
  const [editBody, setEditBody] = useState('')
  const [actionModal, setActionModal] = useState<'endorse' | 'cancel' | 'markNonRenew' | 'reinstate' | 'rewrite' | null>(null)
  const [nonRenewalNoticeTransactionId, setNonRenewalNoticeTransactionId] = useState<string | null>(null)

  const { data: policy, isLoading } = useQuery({
    queryKey: ['policies', id],
    queryFn: () => policiesApi.getById(id!),
  })

  const { data: notes = [] } = useQuery({
    queryKey: ['policies', id, 'notes'],
    queryFn: () => policiesApi.getNotes(id!),
    enabled: !!id,
  })

  const createNoteMutation = useMutation({
    mutationFn: () => policiesApi.createNote(id!, { subject: noteSubject || undefined, body: noteBody }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', id, 'notes'] })
      setNoteSubject('')
      setNoteBody('')
      setShowNoteForm(false)
      toast.success('Note added')
    },
    onError: () => toast.error('Failed to add note'),
  })

  const updateNoteMutation = useMutation({
    mutationFn: (note: Note) => policiesApi.updateNote(id!, note.id, { subject: editSubject || undefined, body: editBody }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', id, 'notes'] })
      setEditingNote(null)
      toast.success('Note updated')
    },
    onError: () => toast.error('Failed to update note'),
  })

  const deleteNoteMutation = useMutation({
    mutationFn: (noteId: string) => policiesApi.deleteNote(id!, noteId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', id, 'notes'] })
      toast.success('Note deleted')
    },
    onError: () => toast.error('Failed to delete note'),
  })

  const togglePinMutation = useMutation({
    mutationFn: (noteId: string) => policiesApi.togglePinNote(id!, noteId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['policies', id, 'notes'] }),
  })

  const { canCreateNotes, canEditNotes, canDeleteNotes, canUploadAttachments, canDeleteAttachments, canIssuePolicies, canEndorsePolicies, canCancelPolicies, canManageUnderwriting, canOverrideClearance, canViewClaims, isAdmin } = usePermissions()

  const { data: issuancePacket } = useQuery({
    queryKey: ['policies', id, 'issuance-packet'],
    queryFn: () => policiesApi.getIssuancePacket(id!),
    enabled: !!id,
  })

  const { data: referralSummary } = useQuery({
    queryKey: ['submissions', policy?.submissionId, 'underwriting-referrals'],
    queryFn: () => submissionsApi.getUnderwritingReferrals(policy!.submissionId),
    enabled: !!policy?.submissionId,
  })

  const { data: policyChecklist = [] } = useQuery({
    queryKey: ['quote-checklist', policy?.boundQuoteId, 'policy-documents'],
    queryFn: () => quotesApi.getChecklist(policy!.boundQuoteId, ['Issue', 'PostBind']),
    enabled: !!policy?.boundQuoteId,
  })

  const { data: enforcementSummary } = useQuery({
    queryKey: ['underwriting-control-enforcement', 'Policy', id],
    queryFn: () => underwritingGuidelinesApi.getEnforcementResults('Policy', id!),
    enabled: !!id,
  })

  const { data: cancellationGuidance } = useQuery({
    queryKey: ['policies', id, 'cancellation-guidance'],
    queryFn: () => policiesApi.getCancellationGuidance(id!),
    enabled: !!id && canCancelPolicies,
  })

  const { data: cancellationReasons = [] } = useQuery({
    queryKey: ['policies', 'cancellation-reasons'],
    queryFn: policiesApi.getCancellationReasons,
    enabled: canCancelPolicies,
  })

  const { data: policyDocumentTemplates = [] } = useQuery({
    queryKey: ['document-templates', 'Policy', 'Document'],
    queryFn: () => documentTemplatesApi.getAll('Policy', false, 'Document'),
    enabled: canCancelPolicies,
  })

  const { data: nonRenewalGuidance } = useQuery({
    queryKey: ['policies', id, 'non-renewal-guidance'],
    queryFn: () => policiesApi.getNonRenewalGuidance(id!),
    enabled: !!id && canCancelPolicies,
  })

  const addEndorsementMutation = useMutation({
    mutationFn: (data: { effectiveDate: string; premiumChange: number; endorsementDescription?: string; notes?: string }) =>
      policiesApi.addEndorsement(id!, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', id] })
      setActionModal(null)
      toast.success('Endorsement transaction added')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Endorsement could not be added'),
  })

  const issueCancellationNoticeMutation = useMutation({
    mutationFn: (data: IssueCancellationNotice) =>
      policiesApi.issueCancellationNotice(id!, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', id] })
      setActionModal(null)
      toast.success('Cancellation notice issued')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Cancellation notice could not be issued'),
  })

  const markNonRenewalMutation = useMutation({
    mutationFn: (data: MarkNonRenewal) => policiesApi.markForNonRenewal(id!, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', id] })
      setActionModal(null)
      toast.success('Policy marked for non-renewal')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Policy could not be marked for non-renewal'),
  })

  const nonRenewMutation = useMutation({
    mutationFn: (data: NonRenewPolicy) => policiesApi.nonRenew(id!, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', id] })
      setActionModal(null)
      setNonRenewalNoticeTransactionId(null)
      toast.success('Non-renewal notice issued')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Non-renewal notice could not be issued'),
  })

  const reinstateMutation = useMutation({
    mutationFn: (data: ReinstatePolicy) => policiesApi.reinstate(id!, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', id] })
      setActionModal(null)
      toast.success('Policy reinstated')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Policy could not be reinstated'),
  })

  const startRewriteMutation = useMutation({
    mutationFn: (data: StartRewritePolicy) => policiesApi.startRewrite(id!, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', id] })
      setActionModal(null)
      toast.success('Rewrite transaction started')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Rewrite could not be started'),
  })

  const issuePolicyMutation = useMutation({
    mutationFn: () => policiesApi.issue(id!, { issuedDate: new Date().toISOString().slice(0, 10) }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', id] })
      qc.invalidateQueries({ queryKey: ['policies', id, 'issuance-packet'] })
      qc.invalidateQueries({ queryKey: ['attachments', 'Policy', policy?.boundQuoteId] })
      toast.success('Policy issued and final packet filed')
    },
    onError: (e: any) => {
      if (e?.response?.data?.errorCode === 'REFERRAL_REQUIRED') {
        toast.error('Required underwriting referrals are still open. Resolve referrals before issuing.')
        return
      }
      if (e?.response?.data?.errorCode === 'UNDERWRITING_CONTROL_BLOCKED') {
        qc.invalidateQueries({ queryKey: ['underwriting-control-enforcement', 'Policy', id] })
        toast.error(e?.response?.data?.errorMessage ?? 'Published underwriting controls are blocking issue.')
        return
      }
      if (e?.response?.data?.errorCode === 'REQUIRED_DOCUMENTS_INCOMPLETE') {
        qc.invalidateQueries({ queryKey: ['quote-checklist', policy?.boundQuoteId, 'policy-documents'] })
        toast.error(e?.response?.data?.errorMessage ?? 'Complete required issue documents before issuing.')
        return
      }
      toast.error(e?.response?.data?.errorMessage ?? 'Policy could not be issued')
    },
  })

  const overrideEnforcementMutation = useMutation({
    mutationFn: ({ resultId, reason }: { resultId: string; reason: string }) =>
      underwritingGuidelinesApi.overrideEnforcementResult(resultId, reason),
    onSuccess: (result) => {
      qc.setQueryData(['underwriting-control-enforcement', 'Policy', id], result)
      toast.success('Underwriting blocker override recorded')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to override underwriting blocker'),
  })

  const togglePolicyChecklistMutation = useMutation({
    mutationFn: ({ itemId, completed }: { itemId: string; completed: boolean }) =>
      quotesApi.toggleChecklistItem(policy!.boundQuoteId, itemId, completed),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quote-checklist', policy?.boundQuoteId, 'policy-documents'] })
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Checklist item could not be updated'),
  })

  const previewPacketMutation = useMutation({
    mutationFn: () => policiesApi.generateIssuancePacketPreview(id!),
    onSuccess: (result) => {
      qc.invalidateQueries({ queryKey: ['attachments', 'Policy', policy?.boundQuoteId] })
      window.open(result.url, '_blank', 'noopener,noreferrer')
      toast.success('Draft policy packet preview filed')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Policy packet preview could not be generated'),
  })

  const voidTestBindMutation = useMutation({
    mutationFn: () => policiesApi.voidTestBind(id!, 'Test bind cleanup'),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies'] })
      qc.invalidateQueries({ queryKey: ['quotes'] })
      toast.success('Test bind voided')
      navigate(`/quotes/${policy?.boundQuoteId}`)
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Test bind could not be voided'),
  })

  if (isLoading) return <LoadingSpinner />
  if (!policy) return <p className="p-6" style={{ color: 'var(--ink-3)' }}>Policy not found.</p>

  const sortedNotes = [...notes].sort((a, b) => {
    if (a.isPinned !== b.isPinned) return a.isPinned ? -1 : 1
    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  })
  const daysRemaining = Math.ceil((new Date(policy.expirationDate).getTime() - Date.now()) / 86400000)
  const activeStage = policy.status === 'Cancelled' || policy.status === 'NonRenewed' || policy.status === 'Expired'
    ? 3
    : daysRemaining <= 45 ? 2 : 1
  const canVoidTestBind = isAdmin && policy.status === 'Active' && !policy.issuedDate && policy.insuredName.toLowerCase().includes('test')
  const issueChecklist = policyChecklist.filter((item) => item.stage === 'Issue')
  const postBindChecklist = policyChecklist.filter((item) => item.stage === 'PostBind')
  const postBindBlockedReason = formatRequiredChecklistBlockers(postBindChecklist, 'post-bind')
  const hasPendingNonRenewalNotice = policy.transactions.some((t) =>
    t.transactionType === 'NonRenewal' && t.status === 'NoticePending'
  )
  const markNonRenewalBlockedReason = postBindBlockedReason ?? (hasPendingNonRenewalNotice ? 'A non-renewal notice is already pending.' : null)

  return (
    <div className="space-y-5 p-6">
      <div className="flex items-center gap-2 text-sm" style={{ color: 'var(--ink-3)' }}>
        <Link to="/policies">Policies</Link>
        <span>/</span>
        <Link to={`/insureds/${policy.insuredId}`}>{policy.insuredName}</Link>
        <span>/</span>
        <span style={{ color: 'var(--ink)', fontWeight: 500 }}>{policy.policyNumber}</span>
      </div>

      {/* Header */}
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="m-0 text-[22px] font-semibold tracking-[-0.01em]" style={{ color: 'var(--ink)' }}>{policy.carrierName}</h1>
            <span className="rounded-md px-2 py-1 font-mono text-xs" style={{ background: 'var(--surface-2)', color: 'var(--ink-3)' }}>{policy.policyNumber}</span>
            <span className={`sd-pill ${POLICY_STATUS_PILL[policy.status]}`}>{POLICY_STATUS_LABELS[policy.status]}</span>
          </div>
          <div className="mt-1 flex flex-wrap items-center gap-2 text-sm" style={{ color: 'var(--ink-3)' }}>
            <span className="sd-lob">{LOB_LABELS[policy.lineOfBusiness]}</span>
            <span>Insured <b style={{ color: 'var(--ink)', fontWeight: 600 }}>{policy.insuredName}</b></span>
            <span>From submission <Link to={`/submissions/${policy.submissionId}`} style={{ color: 'var(--accent-ink)', fontWeight: 600 }}>{policy.submissionNumber}</Link></span>
            <span>Bound <b style={{ color: 'var(--ink)', fontWeight: 600 }}>{formatDate(policy.boundDate)}</b></span>
          </div>
        </div>
        <div className="flex flex-wrap items-center justify-end gap-2">
          {canViewClaims && (
            <button
              onClick={() =>
                downloadLossRunCsv({ policyId: id }).catch((err) =>
                  toast.error(err?.response?.status === 403
                    ? 'You do not have access to this policy’s loss run'
                    : 'Could not generate loss run'))
              }
              title="Download a loss run for this policy"
              className="sd-btn outline"
            >
              <Download className="h-3.5 w-3.5" /> Loss Run
            </button>
          )}
          {policy.status === 'Active' && canEndorsePolicies && (
            <>
              <button
                onClick={() => !postBindBlockedReason && setActionModal('rewrite')}
                disabled={!!postBindBlockedReason}
                title={postBindBlockedReason ?? 'Start a rewrite transaction'}
                className="sd-btn outline"
              >
                <FileText className="h-3.5 w-3.5" /> Rewrite
              </button>
              <button
                onClick={() => !postBindBlockedReason && setActionModal('endorse')}
                disabled={!!postBindBlockedReason}
                title={postBindBlockedReason ?? 'Start an endorsement transaction'}
                className="sd-btn primary"
              >
                <FileSignature className="h-3.5 w-3.5" /> Endorse
              </button>
            </>
          )}
          {policy.status === 'Active' && canCancelPolicies && (
            <>
              <button
                onClick={() => !markNonRenewalBlockedReason && setActionModal('markNonRenew')}
                disabled={!!markNonRenewalBlockedReason}
                title={markNonRenewalBlockedReason ?? 'Mark this policy for non-renewal'}
                className="sd-btn outline"
              >
                <FileX2 className="h-3.5 w-3.5" /> Mark Non-Renewal
              </button>
              <button
                onClick={() => !postBindBlockedReason && setActionModal('cancel')}
                disabled={!!postBindBlockedReason}
                title={postBindBlockedReason ?? 'Start cancellation'}
                className="sd-btn danger"
              >
                <Ban className="h-3.5 w-3.5" /> Cancel
              </button>
            </>
          )}
          {policy.status === 'Cancelled' && canCancelPolicies && (
            <button
              onClick={() => !postBindBlockedReason && setActionModal('reinstate')}
              disabled={!!postBindBlockedReason}
              title={postBindBlockedReason ?? 'Reinstate this policy'}
              className="sd-btn primary"
            >
              <RotateCcw className="h-3.5 w-3.5" /> Reinstate
            </button>
          )}
          {canVoidTestBind && (
            <button
              onClick={() => {
                if (window.confirm('Void this test bind and reverse its invoice? This is only for test insureds.')) {
                  voidTestBindMutation.mutate()
                }
              }}
              disabled={voidTestBindMutation.isPending}
              className="sd-btn danger"
            >
              <Trash2 className="h-3.5 w-3.5" /> Void Test Bind
            </button>
          )}
        </div>
      </div>

      {postBindBlockedReason && (
        <div className="flex items-start gap-2 rounded-lg border px-4 py-3 text-sm" style={{ borderColor: 'var(--warn-fg)', background: 'var(--warn-bg)', color: 'var(--warn-fg)' }}>
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <div>
            <div className="font-semibold">Policy activity is blocked</div>
            <div>{postBindBlockedReason}</div>
          </div>
        </div>
      )}

      <div className="grid overflow-hidden rounded-xl border" style={{ borderColor: 'var(--line)', background: 'var(--surface)', gridTemplateColumns: 'repeat(4, minmax(0, 1fr))' }}>
        {POLICY_STAGES.map((stage, index) => {
          const done = index < activeStage
          const active = index === activeStage
          return (
            <div
              key={stage}
              className="flex items-center justify-center gap-2 border-r px-3 py-2 text-xs font-semibold last:border-r-0"
              style={{
                borderColor: 'var(--line)',
                background: done ? '#e8f2e9' : active ? 'var(--accent-soft)' : 'var(--surface)',
                color: done ? '#1b6238' : active ? 'var(--accent-ink)' : 'var(--ink-3)',
              }}
            >
              {done && <Check className="h-3.5 w-3.5" />}
              <span className="font-mono text-[10px] opacity-70">{String(index + 1).padStart(2, '0')}</span>
              {stage}
            </div>
          )
        })}
      </div>

      {actionModal === 'endorse' && (
        <EndorsePolicyModal
          policy={policy}
          saving={addEndorsementMutation.isPending}
          onClose={() => setActionModal(null)}
          onSave={(data) => addEndorsementMutation.mutate(data)}
        />
      )}

      {actionModal === 'cancel' && (
        <CancelPolicyModal
          policy={policy}
          guidance={cancellationGuidance}
          reasons={cancellationReasons}
          templates={policyDocumentTemplates}
          saving={issueCancellationNoticeMutation.isPending}
          onClose={() => setActionModal(null)}
          onSave={(data) => issueCancellationNoticeMutation.mutate(data)}
        />
      )}

      {actionModal === 'markNonRenew' && (
        <MarkNonRenewalModal
          policy={policy}
          saving={markNonRenewalMutation.isPending}
          onClose={() => setActionModal(null)}
          onSave={(data) => markNonRenewalMutation.mutate(data)}
        />
      )}

      {nonRenewalNoticeTransactionId && (
        <NonRenewPolicyModal
          policy={policy}
          guidance={nonRenewalGuidance}
          templates={policyDocumentTemplates}
          saving={nonRenewMutation.isPending}
          onClose={() => setNonRenewalNoticeTransactionId(null)}
          onSave={(data) => nonRenewMutation.mutate(data)}
        />
      )}

      {actionModal === 'reinstate' && (
        <ReinstatePolicyModal
          policy={policy}
          saving={reinstateMutation.isPending}
          onClose={() => setActionModal(null)}
          onSave={(data) => reinstateMutation.mutate(data)}
        />
      )}

      {actionModal === 'rewrite' && (
        <StartRewritePolicyModal
          policy={policy}
          saving={startRewriteMutation.isPending}
          onClose={() => setActionModal(null)}
          onSave={(data) => startRewriteMutation.mutate(data)}
        />
      )}

      <div className="grid gap-3 md:grid-cols-4">
        <PolicyMetric label="Total premium" value={formatCurrency(policy.totalPremium)} helper={`Base ${formatCurrency(policy.premiumAmount)}`} hero />
        <PolicyMetric label="Commission" value={formatCurrency(policy.agentCommissionAmount)} helper={`${(policy.agentCommissionRate * 100).toFixed(1)}% agent commission`} />
        <PolicyMetric label="Limit / deductible" value={`${policy.limit != null ? formatCurrency(policy.limit) : '-'} / ${policy.deductible != null ? formatCurrency(policy.deductible) : '-'}`} helper="Current policy terms" />
        <PolicyMetric label="Term" value={formatDate(policy.effectiveDate)} helper={`Expires ${formatDate(policy.expirationDate)} · ${Math.max(daysRemaining, 0)} days left`} />
      </div>

      {/* Policy details */}
      <div className="sd-card">
        <div className="sd-card-head">
          <h3>Policy details</h3>
        </div>
        <div className="grid grid-cols-2 gap-4 p-5 text-sm md:grid-cols-4">
        <div>
          <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Line of Business</p>
          <p className="font-medium">{LOB_LABELS[policy.lineOfBusiness]}</p>
        </div>
        <div>
          <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Carrier</p>
          <p className="font-medium">{policy.carrierName}</p>
        </div>
        <div>
          <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Effective Date</p>
          <p className="font-medium">{new Date(policy.effectiveDate).toLocaleDateString()}</p>
        </div>
        <div>
          <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Expiration Date</p>
          <p className="font-medium">{new Date(policy.expirationDate).toLocaleDateString()}</p>
        </div>
        <div>
          <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Premium</p>
          <p className="font-medium">{formatCurrency(policy.premiumAmount)}</p>
        </div>
        <div>
          <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Taxes & Fees</p>
          <p className="font-medium">{formatCurrency(policy.taxesAndFees)}</p>
        </div>
        <div>
          <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Total Premium</p>
          <p className="font-medium">{formatCurrency(policy.totalPremium)}</p>
        </div>
        <div>
          <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Agent Commission</p>
          <p className="font-medium">{formatCurrency(policy.agentCommissionAmount)} ({(policy.agentCommissionRate * 100).toFixed(1)}%)</p>
        </div>
        <div>
          <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Carrier Commission</p>
          <p className="font-medium">{formatCurrency(policy.carrierCommissionAmount)} ({(policy.carrierCommissionRate * 100).toFixed(1)}%)</p>
        </div>
        <div>
          <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>SMM Retention</p>
          <p className="font-medium">{formatCurrency(policy.smmRetentionAmount)} ({(policy.smmRetentionRate * 100).toFixed(1)}%)</p>
        </div>
        <div>
          <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Bound Date</p>
          <p className="font-medium">{new Date(policy.boundDate).toLocaleDateString()}</p>
        </div>
        {policy.issuedDate && (
          <div>
            <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Issued Date</p>
            <p className="font-medium">{new Date(policy.issuedDate).toLocaleDateString()}</p>
          </div>
        )}
        {policy.nonRenewedDate && (
          <div>
            <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Non-Renewed Date</p>
            <p className="font-medium">{new Date(policy.nonRenewedDate).toLocaleDateString()}</p>
          </div>
        )}
        {policy.cancelledDate && (
          <div>
            <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Cancelled Date</p>
            <p className="font-medium">{new Date(policy.cancelledDate).toLocaleDateString()}</p>
          </div>
        )}
        {policy.limit != null && (
          <div>
            <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Limit</p>
            <p className="font-medium">{formatCurrency(policy.limit)}</p>
          </div>
        )}
        {policy.deductible != null && (
          <div>
            <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Deductible</p>
            <p className="font-medium">{formatCurrency(policy.deductible)}</p>
          </div>
        )}
        {policy.coverageDescription && (
          <div className="col-span-2 md:col-span-4">
            <p className="text-xs mb-0.5" style={{ color: 'var(--ink-3)' }}>Coverage Description</p>
            <p style={{ color: 'var(--ink-2)' }}>{policy.coverageDescription}</p>
          </div>
        )}
      </div>
      </div>

      <UnderwritingControlEnforcementPanel
        title="Published UW controls"
        summary={enforcementSummary}
        canOverride={canOverrideClearance}
        isOverriding={overrideEnforcementMutation.isPending}
        onOverride={(resultId, reason) => overrideEnforcementMutation.mutate({ resultId, reason })}
      />

      <PolicyIssuancePanel
        packet={issuancePacket}
        canIssue={canIssuePolicies && policy.status === 'Active' && !policy.issuedDate}
        checklist={issueChecklist}
        canManageChecklist={canManageUnderwriting}
        checklistSaving={togglePolicyChecklistMutation.isPending}
        onToggleChecklist={(itemId, completed) => togglePolicyChecklistMutation.mutate({ itemId, completed })}
        referralSummary={referralSummary}
        submissionId={policy.submissionId}
        issuing={issuePolicyMutation.isPending}
        previewing={previewPacketMutation.isPending}
        onPreview={() => previewPacketMutation.mutate()}
        onIssue={() => issuePolicyMutation.mutate()}
      />

      {/* Transactions */}
      {policy.transactions.length > 0 && (
        <div className="sd-card overflow-hidden">
          <div className="sd-card-head">
            <h3>Transaction history <span className="cnt">{policy.transactions.length}</span></h3>
          </div>
          <table className="sd-table">
            <thead>
              <tr>
                <th className="px-5 py-2 text-left text-xs font-semibold uppercase" style={{ color: 'var(--ink-3)' }}>Txn #</th>
                <th className="px-5 py-2 text-left text-xs font-semibold uppercase" style={{ color: 'var(--ink-3)' }}>Type</th>
                <th className="px-5 py-2 text-left text-xs font-semibold uppercase" style={{ color: 'var(--ink-3)' }}>Status</th>
                <th className="px-5 py-2 text-left text-xs font-semibold uppercase" style={{ color: 'var(--ink-3)' }}>Effective</th>
                <th className="px-5 py-2 text-right text-xs font-semibold uppercase" style={{ color: 'var(--ink-3)' }}>Premium Δ</th>
                <th className="px-5 py-2 text-right text-xs font-semibold uppercase" style={{ color: 'var(--ink-3)' }}>New Total</th>
                <th className="px-5 py-2 text-left text-xs font-semibold uppercase" style={{ color: 'var(--ink-3)' }}>Processed By</th>
              </tr>
            </thead>
            <tbody>
              {policy.transactions.map((t) => (
                <TransactionRows
                  key={t.id}
                  transaction={t}
                  policyDocumentEntityId={policy.boundQuoteId}
                  canUploadProof={canUploadAttachments}
                  canCompleteCancellation={canCancelPolicies}
                  canCompleteRewrite={canEndorsePolicies}
                  postBindBlockedReason={postBindBlockedReason}
                  onIssueNonRenewalNotice={() => setNonRenewalNoticeTransactionId(t.id)}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div>
        {/* Notes */}
        <div className="sd-card overflow-hidden">
          <div className="sd-card-head">
            <h3>Notes <span className="cnt">{notes.length}</span></h3>
            {!showNoteForm && canCreateNotes && (
              <button
                onClick={() => setShowNoteForm(true)}
                className="sd-btn outline sm"
              >
                <Plus className="h-3.5 w-3.5" /> Add Note
              </button>
            )}
          </div>

          {showNoteForm && (
            <div className="space-y-3 border-b px-5 py-4" style={{ background: 'var(--surface-2)', borderColor: 'var(--line-2)' }}>
              <input
                type="text"
                placeholder="Subject (optional)"
                value={noteSubject}
                onChange={(e) => setNoteSubject(e.target.value)}
                className="sims-input"
              />
              <textarea
                placeholder="Note body *"
                value={noteBody}
                onChange={(e) => setNoteBody(e.target.value)}
                rows={3}
                className="sims-textarea"
              />
              <div className="flex gap-2">
                <button
                  disabled={!noteBody.trim() || createNoteMutation.isPending}
                  onClick={() => createNoteMutation.mutate()}
                  className="sd-btn primary sm"
                >
                  <Check className="h-3.5 w-3.5" /> Save
                </button>
                <button onClick={() => { setShowNoteForm(false); setNoteSubject(''); setNoteBody('') }} className="sd-btn outline sm">
                  <X className="h-3.5 w-3.5" /> Cancel
                </button>
              </div>
            </div>
          )}

          <div>
            {sortedNotes.length === 0 && !showNoteForm && (
              <p className="px-5 py-8 text-center text-sm" style={{ color: 'var(--ink-4)' }}>No notes yet.</p>
            )}
            {sortedNotes.map((note) => (
              <div key={note.id} className="border-b px-5 py-4 last:border-b-0" style={{ borderColor: 'var(--line-2)', background: note.isPinned ? 'var(--warn-bg)' : 'var(--surface)' }}>
                {editingNote?.id === note.id ? (
                  <div className="space-y-2">
                    <input
                      type="text"
                      placeholder="Subject (optional)"
                      value={editSubject}
                      onChange={(e) => setEditSubject(e.target.value)}
                      className="sims-input"
                    />
                    <textarea
                      value={editBody}
                      onChange={(e) => setEditBody(e.target.value)}
                      rows={3}
                      className="sims-textarea"
                    />
                    <div className="flex gap-2">
                      <button
                        disabled={!editBody.trim() || updateNoteMutation.isPending}
                        onClick={() => updateNoteMutation.mutate(note)}
                        className="sd-btn primary sm"
                      >
                        <Check className="h-3 w-3" /> Save
                      </button>
                      <button onClick={() => setEditingNote(null)} className="sd-btn outline sm">
                        <X className="h-3 w-3" /> Cancel
                      </button>
                    </div>
                  </div>
                ) : (
                  <>
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0 flex-1">
                        {note.subject && <p className="text-sm font-semibold" style={{ color: 'var(--ink)' }}>{note.subject}</p>}
                        <p className="mt-0.5 whitespace-pre-wrap text-sm" style={{ color: 'var(--ink-2)' }}>{note.body}</p>
                        <p className="mt-1 text-xs" style={{ color: 'var(--ink-4)' }}>
                          {note.createdByName} · {new Date(note.createdAt).toLocaleDateString()}
                        </p>
                      </div>
                      <div className="flex shrink-0 gap-1">
                        {canEditNotes && (
                          <button onClick={() => togglePinMutation.mutate(note.id)} className="sims-icon-btn">
                            {note.isPinned
                              ? <PinOff className="h-3.5 w-3.5" style={{ color: 'var(--warn-fg)' }} />
                              : <Pin className="h-3.5 w-3.5" style={{ color: 'var(--ink-4)' }} />}
                          </button>
                        )}
                        {canEditNotes && (
                          <button onClick={() => { setEditingNote(note); setEditSubject(note.subject ?? ''); setEditBody(note.body) }} className="sims-icon-btn">
                            <Pencil className="h-3.5 w-3.5" style={{ color: 'var(--ink-4)' }} />
                          </button>
                        )}
                        {canDeleteNotes && (
                          <button onClick={() => { if (confirm('Delete note?')) deleteNoteMutation.mutate(note.id) }} className="sims-icon-btn">
                            <Trash2 className="h-3.5 w-3.5" style={{ color: 'var(--ink-4)' }} />
                          </button>
                        )}
                      </div>
                    </div>
                  </>
                )}
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Documents */}
      <div>
        <PolicyDocumentChecklistPanel
          title="Post-bind document checklist"
          items={postBindChecklist}
          canManage={canManageUnderwriting}
          saving={togglePolicyChecklistMutation.isPending}
          onToggle={(itemId, completed) => togglePolicyChecklistMutation.mutate({ itemId, completed })}
        />
        <DocumentsSection entityType="Policy" entityId={policy.boundQuoteId} canUpload={canUploadAttachments} canDelete={canDeleteAttachments} />
      </div>
    </div>
  )
}

function PolicyMetric({ label, value, helper, hero = false }: { label: string; value: string; helper: string; hero?: boolean }) {
  return (
    <div className="sd-card p-4" style={{ background: hero ? 'var(--accent-soft)' : 'var(--surface)', borderColor: hero ? '#cfe0ef' : 'var(--line)' }}>
      <p className="m-0 text-[10.5px] font-semibold uppercase tracking-[0.04em]" style={{ color: 'var(--ink-3)' }}>{label}</p>
      <p className="m-0 mt-1 truncate text-xl font-semibold tracking-[-0.01em]" style={{ color: hero ? 'var(--accent-ink)' : 'var(--ink)' }}>{value}</p>
      <p className="m-0 mt-1 truncate text-xs" style={{ color: 'var(--ink-3)' }}>{helper}</p>
    </div>
  )
}

function formatDate(value: string | null) {
  if (!value) return '-'
  return new Date(value).toLocaleDateString()
}

function formatRequiredChecklistBlockers(items: QuoteChecklistItem[], stageLabel: string) {
  const incompleteRequiredItems = items.filter((item) => item.isBlocker && !item.isCompleted)
  if (incompleteRequiredItems.length === 0) return null

  const labels = incompleteRequiredItems.slice(0, 3).map((item) => item.label)
  const suffix = incompleteRequiredItems.length > 3 ? ` and ${incompleteRequiredItems.length - 3} more` : ''
  return `Complete required ${stageLabel} item${incompleteRequiredItems.length === 1 ? '' : 's'}: ${labels.join(', ')}${suffix}.`
}

function PolicyDocumentChecklistPanel({
  title,
  items,
  canManage,
  saving,
  onToggle,
}: {
  title: string
  items: QuoteChecklistItem[]
  canManage: boolean
  saving: boolean
  onToggle: (itemId: string, completed: boolean) => void
}) {
  if (items.length === 0) return null

  const openBlockers = items.filter((item) => item.isBlocker && !item.isCompleted).length

  return (
    <div className="mb-3 overflow-hidden rounded-lg border" style={{ borderColor: 'var(--line)' }}>
      <div className="flex flex-wrap items-center justify-between gap-2 border-b px-3 py-2" style={{ borderColor: 'var(--line-2)', background: 'var(--surface-2)' }}>
        <div>
          <div className="text-sm font-semibold" style={{ color: 'var(--ink)' }}>{title}</div>
          <div className="text-xs" style={{ color: 'var(--ink-3)' }}>
            {openBlockers > 0 ? `${openBlockers} blocker${openBlockers === 1 ? '' : 's'} open` : 'Document items tracked from published UW controls'}
          </div>
        </div>
      </div>
      <div className="divide-y" style={{ borderColor: 'var(--line-2)' }}>
        {items.map((item) => (
          <label key={item.id} className="flex items-start gap-3 px-3 py-2.5 text-sm">
            <input
              type="checkbox"
              className="mt-0.5"
              checked={item.isCompleted}
              disabled={!canManage || saving}
              onChange={(e) => onToggle(item.id, e.target.checked)}
            />
            <span className="min-w-0 flex-1">
              <span className="block font-medium" style={{ color: item.isCompleted ? 'var(--ink-3)' : 'var(--ink-2)', textDecoration: item.isCompleted ? 'line-through' : undefined }}>{item.label}</span>
              <span className="mt-0.5 flex flex-wrap gap-2 text-xs" style={{ color: 'var(--ink-4)' }}>
                <span>{item.stage === 'PostBind' ? 'Post-bind' : item.stage}</span>
                {item.isBlocker && <span className="font-semibold" style={{ color: 'var(--warn-fg)' }}>Required</span>}
                {item.completedAt && <span>Completed {new Date(item.completedAt).toLocaleString()}</span>}
                {item.completedByName && <span>{item.completedByName}</span>}
              </span>
            </span>
          </label>
        ))}
      </div>
    </div>
  )
}

function PolicyIssuancePanel({
  packet,
  canIssue,
  checklist,
  canManageChecklist,
  checklistSaving,
  onToggleChecklist,
  referralSummary,
  submissionId,
  issuing,
  previewing,
  onPreview,
  onIssue,
}: {
  packet?: PolicyIssuancePacket
  canIssue: boolean
  checklist: QuoteChecklistItem[]
  canManageChecklist: boolean
  checklistSaving: boolean
  onToggleChecklist: (itemId: string, completed: boolean) => void
  referralSummary?: UnderwritingReferralSummary
  submissionId: string
  issuing: boolean
  previewing: boolean
  onPreview: () => void
  onIssue: () => void
}) {
  const includedForms = packet?.forms.filter((form) => form.isIncluded) ?? []
  const excludedForms = packet?.forms.filter((form) => !form.isIncluded) ?? []
  const openRequiredReferrals = referralSummary?.referrals.filter((referral) => referral.required && referral.status === 'Open') ?? []
  const hasOpenRequiredReferrals = openRequiredReferrals.length > 0
  const ready = includedForms.length > 0 && (packet?.isReady ?? false)
  const issued = packet?.isIssued
  const issueChecklistBlockedReason = formatRequiredChecklistBlockers(checklist, 'issue')
  const actionBlockedReason = !canIssue
    ? 'You do not have permission to issue policies.'
    : issueChecklistBlockedReason
      ? issueChecklistBlockedReason
    : hasOpenRequiredReferrals
      ? `${openRequiredReferrals.length} required underwriting referral${openRequiredReferrals.length === 1 ? '' : 's'} open.`
    : includedForms.length === 0
      ? 'No forms are included in this packet.'
      : !ready
        ? (packet?.readinessMessages[0] ?? 'Resolve packet readiness items before preview or issue.')
        : null

  return (
    <div className="sd-card overflow-hidden">
      <div className="sd-card-head flex-wrap gap-3">
        <div>
          <h3>Policy issuance packet</h3>
          <p className="mt-0.5 text-xs" style={{ color: 'var(--ink-3)' }}>
            {issued
              ? `Issued ${packet?.issuedDate ? new Date(packet.issuedDate).toLocaleDateString() : ''}`
                : ready && !hasOpenRequiredReferrals
                ? issueChecklistBlockedReason ?? `${includedForms.length} form${includedForms.length === 1 ? '' : 's'} ready. Preview, then issue.`
                : hasOpenRequiredReferrals
                  ? 'Required underwriting referrals must be resolved before issue'
                : includedForms.length > 0
                  ? 'Packet needs attention before preview or issue'
                : 'No included policy forms found yet'}
          </p>
        </div>
        {issued ? (
          <span className="sd-pill bound">
            <Check className="h-3.5 w-3.5" /> Issued
          </span>
        ) : (
          <div className="flex flex-wrap items-center gap-2">
            <button
              disabled={!canIssue || !!issueChecklistBlockedReason || !ready || hasOpenRequiredReferrals || previewing}
              onClick={onPreview}
              className="sd-btn outline"
              title={actionBlockedReason ?? 'Generate a draft packet PDF for review'}
            >
              <FileText className="h-3.5 w-3.5" /> {previewing ? 'Generating...' : 'Preview packet'}
            </button>
            <button
              disabled={!canIssue || !!issueChecklistBlockedReason || !ready || hasOpenRequiredReferrals || issuing}
              onClick={onIssue}
              className="sd-btn primary"
              title={actionBlockedReason ?? 'File the final policy packet and mark this policy issued'}
            >
              <Send className="h-3.5 w-3.5" /> Issue policy
            </button>
          </div>
        )}
      </div>
      <div className="sd-card-body">
        {!packet ? (
          <div className="flex h-16 items-center justify-center"><LoadingSpinner /></div>
        ) : includedForms.length === 0 ? (
          <div className="rounded border px-3 py-3 text-sm" style={{ background: 'var(--warn-bg)', borderColor: '#f5d7a3', color: 'var(--warn-fg)' }}>
            Review the quote policy forms first. Once forms are included on the bound quote, they will appear here for issuance.
          </div>
        ) : (
          <>
            {hasOpenRequiredReferrals && (
              <div className="mb-3 rounded border px-3 py-3 text-sm" style={{ background: 'var(--warn-bg)', borderColor: '#f5d7a3', color: 'var(--warn-fg)' }}>
                <div className="font-semibold">{openRequiredReferrals.length} required underwriting referral{openRequiredReferrals.length === 1 ? '' : 's'} open</div>
                <div className="mt-1">Resolve submission referrals before previewing or issuing this policy.</div>
                <Link to={`/submissions/${submissionId}`} className="mt-2 inline-flex font-semibold underline underline-offset-2">
                  Open submission referrals
                </Link>
              </div>
            )}
            {!hasOpenRequiredReferrals && !packet.isReady && packet.readinessMessages.length > 0 && (
              <div className="mb-3 rounded border px-3 py-3 text-sm" style={{ background: 'var(--warn-bg)', borderColor: '#f5d7a3', color: 'var(--warn-fg)' }}>
                {packet.readinessMessages[0]}
              </div>
            )}
            {ready && !issued && !hasOpenRequiredReferrals && (
              <div className="mb-3 rounded border px-3 py-3 text-sm" style={{ background: '#f0fdf4', borderColor: '#bbf7d0', color: '#166534' }}>
                Preview creates a draft PDF for review. Issue policy creates and files the final issued packet.
              </div>
            )}
            <PolicyDocumentChecklistPanel
              title="Issue document checklist"
              items={checklist}
              canManage={canManageChecklist}
              saving={checklistSaving}
              onToggle={onToggleChecklist}
            />
            <div className="overflow-hidden rounded-lg border" style={{ borderColor: 'var(--line)' }}>
              {includedForms.map((form) => (
                <div key={form.id} className="flex items-center gap-3 border-b px-3 py-2.5 text-sm last:border-b-0" style={{ borderColor: 'var(--line-2)' }}>
                  <span className="w-8 text-right font-mono text-xs font-semibold" style={{ color: 'var(--ink-4)' }}>{String(form.sequenceOrder).padStart(2, '0')}</span>
                  <ReadinessIcon status={form.readinessStatus} />
                  <div className="min-w-0 flex-1">
                    <div className="truncate font-medium" style={{ color: 'var(--ink)' }}>{form.formName}</div>
                    <div className="mt-0.5 flex flex-wrap gap-2 text-xs" style={{ color: 'var(--ink-3)' }}>
                      <span className="font-mono">{form.formNumber}</span>
                      <span>{form.editionDate || '-'}</span>
                      <span>{form.formType}</span>
                      <span>{form.fileName || 'No file uploaded'}</span>
                      {form.readinessMessage && (
                        <span style={{ color: form.readinessStatus === 'Blocked' ? 'var(--bad-fg)' : 'var(--warn-fg)', fontWeight: 600 }}>
                          {form.readinessMessage}
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </>
        )}
        {excludedForms.length > 0 && (
          <p className="mt-3 text-xs" style={{ color: 'var(--ink-3)' }}>
            {excludedForms.length} form{excludedForms.length === 1 ? '' : 's'} excluded from this packet.
          </p>
        )}
      </div>
    </div>
  )
}

function ReadinessIcon({ status }: { status: 'Ready' | 'Warning' | 'Blocked' }) {
  if (status === 'Ready') {
    return <Check className="h-4 w-4 shrink-0" style={{ color: 'var(--good-fg)' }} />
  }

  return (
    <AlertTriangle
      className="h-4 w-4 shrink-0"
      style={{ color: status === 'Blocked' ? 'var(--bad-fg)' : 'var(--warn-fg)' }}
    />
  )
}

function TransactionRows({
  transaction: t,
  policyDocumentEntityId,
  canUploadProof,
  canCompleteCancellation,
  canCompleteRewrite,
  postBindBlockedReason,
  onIssueNonRenewalNotice,
}: {
  transaction: PolicyTransaction
  policyDocumentEntityId: string
  canUploadProof: boolean
  canCompleteCancellation: boolean
  canCompleteRewrite: boolean
  postBindBlockedReason: string | null
  onIssueNonRenewalNotice: () => void
}) {
  const [expanded, setExpanded] = useState(false)
  const qc = useQueryClient()
  const { data: artifacts } = useQuery({
    queryKey: ['policies', t.policyId, 'transactions', t.id, 'artifacts'],
    queryFn: () => policiesApi.getTransactionArtifacts(t.policyId, t.id),
  })
  const proofUploadApplies = t.transactionType === 'Cancellation' || t.transactionType === 'NonRenewal' || t.transactionType === 'Reinstatement'
  const hasCompletableStatus = t.status === 'NoticeSent' || t.status === 'PendingEffectiveDate' || t.status === 'Issued'
  const canCompleteCancellationTransaction = canCompleteCancellation &&
    t.transactionType === 'Cancellation' &&
    hasCompletableStatus
  const canCompleteNonRenewal = canCompleteCancellation &&
    t.transactionType === 'NonRenewal' &&
    hasCompletableStatus
  const canComplete = canCompleteCancellationTransaction || canCompleteNonRenewal
  const canIssueNonRenewalNotice = canCompleteCancellation &&
    t.transactionType === 'NonRenewal' &&
    t.status === 'NoticePending'
  const completeCancellation = useMutation({
    mutationFn: () => policiesApi.completeCancellation(t.policyId, t.id, {
      completedDate: new Date().toISOString().slice(0, 10),
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', t.policyId] })
      qc.invalidateQueries({ queryKey: ['policies', t.policyId, 'transactions', t.id, 'artifacts'] })
      toast.success('Cancellation completed')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Cancellation could not be completed'),
  })
  const completeNonRenewal = useMutation({
    mutationFn: () => policiesApi.completeNonRenewal(t.policyId, t.id, {
      completedDate: new Date().toISOString().slice(0, 10),
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', t.policyId] })
      qc.invalidateQueries({ queryKey: ['policies', t.policyId, 'transactions', t.id, 'artifacts'] })
      toast.success('Non-renewal completed')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Non-renewal could not be completed'),
  })
  const completePending = completeCancellation.isPending || completeNonRenewal.isPending

  return (
    <>
      <tr onClick={() => setExpanded((value) => !value)} className="cursor-pointer">
        <td className="id">{t.transactionNumber}</td>
        <td>{t.transactionType}</td>
        <td>
          <span className={`sd-pill ${POLICY_TRANSACTION_STATUS_PILL[t.status]}`}>
            {POLICY_TRANSACTION_STATUS_LABELS[t.status]}
          </span>
        </td>
        <td>{formatDate(t.effectiveDate)}</td>
        <td className="num">
          <span style={{ color: t.premiumChange >= 0 ? 'var(--ink-2)' : 'var(--bad-fg)' }}>
            {t.premiumChange >= 0 ? '+' : ''}{formatCurrency(t.premiumChange)}
          </span>
        </td>
        <td className="num font-medium">{formatCurrency(t.newTotalPremium)}</td>
        <td>
          <div className="flex flex-wrap items-center gap-2">
            <span>{t.processedByName}</span>
            {canIssueNonRenewalNotice && (
              <button
                type="button"
                className="sd-btn outline sm"
                disabled={!!postBindBlockedReason}
                title={postBindBlockedReason ?? 'Issue the non-renewal notice'}
                onClick={(event) => {
                  event.stopPropagation()
                  if (!postBindBlockedReason) {
                    onIssueNonRenewalNotice()
                  }
                }}
              >
                <Send className="h-3.5 w-3.5" /> Issue Notice
              </button>
            )}
            {canComplete && (
              <button
                type="button"
                className="sd-btn outline sm"
                disabled={completePending || !!postBindBlockedReason}
                title={postBindBlockedReason ?? `Complete the ${canCompleteNonRenewal ? 'non-renewal' : 'cancellation'} once the effective date has passed`}
                onClick={(event) => {
                  event.stopPropagation()
                  if (postBindBlockedReason) {
                    return
                  } else if (canCompleteNonRenewal) {
                    completeNonRenewal.mutate()
                  } else {
                    completeCancellation.mutate()
                  }
                }}
              >
                <Check className="h-3.5 w-3.5" /> Complete
              </button>
            )}
          </div>
        </td>
      </tr>
      {expanded && artifacts && (
        <TransactionArtifactDetails
          artifacts={artifacts}
          policyDocumentEntityId={policyDocumentEntityId}
          canUploadProof={canUploadProof && proofUploadApplies}
          canCompleteRewrite={canCompleteRewrite}
          postBindBlockedReason={postBindBlockedReason}
        />
      )}
      {expanded && !artifacts && (
        <tr>
          <td colSpan={7} className="px-5 pb-4 text-sm" style={{ color: 'var(--ink-3)' }}>Loading transaction details...</td>
        </tr>
      )}
    </>
  )
}

function TransactionArtifactDetails({
  artifacts,
  policyDocumentEntityId,
  canUploadProof,
  canCompleteRewrite,
  postBindBlockedReason,
}: {
  artifacts: Awaited<ReturnType<typeof policiesApi.getTransactionArtifacts>>
  policyDocumentEntityId: string
  canUploadProof: boolean
  canCompleteRewrite: boolean
  postBindBlockedReason: string | null
}) {
  const qc = useQueryClient()
  const [activeSection, setActiveSection] = useState('Versions')
  const proofUploadConfig = getTransactionProofUploadConfig(artifacts.transaction.transactionType)
  const proofUpload = useMutation({
    mutationFn: (file: File) =>
      attachmentsApi.upload('Policy', policyDocumentEntityId, file, proofUploadConfig.documentType, proofUploadConfig.description, artifacts.transaction.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', artifacts.transaction.policyId, 'transactions', artifacts.transaction.id, 'artifacts'] })
      toast.success(`${proofUploadConfig.description} uploaded`)
    },
    onError: () => toast.error('Proof upload failed'),
  })
  const completeRewrite = useMutation({
    mutationFn: () => policiesApi.completeRewrite(artifacts.transaction.policyId, artifacts.transaction.id, {
      completedDate: new Date().toISOString().slice(0, 10),
      notes: 'Replacement policy bound.',
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', artifacts.transaction.policyId] })
      qc.invalidateQueries({ queryKey: ['policies', artifacts.transaction.policyId, 'transactions', artifacts.transaction.id, 'artifacts'] })
      toast.success('Rewrite completed')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Rewrite could not be completed'),
  })
  const transaction = artifacts.transaction
  const sections = [
    { id: 'Versions', count: (transaction.priorVersion ? 1 : 0) + (transaction.resultingVersion ? 1 : 0) },
    { id: 'Rating', count: artifacts.ratingSnapshots.length },
    { id: 'Documents', count: artifacts.documents.length },
    { id: 'Communications', count: artifacts.communications.length },
    { id: 'Accounting', count: artifacts.invoices.length },
    ...(transaction.transactionType === 'Cancellation' ? [{ id: 'Cancellation', count: transaction.cancellationDetail ? 1 : 0 }] : []),
    ...(transaction.transactionType === 'NonRenewal' ? [{ id: 'Non-Renewal', count: transaction.nonRenewalDetail ? 1 : 0 }] : []),
    ...(transaction.transactionType === 'Reinstatement' ? [{ id: 'Reinstatement', count: transaction.reinstatementDetail ? 1 : 0 }] : []),
    ...(transaction.transactionType === 'Rewrite' ? [{ id: 'Rewrite', count: transaction.rewriteDetail ? 1 : 0 }] : []),
    { id: 'Compliance', count: artifacts.complianceChecklists.reduce((sum, checklist) => sum + checklist.items.length, 0) },
    { id: 'Tasks', count: artifacts.tasks.length },
    { id: 'Approvals', count: artifacts.approvals.length },
  ]

  return (
    <tr>
      <td colSpan={7} className="px-5 pb-4">
        <div className="rounded border p-3 text-sm" style={{ background: 'var(--surface)' }}>
          <div className="mb-3 flex flex-wrap gap-1.5">
            {sections.map((section) => (
              <button
                key={section.id}
                type="button"
                onClick={() => setActiveSection(section.id)}
                className="rounded border px-2.5 py-1 text-xs font-semibold"
                style={activeSection === section.id
                  ? { borderColor: 'var(--accent)', background: 'var(--accent-soft)', color: 'var(--accent-ink)' }
                  : { borderColor: 'var(--line)', background: 'var(--surface)', color: 'var(--ink-2)' }}
              >
                {section.id}
                <span className="ml-1" style={{ color: 'var(--ink-4)' }}>{section.count}</span>
              </button>
            ))}
          </div>

          {activeSection === 'Versions' && (
            <div className="grid gap-3 sm:grid-cols-[1fr_auto_1fr]">
              <VersionSummary label="Before" version={transaction.priorVersion} />
              <div className="hidden items-center justify-center sm:flex" style={{ color: 'var(--ink-4)' }}>-&gt;</div>
              <VersionSummary label="After" version={transaction.resultingVersion} />
            </div>
          )}

          {activeSection === 'Rating' && (
            <CompactList empty="No linked rating snapshots.">
              {artifacts.ratingSnapshots.map((rating) => (
                <CompactRow key={rating.snapshotId} title={formatCurrency(rating.grandTotalPremium)} meta={`Mod ${rating.scheduleModifier.toFixed(2)} - ${formatDate(rating.ratedAt)}`} value={rating.isBoundSnapshot ? 'Bound' : undefined} />
              ))}
            </CompactList>
          )}

          {activeSection === 'Documents' && (
            <div>
              {canUploadProof && (
                <div className="mb-2">
                  <label className="sd-btn outline xs cursor-pointer">
                    {proofUploadConfig.buttonLabel}
                    <input
                      type="file"
                      className="hidden"
                      accept=".pdf,.png,.jpg,.jpeg,.doc,.docx"
                      disabled={proofUpload.isPending}
                      onChange={(event) => {
                        const file = event.target.files?.[0]
                        event.currentTarget.value = ''
                        if (file) proofUpload.mutate(file)
                      }}
                    />
                  </label>
                </div>
              )}
              <CompactList empty="No linked notice, proof, or policy documents.">
                {artifacts.documents.map((doc) => (
                  <CompactRow key={doc.id} title={doc.fileName} meta={`${DOCUMENT_TYPE_LABELS[doc.documentType]}${doc.policyVersionNumber != null ? ` - v${doc.policyVersionNumber}` : ''}`} value={formatDate(doc.createdAt)} />
                ))}
              </CompactList>
            </div>
          )}

          {activeSection === 'Communications' && (
            <CompactList empty="No linked communications.">
              {artifacts.communications.map((communication) => (
                <CompactRow
                  key={communication.id}
                  title={communication.subject}
                  meta={`${formatCommunicationPurpose(communication.purpose)} - ${communication.status}`}
                  value={communication.graphMessageWebLink ? (
                    <a
                      href={communication.graphMessageWebLink}
                      target="_blank"
                      rel="noreferrer"
                      style={{ color: 'var(--accent-ink)' }}
                    >
                      Open
                    </a>
                  ) : (
                    communication.sentAt ? formatDate(communication.sentAt) : formatDate(communication.createdAt)
                  )}
                />
              ))}
            </CompactList>
          )}

          {activeSection === 'Accounting' && (
            <CompactList empty="No linked invoices or accounting records.">
              {artifacts.invoices.map((invoice) => (
                <CompactRow
                  key={invoice.id}
                  title={invoice.invoiceNumber}
                  meta={`${invoice.status} - ${formatDate(invoice.invoiceDate)}`}
                  value={formatCurrency(invoice.totalAmount)}
                  sub={invoice.policyTransactionNumber ? `${invoice.policyTransactionNumber}${invoice.policyTransactionType ? ` - ${invoice.policyTransactionType}` : ''}` : undefined}
                />
              ))}
            </CompactList>
          )}

          {activeSection === 'Cancellation' && (
            <CancellationSummary
              transaction={transaction}
              documents={artifacts.documents.filter((doc) => doc.documentType === 'CancellationNonRenewal' || doc.documentType === 'ProofOfNotice')}
              canUploadProof={canUploadProof}
              proofUploading={proofUpload.isPending}
              onUploadProof={(file) => proofUpload.mutate(file)}
            />
          )}

          {activeSection === 'Non-Renewal' && (
            <NonRenewalSummary
              transaction={transaction}
              documents={artifacts.documents.filter((doc) => doc.documentType === 'CancellationNonRenewal' || doc.documentType === 'ProofOfNotice')}
              canUploadProof={canUploadProof}
              proofUploading={proofUpload.isPending}
              onUploadProof={(file) => proofUpload.mutate(file)}
            />
          )}

          {activeSection === 'Reinstatement' && (
            <ReinstatementSummary
              transaction={transaction}
              documents={artifacts.documents.filter((doc) => doc.documentType === 'ReinstatementApproval')}
              canUploadProof={canUploadProof}
              proofUploading={proofUpload.isPending}
              onUploadProof={(file) => proofUpload.mutate(file)}
            />
          )}

          {activeSection === 'Rewrite' && (
            <RewriteSummary
              transaction={transaction}
              canComplete={canCompleteRewrite && !postBindBlockedReason}
              completeBlockedReason={postBindBlockedReason}
              completing={completeRewrite.isPending}
              onComplete={() => !postBindBlockedReason && completeRewrite.mutate()}
            />
          )}

          {activeSection === 'Compliance' && (
            <div className="space-y-3">
              {transaction.transactionType === 'Cancellation' && (
                <CancellationSummary
                  transaction={transaction}
                  documents={artifacts.documents.filter((doc) => doc.documentType === 'CancellationNonRenewal' || doc.documentType === 'ProofOfNotice')}
                />
              )}
              {transaction.transactionType === 'NonRenewal' && (
                <NonRenewalSummary
                  transaction={transaction}
                  documents={artifacts.documents.filter((doc) => doc.documentType === 'CancellationNonRenewal' || doc.documentType === 'ProofOfNotice')}
                />
              )}
              {artifacts.complianceChecklists.length === 0 ? (
                <EmptyState text="No linked compliance checklist." />
              ) : (
                artifacts.complianceChecklists.flatMap((checklist) =>
                  checklist.items.map((item) => (
                    <CompactRow key={item.id} title={item.label} meta={checklist.purpose} value={item.isCompleted ? 'Complete' : 'Open'} />
                  ))
                )
              )}
            </div>
          )}

          {activeSection === 'Tasks' && (
            <CompactList empty="No linked tasks.">
              {artifacts.tasks.map((task) => (
                <CompactRow key={task.id} title={task.taskTypeName} meta={`${task.priority} - ${task.status}`} value={formatDate(task.dueDate)} />
              ))}
            </CompactList>
          )}

          {activeSection === 'Approvals' && (
            <CompactList empty="No approval history.">
              {artifacts.approvals.map((approval) => (
                <CompactRow key={approval.id} title={approval.approvalType} meta={`${approval.requestedByName} requested ${formatDate(approval.requestedAt)}`} value={approval.decision ?? 'Pending'} sub={approval.notes ?? undefined} />
              ))}
            </CompactList>
          )}
        </div>
      </td>
    </tr>
  )
}

function CompactList({ empty, children }: { empty: string; children: React.ReactNode }) {
  const rows = Array.isArray(children) ? children.filter(Boolean) : children
  if (Array.isArray(rows) && rows.length === 0) return <EmptyState text={empty} />
  return <div className="space-y-2">{rows}</div>
}

function EmptyState({ text }: { text: string }) {
  return <div className="rounded border border-dashed px-3 py-3" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)', color: 'var(--ink-3)' }}>{text}</div>
}

function CompactRow({ title, meta, value, sub }: { title: string; meta?: string; value?: React.ReactNode; sub?: string }) {
  return (
    <div className="flex items-start justify-between gap-3 rounded border px-3 py-2" style={{ borderColor: 'var(--line)' }}>
      <div className="min-w-0">
        <div className="truncate font-medium" style={{ color: 'var(--ink-2)' }}>{title}</div>
        {meta && <div className="text-xs" style={{ color: 'var(--ink-3)' }}>{meta}</div>}
        {sub && <div className="mt-1 text-xs" style={{ color: 'var(--ink-2)' }}>{sub}</div>}
      </div>
      {value && <span className="shrink-0 text-xs font-medium" style={{ color: 'var(--ink-3)' }}>{value}</span>}
    </div>
  )
}

function getTransactionProofUploadConfig(transactionType: PolicyTransaction['transactionType']): {
  documentType: DocumentType
  description: string
  buttonLabel: string
} {
  if (transactionType === 'Reinstatement') {
    return {
      documentType: 'ReinstatementApproval',
      description: 'Reinstatement approval',
      buttonLabel: 'Upload approval',
    }
  }

  return {
    documentType: 'ProofOfNotice',
    description: 'Proof of notice',
    buttonLabel: 'Upload proof',
  }
}

function ReinstatementSummary({
  transaction,
  documents = [],
  canUploadProof = false,
  proofUploading = false,
  onUploadProof,
}: {
  transaction: PolicyTransaction
  documents?: Attachment[]
  canUploadProof?: boolean
  proofUploading?: boolean
  onUploadProof?: (file: File) => void
}) {
  const detail = transaction.reinstatementDetail
  const [downloadingId, setDownloadingId] = useState<string | null>(null)

  const downloadDocument = async (attachment: Attachment) => {
    setDownloadingId(attachment.id)
    try {
      const url = await attachmentsApi.getDownloadUrl(attachment.id)
      const a = document.createElement('a')
      a.href = url
      a.download = attachment.fileName
      a.target = '_blank'
      a.rel = 'noopener noreferrer'
      document.body.appendChild(a)
      a.click()
      document.body.removeChild(a)
    } catch {
      toast.error('Failed to get download link')
    } finally {
      setDownloadingId(null)
    }
  }

  if (!detail) return <EmptyState text="No reinstatement detail saved." />

  return (
    <div className="space-y-3">
      <div className="rounded-lg border p-3" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <div className="text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Reinstatement</div>
            <div className="mt-1 text-sm font-semibold" style={{ color: 'var(--ink)' }}>{formatDate(detail.reinstatementEffectiveDate)}</div>
          </div>
          <span className={`sd-pill ${POLICY_TRANSACTION_STATUS_PILL[transaction.status]}`}>{POLICY_TRANSACTION_STATUS_LABELS[transaction.status]}</span>
        </div>
        <div className="mt-3 grid gap-2 text-sm sm:grid-cols-2" style={{ color: 'var(--ink-2)' }}>
          <div><span style={{ color: 'var(--ink-3)' }}>Reason:</span> {detail.reason || transaction.reasonText || 'Not recorded'}</div>
          <div><span style={{ color: 'var(--ink-3)' }}>Premium change:</span> {formatCurrency(transaction.premiumChange)}</div>
        </div>
        {(detail.notes || transaction.notes) && <p className="mt-2 text-sm" style={{ color: 'var(--ink-2)' }}>{detail.notes ?? transaction.notes}</p>}
      </div>
      <div className="grid gap-3 sm:grid-cols-[1fr_auto_1fr]">
        <VersionSummary label="Cancelled version" version={transaction.priorVersion} />
        <div className="hidden items-center justify-center sm:flex" style={{ color: 'var(--ink-4)' }}>-&gt;</div>
        <VersionSummary label="Reinstated version" version={transaction.resultingVersion} />
      </div>
      {canUploadProof && onUploadProof && (
        <div>
          <label className="sd-btn outline xs cursor-pointer">
            {proofUploading ? 'Uploading...' : 'Upload approval'}
            <input
              type="file"
              className="hidden"
              accept=".pdf,.png,.jpg,.jpeg,.doc,.docx"
              disabled={proofUploading}
              onChange={(event) => {
                const file = event.target.files?.[0]
                event.currentTarget.value = ''
                if (file) onUploadProof(file)
              }}
            />
          </label>
        </div>
      )}
      {documents.length > 0 && (
        <div className="space-y-1">
          <div className="text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Reinstatement Documents</div>
          {documents.map((doc) => (
            <div key={doc.id} className="flex items-center justify-between gap-3 rounded border px-2.5 py-2 text-xs" style={{ borderColor: 'var(--line)', background: 'var(--surface)' }}>
              <div className="min-w-0">
                <div className="truncate font-medium" style={{ color: 'var(--ink-2)' }}>{doc.fileName}</div>
                <div style={{ color: 'var(--ink-3)' }}>{DOCUMENT_TYPE_LABELS[doc.documentType]} - {formatDate(doc.createdAt)}</div>
              </div>
              <button
                type="button"
                className="sims-icon-btn shrink-0"
                disabled={downloadingId === doc.id}
                onClick={() => downloadDocument(doc)}
                title="Download"
              >
                {downloadingId === doc.id ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Download className="h-3.5 w-3.5" />}
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

function RewriteSummary({
  transaction,
  canComplete = false,
  completeBlockedReason,
  completing = false,
  onComplete,
}: {
  transaction: PolicyTransaction
  canComplete?: boolean
  completeBlockedReason?: string | null
  completing?: boolean
  onComplete?: () => void
}) {
  const detail = transaction.rewriteDetail

  if (!detail) return <EmptyState text="No rewrite detail saved." />
  const isComplete = transaction.status === 'Completed'

  return (
    <div className="space-y-3">
      <div className="rounded-lg border p-3" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <div className="text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Rewrite</div>
            <div className="mt-1 text-sm font-semibold" style={{ color: 'var(--ink)' }}>{formatDate(transaction.effectiveDate)}</div>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <span className={`sd-pill ${POLICY_TRANSACTION_STATUS_PILL[transaction.status]}`}>{POLICY_TRANSACTION_STATUS_LABELS[transaction.status]}</span>
            {(canComplete || completeBlockedReason) && !isComplete && onComplete && (
              <button
                type="button"
                className="sd-btn primary xs"
                disabled={completing || !!completeBlockedReason}
                onClick={onComplete}
                title={completeBlockedReason ?? 'Complete after the replacement quote is bound'}
              >
                <Check className="h-3.5 w-3.5" /> {completing ? 'Completing...' : 'Complete'}
              </button>
            )}
          </div>
        </div>
        <div className="mt-3 grid gap-2 text-sm sm:grid-cols-2" style={{ color: 'var(--ink-2)' }}>
          <div><span style={{ color: 'var(--ink-3)' }}>Reason:</span> {detail.reason || transaction.reasonText || 'Not recorded'}</div>
          <div>
            <span style={{ color: 'var(--ink-3)' }}>Replacement quote:</span>{' '}
            <Link to={`/quotes/${detail.replacementQuoteId}`} style={{ color: 'var(--accent-ink)' }}>{detail.replacementQuoteNumber ?? 'Open quote'}</Link>
          </div>
          {detail.replacementPolicyId && (
            <div>
              <span style={{ color: 'var(--ink-3)' }}>Replacement policy:</span>{' '}
              <Link to={`/policies/${detail.replacementPolicyId}`} style={{ color: 'var(--accent-ink)' }}>{detail.replacementPolicyNumber ?? 'Open policy'}</Link>
            </div>
          )}
        </div>
        {(detail.notes || transaction.notes) && <p className="mt-2 text-sm" style={{ color: 'var(--ink-2)' }}>{detail.notes ?? transaction.notes}</p>}
      </div>
      <div className="grid gap-3 sm:grid-cols-[1fr_auto_1fr]">
        <VersionSummary label="Source version" version={transaction.priorVersion} />
        <div className="hidden items-center justify-center sm:flex" style={{ color: 'var(--ink-4)' }}>-&gt;</div>
        <div>
          <div className="text-xs font-semibold uppercase" style={{ color: 'var(--ink-3)' }}>Replacement quote</div>
          <div className="mt-1">
            <Link to={`/quotes/${detail.replacementQuoteId}`} className="font-medium" style={{ color: 'var(--accent-ink)' }}>{detail.replacementQuoteNumber ?? 'Open rewrite quote'}</Link>
          </div>
        </div>
      </div>
    </div>
  )
}

function CancellationSummary({
  transaction,
  documents = [],
  canUploadProof = false,
  proofUploading = false,
  onUploadProof,
}: {
  transaction: PolicyTransaction
  documents?: Attachment[]
  canUploadProof?: boolean
  proofUploading?: boolean
  onUploadProof?: (file: File) => void
}) {
  const detail = transaction.cancellationDetail
  const [downloadingId, setDownloadingId] = useState<string | null>(null)
  const noticeDocuments = documents.filter((doc) => doc.documentType === 'CancellationNonRenewal')
  const proofDocuments = documents.filter((doc) => doc.documentType === 'ProofOfNotice')
  const hasProof = proofDocuments.length > 0

  const downloadDocument = async (attachment: Attachment) => {
    setDownloadingId(attachment.id)
    try {
      const url = await attachmentsApi.getDownloadUrl(attachment.id)
      const a = document.createElement('a')
      a.href = url
      a.download = attachment.fileName
      a.target = '_blank'
      a.rel = 'noopener noreferrer'
      document.body.appendChild(a)
      a.click()
      document.body.removeChild(a)
    } catch {
      toast.error('Failed to get download link')
    } finally {
      setDownloadingId(null)
    }
  }

  return (
    <div className="rounded border px-3 py-2" style={{ borderColor: 'var(--bad-fg)', background: 'var(--bad-bg)', color: 'var(--ink-2)' }}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="font-semibold" style={{ color: 'var(--ink)' }}>Cancellation Notice Detail</div>
        <span className={`sd-pill ${hasProof ? 'bound' : 'warning'}`}>
          {hasProof ? 'Proof Filed' : 'Proof Not Filed'}
        </span>
      </div>
      <div className="mt-2 grid gap-2 text-xs sm:grid-cols-2 lg:grid-cols-3">
        <div><span style={{ color: 'var(--ink-3)' }}>Reason:</span> {detail?.reasonLabel || transaction.cancellationReason || 'Not recorded'}</div>
        <div><span style={{ color: 'var(--ink-3)' }}>Code:</span> {detail?.reasonCode || transaction.reasonCode || '-'}</div>
        <div><span style={{ color: 'var(--ink-3)' }}>Category:</span> {detail?.reasonCategory || '-'}</div>
        <div><span style={{ color: 'var(--ink-3)' }}>Mailing Date:</span> {formatDate(detail?.noticeMailingDate ?? null)}</div>
        <div><span style={{ color: 'var(--ink-3)' }}>Notice Days:</span> {detail ? `${detail.noticeRequirementDays} + ${detail.mailingDays} mailing` : '-'}</div>
        <div><span style={{ color: 'var(--ink-3)' }}>Cancellation Date:</span> {formatDate(detail?.cancellationEffectiveDate ?? transaction.effectiveDate)}</div>
        <div><span style={{ color: 'var(--ink-3)' }}>Method:</span> {detail?.method || transaction.cancellationMethod || 'Not recorded'}</div>
        <div><span style={{ color: 'var(--ink-3)' }}>Template:</span> {detail?.noticeTemplateName || (detail?.noticeTemplateId ? 'Selected template' : 'Default or not recorded')}</div>
        <div><span style={{ color: 'var(--ink-3)' }}>Proof:</span> {hasProof ? `${proofDocuments.length} document${proofDocuments.length === 1 ? '' : 's'} filed` : 'Not filed'}</div>
      </div>
      {detail?.resolvedReasonLanguage && (
        <p className="mt-2 text-xs" style={{ color: 'var(--ink-2)' }}>{detail.resolvedReasonLanguage}</p>
      )}
      {!hasProof && (
        <p className="mt-2 text-xs" style={{ color: 'var(--ink-3)' }}>Proof of notice is tracked here when applicable, but it is not required to complete every cancellation.</p>
      )}
      {canUploadProof && onUploadProof && (
        <div className="mt-3">
          <label className="sd-btn outline xs cursor-pointer">
            {proofUploading ? 'Uploading...' : 'Upload proof'}
            <input
              type="file"
              className="hidden"
              accept=".pdf,.png,.jpg,.jpeg,.doc,.docx"
              disabled={proofUploading}
              onChange={(event) => {
                const file = event.target.files?.[0]
                event.currentTarget.value = ''
                if (file) onUploadProof(file)
              }}
            />
          </label>
        </div>
      )}
      {documents.length > 0 && (
        <div className="mt-3 space-y-1">
          <div className="text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Notice Documents</div>
          {[...noticeDocuments, ...proofDocuments].map((doc) => (
            <div key={doc.id} className="flex items-center justify-between gap-3 rounded border px-2.5 py-2 text-xs" style={{ borderColor: 'var(--bad-fg)', background: 'var(--surface)' }}>
              <div className="min-w-0">
                <div className="truncate font-medium" style={{ color: 'var(--ink-2)' }}>{doc.fileName}</div>
                <div style={{ color: 'var(--ink-3)' }}>{DOCUMENT_TYPE_LABELS[doc.documentType]} - {formatDate(doc.createdAt)}</div>
              </div>
              <button
                type="button"
                className="sims-icon-btn shrink-0"
                disabled={downloadingId === doc.id}
                onClick={() => downloadDocument(doc)}
                title="Download"
              >
                {downloadingId === doc.id ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Download className="h-3.5 w-3.5" />}
              </button>
            </div>
          ))}
        </div>
      )}
      {transaction.notes && <p className="mt-2 text-xs" style={{ color: 'var(--ink-2)' }}>{transaction.notes}</p>}
    </div>
  )
}

function NonRenewalSummary({
  transaction,
  documents = [],
  canUploadProof = false,
  proofUploading = false,
  onUploadProof,
}: {
  transaction: PolicyTransaction
  documents?: Attachment[]
  canUploadProof?: boolean
  proofUploading?: boolean
  onUploadProof?: (file: File) => void
}) {
  const detail = transaction.nonRenewalDetail
  const [downloadingId, setDownloadingId] = useState<string | null>(null)
  const noticeDocuments = documents.filter((doc) => doc.documentType === 'CancellationNonRenewal')
  const proofDocuments = documents.filter((doc) => doc.documentType === 'ProofOfNotice')
  const hasProof = proofDocuments.length > 0

  const downloadDocument = async (attachment: Attachment) => {
    setDownloadingId(attachment.id)
    try {
      const url = await attachmentsApi.getDownloadUrl(attachment.id)
      const a = document.createElement('a')
      a.href = url
      a.download = attachment.fileName
      a.target = '_blank'
      a.rel = 'noopener noreferrer'
      document.body.appendChild(a)
      a.click()
      document.body.removeChild(a)
    } catch {
      toast.error('Failed to get download link')
    } finally {
      setDownloadingId(null)
    }
  }

  return (
    <div className="rounded border px-3 py-2" style={{ borderColor: 'var(--warn-fg)', background: 'var(--warn-bg)', color: 'var(--ink-2)' }}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="font-semibold" style={{ color: 'var(--ink)' }}>Non-Renewal Notice Detail</div>
        <span className={`sd-pill ${hasProof ? 'bound' : 'warning'}`}>
          {hasProof ? 'Proof Filed' : 'Proof Not Filed'}
        </span>
      </div>
      <div className="mt-2 grid gap-2 text-xs sm:grid-cols-2 lg:grid-cols-3">
        <div><span style={{ color: 'var(--ink-3)' }}>Reason:</span> {detail?.reason || transaction.reasonText || 'Not recorded'}</div>
        <div><span style={{ color: 'var(--ink-3)' }}>Mailing Date:</span> {formatDate(detail?.noticeMailingDate ?? null)}</div>
        <div><span style={{ color: 'var(--ink-3)' }}>Notice Days:</span> {detail ? `${detail.noticeRequirementDays} + ${detail.mailingDays} mailing` : '-'}</div>
        <div><span style={{ color: 'var(--ink-3)' }}>Non-Renewal Date:</span> {formatDate(detail?.nonRenewalEffectiveDate ?? transaction.effectiveDate)}</div>
        <div><span style={{ color: 'var(--ink-3)' }}>Method:</span> {detail?.method || 'Not recorded'}</div>
        <div><span style={{ color: 'var(--ink-3)' }}>Template:</span> {detail?.noticeTemplateName || (detail?.noticeTemplateId ? 'Selected template' : 'Default or not recorded')}</div>
        <div><span style={{ color: 'var(--ink-3)' }}>Proof:</span> {hasProof ? `${proofDocuments.length} document${proofDocuments.length === 1 ? '' : 's'} filed` : 'Not filed'}</div>
      </div>
      {!hasProof && (
        <p className="mt-2 text-xs" style={{ color: 'var(--ink-3)' }}>Proof of notice is tracked here when applicable, but it is not required to issue the notice.</p>
      )}
      {canUploadProof && onUploadProof && (
        <div className="mt-3">
          <label className="sd-btn outline xs cursor-pointer">
            {proofUploading ? 'Uploading...' : 'Upload proof'}
            <input
              type="file"
              className="hidden"
              accept=".pdf,.png,.jpg,.jpeg,.doc,.docx"
              disabled={proofUploading}
              onChange={(event) => {
                const file = event.target.files?.[0]
                event.currentTarget.value = ''
                if (file) onUploadProof(file)
              }}
            />
          </label>
        </div>
      )}
      {documents.length > 0 && (
        <div className="mt-3 space-y-1">
          <div className="text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Notice Documents</div>
          {[...noticeDocuments, ...proofDocuments].map((doc) => (
            <div key={doc.id} className="flex items-center justify-between gap-3 rounded border px-2.5 py-2 text-xs" style={{ borderColor: 'var(--warn-fg)', background: 'var(--surface)' }}>
              <div className="min-w-0">
                <div className="truncate font-medium" style={{ color: 'var(--ink-2)' }}>{doc.fileName}</div>
                <div style={{ color: 'var(--ink-3)' }}>{DOCUMENT_TYPE_LABELS[doc.documentType]} - {formatDate(doc.createdAt)}</div>
              </div>
              <button
                type="button"
                className="sims-icon-btn shrink-0"
                disabled={downloadingId === doc.id}
                onClick={() => downloadDocument(doc)}
                title="Download"
              >
                {downloadingId === doc.id ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Download className="h-3.5 w-3.5" />}
              </button>
            </div>
          ))}
        </div>
      )}
      {transaction.notes && <p className="mt-2 text-xs" style={{ color: 'var(--ink-2)' }}>{transaction.notes}</p>}
    </div>
  )
}

function formatCommunicationPurpose(purpose: string) {
  return purpose.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function VersionChangeDetails({ transaction }: { transaction: PolicyTransaction }) {
  const prior = transaction.priorVersion
  const resulting = transaction.resultingVersion

  return (
    <tr>
      <td colSpan={7} className="px-5 pb-4">
        <div className="grid gap-3 rounded border p-3 text-sm sm:grid-cols-[1fr_auto_1fr]" style={{ background: 'var(--surface-2)' }}>
          <VersionSummary label="Before" version={prior} />
          <div className="hidden items-center justify-center sm:flex" style={{ color: 'var(--ink-4)' }}>-&gt;</div>
          <VersionSummary label="After" version={resulting} />
        </div>
      </td>
    </tr>
  )
}

function VersionSummary({ label, version }: { label: string; version: PolicyTransaction['priorVersion'] }) {
  if (!version) {
    return (
      <div>
        <div className="text-xs font-semibold uppercase" style={{ color: 'var(--ink-3)' }}>{label}</div>
        <div className="mt-1" style={{ color: 'var(--ink-3)' }}>No version snapshot</div>
      </div>
    )
  }

  return (
    <div>
      <div className="flex items-center gap-2">
        <span className="text-xs font-semibold uppercase" style={{ color: 'var(--ink-3)' }}>{label}</span>
        <span className="rounded px-1.5 py-0.5 text-xs font-medium" style={{ background: 'var(--surface)', color: 'var(--ink-2)' }}>v{version.versionNumber}</span>
        <span className={`sd-pill ${POLICY_STATUS_PILL[version.status]}`}>{POLICY_STATUS_LABELS[version.status]}</span>
      </div>
      <div className="mt-2 grid gap-2 sm:grid-cols-3" style={{ color: 'var(--ink-2)' }}>
        <div>
          <div className="text-xs" style={{ color: 'var(--ink-3)' }}>Term</div>
          <div>{formatDate(version.effectiveDate)} - {formatDate(version.expirationDate)}</div>
        </div>
        <div>
          <div className="text-xs" style={{ color: 'var(--ink-3)' }}>Premium</div>
          <div>{formatCurrency(version.premiumAmount)}</div>
        </div>
        <div>
          <div className="text-xs" style={{ color: 'var(--ink-3)' }}>Total</div>
          <div className="font-medium">{formatCurrency(version.totalPremium)}</div>
        </div>
      </div>
    </div>
  )
}

function CancellationTransactionDetails({ transaction }: { transaction: PolicyTransaction }) {
  const legalSnapshot = parseLegalSnapshot(transaction.cancellationLegalRequirementSnapshotJson)

  return (
    <tr style={{ background: 'var(--bad-bg)' }}>
      <td colSpan={7} className="px-5 py-4">
        <div className="grid gap-4 text-sm md:grid-cols-[minmax(0,1fr)_minmax(280px,380px)]">
          <div>
            <div className="font-semibold" style={{ color: 'var(--ink)' }}>Cancellation Review</div>
            <div className="mt-2 grid gap-2 sm:grid-cols-2" style={{ color: 'var(--ink-2)' }}>
              <div><span style={{ color: 'var(--ink-3)' }}>Reason:</span> {transaction.cancellationReason || 'Not recorded'}</div>
              <div><span style={{ color: 'var(--ink-3)' }}>Method:</span> {transaction.cancellationMethod || 'Not recorded'}</div>
            </div>
            {transaction.notes && <p className="mt-2" style={{ color: 'var(--ink-2)' }}>{transaction.notes}</p>}
            <div className="mt-3 space-y-1">
              {transaction.cancellationComplianceChecklist.length === 0 ? (
                <p style={{ color: 'var(--ink-3)' }}>No checklist was saved with this transaction.</p>
              ) : transaction.cancellationComplianceChecklist.map((item) => (
                <div key={item.key} className="flex items-start gap-2" style={{ color: 'var(--ink-2)' }}>
                  <span style={{ color: item.isCompleted ? 'var(--good-fg)' : 'var(--ink-4)' }}>{item.isCompleted ? '[x]' : '[ ]'}</span>
                  <span>{item.label}</span>
                </div>
              ))}
            </div>
          </div>
          <div className="rounded border p-3" style={{ background: 'var(--surface)' }}>
            <div className="text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>Saved Legal Snapshot</div>
            {legalSnapshot.length === 0 ? (
              <p className="mt-2 text-sm" style={{ color: 'var(--ink-3)' }}>No legal requirement snapshot was saved.</p>
            ) : (
              <div className="mt-2 max-h-52 space-y-2 overflow-auto pr-1">
                {legalSnapshot.map((row) => (
                  <div key={row.id} className="text-sm">
                    <div className="font-medium" style={{ color: 'var(--ink-2)' }}>{row.topic}</div>
                    <div className="text-xs" style={{ color: 'var(--ink-3)' }}>{row.category}{row.state ? ` - ${row.state}` : ''}</div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </td>
    </tr>
  )
}

function EndorsePolicyModal({
  policy,
  saving,
  onClose,
  onSave,
}: {
  policy: Policy
  saving: boolean
  onClose: () => void
  onSave: (data: { effectiveDate: string; premiumChange: number; endorsementDescription?: string; notes?: string }) => void
}) {
  const [effectiveDate, setEffectiveDate] = useState(toDateInput(policy.effectiveDate))
  const [premiumChange, setPremiumChange] = useState('0')
  const [description, setDescription] = useState('')
  const [notes, setNotes] = useState('')

  function submit(event: React.FormEvent) {
    event.preventDefault()
    onSave({
      effectiveDate,
      premiumChange: Number(premiumChange || 0),
      endorsementDescription: description.trim() || undefined,
      notes: notes.trim() || undefined,
    })
  }

  return (
    <ActionModal title="Add Endorsement" onClose={onClose}>
      <form onSubmit={submit} className="space-y-4">
        <Field label="Effective Date">
          <input type="date" required value={effectiveDate} onChange={(e) => setEffectiveDate(e.target.value)} className={inputClass} />
        </Field>
        <Field label="Premium Change">
          <input type="number" step="0.01" value={premiumChange} onChange={(e) => setPremiumChange(e.target.value)} className={inputClass} />
        </Field>
        <Field label="Description">
          <textarea rows={3} value={description} onChange={(e) => setDescription(e.target.value)} className={textareaClass} />
        </Field>
        <Field label="Notes">
          <textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} className={textareaClass} />
        </Field>
        <ModalActions saving={saving} onClose={onClose} submitLabel="Add Endorsement" />
      </form>
    </ActionModal>
  )
}

function ReinstatePolicyModal({
  policy,
  saving,
  onClose,
  onSave,
}: {
  policy: Policy
  saving: boolean
  onClose: () => void
  onSave: (data: ReinstatePolicy) => void
}) {
  const [reinstatedDate, setReinstatedDate] = useState(toDateInput(new Date().toISOString()))
  const [reason, setReason] = useState('')
  const [notes, setNotes] = useState('')

  function submit(event: React.FormEvent) {
    event.preventDefault()
    onSave({
      reinstatedDate,
      reason: reason.trim(),
      notes: notes.trim() || undefined,
    })
  }

  return (
    <ActionModal title="Reinstate Policy" onClose={onClose}>
      <form onSubmit={submit} className="space-y-4">
        <Field label="Reinstatement Date">
          <input type="date" required value={reinstatedDate} onChange={(e) => setReinstatedDate(e.target.value)} className={inputClass} />
        </Field>
        <Field label="Reason">
          <textarea rows={3} required value={reason} onChange={(e) => setReason(e.target.value)} className={textareaClass} />
        </Field>
        <Field label="Notes">
          <textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} className={textareaClass} />
        </Field>
        <div className="rounded-lg border p-3 text-sm" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
          <div className="font-semibold" style={{ color: 'var(--ink)' }}>Reinstatement Preview</div>
          <div className="mt-2 grid gap-1 text-xs" style={{ color: 'var(--ink-2)' }}>
            <div><span className="font-medium">Policy:</span> {policy.policyNumber}</div>
            <div><span className="font-medium">Current status:</span> {POLICY_STATUS_LABELS[policy.status]}</div>
            <div><span className="font-medium">Reinstatement date:</span> {formatDate(reinstatedDate)}</div>
          </div>
        </div>
        <ModalActions saving={saving} disabled={!reason.trim()} onClose={onClose} submitLabel="Reinstate" />
      </form>
    </ActionModal>
  )
}

function StartRewritePolicyModal({
  policy,
  saving,
  onClose,
  onSave,
}: {
  policy: Policy
  saving: boolean
  onClose: () => void
  onSave: (data: StartRewritePolicy) => void
}) {
  const [effectiveDate, setEffectiveDate] = useState(toDateInput(new Date().toISOString()))
  const [expirationDate, setExpirationDate] = useState(toDateInput(policy.expirationDate))
  const [reason, setReason] = useState('')
  const [notes, setNotes] = useState('')

  function submit(event: React.FormEvent) {
    event.preventDefault()
    onSave({
      effectiveDate,
      expirationDate,
      reason: reason.trim(),
      notes: notes.trim() || undefined,
    })
  }

  return (
    <ActionModal title="Start Rewrite" onClose={onClose}>
      <form onSubmit={submit} className="space-y-4">
        <Field label="Rewrite Effective Date">
          <input type="date" required value={effectiveDate} onChange={(e) => setEffectiveDate(e.target.value)} className={inputClass} />
        </Field>
        <Field label="Replacement Expiration Date">
          <input type="date" required value={expirationDate} onChange={(e) => setExpirationDate(e.target.value)} className={inputClass} />
        </Field>
        <Field label="Reason">
          <textarea rows={3} required value={reason} onChange={(e) => setReason(e.target.value)} className={textareaClass} />
        </Field>
        <Field label="Notes">
          <textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} className={textareaClass} />
        </Field>
        <div className="rounded-lg border p-3 text-sm" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
          <div className="font-semibold" style={{ color: 'var(--ink)' }}>Rewrite Preview</div>
          <div className="mt-2 grid gap-1 text-xs" style={{ color: 'var(--ink-2)' }}>
            <div><span className="font-medium">Source policy:</span> {policy.policyNumber}</div>
            <div><span className="font-medium">Current term:</span> {formatDate(policy.effectiveDate)} - {formatDate(policy.expirationDate)}</div>
            <div><span className="font-medium">Replacement term:</span> {formatDate(effectiveDate)} - {formatDate(expirationDate)}</div>
          </div>
        </div>
        <ModalActions saving={saving} disabled={!reason.trim()} onClose={onClose} submitLabel="Start Rewrite" />
      </form>
    </ActionModal>
  )
}

function CancelPolicyModal({
  policy,
  guidance,
  reasons,
  templates,
  saving,
  onClose,
  onSave,
}: {
  policy: Policy
  guidance?: LegalComplianceGuidance
  reasons: CancellationReason[]
  templates: DocumentTemplateListItem[]
  saving: boolean
  onClose: () => void
  onSave: (data: IssueCancellationNotice) => void
}) {
  const [reasonCode, setReasonCode] = useState('')
  const [reasonInputs, setReasonInputs] = useState<Record<string, string>>({})
  const [noticeMailingDate, setNoticeMailingDate] = useState(toDateInput(new Date().toISOString()))
  const [noticeRequirementDays, setNoticeRequirementDays] = useState('10')
  const [mailingDays, setMailingDays] = useState('0')
  const [method, setMethod] = useState('Written Notice')
  const cancellationTemplates = templates.filter((template) => /cancel/i.test(template.name))
  const [noticeTemplateId, setNoticeTemplateId] = useState('')
  const [notes, setNotes] = useState('')
  const selectedReason = reasons.find((reason) => reason.code === reasonCode)
  const calculatedCancellationDate = addDaysToDateInput(
    noticeMailingDate,
    Number(noticeRequirementDays || 0) + Number(mailingDays || 0)
  )
  const resolvedReason = selectedReason ? resolveReasonPreview(selectedReason, reasonInputs) : ''
  const requiredInputsComplete = selectedReason
    ? selectedReason.requiredInputTokens.every((token) => reasonInputs[token]?.trim())
    : false

  useEffect(() => {
    if (reasons.length > 0 && !reasonCode) {
      const first = reasons[0]
      setReasonCode(first.code)
      setNoticeRequirementDays(String(first.defaultNoticeRequirementDays))
      setReasonInputs({})
    }
  }, [reasonCode, reasons])

  function submit(event: React.FormEvent) {
    event.preventDefault()
    if (!selectedReason) return
    onSave({
      reasonCode: selectedReason.code,
      reasonInputs,
      noticeMailingDate,
      noticeRequirementDays: Number(noticeRequirementDays),
      mailingDays: Number(mailingDays || 0),
      method,
      noticeTemplateId: noticeTemplateId || undefined,
      notes: notes.trim() || undefined,
    })
  }

  return (
    <ActionModal title="Issue Cancellation Notice" onClose={onClose} wide>
      <form onSubmit={submit} className="grid grid-cols-1 gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(320px,420px)]">
        <div className="space-y-4">
          <Field label="Reason">
            <select
              value={reasonCode}
              onChange={(e) => {
                const nextReason = reasons.find((reason) => reason.code === e.target.value)
                setReasonCode(e.target.value)
                setReasonInputs({})
                if (nextReason) setNoticeRequirementDays(String(nextReason.defaultNoticeRequirementDays))
              }}
              className={selectClass}
              required
            >
              {groupCancellationReasons(reasons).map((group) => (
                <optgroup key={group.category} label={group.category}>
                  {group.reasons.map((reason) => (
                    <option key={reason.code} value={reason.code}>
                      {reason.code} - {reason.label}
                    </option>
                  ))}
                </optgroup>
              ))}
            </select>
          </Field>
          {selectedReason?.requiredInputTokens.map((token) => (
            <Field key={token} label={formatReasonTokenLabel(token)}>
              <textarea
                rows={token.startsWith('DESCRIBE_') ? 3 : 1}
                required
                value={reasonInputs[token] ?? ''}
                onChange={(e) => setReasonInputs((current) => ({ ...current, [token]: e.target.value }))}
                className={textareaClass}
              />
            </Field>
          ))}
          <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
            <Field label="Notice Mailing Date">
              <input type="date" required value={noticeMailingDate} onChange={(e) => setNoticeMailingDate(e.target.value)} className={inputClass} />
            </Field>
            <Field label="Notice Days">
              <input type="number" min={1} required value={noticeRequirementDays} onChange={(e) => setNoticeRequirementDays(e.target.value)} className={inputClass} />
            </Field>
            <Field label="Mailing Days">
              <input type="number" min={0} required value={mailingDays} onChange={(e) => setMailingDays(e.target.value)} className={inputClass} />
            </Field>
          </div>
          <Field label="Calculated Cancellation Date">
            <input type="date" readOnly value={calculatedCancellationDate} className={inputClass} />
          </Field>
          <Field label="Notice Method">
            <select value={method} onChange={(e) => setMethod(e.target.value)} className={selectClass}>
              <option>Written Notice</option>
              <option>Certified Mail</option>
              <option>First-Class Mail</option>
              <option>Electronic Notice</option>
              <option>Carrier Issued</option>
            </select>
          </Field>
          <Field label="Notice Template">
            <select value={noticeTemplateId} onChange={(e) => setNoticeTemplateId(e.target.value)} className={selectClass}>
              <option value="">Use default cancellation template</option>
              {cancellationTemplates.map((template) => (
                <option key={template.id} value={template.id}>{template.name}</option>
              ))}
            </select>
          </Field>
          <Field label="Notes">
            <textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} className={textareaClass} />
          </Field>
          <div className="rounded-lg border p-3 text-sm" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
            <div className="font-semibold" style={{ color: 'var(--ink)' }}>Notice Preview</div>
            <div className="mt-2 grid gap-1 text-xs" style={{ color: 'var(--ink-2)' }}>
              <div><span className="font-medium">Reason:</span> {selectedReason ? `${selectedReason.code} - ${selectedReason.label}` : '-'}</div>
              <div><span className="font-medium">Mailing date:</span> {formatDate(noticeMailingDate)}</div>
              <div><span className="font-medium">Notice days:</span> {noticeRequirementDays || '0'} + {mailingDays || '0'} mailing days</div>
              <div><span className="font-medium">Cancellation date:</span> {formatDate(calculatedCancellationDate)}</div>
              <div><span className="font-medium">Template:</span> {cancellationTemplates.find((template) => template.id === noticeTemplateId)?.name ?? 'Default cancellation template'}</div>
            </div>
            {selectedReason?.requiresSpecialHandling && (
              <p className="mt-2 text-xs font-medium" style={{ color: 'var(--warn-fg)' }}>This reason requires special procedural review before use.</p>
            )}
            {resolvedReason && <p className="mt-3 text-xs" style={{ color: 'var(--ink-2)' }}>{resolvedReason}</p>}
          </div>
          <ModalActions saving={saving} disabled={!selectedReason || !requiredInputsComplete} onClose={onClose} submitLabel="Issue Notice" danger />
        </div>
        <LegalGuidancePanel guidance={guidance} mode="Cancellation" />
      </form>
    </ActionModal>
  )
}

function MarkNonRenewalModal({
  policy,
  saving,
  onClose,
  onSave,
}: {
  policy: Policy
  saving: boolean
  onClose: () => void
  onSave: (data: MarkNonRenewal) => void
}) {
  const [nonRenewedDate, setNonRenewedDate] = useState(toDateInput(policy.expirationDate))
  const [reason, setReason] = useState('')
  const [notes, setNotes] = useState('')

  function submit(event: React.FormEvent) {
    event.preventDefault()
    onSave({
      nonRenewedDate,
      reason: reason.trim(),
      notes: notes.trim() || undefined,
    })
  }

  return (
    <ActionModal title="Mark for Non-Renewal" onClose={onClose}>
      <form onSubmit={submit} className="space-y-4">
        <Field label="Non-Renewal Effective Date">
          <input type="date" required value={nonRenewedDate} onChange={(e) => setNonRenewedDate(e.target.value)} className={inputClass} />
        </Field>
        <Field label="Reason">
          <textarea required rows={5} value={reason} onChange={(e) => setReason(e.target.value)} className={textareaClass} />
        </Field>
        <Field label="Notes">
          <textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} className={textareaClass} />
        </Field>
        <ModalActions saving={saving} disabled={!reason.trim()} onClose={onClose} submitLabel="Mark" />
      </form>
    </ActionModal>
  )
}

function NonRenewPolicyModal({
  policy,
  guidance,
  templates,
  saving,
  onClose,
  onSave,
}: {
  policy: Policy
  guidance?: LegalComplianceGuidance
  templates: DocumentTemplateListItem[]
  saving: boolean
  onClose: () => void
  onSave: (data: NonRenewPolicy) => void
}) {
  const [nonRenewedDate, setNonRenewedDate] = useState(toDateInput(policy.expirationDate))
  const [noticeMailingDate, setNoticeMailingDate] = useState(toDateInput(new Date().toISOString()))
  const [noticeRequirementDays, setNoticeRequirementDays] = useState('45')
  const [mailingDays, setMailingDays] = useState('0')
  const [method, setMethod] = useState('Written Notice')
  const nonRenewalTemplates = templates.filter((template) => /non.?renew/i.test(template.name))
  const [noticeTemplateId, setNoticeTemplateId] = useState('')
  const [reason, setReason] = useState('')
  const [checklist, setChecklist] = useState<CancellationComplianceChecklistItem[]>(() => buildNonRenewalChecklist(guidance))
  const calculatedNonRenewalDate = addDaysToDateInput(
    noticeMailingDate,
    Number(noticeRequirementDays || 0) + Number(mailingDays || 0)
  )
  const checklistComplete = checklist.every((item) => item.isCompleted)

  useEffect(() => {
    setChecklist(buildNonRenewalChecklist(guidance))
  }, [guidance])

  function submit(event: React.FormEvent) {
    event.preventDefault()
    onSave({
      nonRenewedDate,
      reason: reason.trim() || undefined,
      noticeMailingDate,
      noticeRequirementDays: Number(noticeRequirementDays),
      mailingDays: Number(mailingDays || 0),
      method,
      noticeTemplateId: noticeTemplateId || undefined,
      complianceChecklist: checklist,
      legalRequirementSectionIds: uniqueIds(checklist.flatMap((item) => item.requirementSectionIds)),
    })
  }

  return (
    <ActionModal title="Issue Non-Renewal Notice" onClose={onClose} wide>
      <form onSubmit={submit} className="grid grid-cols-1 gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(320px,420px)]">
        <div className="space-y-4">
          <Field label="Non-Renewal Effective Date">
            <input type="date" required value={nonRenewedDate} onChange={(e) => setNonRenewedDate(e.target.value)} className={inputClass} />
          </Field>
          <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
            <Field label="Notice Mailing Date">
              <input type="date" required value={noticeMailingDate} onChange={(e) => setNoticeMailingDate(e.target.value)} className={inputClass} />
            </Field>
            <Field label="Notice Days">
              <input type="number" min={1} required value={noticeRequirementDays} onChange={(e) => setNoticeRequirementDays(e.target.value)} className={inputClass} />
            </Field>
            <Field label="Mailing Days">
              <input type="number" min={0} required value={mailingDays} onChange={(e) => setMailingDays(e.target.value)} className={inputClass} />
            </Field>
          </div>
          <Field label="Calculated Notice Date">
            <input type="date" readOnly value={calculatedNonRenewalDate} className={inputClass} />
          </Field>
          <Field label="Notice Method">
            <select value={method} onChange={(e) => setMethod(e.target.value)} className={selectClass}>
              <option>Written Notice</option>
              <option>Certified Mail</option>
              <option>First-Class Mail</option>
              <option>Electronic Notice</option>
              <option>Carrier Issued</option>
            </select>
          </Field>
          <Field label="Notice Template">
            <select value={noticeTemplateId} onChange={(e) => setNoticeTemplateId(e.target.value)} className={selectClass}>
              <option value="">Use default non-renewal template</option>
              {nonRenewalTemplates.map((template) => (
                <option key={template.id} value={template.id}>{template.name}</option>
              ))}
            </select>
          </Field>
          <Field label="Reason">
            <textarea rows={5} value={reason} onChange={(e) => setReason(e.target.value)} className={textareaClass} />
          </Field>
          <div className="rounded-lg border p-3 text-sm" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
            <div className="font-semibold" style={{ color: 'var(--ink)' }}>Notice Preview</div>
            <div className="mt-2 grid gap-1 text-xs" style={{ color: 'var(--ink-2)' }}>
              <div><span className="font-medium">Mailing date:</span> {formatDate(noticeMailingDate)}</div>
              <div><span className="font-medium">Notice days:</span> {noticeRequirementDays || '0'} + {mailingDays || '0'} mailing days</div>
              <div><span className="font-medium">Calculated date:</span> {formatDate(calculatedNonRenewalDate)}</div>
              <div><span className="font-medium">Non-renewal date:</span> {formatDate(nonRenewedDate)}</div>
              <div><span className="font-medium">Template:</span> {nonRenewalTemplates.find((template) => template.id === noticeTemplateId)?.name ?? 'Default non-renewal template'}</div>
            </div>
          </div>
          <ComplianceChecklist items={checklist} onChange={setChecklist} purpose="non-renewal transaction" />
          <ModalActions saving={saving} disabled={!checklistComplete} onClose={onClose} submitLabel="Issue Notice" />
        </div>
        <LegalGuidancePanel guidance={guidance} mode="Non-renewal" />
      </form>
    </ActionModal>
  )
}

function LegalGuidancePanel({ guidance, mode }: { guidance?: LegalComplianceGuidance; mode: string }) {
  const groups = guidance ? [
    { label: 'Notice Period', rows: guidance.noticeRequirements },
    { label: 'Allowed / Prohibited Reasons', rows: guidance.reasonRequirements },
    { label: 'Proof of Notice', rows: guidance.proofOfNoticeRequirements },
    { label: 'Lienholder / Mortgagee Notice', rows: guidance.lienholderRequirements },
    { label: 'State Authority Reporting', rows: guidance.stateAuthorityRequirements },
    { label: 'Return Premium', rows: guidance.returnPremiumRequirements },
  ].filter((group) => group.rows.length > 0) : []

  return (
    <aside className="rounded-lg border p-4" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
      <div className="flex items-start gap-2">
        <AlertTriangle className="mt-0.5 h-4 w-4" style={{ color: 'var(--warn-fg)' }} />
        <div>
          <h3 className="text-sm font-semibold" style={{ color: 'var(--ink)' }}>Legal Guidance</h3>
          <p className="mt-1 text-xs" style={{ color: 'var(--ink-3)' }}>
            {guidance ? `${mode}: ${guidance.state} ${guidance.lineOfBusiness}` : 'No matching guidance loaded.'}
          </p>
        </div>
      </div>
      <div className="mt-4 max-h-[520px] space-y-3 overflow-auto pr-1">
        {groups.length === 0 ? (
          <p className="text-sm" style={{ color: 'var(--ink-3)' }}>No cancellation requirements were found for this policy state.</p>
        ) : groups.map((group) => (
          <section key={group.label} className="rounded-lg border p-3" style={{ borderColor: 'var(--line)', background: 'var(--surface)' }}>
            <div className="text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--ink-3)' }}>{group.label}</div>
            <div className="mt-2 space-y-3">
              {group.rows.map((row) => (
                <LegalRequirementSummary key={row.id} row={row} />
              ))}
            </div>
          </section>
        ))}
      </div>
    </aside>
  )
}

function LegalRequirementSummary({ row }: { row: LegalComplianceRequirement }) {
  return (
    <div>
      <div className="text-sm font-semibold" style={{ color: 'var(--ink)' }}>{row.topic}</div>
      <p className="mt-1 text-sm leading-6" style={{ color: 'var(--ink-2)' }}>{truncateText(row.requirementText, 520)}</p>
      {row.citations.length > 0 && (
        <div className="mt-2 flex flex-wrap gap-1">
          {row.citations.map((citation) => (
            <span key={citation} className="rounded px-2 py-0.5 text-[11px]" style={{ background: 'var(--surface-2)', color: 'var(--ink-3)' }}>{citation}</span>
          ))}
        </div>
      )}
    </div>
  )
}

function ComplianceChecklist({
  items,
  onChange,
  purpose = 'cancellation transaction',
}: {
  items: CancellationComplianceChecklistItem[]
  onChange: (items: CancellationComplianceChecklistItem[]) => void
  purpose?: string
}) {
  return (
    <section className="rounded-lg border p-4" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
      <h3 className="text-sm font-semibold" style={{ color: 'var(--ink)' }}>Compliance Review</h3>
      <div className="mt-3 space-y-2">
        {items.map((item) => (
          <label key={item.key} className="flex items-start gap-2 text-sm" style={{ color: 'var(--ink-2)' }}>
            <input
              type="checkbox"
              checked={item.isCompleted}
              onChange={(event) => onChange(items.map((existing) =>
                existing.key === item.key ? { ...existing, isCompleted: event.target.checked } : existing
              ))}
              className="mt-1"
            />
            <span>{item.label}</span>
          </label>
        ))}
      </div>
      <p className="mt-3 text-xs" style={{ color: 'var(--ink-3)' }}>These selections are saved with the {purpose}.</p>
    </section>
  )
}

function ActionModal({ title, onClose, children, wide = false }: { title: string; onClose: () => void; children: React.ReactNode; wide?: boolean }) {
  return (
    <div className="sims-modal-backdrop">
      <div className={`sims-modal max-h-[92vh] overflow-auto ${wide ? 'max-w-6xl' : 'max-w-xl'}`}>
        <div className="sims-modal-head">
          <h2 className="sims-modal-title">{title}</h2>
          <button type="button" onClick={onClose} className="sims-icon-btn" aria-label="Close">
            <X className="h-4 w-4" />
          </button>
        </div>
        <div className="sims-modal-body">{children}</div>
      </div>
    </div>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="sims-field-label">
      {label}
      <div className="mt-1">{children}</div>
    </label>
  )
}

function ModalActions({ saving, disabled = false, onClose, submitLabel, danger = false }: { saving: boolean; disabled?: boolean; onClose: () => void; submitLabel: string; danger?: boolean }) {
  return (
    <div className="sims-modal-foot -mx-5 -mb-5 mt-4">
      <button type="button" onClick={onClose} className="sd-btn outline">
        Close
      </button>
      <button
        type="submit"
        disabled={saving || disabled}
        className={`sd-btn ${danger ? 'danger' : 'primary'}`}
      >
        {saving ? 'Saving...' : submitLabel}
      </button>
    </div>
  )
}

const inputClass = 'sims-input'
const selectClass = 'sims-select'
const textareaClass = 'sims-textarea'

function toDateInput(value: string) {
  return value.slice(0, 10)
}

function addDaysToDateInput(value: string, days: number) {
  if (!value) return ''
  const date = new Date(`${value.slice(0, 10)}T00:00:00Z`)
  if (Number.isNaN(date.getTime())) return ''
  date.setUTCDate(date.getUTCDate() + days)
  return date.toISOString().slice(0, 10)
}

function groupCancellationReasons(reasons: CancellationReason[]) {
  const groups = new Map<string, CancellationReason[]>()
  for (const reason of reasons) {
    groups.set(reason.category, [...(groups.get(reason.category) ?? []), reason])
  }
  return Array.from(groups.entries()).map(([category, groupReasons]) => ({ category, reasons: groupReasons }))
}

function formatReasonTokenLabel(token: string) {
  return token
    .toLowerCase()
    .split('_')
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ')
}

function resolveReasonPreview(reason: CancellationReason, inputs: Record<string, string>) {
  return reason.languageTemplate.replace(/\[([A-Z0-9_]+)\]/g, (match, token: string) => {
    const value = inputs[token]?.trim()
    return value || match
  })
}

function truncateText(value: string, maxLength: number) {
  return value.length > maxLength ? `${value.slice(0, maxLength)}...` : value
}

function parseLegalSnapshot(value: string | null): LegalRequirementSnapshot[] {
  if (!value) return []

  try {
    const parsed = JSON.parse(value)
    if (!Array.isArray(parsed)) return []

    return parsed.map((row) => ({
      id: row.id ?? row.Id ?? '',
      state: row.state ?? row.State ?? '',
      category: row.category ?? row.Category ?? '',
      topic: row.topic ?? row.Topic ?? '',
      requirementText: row.requirementText ?? row.RequirementText ?? '',
      citations: row.citations ?? row.Citations ?? [],
      lastVerifiedAt: row.lastVerifiedAt ?? row.LastVerifiedAt ?? '',
    })).filter((row) => row.id || row.topic)
  } catch {
    return []
  }
}

function buildCancellationChecklist(guidance?: LegalComplianceGuidance): CancellationComplianceChecklistItem[] {
  return [
    {
      key: 'reason-reviewed',
      label: 'Cancellation reason reviewed against allowed and prohibited reasons.',
      isCompleted: false,
      requirementSectionIds: ids(guidance?.reasonRequirements),
    },
    {
      key: 'notice-period-reviewed',
      label: 'Notice period reviewed for the selected cancellation effective date.',
      isCompleted: false,
      requirementSectionIds: ids(guidance?.noticeRequirements),
    },
    {
      key: 'proof-method-selected',
      label: 'Notice delivery/proof method selected and retained.',
      isCompleted: false,
      requirementSectionIds: ids(guidance?.proofOfNoticeRequirements),
    },
    {
      key: 'lienholder-state-authority-reviewed',
      label: 'Lienholder, mortgagee, and state authority notice requirements considered.',
      isCompleted: false,
      requirementSectionIds: ids([...(guidance?.lienholderRequirements ?? []), ...(guidance?.stateAuthorityRequirements ?? [])]),
    },
    {
      key: 'return-premium-reviewed',
      label: 'Return premium or unearned premium handling reviewed.',
      isCompleted: false,
      requirementSectionIds: ids(guidance?.returnPremiumRequirements),
    },
  ]
}

function buildNonRenewalChecklist(guidance?: LegalComplianceGuidance): CancellationComplianceChecklistItem[] {
  return [
    {
      key: 'non-renewal-reason-reviewed',
      label: 'Non-renewal reason reviewed against allowed and prohibited reasons.',
      isCompleted: false,
      requirementSectionIds: ids(guidance?.reasonRequirements),
    },
    {
      key: 'non-renewal-notice-period-reviewed',
      label: 'Notice period reviewed for the non-renewal effective date.',
      isCompleted: false,
      requirementSectionIds: ids(guidance?.noticeRequirements),
    },
    {
      key: 'non-renewal-proof-method-selected',
      label: 'Notice delivery/proof method selected and retained.',
      isCompleted: false,
      requirementSectionIds: ids(guidance?.proofOfNoticeRequirements),
    },
    {
      key: 'non-renewal-lienholder-state-authority-reviewed',
      label: 'Lienholder, mortgagee, and state authority notice requirements considered.',
      isCompleted: false,
      requirementSectionIds: ids([...(guidance?.lienholderRequirements ?? []), ...(guidance?.stateAuthorityRequirements ?? [])]),
    },
  ]
}

function ids(rows?: LegalComplianceRequirement[]) {
  return uniqueIds((rows ?? []).map((row) => row.id))
}

function uniqueIds(values: string[]) {
  return Array.from(new Set(values.filter(Boolean)))
}
