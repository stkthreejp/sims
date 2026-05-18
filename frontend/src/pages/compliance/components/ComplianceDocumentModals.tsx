import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Loader2, Upload, X } from 'lucide-react'
import { toast } from 'sonner'
import { complianceDocumentsApi } from '@/api/complianceDocuments.api'
import { usersApi } from '@/api/users.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ATTESTATION_STATUS, REVIEW_STATUS } from '@/constants/compliance'
import { formatBytes } from '@/lib/formatBytes'
import type { ComplianceAttestationCampaign, ComplianceAttestationRecipient, ComplianceEvidence } from '@/types/compliance.types'

// Shared primitives

export function SimpleModal({ title, onClose, children }: { title: string; onClose: () => void; children: React.ReactNode }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/30 p-4">
      <div className="sd-card w-full max-w-xl overflow-hidden">
        <div className="sims-modal-head flex items-center justify-between px-5 py-4">
          <h2 className="sims-modal-title">{title}</h2>
          <button type="button" onClick={onClose} className="sims-icon-btn" title="Close">
            <X className="h-4 w-4" />
          </button>
        </div>
        {children}
      </div>
    </div>
  )
}

export function ModalActions({ onClose, children }: { onClose: () => void; children: React.ReactNode }) {
  return (
    <div className="sims-modal-foot flex justify-end gap-2 px-5 py-4">
      <button type="button" onClick={onClose} className="sd-btn outline">
        Cancel
      </button>
      {children}
    </div>
  )
}

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

// Submit attestation

export function SubmitAttestationModal({
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
    mutationFn: (status: typeof ATTESTATION_STATUS.ATTESTED | typeof ATTESTATION_STATUS.DECLINED) =>
      complianceDocumentsApi.submitAttestation(campaign.id, { status, comment: comment || null }),
    onSuccess: () => { toast.success('Attestation recorded'); onSubmitted() },
    onError: () => toast.error('Could not record attestation'),
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/30 p-4">
      <div className="sd-card w-full max-w-lg overflow-hidden">
        <div className="sims-modal-head px-5 py-4">
          <h2 className="sims-modal-title">Complete Attestation</h2>
          <p className="mt-1 text-sm text-slate-500">{campaign.documentTitle} v{campaign.versionNumber}</p>
        </div>
        <div className="space-y-4 p-5">
          <div className="rounded-lg p-3 text-sm leading-6" style={{ border: '1px solid var(--line)', background: 'var(--surface-2)', color: 'var(--ink-2)' }}>
            {campaign.statement}
          </div>
          <div className="text-sm text-slate-600">Recipient: {recipient.userName || recipient.email}</div>
          <label className="block text-sm font-medium text-slate-700">
            Comment
            <textarea
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              rows={3}
              className="sims-textarea mt-1"
              placeholder="Optional"
            />
          </label>
        </div>
        <div className="sims-modal-foot flex justify-end gap-2 px-5 py-4">
          <button type="button" onClick={onClose} className="sd-btn outline">Cancel</button>
          <button
            type="button"
            onClick={() => submitMutation.mutate(ATTESTATION_STATUS.DECLINED)}
            disabled={submitMutation.isPending}
            className="sd-btn danger"
          >
            Decline
          </button>
          <button
            type="button"
            onClick={() => submitMutation.mutate(ATTESTATION_STATUS.ATTESTED)}
            disabled={submitMutation.isPending}
            className="sd-btn success"
          >
            Attest
          </button>
        </div>
      </div>
    </div>
  )
}

// Launch attestation campaign

export function AttestationModal({
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
    onSuccess: () => { toast.success('Attestation campaign launched'); onCreated() },
    onError: () => toast.error('Could not launch attestation campaign'),
  })

  const users = usersQuery.data?.items ?? []
  const canSubmit = !!versionId && name.trim() && statement.trim() && dueDate && selectedUsers.length > 0

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/30 p-4">
      <div className="sd-card w-full max-w-2xl overflow-hidden">
        <div className="sims-modal-head px-5 py-4">
          <h2 className="sims-modal-title">Launch Attestation</h2>
          <p className="mt-1 text-sm text-slate-500">Recipients attest to the exact published version.</p>
        </div>
        <div className="space-y-4 p-5">
          <label className="block text-sm font-medium text-slate-700">
            Campaign Name
            <input value={name} onChange={(e) => setName(e.target.value)} className="sims-input mt-1" />
          </label>
          <label className="block text-sm font-medium text-slate-700">
            Statement
            <textarea value={statement} onChange={(e) => setStatement(e.target.value)} rows={3} className="sims-textarea mt-1" />
          </label>
          <label className="block text-sm font-medium text-slate-700">
            Due Date
            <input type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} className="sims-input mt-1" />
          </label>
          <div>
            <div className="mb-2 text-sm font-medium text-slate-700">Recipients</div>
            <div className="max-h-56 overflow-auto rounded-lg" style={{ border: '1px solid var(--line)' }}>
              {usersQuery.isLoading ? (
                <div className="p-4"><LoadingSpinner /></div>
              ) : users.length === 0 ? (
                <div className="p-4 text-sm text-slate-500">No users found.</div>
              ) : users.map((user) => (
                <label key={user.id} className="flex cursor-pointer items-center gap-3 border-b px-3 py-2 text-sm last:border-0 hover:bg-slate-50">
                  <input
                    type="checkbox"
                    checked={selectedUsers.includes(user.id)}
                    onChange={(e) => setSelectedUsers((current) =>
                      e.target.checked ? [...current, user.id] : current.filter((id) => id !== user.id)
                    )}
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
        <div className="sims-modal-foot flex justify-end gap-2 px-5 py-4">
          <button type="button" onClick={onClose} className="sd-btn outline">Cancel</button>
          <button
            type="button"
            onClick={() => createMutation.mutate()}
            disabled={!canSubmit || createMutation.isPending}
            className="sd-btn primary"
          >
            Launch Campaign
          </button>
        </div>
      </div>
    </div>
  )
}

// Mark reviewed

export function ReviewModal({
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
  const [status, setStatus] = useState<string>(REVIEW_STATUS.COMPLETED)
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
    onSuccess: () => { toast.success('Review recorded'); onSaved() },
    onError: () => toast.error('Could not record review'),
  })

  return (
    <SimpleModal title="Mark Reviewed" onClose={onClose}>
      <div className="space-y-4 p-5">
        <SelectField
          label="Review Status"
          value={status}
          values={[REVIEW_STATUS.COMPLETED, 'Needs Update']}
          onChange={setStatus}
        />
        <label className="block text-sm font-medium text-slate-700">
          Next Review Date
          <input type="date" value={nextReviewDate} onChange={(e) => setNextReviewDate(e.target.value)} className="sims-input mt-1" />
        </label>
        <label className="block text-sm font-medium text-slate-700">
          Notes
          <textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={4} className="sims-textarea mt-1" />
        </label>
      </div>
      <ModalActions onClose={onClose}>
        <button
          type="button"
          onClick={() => reviewMutation.mutate()}
          disabled={reviewMutation.isPending}
          className="sd-btn primary"
        >
          Save Review
        </button>
      </ModalActions>
    </SimpleModal>
  )
}

// Add evidence

export function EvidenceModal({
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
    onSuccess: () => { toast.success('Evidence added'); onSaved() },
    onError: () => toast.error('Could not add evidence'),
  })

  return (
    <SimpleModal title="Add Evidence" onClose={onClose}>
      <div className="space-y-4 p-5">
        <label className="block text-sm font-medium text-slate-700">
          Title
          <input value={title} onChange={(e) => setTitle(e.target.value)} className="sims-input mt-1" />
        </label>
        <SelectField
          label="Evidence Type"
          value={evidenceType}
          values={['Note', 'Link', 'Test Result', 'Training Record', 'Vendor Review', 'Exercise']}
          onChange={setEvidenceType}
        />
        <label className="block text-sm font-medium text-slate-700">
          URL
          <input value={url} onChange={(e) => setUrl(e.target.value)} className="sims-input mt-1" />
        </label>
        <label className="block text-sm font-medium text-slate-700">
          Description
          <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={4} className="sims-textarea mt-1" />
        </label>
      </div>
      <ModalActions onClose={onClose}>
        <button
          type="button"
          onClick={() => evidenceMutation.mutate()}
          disabled={!title.trim() || evidenceMutation.isPending}
          className="sd-btn primary"
        >
          Add Evidence
        </button>
      </ModalActions>
    </SimpleModal>
  )
}

// Upload evidence attachment

export function EvidenceAttachmentUploadModal({
  evidence,
  onClose,
  onUploaded,
}: {
  evidence: ComplianceEvidence
  onClose: () => void
  onUploaded: () => void
}) {
  const [file, setFile] = useState<File | null>(null)
  const [description, setDescription] = useState('')

  const uploadMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.uploadEvidenceAttachment(evidence.id, file!, description || null),
    onSuccess: () => { toast.success('Evidence file uploaded'); onUploaded() },
    onError: (error: any) => toast.error(error?.response?.data?.errorMessage ?? 'Could not upload evidence file'),
  })

  return (
    <SimpleModal title="Upload Evidence File" onClose={onClose}>
      <div className="space-y-4 p-5">
        <div className="rounded-lg p-3 text-sm" style={{ border: '1px solid var(--line)', background: 'var(--surface-2)', color: 'var(--ink-3)' }}>
          <div className="font-medium" style={{ color: 'var(--ink)' }}>{evidence.title}</div>
          <div className="mt-1 text-xs" style={{ color: 'var(--ink-4)' }}>{evidence.evidenceType}</div>
        </div>
        <label className="block text-sm font-medium text-slate-700">
          File
          <input
            type="file"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            className="mt-1 block w-full text-sm text-slate-700 file:mr-3 file:rounded file:border-0 file:bg-slate-100 file:px-3 file:py-2 file:text-sm file:font-medium file:text-slate-700 hover:file:bg-slate-200"
          />
        </label>
        {file && (
          <div className="rounded-lg p-3 text-sm" style={{ border: '1px solid var(--line)', background: 'var(--surface)', color: 'var(--ink-3)' }}>
            {file.name} - {formatBytes(file.size)}
          </div>
        )}
        <label className="block text-sm font-medium text-slate-700">
          Description
          <input value={description} onChange={(e) => setDescription(e.target.value)} className="sims-input mt-1" />
        </label>
      </div>
      <ModalActions onClose={onClose}>
        <button
          type="button"
          onClick={() => uploadMutation.mutate()}
          disabled={!file || uploadMutation.isPending}
          className="sd-btn primary"
        >
          {uploadMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Upload className="h-4 w-4" />}
          Upload
        </button>
      </ModalActions>
    </SimpleModal>
  )
}
