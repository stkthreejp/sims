import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams } from 'react-router-dom'
import { AlertTriangle, ArrowLeft, Check, Download, FileText, GitCompare, Loader2, Save, Send, Upload } from 'lucide-react'
import { toast } from 'sonner'
import { complianceDocumentsApi } from '@/api/complianceDocuments.api'
import { usersApi } from '@/api/users.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { TemplateEditor } from '@/components/editor/TemplateEditor'
import { DOCUMENT_CATEGORIES, DOCUMENT_STATUS, DOCUMENT_STATUS_LIST, REVIEW_CADENCES } from '@/constants/compliance'
import { formatDate, formatDateTime } from '@/lib/utils'
import type { ComplianceEvidence } from '@/types/compliance.types'
import { AttestationCampaignCard, AuditLogEntry, EvidenceAttachmentRow, HistoryPanel } from './components/ComplianceDocumentCards'
import { AttestationModal, EvidenceAttachmentUploadModal, EvidenceModal, ReviewModal } from './components/ComplianceDocumentModals'

const TYPES = ['Policy', 'Plan', 'Procedure', 'Standard', 'Checklist', 'Evidence']

export function ComplianceDocumentDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()

  // Form state
  const [title, setTitle] = useState('')
  const [category, setCategory] = useState('IT')
  const [documentType, setDocumentType] = useState('Policy')
  const [status, setStatus] = useState<string>(DOCUMENT_STATUS.DRAFT)
  const [ownerId, setOwnerId] = useState('')
  const [approverId, setApproverId] = useState('')
  const [reviewCadence, setReviewCadence] = useState('Annual')
  const [nextReviewDate, setNextReviewDate] = useState('')
  const [tagsText, setTagsText] = useState('')
  const [content, setContent] = useState('<p></p>')
  const [changeSummary, setChangeSummary] = useState('')
  const [isDirty, setIsDirty] = useState(false)

  // UI state
  const [showCompare, setShowCompare] = useState(false)
  const [attestationOpen, setAttestationOpen] = useState(false)
  const [reviewOpen, setReviewOpen] = useState(false)
  const [evidenceOpen, setEvidenceOpen] = useState(false)
  const [evidenceUpload, setEvidenceUpload] = useState<ComplianceEvidence | null>(null)

  // Queries
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

  // Sync form state when document loads
  useEffect(() => {
    const doc = documentQuery.data
    if (!doc) return
    setTitle(doc.title)
    setCategory(doc.category)
    setDocumentType(doc.documentType)
    setStatus(doc.status)
    setOwnerId(doc.ownerId ?? '')
    setApproverId(doc.approverId ?? '')
    setReviewCadence(doc.reviewCadence)
    setNextReviewDate(doc.nextReviewDate ?? '')
    setTagsText(doc.tags.join(', '))
    setContent(doc.currentDraftVersion?.htmlContent || doc.currentPublishedVersion?.htmlContent || '<p></p>')
    setChangeSummary(doc.currentDraftVersion?.changeSummary ?? '')
    setIsDirty(false)
  }, [documentQuery.data])

  // Shared query invalidation helper
  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['compliance-documents'] })
    qc.invalidateQueries({ queryKey: ['compliance-documents', id, 'audit-log'] })
  }

  // Mutations
  const updateMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.update(id!, {
      title, category, documentType, status,
      ownerId: ownerId || null,
      approverId: approverId || null,
      reviewCadence,
      nextReviewDate: nextReviewDate || null,
      tags: parseTags(tagsText),
    }),
    onSuccess: (doc) => {
      qc.setQueryData(['compliance-documents', id], doc)
      invalidate()
      toast.success('Document details saved')
      setIsDirty(false)
    },
    onError: () => toast.error('Could not save document details'),
  })

  const draftMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.saveDraft(id!, { htmlContent: content, changeSummary: changeSummary || null }),
    onSuccess: (doc) => {
      qc.setQueryData(['compliance-documents', id], doc)
      invalidate()
      toast.success('Draft saved')
      setIsDirty(false)
    },
    onError: () => toast.error('Could not save draft'),
  })

  const publishMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.publishDraft(id!, { notes: changeSummary || null }),
    onSuccess: (doc) => {
      qc.setQueryData(['compliance-documents', id], doc)
      invalidate()
      setShowCompare(false)
      toast.success('Draft published')
    },
    onError: () => toast.error('Could not publish draft'),
  })

  const submitMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.submitForReview(id!, { notes: changeSummary || null }),
    onSuccess: (doc) => {
      qc.setQueryData(['compliance-documents', id], doc)
      invalidate()
      toast.success('Draft submitted for review')
    },
    onError: () => toast.error('Could not submit for review'),
  })

  const requireChangesMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.requireChanges(id!, { notes: changeSummary || null }),
    onSuccess: (doc) => {
      qc.setQueryData(['compliance-documents', id], doc)
      invalidate()
      toast.success('Changes requested')
    },
    onError: () => toast.error('Could not request changes'),
  })

  const exportPdfMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.exportPdf(id!),
    onSuccess: (blob) => {
      const url = URL.createObjectURL(blob)
      const link = window.document.createElement('a')
      link.href = url
      link.download = `${safeFileName(title || documentQuery.data?.title || 'compliance-document')}.pdf`
      window.document.body.appendChild(link)
      link.click()
      window.document.body.removeChild(link)
      URL.revokeObjectURL(url)
      toast.success('PDF exported')
    },
    onError: () => toast.error('Could not export PDF'),
  })

  const busy = updateMutation.isPending || draftMutation.isPending || publishMutation.isPending
    || submitMutation.isPending || requireChangesMutation.isPending || exportPdfMutation.isPending

  if (documentQuery.isLoading) return <LoadingSpinner />
  if (!documentQuery.data) return <div className="p-6 text-sm text-slate-500">Compliance document not found.</div>

  const doc = documentQuery.data
  const canCompare = !!doc.currentPublishedVersion && !!doc.currentDraftVersion

  const onDocumentChange = () => {
    qc.invalidateQueries({ queryKey: ['compliance-documents', id] })
    qc.invalidateQueries({ queryKey: ['compliance-documents', id, 'audit-log'] })
    qc.invalidateQueries({ queryKey: ['compliance-documents'] })
  }

  return (
    <div className="flex h-full flex-col" style={{ background: 'var(--surface-2)' }}>
      {/* Top bar */}
      <div className="flex flex-wrap items-center gap-3 border-b px-6 py-3" style={{ borderColor: 'var(--line)', background: 'var(--surface)' }}>
        <button type="button" onClick={() => navigate('/compliance-documentation')} className="sd-btn ghost sm">
          <ArrowLeft className="h-4 w-4" />
          Compliance
        </button>
        <div className="h-4 w-px" style={{ background: 'var(--line)' }} />
        <input
          value={title}
          onChange={(e) => { setTitle(e.target.value); setIsDirty(true) }}
          className="min-w-[240px] flex-1 border-0 bg-transparent text-sm font-semibold outline-none"
          style={{ color: 'var(--ink)' }}
        />
        <button type="button" onClick={() => updateMutation.mutate()} disabled={busy || !title.trim()} className="sd-btn outline sm">
          {updateMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
          Save Details
        </button>
        <button type="button" onClick={() => draftMutation.mutate()} disabled={busy} className="sd-btn primary sm">
          {draftMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
          Save Draft
        </button>
        <button
          type="button"
          onClick={() => publishMutation.mutate()}
          disabled={busy || !doc.currentDraftVersion || doc.status !== DOCUMENT_STATUS.UNDER_REVIEW}
          className="sd-btn success sm"
        >
          {publishMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
          Publish
        </button>
        <button type="button" onClick={() => navigate(`/compliance-documentation/${doc.id}/report`)} className="sd-btn outline sm">
          <FileText className="h-4 w-4" />
          Report
        </button>
        <button type="button" onClick={() => exportPdfMutation.mutate()} disabled={exportPdfMutation.isPending} className="sd-btn outline sm">
          {exportPdfMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
          Document PDF
        </button>
      </div>

      <div className="grid flex-1 grid-cols-1 overflow-hidden lg:grid-cols-[280px_minmax(0,1fr)]">
        {/* Sidebar */}
        <aside className="overflow-auto border-r p-4" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
          <div className="sd-card">
            <div className="sd-card-head"><h3>Document Details</h3></div>
            <div className="sd-card-body space-y-4">
              <SelectField label="Category" value={category} values={[...DOCUMENT_CATEGORIES]} onChange={(v) => { setCategory(v); setIsDirty(true) }} />
              <SelectField label="Type" value={documentType} values={TYPES} onChange={(v) => { setDocumentType(v); setIsDirty(true) }} />
              <SelectField label="Status" value={status} values={DOCUMENT_STATUS_LIST} onChange={(v) => { setStatus(v); setIsDirty(true) }} />
              <UserSelectField label="Owner" value={ownerId} users={usersQuery.data?.items ?? []} onChange={(v) => { setOwnerId(v); setIsDirty(true) }} />
              <UserSelectField label="Approver" value={approverId} users={usersQuery.data?.items ?? []} onChange={(v) => { setApproverId(v); setIsDirty(true) }} />
              <SelectField label="Review Cadence" value={reviewCadence} values={[...REVIEW_CADENCES]} onChange={(v) => { setReviewCadence(v); setIsDirty(true) }} />
              <label className="block text-sm font-medium text-slate-700">
                Next Review
                <input type="date" value={nextReviewDate} onChange={(e) => { setNextReviewDate(e.target.value); setIsDirty(true) }} className="sims-input mt-1" />
              </label>
              <label className="block text-sm font-medium text-slate-700">
                Tags
                <textarea value={tagsText} onChange={(e) => { setTagsText(e.target.value); setIsDirty(true) }} rows={3} placeholder="Separate with commas" className="sims-textarea mt-1" />
              </label>
              <label className="block text-sm font-medium text-slate-700">
                Change Summary
                <textarea value={changeSummary} onChange={(e) => { setChangeSummary(e.target.value); setIsDirty(true) }} rows={4} placeholder="What changed in this draft?" className="sims-textarea mt-1" />
              </label>
              <div className="rounded-lg p-3 text-sm" style={{ border: '1px solid var(--line)', background: 'var(--surface-2)', color: 'var(--ink-3)' }}>
                <div className="font-medium" style={{ color: 'var(--ink)' }}>Review</div>
                <div className="mt-2">Owner: {doc.ownerName ?? '-'}</div>
                <div>Approver: {doc.approverName ?? '-'}</div>
                <div className="mt-2">Last reviewed: {formatDate(doc.lastReviewedDate)}</div>
                <div>Next review: {formatDate(doc.nextReviewDate)}</div>
                <div>Effective: {formatDate(doc.effectiveDate)}</div>
                {isDirty && <div className="mt-2 text-xs font-medium" style={{ color: 'var(--warn-fg)' }}>Unsaved changes</div>}
              </div>
            </div>
          </div>
        </aside>

        {/* Main content */}
        <main className="overflow-auto p-6">
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div>
              <div className="text-sm font-semibold" style={{ color: 'var(--ink)' }}>Document Editor</div>
              <div className="text-xs" style={{ color: 'var(--ink-4)' }}>
                Published {doc.currentPublishedVersion ? `v${doc.currentPublishedVersion.versionNumber}` : 'none'} · Draft {doc.currentDraftVersion ? `v${doc.currentDraftVersion.versionNumber}` : 'none'}
              </div>
            </div>
            <button type="button" onClick={() => setShowCompare((v) => !v)} disabled={!canCompare} className="sd-btn outline">
              <GitCompare className="h-4 w-4" />
              Compare Draft
            </button>
          </div>

          {/* Workflow bar */}
          <section className="sd-card mb-5">
            <div className="sd-card-body">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <div className="text-sm font-semibold" style={{ color: 'var(--ink)' }}>Review Workflow</div>
                  <div className="mt-1 text-xs" style={{ color: 'var(--ink-4)' }}>Save a draft, submit it for review, then approve or request changes.</div>
                </div>
                <div className="flex flex-wrap gap-2">
                  <button
                    type="button"
                    onClick={() => submitMutation.mutate()}
                    disabled={busy || !doc.currentDraftVersion || doc.status === DOCUMENT_STATUS.UNDER_REVIEW}
                    className="sd-btn outline"
                  >
                    <Send className="h-4 w-4" />
                    Submit for Review
                  </button>
                  <button
                    type="button"
                    onClick={() => requireChangesMutation.mutate()}
                    disabled={busy || !doc.currentDraftVersion || doc.status !== DOCUMENT_STATUS.UNDER_REVIEW}
                    className="sd-btn outline"
                  >
                    <AlertTriangle className="h-4 w-4" />
                    Require Changes
                  </button>
                </div>
              </div>
            </div>
          </section>

          {/* Diff view */}
          {showCompare && canCompare && (
            <section className="sd-card mb-5">
              <div className="sd-card-head"><h3>Changes from published version</h3></div>
              <div className="sd-card-body">
                {compareQuery.isLoading ? (
                  <LoadingSpinner />
                ) : (
                  <p className="whitespace-pre-wrap text-sm leading-7" style={{ color: 'var(--ink-2)' }}>
                    {compareQuery.data?.parts.map((part, i) => (
                      <span key={`${part.kind}-${i}`} className={diffClass(part.kind)}>{part.text}</span>
                    ))}
                  </p>
                )}
              </div>
            </section>
          )}

          <TemplateEditor content={content} onChange={(html) => { setContent(html); setIsDirty(true) }} entityType="General" />

          {/* Bottom panels */}
          <section className="mt-6 grid grid-cols-1 gap-4 lg:grid-cols-2">
            <HistoryPanel title="Version History">
              {doc.versions.length === 0 ? (
                <div className="text-sm text-slate-500">No versions yet.</div>
              ) : doc.versions.map((version) => (
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
                <button type="button" onClick={() => setAttestationOpen(true)} disabled={!doc.currentPublishedVersion} className="sd-btn primary sm">
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
                <button type="button" onClick={() => setReviewOpen(true)} className="sd-btn outline sm">Mark Reviewed</button>
                <button type="button" onClick={() => setEvidenceOpen(true)} className="sd-btn outline sm">
                  <Upload className="h-3.5 w-3.5" />
                  Add Evidence
                </button>
              </div>
              {doc.reviews.length === 0 && doc.evidenceItems.length === 0 ? (
                <div className="text-sm text-slate-500">No reviews or evidence yet.</div>
              ) : (
                <>
                  {doc.reviews.map((review) => (
                    <div key={review.id} className="border-b py-3">
                      <div className="font-medium text-slate-800">{review.status}</div>
                      <div className="mt-1 text-xs text-slate-500">{review.reviewedByName} · {formatDateTime(review.reviewedAt)}</div>
                      {review.notes && <div className="mt-2 text-sm text-slate-600">{review.notes}</div>}
                    </div>
                  ))}
                  {doc.evidenceItems.map((evidence) => (
                    <div key={evidence.id} className="border-b py-3 last:border-0">
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <div className="font-medium text-slate-800">{evidence.title}</div>
                          <div className="mt-1 text-xs text-slate-500">{evidence.evidenceType} · {formatDateTime(evidence.createdAt)}</div>
                        </div>
                        <button type="button" onClick={() => setEvidenceUpload(evidence)} className="sd-btn outline sm">
                          <Upload className="h-3.5 w-3.5" />
                          File
                        </button>
                      </div>
                      {evidence.description && <div className="mt-2 text-sm text-slate-600">{evidence.description}</div>}
                      {evidence.attachments.length > 0 && (
                        <div className="mt-3 space-y-1">
                          {evidence.attachments.map((attachment) => (
                            <EvidenceAttachmentRow key={attachment.id} attachment={attachment} onChanged={onDocumentChange} />
                          ))}
                        </div>
                      )}
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

      {/* Modals */}
      {attestationOpen && (
        <AttestationModal
          documentId={doc.id}
          versionId={doc.currentPublishedVersion?.id ?? null}
          versionNumber={doc.currentPublishedVersion?.versionNumber ?? null}
          title={doc.title}
          onClose={() => setAttestationOpen(false)}
          onCreated={() => {
            qc.invalidateQueries({ queryKey: ['compliance-documents', id, 'attestations'] })
            onDocumentChange()
            setAttestationOpen(false)
          }}
        />
      )}
      {reviewOpen && (
        <ReviewModal
          documentId={doc.id}
          reviewCadence={doc.reviewCadence}
          onClose={() => setReviewOpen(false)}
          onSaved={() => { onDocumentChange(); setReviewOpen(false) }}
        />
      )}
      {evidenceOpen && (
        <EvidenceModal
          documentId={doc.id}
          onClose={() => setEvidenceOpen(false)}
          onSaved={() => { onDocumentChange(); setEvidenceOpen(false) }}
        />
      )}
      {evidenceUpload && (
        <EvidenceAttachmentUploadModal
          evidence={evidenceUpload}
          onClose={() => setEvidenceUpload(null)}
          onUploaded={() => { onDocumentChange(); setEvidenceUpload(null) }}
        />
      )}
    </div>
  )
}

// ─── Local helpers ────────────────────────────────────────────────────────────

function SelectField({ label, value, values, onChange }: { label: string; value: string; values: string[]; onChange: (value: string) => void }) {
  return (
    <label className="block text-sm font-medium text-slate-700">
      {label}
      <select value={value} onChange={(e) => onChange(e.target.value)} className="sims-select mt-1">
        {values.map((option) => <option key={option} value={option}>{option}</option>)}
      </select>
    </label>
  )
}

function UserSelectField({ label, value, users, onChange }: { label: string; value: string; users: { id: string; fullName?: string; userName?: string }[]; onChange: (value: string) => void }) {
  return (
    <label className="block text-sm font-medium text-slate-700">
      {label}
      <select value={value} onChange={(e) => onChange(e.target.value)} className="sims-select mt-1">
        <option value="">Unassigned</option>
        {users.map((user) => (
          <option key={user.id} value={user.id}>{user.fullName || user.userName}</option>
        ))}
      </select>
    </label>
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

function safeFileName(value: string) {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '') || 'compliance-document'
}
