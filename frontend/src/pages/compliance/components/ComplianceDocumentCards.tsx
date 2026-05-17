import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Download, Loader2, Paperclip, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { complianceDocumentsApi } from '@/api/complianceDocuments.api'
import { ATTESTATION_STATUS } from '@/constants/compliance'
import { formatBytes } from '@/lib/formatBytes'
import { formatDateTime } from '@/lib/utils'
import { useAuthStore } from '@/store/authStore'
import type { ComplianceAttestationCampaign, ComplianceAuditLog, ComplianceEvidenceAttachment } from '@/types/compliance.types'
import { SubmitAttestationModal } from './ComplianceDocumentModals'

// HistoryPanel

export function HistoryPanel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="sd-card">
      <div className="sd-card-head">
        <h3>{title}</h3>
      </div>
      <div className="sd-card-body">
        {children}
      </div>
    </section>
  )
}

// AttestationCampaignCard

export function AttestationCampaignCard({ campaign }: { campaign: ComplianceAttestationCampaign }) {
  const qc = useQueryClient()
  const currentUser = useAuthStore((state) => state.user)
  const [attesting, setAttesting] = useState<typeof campaign.recipients[number] | null>(null)

  const completion = campaign.recipientCount === 0
    ? 0
    : Math.round((campaign.attestedCount / campaign.recipientCount) * 100)

  const currentRecipient = campaign.recipients.find(
    (r) => r.userId === currentUser?.id && r.status === ATTESTATION_STATUS.PENDING
  )

  return (
    <div className="rounded border p-3">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="font-medium text-slate-800">{campaign.name}</div>
          <div className="mt-1 text-xs text-slate-500">v{campaign.versionNumber} - Due {formatDateTime(campaign.dueDate)}</div>
        </div>
        <span className="rounded-full border border-blue-200 bg-blue-50 px-2 py-0.5 text-xs font-medium text-blue-700">
          {campaign.status}
        </span>
      </div>

      <div className="mt-3 h-2 rounded-full bg-slate-100">
        <div className="h-2 rounded-full bg-green-500" style={{ width: `${completion}%` }} />
      </div>
      <div className="mt-2 text-xs text-slate-500">
        {campaign.attestedCount} attested - {campaign.pendingCount} pending - {campaign.declinedCount} declined
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
            <span className={
              recipient.status === ATTESTATION_STATUS.ATTESTED ? 'text-green-700' :
              recipient.status === ATTESTATION_STATUS.DECLINED ? 'text-red-700' :
              'text-amber-700'
            }>
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

// AuditLogEntry

export function AuditLogEntry({ log }: { log: ComplianceAuditLog }) {
  return (
    <div className="border-b py-3 last:border-0">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="font-medium text-slate-800">{cleanAction(log.action)}</div>
          <div className="mt-1 text-xs text-slate-500">{log.userName || '-'} - {formatDateTime(log.createdAt)}</div>
        </div>
        {log.fieldName && (
          <span className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{log.fieldName}</span>
        )}
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

// EvidenceAttachmentRow

export function EvidenceAttachmentRow({ attachment, onChanged }: { attachment: ComplianceEvidenceAttachment; onChanged: () => void }) {
  const [busy, setBusy] = useState(false)

  const deleteMutation = useMutation({
    mutationFn: () => complianceDocumentsApi.deleteEvidenceAttachment(attachment.id),
    onSuccess: () => { toast.success('Evidence file deleted'); onChanged() },
    onError: () => toast.error('Could not delete evidence file'),
  })

  async function download() {
    setBusy(true)
    try {
      const url = await complianceDocumentsApi.getEvidenceAttachmentDownloadUrl(attachment.id)
      const link = document.createElement('a')
      link.href = url
      link.target = '_blank'
      link.rel = 'noopener noreferrer'
      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
    } catch {
      toast.error('Could not get download link')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex items-center justify-between gap-3 rounded-lg px-3 py-2" style={{ border: '1px solid var(--line)', background: 'var(--surface-2)' }}>
      <div className="flex min-w-0 items-center gap-2">
        <Paperclip className="h-4 w-4 shrink-0 text-slate-400" />
        <div className="min-w-0">
          <div className="truncate text-sm font-medium text-slate-700">{attachment.fileName}</div>
          <div className="text-xs text-slate-500">
            {formatBytes(attachment.fileSizeBytes)} - {attachment.uploadedByName} - {formatDateTime(attachment.createdAt)}
          </div>
        </div>
      </div>
      <div className="flex shrink-0 items-center gap-1">
        <button type="button" onClick={download} disabled={busy} className="sims-icon-btn" title="Download">
          {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
        </button>
        <button type="button" onClick={() => deleteMutation.mutate()} disabled={deleteMutation.isPending} className="sims-icon-btn" title="Delete">
          <Trash2 className="h-4 w-4" />
        </button>
      </div>
    </div>
  )
}

function cleanAction(value: string) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}
