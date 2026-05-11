import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, Ban, FileSignature, FileX2, Pin, PinOff, Pencil, Trash2, Plus, X, Check, FileText } from 'lucide-react'
import { toast } from 'sonner'
import { policiesApi } from '@/api/policies.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { LOB_LABELS } from '@/types/quote.types'
import { POLICY_STATUS_LABELS, POLICY_STATUS_COLORS } from '@/types/policy.types'
import type { CancellationComplianceChecklistItem, LegalComplianceGuidance, LegalComplianceRequirement, LegalRequirementSnapshot, Policy, PolicyTransaction } from '@/types/policy.types'
import { formatCurrency } from '@/lib/utils'
import type { Note } from '@/types/quote.types'
import { DocumentsSection } from '@/components/documents/DocumentsSection'
import { GenerateDocumentModal } from '@/components/documents/GenerateDocumentModal'
import { usePermissions } from '@/hooks/usePermissions'

export function PolicyDetailPage() {
  const { id } = useParams<{ id: string }>()
  const qc = useQueryClient()

  const [showGenerateModal, setShowGenerateModal] = useState(false)

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

  const { canCreateNotes, canEditNotes, canDeleteNotes, canUploadAttachments, canDeleteAttachments, canCreatePolicies, canEndorsePolicies, canCancelPolicies } = usePermissions()

  const { data: cancellationGuidance } = useQuery({
    queryKey: ['policies', id, 'cancellation-guidance'],
    queryFn: () => policiesApi.getCancellationGuidance(id!),
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

  if (isLoading) return <LoadingSpinner />
  if (!policy) return <p className="p-6 text-slate-500">Policy not found.</p>

  const sortedNotes = [...notes].sort((a, b) => {
    if (a.isPinned !== b.isPinned) return a.isPinned ? -1 : 1
    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  })

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center gap-2 text-sm text-slate-500">
        <Link to={`/insureds/${policy.insuredId}`} className="hover:text-slate-900">{policy.insuredName}</Link>
        <span>/</span>
        <Link to={`/submissions/${policy.submissionId}`} className="hover:text-slate-900">{policy.submissionNumber}</Link>
        <span>/</span>
        <span className="text-slate-700">{policy.policyNumber}</span>
      </div>

      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-xl font-semibold text-slate-900">{policy.policyNumber}</h1>
          <p className="text-sm text-slate-500 mt-0.5">{policy.insuredName} · {policy.carrierName}</p>
        </div>
        <div className="flex items-center gap-2">
          {policy.status === 'Active' && canEndorsePolicies && (
            <button
              onClick={() => setActionModal('endorse')}
              className="flex items-center gap-1.5 px-3 py-1.5 border rounded text-sm text-slate-700 hover:bg-slate-50"
            >
              <FileSignature className="h-3.5 w-3.5" /> Endorse
            </button>
          )}
          {policy.status === 'Active' && canCancelPolicies && (
            <>
              <button
                onClick={() => setActionModal('nonRenew')}
                className="flex items-center gap-1.5 px-3 py-1.5 border rounded text-sm text-slate-700 hover:bg-slate-50"
              >
                <FileX2 className="h-3.5 w-3.5" /> Non-Renew
              </button>
              <button
                onClick={() => setActionModal('cancel')}
                className="flex items-center gap-1.5 px-3 py-1.5 border border-red-200 rounded text-sm text-red-700 hover:bg-red-50"
              >
                <Ban className="h-3.5 w-3.5" /> Cancel
              </button>
            </>
          )}
          {canCreatePolicies && (
            <button
              onClick={() => setShowGenerateModal(true)}
              className="flex items-center gap-1.5 px-3 py-1.5 border rounded text-sm text-slate-700 hover:bg-slate-50"
            >
              <FileText className="h-3.5 w-3.5" /> Generate Document
            </button>
          )}
          <span className={`inline-flex px-2.5 py-1 rounded-full text-xs font-medium ${POLICY_STATUS_COLORS[policy.status]}`}>
            {POLICY_STATUS_LABELS[policy.status]}
          </span>
        </div>
      </div>

      {showGenerateModal && (
        <GenerateDocumentModal
          entityType="Policy"
          entityId={id!}
          onClose={() => setShowGenerateModal(false)}
        />
      )}

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
          guidance={cancellationGuidance}
          saving={nonRenewMutation.isPending}
          onClose={() => setActionModal(null)}
          onSave={(data) => nonRenewMutation.mutate(data)}
        />
      )}

      {/* Policy details */}
      <div className="bg-white border rounded-lg p-5 grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
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

      {/* Transactions */}
      {policy.transactions.length > 0 && (
        <div className="bg-white border rounded-lg overflow-hidden">
          <div className="px-5 py-4 border-b">
            <h2 className="text-sm font-semibold text-slate-900">Transaction History</h2>
          </div>
          <table className="min-w-full divide-y divide-slate-100 text-sm">
            <thead className="bg-slate-50">
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
            <tbody className="divide-y divide-slate-100">
              {policy.transactions.map((t) => (
                <TransactionRows key={t.id} transaction={t} />
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Notes */}
        <div className="bg-white border rounded-lg">
          <div className="flex items-center justify-between px-5 py-4 border-b">
            <h2 className="text-sm font-semibold text-slate-900">Notes ({notes.length})</h2>
            {!showNoteForm && canCreateNotes && (
              <button
                onClick={() => setShowNoteForm(true)}
                className="flex items-center gap-1 text-sm text-blue-600 hover:underline"
              >
                <Plus className="h-3.5 w-3.5" /> Add Note
              </button>
            )}
          </div>

          {showNoteForm && (
            <div className="px-5 py-4 border-b bg-slate-50 space-y-3">
              <input
                type="text"
                placeholder="Subject (optional)"
                value={noteSubject}
                onChange={(e) => setNoteSubject(e.target.value)}
                className="w-full border rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              <textarea
                placeholder="Note body *"
                value={noteBody}
                onChange={(e) => setNoteBody(e.target.value)}
                rows={3}
                className="w-full border rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
              />
              <div className="flex gap-2">
                <button
                  disabled={!noteBody.trim() || createNoteMutation.isPending}
                  onClick={() => createNoteMutation.mutate()}
                  className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-600 text-white rounded text-sm hover:bg-blue-700 disabled:opacity-50"
                >
                  <Check className="h-3.5 w-3.5" /> Save
                </button>
                <button onClick={() => { setShowNoteForm(false); setNoteSubject(''); setNoteBody('') }} className="flex items-center gap-1.5 px-3 py-1.5 border rounded text-sm hover:bg-white">
                  <X className="h-3.5 w-3.5" /> Cancel
                </button>
              </div>
            </div>
          )}

          <div className="divide-y">
            {sortedNotes.length === 0 && !showNoteForm && (
              <p className="text-sm text-slate-400 px-5 py-8 text-center">No notes yet.</p>
            )}
            {sortedNotes.map((note) => (
              <div key={note.id} className={`px-5 py-4 ${note.isPinned ? 'bg-yellow-50' : ''}`}>
                {editingNote?.id === note.id ? (
                  <div className="space-y-2">
                    <input
                      type="text"
                      placeholder="Subject (optional)"
                      value={editSubject}
                      onChange={(e) => setEditSubject(e.target.value)}
                      className="w-full border rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                    />
                    <textarea
                      value={editBody}
                      onChange={(e) => setEditBody(e.target.value)}
                      rows={3}
                      className="w-full border rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
                    />
                    <div className="flex gap-2">
                      <button
                        disabled={!editBody.trim() || updateNoteMutation.isPending}
                        onClick={() => updateNoteMutation.mutate(note)}
                        className="flex items-center gap-1.5 px-3 py-1 bg-blue-600 text-white rounded text-xs hover:bg-blue-700 disabled:opacity-50"
                      >
                        <Check className="h-3 w-3" /> Save
                      </button>
                      <button onClick={() => setEditingNote(null)} className="flex items-center gap-1.5 px-3 py-1 border rounded text-xs hover:bg-white">
                        <X className="h-3 w-3" /> Cancel
                      </button>
                    </div>
                  </div>
                ) : (
                  <>
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0 flex-1">
                        {note.subject && <p className="text-sm font-medium text-slate-900">{note.subject}</p>}
                        <p className="text-sm text-slate-700 whitespace-pre-wrap mt-0.5">{note.body}</p>
                        <p className="text-xs text-slate-400 mt-1">
                          {note.createdByName} · {new Date(note.createdAt).toLocaleDateString()}
                        </p>
                      </div>
                      <div className="flex gap-1 shrink-0">
                        {canEditNotes && (
                          <button onClick={() => togglePinMutation.mutate(note.id)} className="p-1 rounded hover:bg-slate-100">
                            {note.isPinned
                              ? <PinOff className="h-3.5 w-3.5 text-yellow-500" />
                              : <Pin className="h-3.5 w-3.5 text-slate-400" />}
                          </button>
                        )}
                        {canEditNotes && (
                          <button onClick={() => { setEditingNote(note); setEditSubject(note.subject ?? ''); setEditBody(note.body) }} className="p-1 rounded hover:bg-slate-100">
                            <Pencil className="h-3.5 w-3.5 text-slate-400" />
                          </button>
                        )}
                        {canDeleteNotes && (
                          <button onClick={() => { if (confirm('Delete note?')) deleteNoteMutation.mutate(note.id) }} className="p-1 rounded hover:bg-slate-100">
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
      <div className="bg-white border rounded-lg p-5">
        <DocumentsSection entityType="Policy" entityId={id!} canUpload={canUploadAttachments} canDelete={canDeleteAttachments} />
      </div>
    </div>
  )
}

function TransactionRows({ transaction: t }: { transaction: PolicyTransaction }) {
  return (
    <>
      <tr className="hover:bg-slate-50">
        <td className="px-5 py-2.5 font-mono text-xs text-slate-600">{t.transactionNumber}</td>
        <td className="px-5 py-2.5 text-slate-700">{t.transactionType}</td>
        <td className="px-5 py-2.5">
          <span className={`inline-flex px-2 py-0.5 rounded-full text-xs font-medium ${
            t.status === 'Issued' ? 'bg-green-100 text-green-700' : 'bg-yellow-100 text-yellow-700'
          }`}>
            {t.status}
          </span>
        </td>
        <td className="px-5 py-2.5 text-slate-500">{new Date(t.effectiveDate).toLocaleDateString()}</td>
        <td className="px-5 py-2.5 text-right">
          <span className={t.premiumChange >= 0 ? 'text-green-600' : 'text-red-600'}>
            {t.premiumChange >= 0 ? '+' : ''}{formatCurrency(t.premiumChange)}
          </span>
        </td>
        <td className="px-5 py-2.5 text-right font-medium text-slate-700">{formatCurrency(t.newTotalPremium)}</td>
        <td className="px-5 py-2.5 text-slate-500">{t.processedByName}</td>
      </tr>
      {t.transactionType === 'Cancellation' && (
        <CancellationTransactionDetails transaction={t} />
      )}
    </>
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
          <textarea rows={3} value={description} onChange={(e) => setDescription(e.target.value)} className={inputClass} />
        </Field>
        <Field label="Notes">
          <textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} className={inputClass} />
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
            <textarea rows={4} required value={reason} onChange={(e) => setReason(e.target.value)} className={inputClass} />
          </Field>
          <Field label="Notice Method">
            <select value={method} onChange={(e) => setMethod(e.target.value)} className={inputClass}>
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
            <textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} className={inputClass} />
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
            <textarea rows={5} value={reason} onChange={(e) => setReason(e.target.value)} className={inputClass} />
          </Field>
          <ModalActions saving={saving} onClose={onClose} submitLabel="Mark Non-Renewed" />
        </div>
        <LegalGuidancePanel guidance={guidance} mode="Cancellation and non-renewal reference" />
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
    <aside className="rounded border bg-slate-50 p-4">
      <div className="flex items-start gap-2">
        <AlertTriangle className="mt-0.5 h-4 w-4 text-amber-600" />
        <div>
          <h3 className="text-sm font-semibold text-slate-900">Legal Guidance</h3>
          <p className="mt-1 text-xs text-slate-500">
            {guidance ? `${mode}: ${guidance.state} ${guidance.lineOfBusiness}` : 'No matching guidance loaded.'}
          </p>
        </div>
      </div>
      <div className="mt-4 max-h-[520px] space-y-3 overflow-auto pr-1">
        {groups.length === 0 ? (
          <p className="text-sm text-slate-500">No cancellation requirements were found for this policy state.</p>
        ) : groups.map((group) => (
          <section key={group.label} className="rounded border bg-white p-3">
            <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">{group.label}</div>
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
      <div className="text-sm font-semibold text-slate-800">{row.topic}</div>
      <p className="mt-1 text-sm leading-6 text-slate-700">{truncateText(row.requirementText, 520)}</p>
      {row.citations.length > 0 && (
        <div className="mt-2 flex flex-wrap gap-1">
          {row.citations.map((citation) => (
            <span key={citation} className="rounded bg-slate-100 px-2 py-0.5 text-[11px] text-slate-600">{citation}</span>
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
    <section className="rounded border bg-slate-50 p-4">
      <h3 className="text-sm font-semibold text-slate-900">Compliance Review</h3>
      <div className="mt-3 space-y-2">
        {items.map((item) => (
          <label key={item.key} className="flex items-start gap-2 text-sm text-slate-700">
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
      <p className="mt-3 text-xs text-slate-500">These selections are saved with the cancellation transaction.</p>
    </section>
  )
}

function ActionModal({ title, onClose, children, wide = false }: { title: string; onClose: () => void; children: React.ReactNode; wide?: boolean }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/30 p-4">
      <div className={`max-h-[92vh] w-full overflow-auto rounded border bg-white shadow-xl ${wide ? 'max-w-6xl' : 'max-w-xl'}`}>
        <div className="flex items-center justify-between border-b px-5 py-4">
          <h2 className="text-lg font-semibold text-slate-900">{title}</h2>
          <button type="button" onClick={onClose} className="rounded p-2 text-slate-500 hover:bg-slate-100" aria-label="Close">
            <X className="h-5 w-5" />
          </button>
        </div>
        <div className="p-5">{children}</div>
      </div>
    </div>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block text-sm font-medium text-slate-700">
      {label}
      <div className="mt-1">{children}</div>
    </label>
  )
}

function ModalActions({ saving, disabled = false, onClose, submitLabel, danger = false }: { saving: boolean; disabled?: boolean; onClose: () => void; submitLabel: string; danger?: boolean }) {
  return (
    <div className="flex justify-end gap-2 border-t pt-4">
      <button type="button" onClick={onClose} className="rounded border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
        Close
      </button>
      <button
        type="submit"
        disabled={saving || disabled}
        className={`rounded px-3 py-2 text-sm font-medium text-white disabled:opacity-50 ${danger ? 'bg-red-600 hover:bg-red-700' : 'bg-blue-600 hover:bg-blue-700'}`}
      >
        {saving ? 'Saving...' : submitLabel}
      </button>
    </div>
  )
}

const inputClass = 'w-full rounded border border-slate-300 px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100'

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
