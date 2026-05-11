import { apiClient } from './client'
import type { Attachment, DocumentType } from '@/types/attachment.types'

export type DocumentTemplate = {
  id: string
  name: string
  entityType: string
}

export type GenerateDocumentRequest = {
  templateId: string
  entityType: string
  entityId: string
  documentType?: DocumentType
}

export type GenerateDocumentResponse = {
  url: string
  attachment: Attachment
}

export const documentGenerationApi = {
  getTemplates: (entityType: string) =>
    apiClient.get<DocumentTemplate[]>('/document-templates', { params: { entityType } }).then((r) => r.data),

  generate: (data: GenerateDocumentRequest) =>
    apiClient.post<GenerateDocumentResponse>('/document-generation', data).then((r) => r.data),
}
