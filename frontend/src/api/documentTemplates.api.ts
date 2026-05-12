import { apiClient } from './client'
import type { DocumentTemplate, DocumentTemplateListItem, DocumentTemplateCreate, DocumentTemplateUpdate, TemplateEntityType, DocumentTemplateKind } from '@/types/documentTemplate.types'

export const documentTemplatesApi = {
  getAll: (entityType?: TemplateEntityType, includeInactive = false, kind?: DocumentTemplateKind) =>
    apiClient
      .get<DocumentTemplateListItem[]>('/document-templates', { params: { entityType, includeInactive, kind } })
      .then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<DocumentTemplate>(`/document-templates/${id}`).then((r) => r.data),

  create: (data: DocumentTemplateCreate) =>
    apiClient.post<DocumentTemplate>('/document-templates', data).then((r) => r.data),

  update: (id: string, data: DocumentTemplateUpdate) =>
    apiClient.put<DocumentTemplate>(`/document-templates/${id}`, data).then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`/document-templates/${id}`),
}
