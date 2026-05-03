import { apiClient } from './client'
import type { Quote, QuoteListItem, QuoteCreate, QuoteUpdate, QuoteBind, CommissionOverrideRequest, Note, Attachment } from '@/types/quote.types'
import type { PagedResult, QueryParameters } from '@/types/common.types'

export const quotesApi = {
  getAll: (params: QueryParameters) =>
    apiClient.get<PagedResult<QuoteListItem>>('/quotes', { params }).then((r) => r.data),

  getAllPolicies: (params: QueryParameters) =>
    apiClient.get<PagedResult<QuoteListItem>>('/quotes/policies', { params }).then((r) => r.data),

  getBySubmission: (submissionId: string) =>
    apiClient.get<QuoteListItem[]>(`/quotes/by-submission/${submissionId}`).then((r) => r.data),

  getBoundByInsured: (insuredId: string) =>
    apiClient.get<QuoteListItem[]>(`/quotes/bound-by-insured/${insuredId}`).then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<Quote>(`/quotes/${id}`).then((r) => r.data),

  create: (data: QuoteCreate) =>
    apiClient.post<Quote>('/quotes', data).then((r) => r.data),

  update: (id: string, data: QuoteUpdate) =>
    apiClient.put<Quote>(`/quotes/${id}`, data).then((r) => r.data),

  bind: (id: string, data: QuoteBind) =>
    apiClient.post<Quote>(`/quotes/${id}/bind`, data).then((r) => r.data),

  commissionOverride: (id: string, data: CommissionOverrideRequest) =>
    apiClient.post<Quote>(`/quotes/${id}/commission-override`, data).then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`/quotes/${id}`),

  // Notes
  getNotes: (quoteId: string) =>
    apiClient.get<Note[]>(`/quotes/${quoteId}/notes`).then((r) => r.data),

  createNote: (quoteId: string, data: { subject?: string; body: string }) =>
    apiClient.post<Note>(`/quotes/${quoteId}/notes`, data).then((r) => r.data),

  updateNote: (quoteId: string, noteId: string, data: { subject?: string; body: string }) =>
    apiClient.put<Note>(`/quotes/${quoteId}/notes/${noteId}`, data).then((r) => r.data),

  deleteNote: (quoteId: string, noteId: string) =>
    apiClient.delete(`/quotes/${quoteId}/notes/${noteId}`),

  togglePinNote: (quoteId: string, noteId: string) =>
    apiClient.patch<Note>(`/quotes/${quoteId}/notes/${noteId}/pin`).then((r) => r.data),

  // Attachments
  getAttachments: (quoteId: string) =>
    apiClient.get<Attachment[]>(`/quotes/${quoteId}/attachments`).then((r) => r.data),

  uploadAttachment: (quoteId: string, file: File, description?: string) => {
    const formData = new FormData()
    formData.append('file', file)
    if (description) formData.append('description', description)
    return apiClient.post<Attachment>(`/quotes/${quoteId}/attachments`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then((r) => r.data)
  },

  downloadAttachment: (quoteId: string, attachmentId: string) =>
    apiClient.get(`/quotes/${quoteId}/attachments/${attachmentId}/download`, { responseType: 'blob' }),

  deleteAttachment: (quoteId: string, attachmentId: string) =>
    apiClient.delete(`/quotes/${quoteId}/attachments/${attachmentId}`),
}
