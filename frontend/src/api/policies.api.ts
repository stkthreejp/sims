import { apiClient } from './client'
import type { Policy, PolicyListItem, PolicyTransaction, CreateEndorsement, IssueEndorsement, NonRenewPolicy } from '@/types/policy.types'
import type { Quote } from '@/types/quote.types'
import type { Note } from '@/types/quote.types'
import type { PagedResult, QueryParameters } from '@/types/common.types'

export const policiesApi = {
  getAll: (params: QueryParameters) =>
    apiClient.get<PagedResult<PolicyListItem>>('/policies', { params }).then((r) => r.data),

  getByInsured: (insuredId: string) =>
    apiClient.get<PolicyListItem[]>(`/policies/by-insured/${insuredId}`).then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<Policy>(`/policies/${id}`).then((r) => r.data),

  addEndorsement: (id: string, data: CreateEndorsement) =>
    apiClient.post<PolicyTransaction>(`/policies/${id}/endorsements`, data).then((r) => r.data),

  issueEndorsement: (id: string, txnId: string, data: IssueEndorsement) =>
    apiClient.post<PolicyTransaction>(`/policies/${id}/endorsements/${txnId}/issue`, data).then((r) => r.data),

  createRenewalQuote: (id: string) =>
    apiClient.post<Quote>(`/policies/${id}/renew`).then((r) => r.data),

  nonRenew: (id: string, data: NonRenewPolicy) =>
    apiClient.post<Policy>(`/policies/${id}/non-renew`, data).then((r) => r.data),

  // Notes
  getNotes: (id: string) =>
    apiClient.get<Note[]>(`/policies/${id}/notes`).then((r) => r.data),

  createNote: (id: string, data: { subject?: string; body: string }) =>
    apiClient.post<Note>(`/policies/${id}/notes`, data).then((r) => r.data),

  updateNote: (id: string, noteId: string, data: { subject?: string; body: string }) =>
    apiClient.put<Note>(`/policies/${id}/notes/${noteId}`, data).then((r) => r.data),

  deleteNote: (id: string, noteId: string) =>
    apiClient.delete(`/policies/${id}/notes/${noteId}`),

  togglePinNote: (id: string, noteId: string) =>
    apiClient.patch<Note>(`/policies/${id}/notes/${noteId}/pin`).then((r) => r.data),

  // Attachments
  getAttachments: (id: string) =>
    apiClient.get(`/policies/${id}/attachments`).then((r) => r.data),

  uploadAttachment: (id: string, file: File, documentType: string, description?: string) => {
    const formData = new FormData()
    formData.append('file', file)
    formData.append('documentType', documentType)
    if (description) formData.append('description', description)
    return apiClient.post(`/policies/${id}/attachments`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then((r) => r.data)
  },

  downloadAttachment: (id: string, attachmentId: string) =>
    `${apiClient.defaults.baseURL}/policies/${id}/attachments/${attachmentId}/download`,

  deleteAttachment: (id: string, attachmentId: string) =>
    apiClient.delete(`/policies/${id}/attachments/${attachmentId}`),
}
