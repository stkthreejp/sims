import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, AlertTriangle, Check, FileText, GitCompare, Loader2, Save, Send } from 'lucide-react'
import { toast } from 'sonner'
import { complianceDocumentsApi } from '@/api/complianceDocuments.api'
import { usersApi } from '@/api/users.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { TemplateEditor } from '@/components/editor/TemplateEditor'
import { formatDate, formatDateTime } from '@/lib/utils'
import { useAuthStore } from '@/store/authStore'
import type { ComplianceAttestationCampaign, ComplianceAttestationRecipient, ComplianceAuditLog } from '@/types/compliance.types'
import type { User } from '@/types/user.types'

const CATEGORIES = ['IT', 'Security', 'Business Continuity', 'Privacy', 'Operations', 'Vendor Management', 'HR', 'Finance']
const TYPES = ['Policy', 'Plan', 'Procedure', 'Standard', 'Checklist', 'Evidence']
const STATUSES = ['Draft', 'Active', 'Under Review', 'Needs Update', 'Retired']
const CADENCES = ['Quarterly', 'Semiannual', 'Annual', 'Biennial', 'Manual']

export function ComplianceDocumentDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [title, setTitle] = useState('')
  const [category, setCategory] = useState('IT')
  const [documentType, setDocumentType] = useState('Policy')
  const [status, setStatus] = useState('Draft')
  const [ownerId, setOwnerId] = useState('')
  const [approverId, setApproverId] = useState('')
  const [reviewCadence, setReviewCadence] = useState('Annual')
  const [nextReviewDate, setNextReviewDate] = useState('')
  const [tagsText, setTagsText] = useState('')
  const [content, setContent] = useState('<p></p>')
  const [changeSummary, setChangeSummary] = useState('')
  const [showCompare, setShowCompare] = useState(false)
  const [attestationOpen, setAttestationOpen] = useState(false)
  const [reviewOpen, setReviewOpen] = useState(false)
  const [evidenceOpen, setEvidenceOpen] = useState(false)
  const [isDirty, setIsDirty] = useState(false)

  const documentQuery = useQuery({
    queryKey: ['compliance-documents', id],
    queryFn: () => complianceDocumentsApi.getById(id!),
    enabled: !!id,
  })

  const compareQuery = useQuery({
    queryKey: ['compliance-documents', id, 'compare'],
    queryFn: () => complianceDocumentsApi.compare(id!),
    enabled: !!id && showCompare && !!documentQuery.data?.currentPublishedVersion && !!documentQuery.data?.currentDraftVersion,
  })

  const attestationQuery = useQuery({
    queryKey: ['compliance-documents', id, 'attestations'],
    queryFn: () => complianceDocumentsApi.getAttestationCampaigns(id),
    enabled: !!id,
  })

  const auditLogQuery = useQuery({
    queryKey: ['compliance-documents', id, 'audit-log'],
    queryFn: () => complianceDocumentsApi.getAuditLog(id!),
    enabled: !!id,
  })

  const usersQuery = useQuery({
    queryKey: ['users', 'compliance-owner-picker'],
    queryFn: () => usersApi.getAll({ page: 1, pageSize: 200 }),
  })

  useEffect(() => {
    const document = documentQuery.data
    if (!document) return

    setTitle(document.title)
    setCategory(document.category)
    setDocumentType(document.documentType)
    setStatus(document.status)
    setOwnerId(document.ownerId ?? '')
    setApproverId(document.approverId ?? '')
    setReviewCadence(document.reviewCadence)
    setNextReviewDate(document.nextReviewDate ?? '')
    setTagsText(document.tags.join(', '))
    setContent(document.currentDraftVersion?.htmlContent || document.currentPublishedVersion?.htmlContent || '<p></p>')
    setChangeSummary(document.currentDraftVersion?.changeSummary ?? '')
    setIsDirty(false)
  }, [documentQuery.data])

  const updateMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.update(id!, {
      title,
      category,
      documentType,
      status,
      ownerId: ownerId || null,
      approverId: approverId || null,
      reviewCadence,
      nextReviewDate: nextReviewDate || null,
      tags: parseTags(tagsText),
    }),
    onSuccess: (document) => {
      qc.setQueryData(['compliance-documents', id], document)
      qc.invalidateQueries({ queryKey: ['compliance-documents'] })
      qc.invalidateQueries({ queryKey: ['compliance-documents', id, 'audit-log'] })
      toast.success('Document details saved')
      setIsDirty(false)
    },
    onError: () => toast.error('Could not save document details'),
  })

  const draftMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.saveDraft(id!, { htmlContent: content, changeSummary: changeSummary || null }),
    onSuccess: (document) => {
      qc.setQueryData(['compliance-documents', id], document)
      qc.invalidateQueries({ queryKey: ['compliance-documents'] })
      qc.invalidateQueries({ queryKey: ['compliance-documents', id, 'audit-log'] })
      toast.success('Draft saved')
      setIsDirty(false)
    },
    onError: () => toast.error('Could not save draft'),
  })

  const publishMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.publishDraft(id!, { notes: changeSummary || null }),
    onSuccess: (document) => {
      qc.setQueryData(['compliance-documents', id], document)
      qc.invalidateQueries({ queryKey: ['compliance-documents'] })
      qc.invalidateQueries({ queryKey: ['compliance-documents', id, 'audit-log'] })
      setShowCompare(false)
      toast.success('Draft published')
    },
    onError: () => toast.error('Could not publish draft'),
  })

  const submitMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.submitForReview(id!, { notes: changeSummary || null }),
    onSuccess: (document) => {
      qc.setQueryData(['compliance-documents', id], document)
      qc.invalidateQueries({ queryKey: ['compliance-documents'] })
      qc.invalidateQueries({ queryKey: ['compliance-documents', id, 'audit-log'] })
      toast.success('Draft submitted for review')
    },
    onError: () => toast.error('Could not submit for review'),
  })

  const requireChangesMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.requireChanges(id!, { notes: changeSummary || null }),
    onSuccess: (document) => {
      qc.setQueryData(['compliance-documents', id], document)
      qc.invalidateQueries({ queryKey: ['compliance-documents'] })
      qc.invalidateQueries({ queryKey: ['compliance-documents', id, 'audit-log'] })
      toast.success('Changes requested')
    },
    onError: () => toast.error('Could not request changes'),
  })

  const busy = updateMutation.isPending || draftMutation.isPending || publishMutation.isPending || submitMutation.isPending || requireChangesMutation.isPending

  if (documentQuery.isLoading) return <LoadingSpinner />
  if (!documentQuery.data) return <div className="p-6 text-sm text-slate-500">Compliance document not found.</div>

  const document = documentQuery.data
  const canCompare = !!document.currentPublishedVersion && !!document.currentDraftVersion

  return (
    <div className="flex h-full flex-col">
      <div className="flex items-center gap-3 border-b bg-white px-6 py-3">
        <button
          type="button"
          onClick={() => navigate('/compliance-documentation')}
          className="inline-flex items-center gap-1.5 text-sm text-slate-500 hover:text-slate-900"
        >
          <ArrowLeft className="h-4 w-4" />
          Compliance
        </button>
        <div className="h-4 w-px bg-slate-200" />
        <input
          value={title}
          onChange={(event) => { setTitle(event.target.value); setIsDirty(true) }}
          className="min-w-0 flex-1 border-0 bg-transparent text-sm font-semibold text-slate-900 outline-none"
        />
        <button
          type="button"
          onClick={() => updateMutation.mutate()}
          disabled={busy || !title.trim()}
          className="inline-flex items-center gap-1.5 rounded border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
        >
          {updateMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
          Save Details
        </button>
        <button
          type="button"
          onClick={() => draftMutation.mutate()}
          disabled={busy}
          className="inline-flex items-center gap-1.5 rounded bg-blue-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
        >
          {draftMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
          Save Draft
        </button>
        <button
          type="button"
          onClick={() => publishMutation.mutate()}
          disabled={busy || !document.currentDraftVersion || document.status !== 'Under Review'}
          className="inline-flex items-center gap-1.5 rounded bg-green-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50"
        >
          {publishMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
          Publish
        </button>
        <button
          type="button"
          onClick={() => navigate(`/compliance-documentation/${document.id}/report`)}
          className="inline-flex items-center gap-1.5 rounded border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          <FileText className="h-4 w-4" />
          Report
        </button>
      </div>

      <div className="grid flex-1 grid-cols-1 overflow-hidden lg:grid-cols-[280px_minmax(0,1fr)]">
        <aside className="overflow-auto border-r bg-slate-50 p-4">
          <div className="space-y-4">
            <SelectField label="Category" value={category} values={CATEGORIES} onChange={(value) => { setCategory(value); setIsDirty(true) }} />
            <SelectField label="Type" value={documentType} values={TYPES} onChange={(value) => { setDocumentType(value); setIsDirty(true) }} />
            <SelectField label="Status" value={status} values={STATUSES} onChange={(value) => { setStatus(value); setIsDirty(true) }} />
            <UserSelectField
              label="Owner"
              value={ownerId}
              users={usersQuery.data?.items ?? []}
              onChange={(value) => { setOwnerId(value); setIsDirty(true) }}
            />
            <UserSelectField
              label="Approver"
              value={approverId}
              users={usersQuery.data?.items ?? []}
              onChange={(value) => { setApproverId(value); setIsDirty(true) }}
            />
            <SelectField label="Review Cadence" value={reviewCadence} values={CADENCES} onChange={(value) => { setReviewCadence(value); setIsDirty(true) }} />
            <label className="block text-sm font-medium text-slate-700">
              Next Review
              <input
                type="date"
                value={nextReviewDate}
                onChange={(event) => { setNextReviewDate(event.target.value); setIsDirty(true) }}
                className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm"
              />
            </label>
            <label className="block text-sm font-medium text-slate-700">
              Tags
              <textarea
                value={tagsText}
                onChange={(event) => { setTagsText(event.target.value); setIsDirty(true) }}
                rows={3}
                placeholder="Separate with commas"
                className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm"
              />
            </label>
            <label className="block text-sm font-medium text-slate-700">
              Change Summary
              <textarea
                value={changeSummary}
                onChange={(event) => { setChangeSummary(event.target.value); setIsDirty(true) }}
                rows={4}
                placeholder="What changed in this draft?"
                className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm"
              />
            </label>

            <div className="rounded border bg-white p-3 text-sm text-slate-600">
              <div className="font-medium text-slate-800">Review</div>
              <div className="mt-2">Owner: {document.ownerName ?? '-'}</div>
              <div>Approver: {document.approverName ?? '-'}</div>
              <div className="mt-2">Last reviewed: {formatDate(document.lastReviewedDate)}</div>
              <div>Next review: {formatDate(document.nextReviewDate)}</div>
              <div>Effective: {formatDate(document.effectiveDate)}</div>
              {isDirty && <div className="mt-2 text-xs text-amber-600">Unsaved changes</div>}
            </div>
          </div>
        </aside>

        <main className="overflow-auto p-6">
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div>
              <div className="text-sm font-semibold text-slate-900">Document Editor</div>
              <div className="text-xs text-slate-500">
                Published {document.currentPublishedVersion ? `v${document.currentPublishedVersion.versionNumber}` : 'none'} · Draft {document.currentDraftVersion ? `v${document.currentDraftVersion.versionNumber}` : 'none'}
              </div>
            </div>
            <button
              type="button"
              onClick={() => setShowCompare((value) => !value)}
              disabled={!canCompare}
              className="inline-flex items-center gap-2 rounded border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
            >
              <GitCompare className="h-4 w-4" />
              Compare Draft
            </button>
          </div>

          <section className="mb-5 rounded border bg-white p-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <div className="text-sm font-semibold text-slate-900">Review Workflow</div>
                <div className="mt-1 text-xs text-slate-500">Save a draft, submit it for review, then approve or request changes.</div>
              </div>
              <div className="flex flex-wrap gap-2">
                <button
                  type="button"
                  onClick={() => submitMutation.mutate()}
                  disabled={busy || !document.currentDraftVersion || document.status === 'Under Review'}
                  className="inline-flex items-center gap-1.5 rounded border border-blue-200 px-3 py-2 text-sm font-medium text-blue-700 hover:bg-blue-50 disabled:opacity-50"
                >
                  <Send className="h-4 w-4" />
                  Submit for Review
                </button>
                <button
                  type="button"
                  onClick={() => requireChangesMutation.mutate()}
                  disabled={busy || !document.currentDraftVersion || document.status !== 'Under Review'}
                  className="inline-flex items-center gap-1.5 rounded border border-amber-200 px-3 py-2 text-sm font-medium text-amber-700 hover:bg-amber-50 disabled:opacity-50"
                >
                  <AlertTriangle className="h-4 w-4" />
                  Require Changes
                </button>
              </div>
            </div>
          </section>

          {showCompare && canCompare && (
            <section className="mb-5 rounded border bg-white p-4">
              <div className="mb-3 text-sm font-semibold text-slate-800">Changes from published version</div>
              {compareQuery.isLoading ? (
                <LoadingSpinner />
              ) : (
                <p className="whitespace-pre-wrap text-sm leading-7 text-slate-700">
                  {compareQuery.data?.parts.map((part, index) => (
                    <span key={`${part.kind}-${index}`} className={diffClass(part.kind)}>{part.text}</span>
                  ))}
                </p>
              )}
            </section>
          )}

          <TemplateEditor
            content={content}
            onChange={(html) => { setContent(html); setIsDirty(true) }}
            entityType="General"
          />

          <section className="mt-6 grid grid-cols-1 gap-4 lg:grid-cols-2">
            <HistoryPanel title="Version History">
              {document.versions.length === 0 ? (
                <div className="text-sm text-slate-500">No versions yet.</div>
              ) : document.versions.map((version) => (
                <div key={version.id} className="border-b py-3 last:border-0">
                  <div className="flex items-center justify-between gap-3">
                    <div className="font-medium text-slate-800">v{version.versionNumber} · {version.status}</div>
                    <div className="text-xs text-slate-500">{formatDateTime(version.createdAt)}</div>
                  </div>
                  <div className="mt-1 text-xs text-slate-500">Created by {version.createdByName || '-'}</div>
                  {version.approvedByName && <div className="mt-1 text-xs text-green-700">Approved by {version.approvedByName}</div>}
                  {version.changeSummary && <div className="mt-2 text-sm text-slate-600">{version.changeSummary}</div>}
                </div>
              ))}
            </HistoryPanel>

            <HistoryPanel title="Attestations">
              <div className="mb-3 flex justify-end">
                <button
                  type="button"
                  onClick={() => setAttestationOpen(true)}
                  disabled={!document.currentPublishedVersion}
                  className="rounded bg-slate-900 px-3 py-1.5 text-xs font-medium text-white hover:bg-slate-800 disabled:opacity-50"
                >
                  Launch Campaign
                </button>
              </div>
              {(attestationQuery.data ?? []).length === 0 ? (
                <div className="text-sm text-slate-500">No attestation campaigns yet.</div>
              ) : (
                <div className="space-y-3">
                  {(attestationQuery.data ?? []).map((campaign) => (
                    <AttestationCampaignCard key={campaign.id} campaign={campaign} />
                  ))}
                </div>
              )}
            </HistoryPanel>

            <HistoryPanel title="Reviews and Evidence">
              <div className="mb-3 flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setReviewOpen(true)}
                  className="rounded border border-slate-300 px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
                >
                  Mark Reviewed
                </button>
                <button
                  type="button"
                  onClick={() => setEvidenceOpen(true)}
                  className="rounded border border-slate-300 px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
                >
                  Add Evidence
                </button>
              </div>
              {document.reviews.length === 0 && document.evidenceItems.length === 0 ? (
                <div className="text-sm text-slate-500">No reviews or evidence yet.</div>
              ) : (
                <>
                  {document.reviews.map((review) => (
                    <div key={review.id} className="border-b py-3">
                      <div className="font-medium text-slate-800">{review.status}</div>
                      <div className="mt-1 text-xs text-slate-500">{review.reviewedByName} · {formatDateTime(review.reviewedAt)}</div>
                      {review.notes && <div className="mt-2 text-sm text-slate-600">{review.notes}</div>}
                    </div>
                  ))}
                  {document.evidenceItems.map((evidence) => (
                    <div key={evidence.id} className="border-b py-3 last:border-0">
                      <div className="font-medium text-slate-800">{evidence.title}</div>
                      <div className="mt-1 text-xs text-slate-500">{evidence.evidenceType} · {formatDateTime(evidence.createdAt)}</div>
                      {evidence.description && <div className="mt-2 text-sm text-slate-600">{evidence.description}</div>}
                    </div>
                  ))}
                </>
              )}
            </HistoryPanel>

            <HistoryPanel title="Audit Trail">
              {auditLogQuery.isLoading ? (
                <LoadingSpinner />
              ) : (auditLogQuery.data ?? []).length === 0 ? (
                <div className="text-sm text-slate-500">No audit entries have been recorded yet.</div>
              ) : (
                <div className="max-h-96 overflow-auto">
                  {(auditLogQuery.data ?? []).map((log) => (
                    <AuditLogEntry key={log.id} log={log} />
                  ))}
                </div>
              )}
            </HistoryPanel>
          </section>
        </main>
      </div>

      {attestationOpen && (
        <AttestationModal
          documentId={document.id}
          versionId={document.currentPublishedVersion?.id ?? null}
          versionNumber={document.currentPublishedVersion?.versionNumber ?? null}
          title={document.title}
          onClose={() => setAttestationOpen(false)}
          onCreated={() => {
            qc.invalidateQueries({ queryKey: ['compliance-documents', id, 'attestations'] })
            qc.invalidateQueries({ queryKey: ['compliance-documents', id, 'audit-log'] })
            qc.invalidateQueries({ queryKey: ['compliance-documents'] })
            setAttestationOpen(false)
          }}
        />
      )}

      {reviewOpen && (
        <ReviewModal
          documentId={document.id}
          reviewCadence={document.reviewCadence}
          onClose={() => setReviewOpen(false)}
          onSaved={() => {
            qc.invalidateQueries({ queryKey: ['compliance-documents', id] })
            qc.invalidateQueries({ queryKey: ['compliance-documents', id, 'audit-log'] })
            qc.invalidateQueries({ queryKey: ['compliance-documents'] })
            setReviewOpen(false)
          }}
        />
      )}

      {evidenceOpen && (
        <EvidenceModal
          documentId={document.id}
          onClose={() => setEvidenceOpen(false)}
          onSaved={() => {
            qc.invalidateQueries({ queryKey: ['compliance-documents', id] })
            qc.invalidateQueries({ queryKey: ['compliance-documents', id, 'audit-log'] })
            qc.invalidateQueries({ queryKey: ['compliance-documents'] })
            setEvidenceOpen(false)
          }}
        />
      )}
    </div>
  )
}

function SelectField({ label, value, values, onChange }: { label: string; value: string; values: string[]; onChange: (value: string) => void }) {
  return (
    <label className="block text-sm font-medium text-slate-700">
      {label}
      <select
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="mt-1 w-full rounded border border-slate-300 bg-white px-3 py-2 text-sm"
      >
        {values.map((option) => <option key={option} value={option}>{option}</option>)}
      </select>
    </label>
  )
}

function UserSelectField({ label, value, users, onChange }: { label: string; value: string; users: User[]; onChange: (value: string) => void }) {
  return (
    <label className="block text-sm font-medium text-slate-700">
      {label}
      <select
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="mt-1 w-full rounded border border-slate-300 bg-white px-3 py-2 text-sm"
      >
        <option value="">Unassigned</option>
        {users.map((user) => (
          <option key={user.id} value={user.id}>
            {user.fullName || user.userName}
          </option>
        ))}
      </select>
    </label>
  )
}

function HistoryPanel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="rounded border bg-white p-4">
      <div className="mb-2 text-sm font-semibold text-slate-800">{title}</div>
      {children}
    </section>
  )
}

function AuditLogEntry({ log }: { log: ComplianceAuditLog }) {
  return (
    <div className="border-b py-3 last:border-0">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="font-medium text-slate-800">{cleanAction(log.action)}</div>
          <div className="mt-1 text-xs text-slate-500">{log.userName || '-'} · {formatDateTime(log.createdAt)}</div>
        </div>
        {log.fieldName && <span className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{log.fieldName}</span>}
      </div>
      {(log.oldValue || log.newValue) && (
        <div className="mt-2 grid grid-cols-1 gap-2 text-xs md:grid-cols-2">
          {log.oldValue && (
            <div className="rounded border border-red-100 bg-red-50 p-2 text-red-800">
              <div className="mb-1 font-semibold">Old</div>
              <div className="line-clamp-3">{log.oldValue}</div>
            </div>
          )}
          {log.newValue && (
            <div className="rounded border border-green-100 bg-green-50 p-2 text-green-800">
              <div className="mb-1 font-semibold">New</div>
              <div className="line-clamp-3">{log.newValue}</div>
            </div>
          )}
        </div>
      )}
      {log.comment && <div className="mt-2 text-sm text-slate-600">{log.comment}</div>}
    </div>
  )
}

function cleanAction(value: string) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function AttestationCampaignCard({ campaign }: { campaign: ComplianceAttestationCampaign }) {
  const qc = useQueryClient()
  const currentUser = useAuthStore((state) => state.user)
  const [attesting, setAttesting] = useState<ComplianceAttestationRecipient | null>(null)
  const completion = campaign.recipientCount === 0 ? 0 : Math.round((campaign.attestedCount / campaign.recipientCount) * 100)
  const currentRecipient = campaign.recipients.find((recipient) => recipient.userId === currentUser?.id && recipient.status === 'Pending')

  return (
    <div className="rounded border p-3">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="font-medium text-slate-800">{campaign.name}</div>
          <div className="mt-1 text-xs text-slate-500">v{campaign.versionNumber} · Due {formatDate(campaign.dueDate)}</div>
        </div>
        <span className="rounded-full border border-blue-200 bg-blue-50 px-2 py-0.5 text-xs font-medium text-blue-700">{campaign.status}</span>
      </div>
      <div className="mt-3 h-2 rounded-full bg-slate-100">
        <div className="h-2 rounded-full bg-green-500" style={{ width: `${completion}%` }} />
      </div>
      <div className="mt-2 text-xs text-slate-500">
        {campaign.attestedCount} attested · {campaign.pendingCount} pending · {campaign.declinedCount} declined
      </div>
      {currentRecipient && (
        <button
          type="button"
          onClick={() => setAttesting(currentRecipient)}
          className="mt-3 rounded bg-green-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-green-700"
        >
          Complete My Attestation
        </button>
      )}
      <div className="mt-3 max-h-32 overflow-auto border-t pt-2">
        {campaign.recipients.map((recipient) => (
          <div key={recipient.id} className="flex items-center justify-between gap-3 py-1 text-xs">
            <span className="truncate text-slate-600">{recipient.userName || recipient.email}</span>
            <span className={recipient.status === 'Attested' ? 'text-green-700' : recipient.status === 'Declined' ? 'text-red-700' : 'text-amber-700'}>
              {recipient.status}
            </span>
          </div>
        ))}
      </div>
      {attesting && (
        <SubmitAttestationModal
          campaign={campaign}
          recipient={attesting}
          onClose={() => setAttesting(null)}
          onSubmitted={() => {
            qc.invalidateQueries({ queryKey: ['compliance-documents', campaign.documentId, 'attestations'] })
            qc.invalidateQueries({ queryKey: ['compliance-documents'] })
            setAttesting(null)
          }}
        />
      )}
    </div>
  )
}

function SubmitAttestationModal({
  campaign,
  recipient,
  onClose,
  onSubmitted,
}: {
  campaign: ComplianceAttestationCampaign
  recipient: ComplianceAttestationRecipient
  onClose: () => void
  onSubmitted: () => void
}) {
  const [comment, setComment] = useState('')
  const submitMutation = useMutation({
    mutationFn: (status: 'Attested' | 'Declined') => complianceDocumentsApi.submitAttestation(campaign.id, {
      status,
      comment: comment || null,
    }),
    onSuccess: () => {
      toast.success('Attestation recorded')
      onSubmitted()
    },
    onError: () => toast.error('Could not record attestation'),
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/30 p-4">
      <div className="w-full max-w-lg rounded border bg-white shadow-xl">
        <div className="border-b px-5 py-4">
          <h2 className="text-lg font-semibold text-slate-900">Complete Attestation</h2>
          <p className="mt-1 text-sm text-slate-500">{campaign.documentTitle} v{campaign.versionNumber}</p>
        </div>
        <div className="space-y-4 p-5">
          <div className="rounded border bg-slate-50 p-3 text-sm leading-6 text-slate-700">{campaign.statement}</div>
          <div className="text-sm text-slate-600">Recipient: {recipient.userName || recipient.email}</div>
          <label className="block text-sm font-medium text-slate-700">
            Comment
            <textarea
              value={comment}
              onChange={(event) => setComment(event.target.value)}
              rows={3}
              className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm"
              placeholder="Optional"
            />
          </label>
        </div>
        <div className="flex justify-end gap-2 border-t px-5 py-4">
          <button type="button" onClick={onClose} className="rounded border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
            Cancel
          </button>
          <button
            type="button"
            onClick={() => submitMutation.mutate('Declined')}
            disabled={submitMutation.isPending}
            className="rounded border border-red-200 px-3 py-2 text-sm font-medium text-red-700 hover:bg-red-50 disabled:opacity-50"
          >
            Decline
          </button>
          <button
            type="button"
            onClick={() => submitMutation.mutate('Attested')}
            disabled={submitMutation.isPending}
            className="rounded bg-green-600 px-3 py-2 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50"
          >
            Attest
          </button>
        </div>
      </div>
    </div>
  )
}

function AttestationModal({
  documentId,
  versionId,
  versionNumber,
  title,
  onClose,
  onCreated,
}: {
  documentId: string
  versionId: string | null
  versionNumber: number | null
  title: string
  onClose: () => void
  onCreated: () => void
}) {
  const [name, setName] = useState(versionNumber ? `${title} v${versionNumber} Attestation` : `${title} Attestation`)
  const [statement, setStatement] = useState('I acknowledge that I have reviewed and understand this document version.')
  const [dueDate, setDueDate] = useState(() => {
    const date = new Date()
    date.setDate(date.getDate() + 14)
    return date.toISOString().slice(0, 10)
  })
  const [selectedUsers, setSelectedUsers] = useState<string[]>([])

  const usersQuery = useQuery({
    queryKey: ['users', 'attestation-picker'],
    queryFn: () => usersApi.getAll({ page: 1, pageSize: 200 }),
  })

  const createMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.createAttestationCampaign(documentId, {
      versionId: versionId!,
      name,
      statement,
      dueDate,
      userIds: selectedUsers,
    }),
    onSuccess: () => {
      toast.success('Attestation campaign launched')
      onCreated()
    },
    onError: () => toast.error('Could not launch attestation campaign'),
  })

  const users = usersQuery.data?.items ?? []
  const canSubmit = !!versionId && name.trim() && statement.trim() && dueDate && selectedUsers.length > 0

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/30 p-4">
      <div className="w-full max-w-2xl rounded border bg-white shadow-xl">
        <div className="border-b px-5 py-4">
          <h2 className="text-lg font-semibold text-slate-900">Launch Attestation</h2>
          <p className="mt-1 text-sm text-slate-500">Recipients attest to the exact published version.</p>
        </div>
        <div className="space-y-4 p-5">
          <label className="block text-sm font-medium text-slate-700">
            Campaign Name
            <input value={name} onChange={(event) => setName(event.target.value)} className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm" />
          </label>
          <label className="block text-sm font-medium text-slate-700">
            Statement
            <textarea value={statement} onChange={(event) => setStatement(event.target.value)} rows={3} className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm" />
          </label>
          <label className="block text-sm font-medium text-slate-700">
            Due Date
            <input type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm" />
          </label>
          <div>
            <div className="mb-2 text-sm font-medium text-slate-700">Recipients</div>
            <div className="max-h-56 overflow-auto rounded border">
              {usersQuery.isLoading ? (
                <div className="p-4"><LoadingSpinner /></div>
              ) : users.length === 0 ? (
                <div className="p-4 text-sm text-slate-500">No users found.</div>
              ) : users.map((user) => (
                <label key={user.id} className="flex cursor-pointer items-center gap-3 border-b px-3 py-2 text-sm last:border-0 hover:bg-slate-50">
                  <input
                    type="checkbox"
                    checked={selectedUsers.includes(user.id)}
                    onChange={(event) => {
                      setSelectedUsers((current) => event.target.checked
                        ? [...current, user.id]
                        : current.filter((id) => id !== user.id))
                    }}
                  />
                  <span className="flex-1">
                    <span className="font-medium text-slate-800">{user.fullName || user.userName}</span>
                    <span className="ml-2 text-xs text-slate-500">{user.email}</span>
                  </span>
                </label>
              ))}
            </div>
          </div>
        </div>
        <div className="flex justify-end gap-2 border-t px-5 py-4">
          <button type="button" onClick={onClose} className="rounded border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
            Cancel
          </button>
          <button
            type="button"
            onClick={() => createMutation.mutate()}
            disabled={!canSubmit || createMutation.isPending}
            className="rounded bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
          >
            Launch Campaign
          </button>
        </div>
      </div>
    </div>
  )
}

function ReviewModal({
  documentId,
  reviewCadence,
  onClose,
  onSaved,
}: {
  documentId: string
  reviewCadence: string
  onClose: () => void
  onSaved: () => void
}) {
  const [status, setStatus] = useState('Completed')
  const [notes, setNotes] = useState('')
  const [nextReviewDate, setNextReviewDate] = useState(() => {
    const date = new Date()
    const cadence = reviewCadence.toLowerCase()
    if (cadence === 'quarterly') date.setMonth(date.getMonth() + 3)
    else if (cadence === 'semiannual' || cadence === 'semi-annual') date.setMonth(date.getMonth() + 6)
    else if (cadence === 'biennial') date.setFullYear(date.getFullYear() + 2)
    else date.setFullYear(date.getFullYear() + 1)
    return date.toISOString().slice(0, 10)
  })

  const reviewMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.addReview(documentId, {
      status,
      notes: notes || null,
      nextReviewDate: nextReviewDate || null,
    }),
    onSuccess: () => {
      toast.success('Review recorded')
      onSaved()
    },
    onError: () => toast.error('Could not record review'),
  })

  return (
    <SimpleModal title="Mark Reviewed" onClose={onClose}>
      <div className="space-y-4 p-5">
        <SelectField label="Review Status" value={status} values={['Completed', 'Needs Update']} onChange={setStatus} />
        <label className="block text-sm font-medium text-slate-700">
          Next Review Date
          <input type="date" value={nextReviewDate} onChange={(event) => setNextReviewDate(event.target.value)} className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm" />
        </label>
        <label className="block text-sm font-medium text-slate-700">
          Notes
          <textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={4} className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm" />
        </label>
      </div>
      <ModalActions onClose={onClose}>
        <button
          type="button"
          onClick={() => reviewMutation.mutate()}
          disabled={reviewMutation.isPending}
          className="rounded bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
        >
          Save Review
        </button>
      </ModalActions>
    </SimpleModal>
  )
}

function EvidenceModal({
  documentId,
  onClose,
  onSaved,
}: {
  documentId: string
  onClose: () => void
  onSaved: () => void
}) {
  const [title, setTitle] = useState('')
  const [evidenceType, setEvidenceType] = useState('Note')
  const [description, setDescription] = useState('')
  const [url, setUrl] = useState('')

  const evidenceMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.addEvidence(documentId, {
      title,
      evidenceType,
      description: description || null,
      url: url || null,
    }),
    onSuccess: () => {
      toast.success('Evidence added')
      onSaved()
    },
    onError: () => toast.error('Could not add evidence'),
  })

  return (
    <SimpleModal title="Add Evidence" onClose={onClose}>
      <div className="space-y-4 p-5">
        <label className="block text-sm font-medium text-slate-700">
          Title
          <input value={title} onChange={(event) => setTitle(event.target.value)} className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm" />
        </label>
        <SelectField label="Evidence Type" value={evidenceType} values={['Note', 'Link', 'Test Result', 'Training Record', 'Vendor Review', 'Exercise']} onChange={setEvidenceType} />
        <label className="block text-sm font-medium text-slate-700">
          URL
          <input value={url} onChange={(event) => setUrl(event.target.value)} className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm" />
        </label>
        <label className="block text-sm font-medium text-slate-700">
          Description
          <textarea value={description} onChange={(event) => setDescription(event.target.value)} rows={4} className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm" />
        </label>
      </div>
      <ModalActions onClose={onClose}>
        <button
          type="button"
          onClick={() => evidenceMutation.mutate()}
          disabled={!title.trim() || evidenceMutation.isPending}
          className="rounded bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
        >
          Add Evidence
        </button>
      </ModalActions>
    </SimpleModal>
  )
}

function SimpleModal({ title, onClose, children }: { title: string; onClose: () => void; children: React.ReactNode }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/30 p-4">
      <div className="w-full max-w-xl rounded border bg-white shadow-xl">
        <div className="flex items-center justify-between border-b px-5 py-4">
          <h2 className="text-lg font-semibold text-slate-900">{title}</h2>
          <button type="button" onClick={onClose} className="text-sm text-slate-500 hover:text-slate-900">Close</button>
        </div>
        {children}
      </div>
    </div>
  )
}

function ModalActions({ onClose, children }: { onClose: () => void; children: React.ReactNode }) {
  return (
    <div className="flex justify-end gap-2 border-t px-5 py-4">
      <button type="button" onClick={onClose} className="rounded border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
        Cancel
      </button>
      {children}
    </div>
  )
}

function parseTags(value: string) {
  return value.split(',').map((tag) => tag.trim()).filter(Boolean)
}

function diffClass(kind: 'Same' | 'Added' | 'Removed') {
  if (kind === 'Added') return 'rounded bg-green-100 px-0.5 text-green-900'
  if (kind === 'Removed') return 'rounded bg-red-100 px-0.5 text-red-900 line-through decoration-red-500'
  return ''
}
