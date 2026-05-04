import { useEffect, useId, useRef, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Upload, Trash2, Download, FileText, ChevronDown, ChevronRight,
  Paperclip, Loader2, Plus,
} from 'lucide-react'
import { toast } from 'sonner'
import { attachmentsApi } from '@/api/attachments.api'
import type { Attachment, DocumentEntityType, DocumentType } from '@/types/attachment.types'
import { DOCUMENT_TYPE_LABELS, DOCUMENT_TYPES_BY_ENTITY } from '@/types/attachment.types'

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
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md p-6 space-y-4">
        <h2 className="text-base font-semibold text-slate-800">Upload Document</h2>

        {/* Document type */}
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">Document Type *</label>
          <select
            value={docType}
            onChange={(e) => setDocType(e.target.value as DocumentType)}
            className="w-full border border-slate-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
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
          className={`border-2 border-dashed rounded-lg p-6 text-center cursor-pointer transition-colors ${
            dragging ? 'border-blue-400 bg-blue-50' : 'border-slate-200 hover:border-blue-300 hover:bg-slate-50'
          }`}
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
              <Upload className="h-7 w-7 text-slate-300 mx-auto mb-2" />
              <p className="text-sm text-slate-500">Drop file here or <span className="text-blue-600">browse</span></p>
              <p className="text-xs text-slate-400 mt-1">Up to 50 MB</p>
            </div>
          )}
        </div>

        {/* Description */}
        <div>
          <label className="block text-xs font-medium text-slate-600 mb-1">Description <span className="text-slate-400">(optional)</span></label>
          <input
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="e.g. 2024 loss runs from prior carrier"
            className="w-full border border-slate-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        <div className="flex gap-2 pt-1">
          <button
            onClick={() => uploadMutation.mutate()}
            disabled={!selectedFile || uploadMutation.isPending}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-md text-sm hover:bg-blue-700 disabled:opacity-40"
          >
            {uploadMutation.isPending
              ? <><Loader2 className="h-4 w-4 animate-spin" /> Uploading…</>
              : <><Upload className="h-4 w-4" /> Upload</>
            }
          </button>
          <button
            onClick={onClose}
            className="px-4 py-2 border border-slate-300 rounded-md text-sm hover:bg-slate-50"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Document row ──────────────────────────────────────────────────────────────

function DocumentRow({
  attachment,
  onDelete,
  canDelete,
}: {
  attachment: Attachment
  onDelete: () => void
  canDelete: boolean
}) {
  const [downloading, setDownloading] = useState(false)
  const [confirming, setConfirming] = useState(false)

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
    <div className="flex items-center justify-between py-2 px-3 rounded-lg hover:bg-slate-50 group">
      <div className="flex items-center gap-3 min-w-0">
        <FileText className={`h-4 w-4 shrink-0 ${fileIconColor(attachment.contentType)}`} />
        <div className="min-w-0">
          <p className="text-sm text-slate-800 truncate">{attachment.fileName}</p>
          <div className="flex items-center gap-2 mt-0.5">
            <span className="text-xs text-slate-400">{formatBytes(attachment.fileSizeBytes)}</span>
            {attachment.description && (
              <span className="text-xs text-slate-400 truncate">· {attachment.description}</span>
            )}
            <span className="text-xs text-slate-400">
              · {attachment.uploadedByName} · {new Date(attachment.createdAt).toLocaleDateString()}
            </span>
          </div>
        </div>
      </div>

      {confirming ? (
        <div className="flex items-center gap-1 shrink-0 ml-2">
          <span className="text-xs text-slate-500 mr-1">Delete?</span>
          <button
            onClick={() => { onDelete(); setConfirming(false) }}
            className="px-2 py-1 rounded text-xs bg-red-600 text-white hover:bg-red-700"
          >
            Yes
          </button>
          <button
            onClick={() => setConfirming(false)}
            className="px-2 py-1 rounded text-xs border border-slate-300 hover:bg-slate-50"
          >
            No
          </button>
        </div>
      ) : (
        <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity shrink-0 ml-2">
          <button
            onClick={handleDownload}
            disabled={downloading}
            className="p-1.5 rounded text-slate-400 hover:text-blue-600 hover:bg-blue-50"
            title="Download"
          >
            {downloading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Download className="h-3.5 w-3.5" />}
          </button>
          {canDelete && (
            <button
              onClick={() => setConfirming(true)}
              className="p-1.5 rounded text-slate-400 hover:text-red-600 hover:bg-red-50"
              title="Delete"
            >
              <Trash2 className="h-3.5 w-3.5" />
            </button>
          )}
        </div>
      )}
    </div>
  )
}

// ── Document type zone (collapsible) ──────────────────────────────────────────

function DocumentZone({
  label,
  attachments,
  defaultOpen,
  onDelete,
  canDelete,
}: {
  label: string
  attachments: Attachment[]
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
    <div className="border border-slate-200 rounded-lg overflow-hidden">
      <button
        onClick={() => setOpen(!open)}
        aria-expanded={open}
        aria-controls={contentId}
        className="w-full flex items-center justify-between px-4 py-2.5 bg-slate-50 hover:bg-slate-100 transition-colors"
      >
        <div className="flex items-center gap-2">
          {open ? <ChevronDown className="h-3.5 w-3.5 text-slate-400" /> : <ChevronRight className="h-3.5 w-3.5 text-slate-400" />}
          <span className="text-sm font-medium text-slate-700">{label}</span>
          {attachments.length > 0 && (
            <span className="px-1.5 py-0.5 text-xs rounded-full bg-blue-100 text-blue-700 font-medium">
              {attachments.length}
            </span>
          )}
        </div>
      </button>

      {open && (
        <div id={contentId} className="px-2 py-1">
          {attachments.length === 0 ? (
            <p className="text-xs text-slate-400 px-3 py-2 italic">No documents uploaded</p>
          ) : (
            attachments.map((a) => (
              <DocumentRow key={a.id} attachment={a} onDelete={() => onDelete(a.id)} canDelete={canDelete} />
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
    <div className="space-y-3">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Paperclip className="h-4 w-4 text-slate-400" />
          <h2 className="text-base font-semibold text-slate-800">Documents</h2>
          {totalCount > 0 && (
            <span className="text-xs text-slate-400">({totalCount} file{totalCount !== 1 ? 's' : ''})</span>
          )}
        </div>
        {canUpload && (
          <button
            onClick={() => setShowUpload(true)}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-600 text-white rounded-md text-sm hover:bg-blue-700"
          >
            <Plus className="h-3.5 w-3.5" /> Upload
          </button>
        )}
      </div>

      {/* Zones */}
      {isLoading ? (
        <div className="flex items-center gap-2 py-4 text-sm text-slate-400">
          <Loader2 className="h-4 w-4 animate-spin" /> Loading documents…
        </div>
      ) : (
        <div className="space-y-2">
          {docTypes.map((t) => (
            <DocumentZone
              key={t}
              label={DOCUMENT_TYPE_LABELS[t]}
              attachments={grouped[t]}
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
