import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { X, FileText, Loader2 } from 'lucide-react'
import { toast } from 'sonner'
import { documentGenerationApi } from '@/api/documentGeneration.api'
import { DOCUMENT_TYPE_LABELS, DOCUMENT_TYPES_BY_ENTITY, type DocumentEntityType, type DocumentType } from '@/types/attachment.types'

type Props = {
  entityType: string
  entityId: string
  attachmentEntityType?: DocumentEntityType
  attachmentEntityId?: string
  onClose: () => void
}

export function GenerateDocumentModal({ entityType, entityId, attachmentEntityType, attachmentEntityId, onClose }: Props) {
  const qc = useQueryClient()
  const [selectedTemplateId, setSelectedTemplateId] = useState('')
  const [generatedUrl, setGeneratedUrl] = useState<string | null>(null)
  const storageEntityType = attachmentEntityType ?? storageEntityForTemplate(entityType)
  const storageEntityId = attachmentEntityId ?? entityId
  const documentTypes = storageEntityType ? DOCUMENT_TYPES_BY_ENTITY[storageEntityType] : []
  const [documentType, setDocumentType] = useState<DocumentType>(defaultDocumentType(entityType, documentTypes))

  const { data: templates = [], isLoading: loadingTemplates } = useQuery({
    queryKey: ['document-templates', entityType],
    queryFn: () => documentGenerationApi.getTemplates(entityType),
  })

  const generateMutation = useMutation({
    mutationFn: () =>
      documentGenerationApi.generate({ templateId: selectedTemplateId, entityType, entityId, documentType }),
    onSuccess: (data) => {
      if (storageEntityType) {
        qc.invalidateQueries({ queryKey: ['attachments', storageEntityType, storageEntityId] })
      }
      qc.invalidateQueries({ queryKey: ['quote-attachments', storageEntityId] })
      // Don't auto-window.open here — browsers block popups opened from an async
      // callback, so the doc silently fails to open and users regenerate duplicates.
      // Show a success state with a user-clicked link instead (audit O16).
      setGeneratedUrl(data.url)
      toast.success('Document generated and saved')
    },
    onError: (e: any) =>
      toast.error(e?.response?.data?.errorMessage ?? 'Failed to generate document'),
  })

  return (
    <div className="sims-modal-backdrop">
      <div className="sims-modal">
        <div className="sims-modal-head">
          <div className="flex items-center gap-2">
            <FileText size={16} strokeWidth={1.7} style={{ color: 'var(--ink-3)' }} />
            <h2 className="sims-modal-title">Generate Document</h2>
          </div>
          <button onClick={onClose} className="sims-icon-btn" aria-label="Close">
            <X size={16} strokeWidth={1.7} />
          </button>
        </div>

        {generatedUrl ? (
          <>
            <div className="sims-modal-body space-y-3">
              <p style={{ margin: 0, fontSize: 'var(--fs-body)', color: 'var(--ink-2)' }}>
                Document generated and saved to the entity's files.
              </p>
              <a
                href={generatedUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="sd-btn primary sm"
                style={{ display: 'inline-flex', textDecoration: 'none' }}
              >
                <FileText size={14} strokeWidth={1.7} />
                Open document
              </a>
            </div>
            <div className="sims-modal-foot">
              <button onClick={onClose} className="sd-btn outline sm">Done</button>
            </div>
          </>
        ) : (
        <>
        <div className="sims-modal-body space-y-4">
          <div>
            <label className="sims-field-label">Template *</label>
            {loadingTemplates ? (
              <div className="flex items-center gap-2 py-2" style={{ color: 'var(--ink-3)', fontSize: 'var(--fs-body)' }}>
                <Loader2 size={14} className="animate-spin" />
                Loading templates...
              </div>
            ) : templates.length === 0 ? (
              <p style={{ margin: 0, padding: '8px 0', color: 'var(--ink-3)', fontSize: 'var(--fs-body)' }}>No templates available for {entityType}.</p>
            ) : (
              <select
                value={selectedTemplateId}
                onChange={(e) => setSelectedTemplateId(e.target.value)}
                className="sims-select"
              >
                <option value="">Select a template</option>
                {templates.map((t) => (
                  <option key={t.id} value={t.id}>{t.name}</option>
                ))}
              </select>
            )}
          </div>
          {documentTypes.length > 0 && (
            <div>
              <label className="sims-field-label">Save As *</label>
              <select
                value={documentType}
                onChange={(e) => setDocumentType(e.target.value as DocumentType)}
                className="sims-select"
              >
                {documentTypes.map((t) => (
                  <option key={t} value={t}>{DOCUMENT_TYPE_LABELS[t]}</option>
                ))}
              </select>
            </div>
          )}
        </div>

        <div className="sims-modal-foot">
          <button
            onClick={onClose}
            className="sd-btn outline sm"
          >
            Cancel
          </button>
          <button
            disabled={!selectedTemplateId || generateMutation.isPending}
            onClick={() => generateMutation.mutate()}
            className="sd-btn primary sm"
          >
            {generateMutation.isPending && <Loader2 size={14} className="animate-spin" />}
            Generate
          </button>
        </div>
        </>
        )}
      </div>
    </div>
  )
}

function storageEntityForTemplate(entityType: string): DocumentEntityType | undefined {
  if (entityType === 'Quote' || entityType === 'Policy') return 'Policy'
  if (entityType === 'Submission' || entityType === 'Carrier' || entityType === 'Agent') return entityType
  return undefined
}

function defaultDocumentType(entityType: string, documentTypes: DocumentType[]): DocumentType {
  const preferred = entityType === 'Quote' ? 'ProposalQuoteLetter'
    : entityType === 'Policy' ? 'PolicyForm'
    : 'Correspondence'

  return documentTypes.includes(preferred as DocumentType)
    ? preferred as DocumentType
    : documentTypes[0] ?? 'Other'
}
