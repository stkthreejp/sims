import { apiClient } from './client'

export type DocumentTemplate = {
  id: string
  name: string
  entityType: string
}

export type GenerateDocumentRequest = {
  templateId: string
  entityType: string
  entityId: string
}

export type GenerateDocumentResponse = {
  url: string
}

export const documentGenerationApi = {
  getTemplates: (entityType: string) =>
    apiClient.get<DocumentTemplate[]>('/document-templates', { params: { entityType } }).then((r) => r.data),

  generate: (data: GenerateDocumentRequest) =>
    apiClient.post<GenerateDocumentResponse>('/document-generation', data).then((r) => r.data),
}
