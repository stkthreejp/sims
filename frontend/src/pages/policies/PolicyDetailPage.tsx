import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, Ban, FileSignature, FileX2, Pin, PinOff, Pencil, Trash2, Plus, X, Check, FileText, Send } from 'lucide-react'
import { toast } from 'sonner'
import { policiesApi } from '@/api/policies.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { LOB_LABELS } from '@/types/quote.types'
import { POLICY_STATUS_LABELS, POLICY_TRANSACTION_STATUS_LABELS, POLICY_TRANSACTION_STATUS_PILL } from '@/types/policy.types'
import type { CancellationComplianceChecklistItem, LegalComplianceGuidance, LegalComplianceRequirement, LegalRequirementSnapshot, Policy, PolicyIssuancePacket, PolicyTransaction } from '@/types/policy.types'
import { DOCUMENT_TYPE_LABELS } from '@/types/attachment.types'
import { formatCurrency } from '@/lib/utils'
import type { Note } from '@/types/quote.types'
import { DocumentsSection } from '@/components/documents/DocumentsSection'
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
  const [actionModal, setActionModal] = useState<'endorse' | 'cancel' | 'nonRenew' | null>(null)

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

  const { canCreateNotes, canEditNotes, canDeleteNotes, canUploadAttachments, canDeleteAttachments, canIssuePolicies, canEndorsePolicies, canCancelPolicies, isAdmin } = usePermissions()

  const { data: issuancePacket } = useQuery({
    queryKey: ['policies', id, 'issuance-packet'],
    queryFn: () => policiesApi.getIssuancePacket(id!),
    enabled: !!id,
  })

  const { data: cancellationGuidance } = useQuery({
    queryKey: ['policies', id, 'cancellation-guidance'],
    queryFn: () => policiesApi.getCancellationGuidance(id!),
    enabled: !!id && canCancelPolicies,
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
    onError: () => toast.error('Endorsement could not be added'),
  })

  const cancelPolicyMutation = useMutation({
    mutationFn: (data: { cancelledDate: string; reason: string; method: string; premiumChange: number; complianceChecklist: CancellationComplianceChecklistItem[]; legalRequirementSectionIds: string[]; notes?: string }) =>
      policiesApi.cancel(id!, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', id] })
      setActionModal(null)
      toast.success('Policy cancelled')
    },
    onError: () => toast.error('Policy could not be cancelled'),
  })

  const nonRenewMutation = useMutation({
    mutationFn: (data: { nonRenewedDate: string; reason?: string }) => policiesApi.nonRenew(id!, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', id] })
      setActionModal(null)
      toast.success('Policy marked non-renewed')
    },
    onError: () => toast.error('Policy could not be non-renewed'),
  })

  const issuePolicyMutation = useMutation({
    mutationFn: () => policiesApi.issue(id!, { issuedDate: new Date().toISOString().slice(0, 10) }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['policies', id] })
      qc.invalidateQueries({ queryKey: ['policies', id, 'issuance-packet'] })
      qc.invalidateQueries({ queryKey: ['attachments', 'Policy', policy?.boundQuoteId] })
      toast.success('Policy issued and final packet filed')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Policy could not be issued'),
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
  if (!policy) return <p className="p-6 text-slate-500">Policy not found.</p>

  const sortedNotes = [...notes].sort((a, b) => {
    if (a.isPinned !== b.isPinned) return a.isPinned ? -1 : 1
    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  })
  const daysRemaining = Math.ceil((new Date(policy.expirationDate).getTime() - Date.now()) / 86400000)
  const activeStage = policy.status === 'Cancelled' || policy.status === 'NonRenewed' || policy.status === 'Expired'
    ? 3
    : daysRemaining <= 45 ? 2 : 1
  const canVoidTestBind = isAdmin && policy.status === 'Active' && !policy.issuedDate && policy.insuredName.toLowerCase().includes('test')

  return (
    <div className="space-y-5 p-6">
      <div className="flex items-center gap-2 text-sm" style={{ color: 'var(--ink-3)' }}>
        <Link to="/policies" className="hover:text-slate-900">Policies</Link>
        <span>/</span>
        <Link to={`/insureds/${policy.insuredId}`} className="hover:text-slate-900">{policy.insuredName}</Link>
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
          {policy.status === 'Active' && canEndorsePolicies && (
            <button
              onClick={() => setActionModal('endorse')}
              className="sd-btn primary"
            >
              <FileSignature className="h-3.5 w-3.5" /> Endorse
            </button>
          )}
          {policy.status === 'Active' && canCancelPolicies && (
            <>
              <button
                onClick={() => setActionModal('nonRenew')}
                className="sd-btn outline"
              >
                <FileX2 className="h-3.5 w-3.5" /> Non-Renew
              </button>
              <button
                onClick={() => setActionModal('cancel')}
                className="sd-btn danger"
              >
                <Ban className="h-3.5 w-3.5" /> Cancel
              </button>
            </>
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
          saving={cancelPolicyMutation.isPending}
          onClose={() => setActionModal(null)}
          onSave={(data) => cancelPolicyMutation.mutate(data)}
        />
      )}

      {actionModal === 'nonRenew' && (
        <NonRenewPolicyModal
          policy={policy}
          guidance={nonRenewalGuidance}
          saving={nonRenewMutation.isPending}
          onClose={() => setActionModal(null)}
          onSave={(data) => nonRenewMutation.mutate(data)}
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
          <p className="text-xs text-slate-500 mb-0.5">Line of Business</p>
          <p className="font-medium">{LOB_LABELS[policy.lineOfBusiness]}</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Carrier</p>
          <p className="font-medium">{policy.carrierName}</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Effective Date</p>
          <p className="font-medium">{new Date(policy.effectiveDate).toLocaleDateString()}</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Expiration Date</p>
          <p className="font-medium">{new Date(policy.expirationDate).toLocaleDateString()}</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Premium</p>
          <p className="font-medium">{formatCurrency(policy.premiumAmount)}</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Taxes & Fees</p>
          <p className="font-medium">{formatCurrency(policy.taxesAndFees)}</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Total Premium</p>
          <p className="font-medium">{formatCurrency(policy.totalPremium)}</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Agent Commission</p>
          <p className="font-medium">{formatCurrency(policy.agentCommissionAmount)} ({(policy.agentCommissionRate * 100).toFixed(1)}%)</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Carrier Commission</p>
          <p className="font-medium">{formatCurrency(policy.carrierCommissionAmount)} ({(policy.carrierCommissionRate * 100).toFixed(1)}%)</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">SMM Retention</p>
          <p className="font-medium">{formatCurrency(policy.smmRetentionAmount)} ({(policy.smmRetentionRate * 100).toFixed(1)}%)</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Bound Date</p>
          <p className="font-medium">{new Date(policy.boundDate).toLocaleDateString()}</p>
        </div>
        {policy.issuedDate && (
          <div>
            <p className="text-xs text-slate-500 mb-0.5">Issued Date</p>
            <p className="font-medium">{new Date(policy.issuedDate).toLocaleDateString()}</p>
          </div>
        )}
        {policy.nonRenewedDate && (
          <div>
            <p className="text-xs text-slate-500 mb-0.5">Non-Renewed Date</p>
            <p className="font-medium">{new Date(policy.nonRenewedDate).toLocaleDateString()}</p>
          </div>
        )}
        {policy.cancelledDate && (
          <div>
            <p className="text-xs text-slate-500 mb-0.5">Cancelled Date</p>
            <p className="font-medium">{new Date(policy.cancelledDate).toLocaleDateString()}</p>
          </div>
        )}
        {policy.limit != null && (
          <div>
            <p className="text-xs text-slate-500 mb-0.5">Limit</p>
            <p className="font-medium">{formatCurrency(policy.limit)}</p>
          </div>
        )}
        {policy.deductible != null && (
          <div>
            <p className="text-xs text-slate-500 mb-0.5">Deductible</p>
            <p className="font-medium">{formatCurrency(policy.deductible)}</p>
          </div>
        )}
        {policy.coverageDescription && (
          <div className="col-span-2 md:col-span-4">
            <p className="text-xs text-slate-500 mb-0.5">Coverage Description</p>
            <p className="text-slate-700">{policy.coverageDescription}</p>
          </div>
        )}
      </div>
      </div>

      <PolicyIssuancePanel
        packet={issuancePacket}
        canIssue={canIssuePolicies && policy.status === 'Active' && !policy.issuedDate}
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
                <th className="px-5 py-2 text-left text-xs font-semibold text-slate-500 uppercase">Txn #</th>
                <th className="px-5 py-2 text-left text-xs font-semibold text-slate-500 uppercase">Type</th>
                <th className="px-5 py-2 text-left text-xs font-semibold text-slate-500 uppercase">Status</th>
                <th className="px-5 py-2 text-left text-xs font-semibold text-slate-500 uppercase">Effective</th>
                <th className="px-5 py-2 text-right text-xs font-semibold text-slate-500 uppercase">Premium Δ</th>
                <th className="px-5 py-2 text-right text-xs font-semibold text-slate-500 uppercase">New Total</th>
                <th className="px-5 py-2 text-left text-xs font-semibold text-slate-500 uppercase">Processed By</th>
              </tr>
            </thead>
            <tbody>
              {policy.transactions.map((t) => (
                <TransactionRows key={t.id} transaction={t} />
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
                              ? <PinOff className="h-3.5 w-3.5 text-yellow-500" />
                              : <Pin className="h-3.5 w-3.5 text-slate-400" />}
                          </button>
                        )}
                        {canEditNotes && (
                          <button onClick={() => { setEditingNote(note); setEditSubject(note.subject ?? ''); setEditBody(note.body) }} className="sims-icon-btn">
                            <Pencil className="h-3.5 w-3.5 text-slate-400" />
                          </button>
                        )}
                        {canDeleteNotes && (
                          <button onClick={() => { if (confirm('Delete note?')) deleteNoteMutation.mutate(note.id) }} className="sims-icon-btn hover:text-red-500">
                            <Trash2 className="h-3.5 w-3.5 text-slate-400 hover:text-red-500" />
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

function PolicyIssuancePanel({
  packet,
  canIssue,
  issuing,
  previewing,
  onPreview,
  onIssue,
}: {
  packet?: PolicyIssuancePacket
  canIssue: boolean
  issuing: boolean
  previewing: boolean
  onPreview: () => void
  onIssue: () => void
}) {
  const includedForms = packet?.forms.filter((form) => form.isIncluded) ?? []
  const excludedForms = packet?.forms.filter((form) => !form.isIncluded) ?? []
  const ready = includedForms.length > 0 && (packet?.isReady ?? false)
  const issued = packet?.isIssued
  const actionBlockedReason = !canIssue
    ? 'You do not have permission to issue policies.'
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
                : ready
                ? `${includedForms.length} form${includedForms.length === 1 ? '' : 's'} ready. Preview, then issue.`
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
              disabled={!canIssue || !ready || previewing}
              onClick={onPreview}
              className="sd-btn outline"
              title={actionBlockedReason ?? 'Generate a draft packet PDF for review'}
            >
              <FileText className="h-3.5 w-3.5" /> {previewing ? 'Generating...' : 'Preview packet'}
            </button>
            <button
              disabled={!canIssue || !ready || issuing}
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
            {!packet.isReady && packet.readinessMessages.length > 0 && (
              <div className="mb-3 rounded border px-3 py-3 text-sm" style={{ background: 'var(--warn-bg)', borderColor: '#f5d7a3', color: 'var(--warn-fg)' }}>
                {packet.readinessMessages[0]}
              </div>
            )}
            {ready && !issued && (
              <div className="mb-3 rounded border px-3 py-3 text-sm" style={{ background: '#f0fdf4', borderColor: '#bbf7d0', color: '#166534' }}>
                Preview creates a draft PDF for review. Issue policy creates and files the final issued packet.
              </div>
            )}
            <div className="overflow-hidden rounded-lg border" style={{ borderColor: 'var(--line)' }}>
              {includedForms.map((form) => (
                <div key={form.id} className="flex items-center gap-3 border-b px-3 py-2.5 text-sm last:border-b-0" style={{ borderColor: 'var(--line-2)' }}>
                  <span className="w-8 text-right font-mono text-xs font-semibold text-slate-400">{String(form.sequenceOrder).padStart(2, '0')}</span>
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
          <p className="mt-3 text-xs text-slate-500">
            {excludedForms.length} form{excludedForms.length === 1 ? '' : 's'} excluded from this packet.
          </p>
        )}
      </div>
    </div>
  )
}

function ReadinessIcon({ status }: { status: 'Ready' | 'Warning' | 'Blocked' }) {
  if (status === 'Ready') {
    return <Check className="h-4 w-4 shrink-0 text-green-700" />
  }

  return (
    <AlertTriangle
      className="h-4 w-4 shrink-0"
      style={{ color: status === 'Blocked' ? 'var(--bad-fg)' : 'var(--warn-fg)' }}
    />
  )
}

function TransactionRows({ transaction: t }: { transaction: PolicyTransaction }) {
  const { data: artifacts } = useQuery({
    queryKey: ['policies', t.policyId, 'transactions', t.id, 'artifacts'],
    queryFn: () => policiesApi.getTransactionArtifacts(t.policyId, t.id),
  })

  return (
    <>
      <tr>
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
        <td>{t.processedByName}</td>
      </tr>
      {t.transactionType === 'Cancellation' && (
        <CancellationTransactionDetails transaction={t} />
      )}
      {(t.priorVersion || t.resultingVersion) && (
        <VersionChangeDetails transaction={t} />
      )}
      {artifacts && (artifacts.documents.length > 0 || artifacts.invoices.length > 0) && (
        <TransactionArtifactDetails artifacts={artifacts} />
      )}
    </>
  )
}

function TransactionArtifactDetails({ artifacts }: { artifacts: Awaited<ReturnType<typeof policiesApi.getTransactionArtifacts>> }) {
  return (
    <tr>
      <td colSpan={7} className="px-5 pb-4">
        <div className="grid gap-3 rounded border bg-white p-3 text-sm md:grid-cols-2">
          <div>
            <div className="mb-2 text-xs font-semibold uppercase text-slate-500">Documents</div>
            {artifacts.documents.length === 0 ? (
              <div className="text-slate-500">No linked documents</div>
            ) : (
              <div className="space-y-2">
                {artifacts.documents.map((doc) => (
                  <div key={doc.id} className="flex items-center justify-between gap-3">
                    <div className="min-w-0">
                      <div className="truncate font-medium text-slate-800">{doc.fileName}</div>
                      <div className="text-xs text-slate-500">
                        {DOCUMENT_TYPE_LABELS[doc.documentType]}{doc.policyVersionNumber != null ? ` · v${doc.policyVersionNumber}` : ''}
                      </div>
                    </div>
                    <span className="shrink-0 text-xs text-slate-400">{formatDate(doc.createdAt)}</span>
                  </div>
                ))}
              </div>
            )}
          </div>
          <div>
            <div className="mb-2 text-xs font-semibold uppercase text-slate-500">Invoices</div>
            {artifacts.invoices.length === 0 ? (
              <div className="text-slate-500">No linked invoices</div>
            ) : (
              <div className="space-y-2">
                {artifacts.invoices.map((invoice) => (
                  <div key={invoice.id} className="flex items-center justify-between gap-3">
                    <div>
                      <div className="font-medium text-slate-800">{invoice.invoiceNumber}</div>
                      <div className="text-xs text-slate-500">{invoice.status} · {formatDate(invoice.invoiceDate)}</div>
                    </div>
                    <span className="font-mono text-slate-700">{formatCurrency(invoice.totalAmount)}</span>
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

function VersionChangeDetails({ transaction }: { transaction: PolicyTransaction }) {
  const prior = transaction.priorVersion
  const resulting = transaction.resultingVersion

  return (
    <tr>
      <td colSpan={7} className="px-5 pb-4">
        <div className="grid gap-3 rounded border bg-slate-50/70 p-3 text-sm sm:grid-cols-[1fr_auto_1fr]">
          <VersionSummary label="Before" version={prior} />
          <div className="hidden items-center justify-center text-slate-400 sm:flex">-&gt;</div>
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
        <div className="text-xs font-semibold uppercase text-slate-500">{label}</div>
        <div className="mt-1 text-slate-500">No version snapshot</div>
      </div>
    )
  }

  return (
    <div>
      <div className="flex items-center gap-2">
        <span className="text-xs font-semibold uppercase text-slate-500">{label}</span>
        <span className="rounded bg-white px-1.5 py-0.5 text-xs font-medium text-slate-700">v{version.versionNumber}</span>
        <span className={`sd-pill ${POLICY_STATUS_PILL[version.status]}`}>{POLICY_STATUS_LABELS[version.status]}</span>
      </div>
      <div className="mt-2 grid gap-2 text-slate-700 sm:grid-cols-3">
        <div>
          <div className="text-xs text-slate-500">Term</div>
          <div>{formatDate(version.effectiveDate)} - {formatDate(version.expirationDate)}</div>
        </div>
        <div>
          <div className="text-xs text-slate-500">Premium</div>
          <div>{formatCurrency(version.premiumAmount)}</div>
        </div>
        <div>
          <div className="text-xs text-slate-500">Total</div>
          <div className="font-medium">{formatCurrency(version.totalPremium)}</div>
        </div>
      </div>
    </div>
  )
}

function CancellationTransactionDetails({ transaction }: { transaction: PolicyTransaction }) {
  const legalSnapshot = parseLegalSnapshot(transaction.cancellationLegalRequirementSnapshotJson)

  return (
    <tr className="bg-red-50/40">
      <td colSpan={7} className="px-5 py-4">
        <div className="grid gap-4 text-sm md:grid-cols-[minmax(0,1fr)_minmax(280px,380px)]">
          <div>
            <div className="font-semibold text-slate-900">Cancellation Review</div>
            <div className="mt-2 grid gap-2 text-slate-700 sm:grid-cols-2">
              <div><span className="text-slate-500">Reason:</span> {transaction.cancellationReason || 'Not recorded'}</div>
              <div><span className="text-slate-500">Method:</span> {transaction.cancellationMethod || 'Not recorded'}</div>
            </div>
            {transaction.notes && <p className="mt-2 text-slate-600">{transaction.notes}</p>}
            <div className="mt-3 space-y-1">
              {transaction.cancellationComplianceChecklist.length === 0 ? (
                <p className="text-slate-500">No checklist was saved with this transaction.</p>
              ) : transaction.cancellationComplianceChecklist.map((item) => (
                <div key={item.key} className="flex items-start gap-2 text-slate-700">
                  <span className={item.isCompleted ? 'text-green-700' : 'text-slate-400'}>{item.isCompleted ? '[x]' : '[ ]'}</span>
                  <span>{item.label}</span>
                </div>
              ))}
            </div>
          </div>
          <div className="rounded border bg-white p-3">
            <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">Saved Legal Snapshot</div>
            {legalSnapshot.length === 0 ? (
              <p className="mt-2 text-sm text-slate-500">No legal requirement snapshot was saved.</p>
            ) : (
              <div className="mt-2 max-h-52 space-y-2 overflow-auto pr-1">
                {legalSnapshot.map((row) => (
                  <div key={row.id} className="text-sm">
                    <div className="font-medium text-slate-800">{row.topic}</div>
                    <div className="text-xs text-slate-500">{row.category}{row.state ? ` - ${row.state}` : ''}</div>
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

function CancelPolicyModal({
  policy,
  guidance,
  saving,
  onClose,
  onSave,
}: {
  policy: Policy
  guidance?: LegalComplianceGuidance
  saving: boolean
  onClose: () => void
  onSave: (data: { cancelledDate: string; reason: string; method: string; premiumChange: number; complianceChecklist: CancellationComplianceChecklistItem[]; legalRequirementSectionIds: string[]; notes?: string }) => void
}) {
  const [cancelledDate, setCancelledDate] = useState(toDateInput(policy.effectiveDate))
  const [reason, setReason] = useState('')
  const [method, setMethod] = useState('Written Notice')
  const [premiumChange, setPremiumChange] = useState('0')
  const [notes, setNotes] = useState('')
  const [checklist, setChecklist] = useState<CancellationComplianceChecklistItem[]>(() => buildCancellationChecklist(guidance))
  const allChecklistComplete = checklist.length > 0 && checklist.every((item) => item.isCompleted)

  useEffect(() => {
    setChecklist((current) => {
      const next = buildCancellationChecklist(guidance)
      if (current.length === 0) return next
      return next.map((item) => ({
        ...item,
        isCompleted: current.find((existing) => existing.key === item.key)?.isCompleted ?? false,
      }))
    })
  }, [guidance])

  function submit(event: React.FormEvent) {
    event.preventDefault()
    onSave({
      cancelledDate,
      reason: reason.trim(),
      method,
      premiumChange: Number(premiumChange || 0),
      complianceChecklist: checklist,
      legalRequirementSectionIds: uniqueIds(checklist.flatMap((item) => item.requirementSectionIds)),
      notes: notes.trim() || undefined,
    })
  }

  return (
    <ActionModal title="Cancel Policy" onClose={onClose} wide>
      <form onSubmit={submit} className="grid grid-cols-1 gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(320px,420px)]">
        <div className="space-y-4">
          <Field label="Cancellation Date">
            <input type="date" required value={cancelledDate} onChange={(e) => setCancelledDate(e.target.value)} className={inputClass} />
          </Field>
          <Field label="Reason">
            <textarea rows={4} required value={reason} onChange={(e) => setReason(e.target.value)} className={textareaClass} />
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
          <Field label="Premium Change">
            <input type="number" step="0.01" value={premiumChange} onChange={(e) => setPremiumChange(e.target.value)} className={inputClass} />
          </Field>
          <Field label="Notes">
            <textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} className={textareaClass} />
          </Field>
          <ComplianceChecklist items={checklist} onChange={setChecklist} />
          <ModalActions saving={saving} disabled={!allChecklistComplete} onClose={onClose} submitLabel="Cancel Policy" danger />
        </div>
        <LegalGuidancePanel guidance={guidance} mode="Cancellation" />
      </form>
    </ActionModal>
  )
}

function NonRenewPolicyModal({
  policy,
  guidance,
  saving,
  onClose,
  onSave,
}: {
  policy: Policy
  guidance?: LegalComplianceGuidance
  saving: boolean
  onClose: () => void
  onSave: (data: { nonRenewedDate: string; reason?: string }) => void
}) {
  const [nonRenewedDate, setNonRenewedDate] = useState(toDateInput(policy.expirationDate))
  const [reason, setReason] = useState('')

  function submit(event: React.FormEvent) {
    event.preventDefault()
    onSave({
      nonRenewedDate,
      reason: reason.trim() || undefined,
    })
  }

  return (
    <ActionModal title="Non-Renew Policy" onClose={onClose} wide>
      <form onSubmit={submit} className="grid grid-cols-1 gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(320px,420px)]">
        <div className="space-y-4">
          <Field label="Non-Renewed Date">
            <input type="date" required value={nonRenewedDate} onChange={(e) => setNonRenewedDate(e.target.value)} className={inputClass} />
          </Field>
          <Field label="Reason">
            <textarea rows={5} value={reason} onChange={(e) => setReason(e.target.value)} className={textareaClass} />
          </Field>
          <ModalActions saving={saving} onClose={onClose} submitLabel="Mark Non-Renewed" />
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
}: {
  items: CancellationComplianceChecklistItem[]
  onChange: (items: CancellationComplianceChecklistItem[]) => void
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
      <p className="mt-3 text-xs" style={{ color: 'var(--ink-3)' }}>These selections are saved with the cancellation transaction.</p>
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

function ids(rows?: LegalComplianceRequirement[]) {
  return uniqueIds((rows ?? []).map((row) => row.id))
}

function uniqueIds(values: string[]) {
  return Array.from(new Set(values.filter(Boolean)))
}
