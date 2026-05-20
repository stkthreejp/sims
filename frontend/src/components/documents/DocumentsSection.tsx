import { useEffect, useId, useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Upload, Trash2, Download, FileText, ChevronDown, ChevronRight,
  Paperclip, Loader2, Plus, X, FileSearch,
} from 'lucide-react'
import { toast } from 'sonner'
import { attachmentsApi } from '@/api/attachments.api'
import type { Attachment, DocumentEntityType, DocumentType } from '@/types/attachment.types'
import { DOCUMENT_TYPE_LABELS, DOCUMENT_TYPES_BY_ENTITY } from '@/types/attachment.types'
import type { DocumentAiNormalizationPreview } from '@/types/documentAi.types'

// ── File size helper ──────────────────────────────────────────────────────────

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

// ── File icon by MIME type ─────────────────────────────────────────────────────

function fileIconColor(contentType: string): string {
  if (contentType === 'application/pdf') return 'text-red-500'
  if (contentType.startsWith('image/')) return 'text-blue-500'
  if (contentType.includes('word') || contentType.includes('document')) return 'text-blue-700'
  if (contentType.includes('excel') || contentType.includes('sheet')) return 'text-green-600'
  return 'text-slate-400'
}

// ── Upload modal ──────────────────────────────────────────────────────────────

function UploadDialog({
  entityType,
  entityId,
  onClose,
}: {
  entityType: DocumentEntityType
  entityId: string
  onClose: () => void
}) {
  const qc = useQueryClient()
  const fileRef = useRef<HTMLInputElement>(null)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [docType, setDocType] = useState<DocumentType>(DOCUMENT_TYPES_BY_ENTITY[entityType][0])
  const [description, setDescription] = useState('')
  const [dragging, setDragging] = useState(false)

  const uploadMutation = useMutation({
    mutationFn: () => attachmentsApi.upload(entityType, entityId, selectedFile!, docType, description || undefined),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['attachments', entityType, entityId] })
      toast.success('Document uploaded')
      onClose()
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Upload failed'),
  })

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault()
    setDragging(false)
    const file = e.dataTransfer.files[0]
    if (file) setSelectedFile(file)
  }

  return (
    <div className="sims-modal-backdrop">
      <div className="sims-modal max-w-md">
        <div className="sims-modal-head">
          <h2 className="sims-modal-title">Upload document</h2>
          <button type="button" onClick={onClose} className="sims-icon-btn" aria-label="Close">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="sims-modal-body space-y-4">
        <div>
          <label className="sims-field-label">Document Type *</label>
          <select
            value={docType}
            onChange={(e) => setDocType(e.target.value as DocumentType)}
            className="sims-select"
          >
            {DOCUMENT_TYPES_BY_ENTITY[entityType].map((t) => (
              <option key={t} value={t}>{DOCUMENT_TYPE_LABELS[t]}</option>
            ))}
          </select>
        </div>

        {/* Drop zone */}
        <div
          onDragOver={(e) => { e.preventDefault(); setDragging(true) }}
          onDragLeave={() => setDragging(false)}
          onDrop={handleDrop}
          onClick={() => fileRef.current?.click()}
          className="cursor-pointer rounded-lg border border-dashed p-6 text-center transition-colors"
          style={{
            borderColor: dragging ? 'var(--accent)' : 'var(--line)',
            background: dragging ? 'var(--accent-soft)' : 'var(--surface-2)',
          }}
        >
          <input
            ref={fileRef}
            type="file"
            className="hidden"
            onChange={(e) => setSelectedFile(e.target.files?.[0] ?? null)}
          />
          {selectedFile ? (
            <div className="flex items-center justify-center gap-2 text-sm">
              <FileText className={`h-5 w-5 ${fileIconColor(selectedFile.type)}`} />
              <div className="text-left">
                <p className="font-medium text-slate-700 truncate max-w-xs">{selectedFile.name}</p>
                <p className="text-xs text-slate-400">{formatBytes(selectedFile.size)}</p>
              </div>
            </div>
          ) : (
            <div>
              <Upload className="mx-auto mb-2 h-7 w-7" style={{ color: 'var(--ink-4)' }} />
              <p className="text-sm" style={{ color: 'var(--ink-3)' }}>Drop file here or <span style={{ color: 'var(--accent-ink)', fontWeight: 600 }}>browse</span></p>
              <p className="mt-1 text-xs" style={{ color: 'var(--ink-4)' }}>Up to 50 MB</p>
            </div>
          )}
        </div>

        {/* Description */}
        <div>
          <label className="sims-field-label">Description <span className="normal-case tracking-normal text-slate-400">(optional)</span></label>
          <input
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="e.g. 2024 loss runs from prior carrier"
            className="sims-input"
          />
        </div>
        </div>

        <div className="sims-modal-foot">
          <button
            onClick={onClose}
            className="sd-btn outline"
          >
            Cancel
          </button>
          <button
            onClick={() => uploadMutation.mutate()}
            disabled={!selectedFile || uploadMutation.isPending}
            className="sd-btn primary"
          >
            {uploadMutation.isPending
              ? <><Loader2 className="h-4 w-4 animate-spin" /> Uploading...</>
              : <><Upload className="h-4 w-4" /> Upload</>
            }
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Document row ──────────────────────────────────────────────────────────────

function DocumentAiPreviewDialog({
  attachment,
  preview,
  onClose,
}: {
  attachment: Attachment
  preview: DocumentAiNormalizationPreview
  onClose: () => void
}) {
  const submissionData = preview.submissionData
  const hasSubmissionData = Boolean(
    submissionData.descriptionOfOperations ||
    submissionData.dba ||
    submissionData.entityType ||
    submissionData.imCoverages
  )

  return (
    <div className="sims-modal-backdrop">
      <div className="sims-modal max-w-4xl">
        <div className="sims-modal-head">
          <h2 className="sims-modal-title">AI preview</h2>
          <button type="button" onClick={onClose} className="sims-icon-btn" aria-label="Close">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="sims-modal-body space-y-5">
          <div>
            <p className="text-sm font-medium" style={{ color: 'var(--ink)' }}>{attachment.fileName}</p>
            <p className="text-xs" style={{ color: 'var(--ink-4)' }}>Preview only. No SIMS records have been updated.</p>
          </div>

          {preview.warnings.length > 0 && (
            <div className="rounded-md border px-3 py-2 text-sm" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)', color: 'var(--ink-2)' }}>
              {preview.warnings.map((warning) => <div key={warning}>{warning}</div>)}
            </div>
          )}

          {hasSubmissionData && (
            <section>
              <h3 className="mb-2 text-sm font-semibold" style={{ color: 'var(--ink-2)' }}>Submission fields</h3>
              <div className="grid gap-2 sm:grid-cols-2">
                <PreviewField label="Operations" value={submissionData.descriptionOfOperations} />
                <PreviewField label="DBA" value={submissionData.dba} />
                <PreviewField label="Entity" value={submissionData.entityType} />
                <PreviewField label="Inland marine" value={submissionData.imCoverages ? 'Detected' : null} />
              </div>
            </section>
          )}

          {preview.lossYears.length > 0 && (
            <section>
              <h3 className="mb-2 text-sm font-semibold" style={{ color: 'var(--ink-2)' }}>Loss year preview</h3>
              <div className="overflow-x-auto rounded-md border" style={{ borderColor: 'var(--line)' }}>
                <table className="w-full text-left text-sm">
                  <thead style={{ background: 'var(--surface-2)', color: 'var(--ink-3)' }}>
                    <tr>
                      <th className="px-3 py-2 font-medium">Year</th>
                      <th className="px-3 py-2 font-medium">LOB</th>
                      <th className="px-3 py-2 font-medium">Carrier</th>
                      <th className="px-3 py-2 font-medium">Policy</th>
                      <th className="px-3 py-2 font-medium">As of</th>
                      <th className="px-3 py-2 text-right font-medium">Paid</th>
                      <th className="px-3 py-2 text-right font-medium">Reserve</th>
                      <th className="px-3 py-2 text-right font-medium">Expense</th>
                    </tr>
                  </thead>
                  <tbody>
                    {preview.lossYears.map((year) => (
                      <tr key={`${year.policyYear}-${year.policyNumber ?? ''}`} className="border-t" style={{ borderColor: 'var(--line)' }}>
                        <td className="px-3 py-2">{year.policyYear}</td>
                        <td className="px-3 py-2">{year.lineOfBusiness || '-'}</td>
                        <td className="px-3 py-2">{year.carrierName || '-'}</td>
                        <td className="px-3 py-2">{year.policyNumber || '-'}</td>
                        <td className="px-3 py-2">{year.asOfDate || '-'}</td>
                        <td className="px-3 py-2 text-right">{formatMoney(year.paidOverride)}</td>
                        <td className="px-3 py-2 text-right">{formatMoney(year.reservedOverride)}</td>
                        <td className="px-3 py-2 text-right">{formatMoney(year.expenseOverride)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          )}

          {preview.fieldsRequiringReview.length > 0 && (
            <section>
              <h3 className="mb-2 text-sm font-semibold" style={{ color: 'var(--ink-2)' }}>Needs review</h3>
              <div className="max-h-56 overflow-auto rounded-md border" style={{ borderColor: 'var(--line)' }}>
                {preview.fieldsRequiringReview.slice(0, 40).map((field, index) => (
                  <div key={`${field.pageNumber}-${field.name}-${index}`} className="grid gap-2 border-b px-3 py-2 text-sm sm:grid-cols-[1fr_1fr_auto]" style={{ borderColor: 'var(--line)' }}>
                    <span className="font-medium" style={{ color: 'var(--ink)' }}>{field.name || '-'}</span>
                    <span style={{ color: 'var(--ink-2)' }}>{field.value || '-'}</span>
                    <span className="text-xs" style={{ color: 'var(--ink-4)' }}>p{field.pageNumber} {Math.round(field.confidence * 100)}%</span>
                  </div>
                ))}
              </div>
            </section>
          )}
        </div>

        <div className="sims-modal-foot">
          <button onClick={onClose} className="sd-btn primary">Close</button>
        </div>
      </div>
    </div>
  )
}

function PreviewField({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="rounded-md border px-3 py-2" style={{ borderColor: 'var(--line)' }}>
      <div className="text-xs" style={{ color: 'var(--ink-4)' }}>{label}</div>
      <div className="mt-0.5 text-sm font-medium" style={{ color: 'var(--ink)' }}>{value || '-'}</div>
    </div>
  )
}

function formatMoney(value?: number | null): string {
  if (value == null) return '-'
  return value.toLocaleString(undefined, { style: 'currency', currency: 'USD' })
}

function DocumentRow({
  attachment,
  entityType,
  entityId,
  onDelete,
  canDelete,
}: {
  attachment: Attachment
  entityType: DocumentEntityType
  entityId: string
  onDelete: () => void
  canDelete: boolean
}) {
  const [downloading, setDownloading] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [preview, setPreview] = useState<DocumentAiNormalizationPreview | null>(null)
  const canPreview = entityType === 'Submission' && (
    attachment.contentType.toLowerCase().includes('pdf') ||
    attachment.fileName.toLowerCase().endsWith('.pdf')
  )

  const previewMutation = useMutation({
    mutationFn: () => attachmentsApi.previewDocumentAi(entityId, attachment.id),
    onSuccess: (data) => setPreview(data),
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'AI preview failed'),
  })

  const handleDownload = async () => {
    setDownloading(true)
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
      setDownloading(false)
    }
  }

  return (
    <div className="group flex items-center justify-between rounded-lg px-3 py-2 hover:bg-[var(--hover)]">
      <div className="flex min-w-0 items-center gap-3">
        <FileText className={`h-4 w-4 shrink-0 ${fileIconColor(attachment.contentType)}`} />
        <div className="min-w-0">
          <p className="truncate text-sm font-medium" style={{ color: 'var(--ink)' }}>{attachment.fileName}</p>
          <div className="mt-0.5 flex items-center gap-2">
            <span className="text-xs text-slate-400">{formatBytes(attachment.fileSizeBytes)}</span>
            {attachment.description && (
              <span className="text-xs text-slate-400 truncate">- {attachment.description}</span>
            )}
            {attachment.policyVersionNumber != null && (
              <span className="rounded bg-slate-100 px-1.5 py-0.5 text-xs font-medium text-slate-500">
                v{attachment.policyVersionNumber}
              </span>
            )}
            <span className="text-xs text-slate-400">
              - {attachment.uploadedByName} - {new Date(attachment.createdAt).toLocaleDateString()}
            </span>
          </div>
        </div>
      </div>

      {confirming ? (
        <div className="ml-2 flex shrink-0 items-center gap-1">
          <span className="text-xs text-slate-500 mr-1">Delete?</span>
          <button
            onClick={() => { onDelete(); setConfirming(false) }}
            className="sd-btn danger sm"
          >
            Yes
          </button>
          <button
            onClick={() => setConfirming(false)}
            className="sd-btn outline sm"
          >
            No
          </button>
        </div>
      ) : (
        <div className="ml-2 flex shrink-0 items-center gap-1 opacity-0 transition-opacity group-hover:opacity-100 group-focus-within:opacity-100">
          {canPreview && (
            <button
              onClick={() => previewMutation.mutate()}
              disabled={previewMutation.isPending}
              className="sims-icon-btn hover:text-emerald-600"
              title="AI preview"
            >
              {previewMutation.isPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <FileSearch className="h-3.5 w-3.5" />}
            </button>
          )}
          <button
            onClick={handleDownload}
            disabled={downloading}
            className="sims-icon-btn hover:text-sky-600"
            title="Download"
          >
            {downloading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Download className="h-3.5 w-3.5" />}
          </button>
          {canDelete && (
            <button
              onClick={() => setConfirming(true)}
              className="sims-icon-btn hover:text-red-500"
              title="Delete"
            >
              <Trash2 className="h-3.5 w-3.5" />
            </button>
          )}
        </div>
      )}
      {preview && (
        <DocumentAiPreviewDialog
          attachment={attachment}
          preview={preview}
          onClose={() => setPreview(null)}
        />
      )}
    </div>
  )
}

// ── Document type zone (collapsible) ──────────────────────────────────────────

function DocumentZone({
  label,
  attachments,
  entityType,
  entityId,
  defaultOpen,
  onDelete,
  canDelete,
}: {
  label: string
  attachments: Attachment[]
  entityType: DocumentEntityType
  entityId: string
  defaultOpen: boolean
  onDelete: (id: string) => void
  canDelete: boolean
}) {
  const [open, setOpen] = useState(defaultOpen)
  const contentId = useId()

  // Open the zone when files arrive after initial load
  useEffect(() => {
    if (defaultOpen) setOpen(true)
  }, [defaultOpen])

  return (
    <div className="overflow-hidden rounded-lg border" style={{ borderColor: 'var(--line)' }}>
      <button
        onClick={() => setOpen(!open)}
        aria-expanded={open}
        aria-controls={contentId}
        className="flex w-full items-center justify-between px-4 py-2.5 transition-colors hover:bg-[var(--hover)]"
        style={{ background: 'var(--surface-2)' }}
      >
        <div className="flex items-center gap-2">
          {open ? <ChevronDown className="h-3.5 w-3.5" style={{ color: 'var(--ink-4)' }} /> : <ChevronRight className="h-3.5 w-3.5" style={{ color: 'var(--ink-4)' }} />}
          <span className="text-sm font-semibold" style={{ color: 'var(--ink-2)' }}>{label}</span>
          {attachments.length > 0 && (
            <span className="rounded-full px-1.5 py-0.5 text-xs font-medium" style={{ background: 'var(--accent-soft)', color: 'var(--accent-ink)' }}>
              {attachments.length}
            </span>
          )}
        </div>
      </button>

      {open && (
        <div id={contentId} className="px-2 py-1">
          {attachments.length === 0 ? (
            <p className="px-3 py-2 text-xs" style={{ color: 'var(--ink-4)' }}>No documents uploaded</p>
          ) : (
            attachments.map((a) => (
              <DocumentRow
                key={a.id}
                attachment={a}
                entityType={entityType}
                entityId={entityId}
                onDelete={() => onDelete(a.id)}
                canDelete={canDelete}
              />
            ))
          )}
        </div>
      )}
    </div>
  )
}

// ── Main component ─────────────────────────────────────────────────────────────

export function DocumentsSection({
  entityType,
  entityId,
  canUpload = true,
  canDelete = true,
}: {
  entityType: DocumentEntityType
  entityId: string
  canUpload?: boolean
  canDelete?: boolean
}) {
  const qc = useQueryClient()
  const [showUpload, setShowUpload] = useState(false)

  const { data: attachments = [], isLoading } = useQuery({
    queryKey: ['attachments', entityType, entityId],
    queryFn: () => attachmentsApi.getAll(entityType, entityId),
    enabled: !!entityId,
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => attachmentsApi.delete(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['attachments', entityType, entityId] })
      toast.success('Document deleted')
    },
    onError: () => toast.error('Failed to delete document'),
  })

  const docTypes = DOCUMENT_TYPES_BY_ENTITY[entityType]
  const grouped = docTypes.reduce<Record<DocumentType, Attachment[]>>((acc, t) => {
    acc[t] = attachments.filter((a) => a.documentType === t)
    return acc
  }, {} as Record<DocumentType, Attachment[]>)

  const totalCount = attachments.length

  return (
    <div className="sd-card overflow-hidden">
      {/* Header */}
      <div className="sd-card-head">
        <h3>
          <Paperclip className="h-4 w-4" style={{ color: 'var(--ink-3)' }} />
          Documents
          {totalCount > 0 && (
            <span className="cnt">{totalCount} file{totalCount !== 1 ? 's' : ''}</span>
          )}
        </h3>
        {canUpload && (
          <button
            onClick={() => setShowUpload(true)}
            className="sd-btn primary sm"
          >
            <Plus className="h-3.5 w-3.5" /> Upload
          </button>
        )}
      </div>

      {/* Zones */}
      {isLoading ? (
        <div className="flex items-center gap-2 px-4 py-5 text-sm" style={{ color: 'var(--ink-3)' }}>
          <Loader2 className="h-4 w-4 animate-spin" /> Loading documents...
        </div>
      ) : (
        <div className="space-y-2 p-4">
          {docTypes.map((t) => (
            <DocumentZone
              key={t}
              label={DOCUMENT_TYPE_LABELS[t]}
              attachments={grouped[t]}
              entityType={entityType}
              entityId={entityId}
              defaultOpen={grouped[t].length > 0}
              onDelete={(id) => deleteMutation.mutate(id)}
              canDelete={canDelete}
            />
          ))}
        </div>
      )}

      {/* Upload modal */}
      {showUpload && canUpload && (
        <UploadDialog
          entityType={entityType}
          entityId={entityId}
          onClose={() => setShowUpload(false)}
        />
      )}
    </div>
  )
}
