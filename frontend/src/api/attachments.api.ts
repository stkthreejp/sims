import { apiClient } from './client'
import type { Attachment, DocumentEntityType, DocumentType } from '@/types/attachment.types'
import type { DocumentAiNormalizationPreview } from '@/types/documentAi.types'

const entityPath = (entityType: DocumentEntityType): string => {
  switch (entityType) {
    case 'Submission': return 'submissions'
    case 'Policy':     return 'quotes'
    case 'Carrier':    return 'carriers'
    case 'Agent':      return 'agents'
    case 'Insured':    return 'insureds'
  }
}

export const attachmentsApi = {
  getAll: (entityType: DocumentEntityType, entityId: string) =>
    apiClient
      .get<Attachment[]>(`/${entityPath(entityType)}/${entityId}/attachments`)
      .then((r) => r.data),

  upload: (
    entityType: DocumentEntityType,
    entityId: string,
    file: File,
    documentType: DocumentType,
    description?: string,
    policyTransactionId?: string,
  ) => {
    const form = new FormData()
    form.append('file', file)
    form.append('documentType', documentType)
    if (description) form.append('description', description)
    if (policyTransactionId) form.append('policyTransactionId', policyTransactionId)
    return apiClient
      .post<Attachment>(`/${entityPath(entityType)}/${entityId}/attachments`, form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      .then((r) => r.data)
  },

  getDownloadUrl: (id: string) =>
    apiClient
      .get<{ url: string }>(`/attachments/${id}/download-url`)
      .then((r) => r.data.url),

  previewDocumentAi: (submissionId: string, attachmentId: string) =>
    apiClient
      .post<DocumentAiNormalizationPreview>(`/submissions/${submissionId}/attachments/${attachmentId}/ai-preview`)
      .then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`/attachments/${id}`),
}
