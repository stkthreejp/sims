import { apiClient } from './client'
import type { Attachment, DocumentEntityType, DocumentType } from '@/types/attachment.types'

const entityPath = (entityType: DocumentEntityType): string => {
  switch (entityType) {
    case 'Submission': return 'submissions'
    case 'Policy':     return 'quotes'
    case 'Carrier':    return 'carriers'
    case 'Agent':      return 'agents'
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
  ) => {
    const form = new FormData()
    form.append('file', file)
    form.append('documentType', documentType)
    if (description) form.append('description', description)
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

  delete: (id: string) =>
    apiClient.delete(`/attachments/${id}`),
}
