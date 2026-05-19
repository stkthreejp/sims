import { apiClient } from './client'
import type { Policy, PolicyListItem, PolicyTransaction, PolicyTransactionArtifacts, CreateEndorsement, IssueEndorsement, IssuePolicy, PolicyIssuancePacket, NonRenewPolicy, CancelPolicy, LegalComplianceGuidance, CancellationReason, IssueCancellationNotice, CompleteCancellation, CompleteNonRenewal, ReinstatePolicy, StartRewritePolicy } from '@/types/policy.types'
import type { Quote } from '@/types/quote.types'
import type { Note } from '@/types/quote.types'
import type { PagedResult, QueryParameters } from '@/types/common.types'
import type { GenerateDocumentResponse } from './documentGeneration.api'

export const policiesApi = {
  getAll: (params: QueryParameters) =>
    apiClient.get<PagedResult<PolicyListItem>>('/policies', { params }).then((r) => r.data),

  getByInsured: (insuredId: string) =>
    apiClient.get<PolicyListItem[]>(`/policies/by-insured/${insuredId}`).then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<Policy>(`/policies/${id}`).then((r) => r.data),

  getCancellationReasons: () =>
    apiClient.get<CancellationReason[]>('/policies/cancellation-reasons').then((r) => r.data),

  getTransactionArtifacts: (id: string, txnId: string) =>
    apiClient.get<PolicyTransactionArtifacts>(`/policies/${id}/transactions/${txnId}/artifacts`).then((r) => r.data),

  addEndorsement: (id: string, data: CreateEndorsement) =>
    apiClient.post<PolicyTransaction>(`/policies/${id}/endorsements`, data).then((r) => r.data),

  issueEndorsement: (id: string, txnId: string, data: IssueEndorsement) =>
    apiClient.post<PolicyTransaction>(`/policies/${id}/endorsements/${txnId}/issue`, data).then((r) => r.data),

  getIssuancePacket: (id: string) =>
    apiClient.get<PolicyIssuancePacket>(`/policies/${id}/issuance-packet`).then((r) => r.data),

  generateIssuancePacketPreview: (id: string) =>
    apiClient.post<GenerateDocumentResponse>(`/policies/${id}/issuance-packet/preview`).then((r) => r.data),

  issue: (id: string, data: IssuePolicy) =>
    apiClient.post<Policy>(`/policies/${id}/issue`, data).then((r) => r.data),

  voidTestBind: (id: string, reason?: string) =>
    apiClient.post(`/policies/${id}/void-test-bind`, { reason }).then((r) => r.data),

  createRenewalQuote: (id: string) =>
    apiClient.post<Quote>(`/policies/${id}/renew`).then((r) => r.data),

  getCancellationGuidance: (id: string) =>
    apiClient.get<LegalComplianceGuidance>(`/policies/${id}/cancellation-guidance`).then((r) => r.data),

  getNonRenewalGuidance: (id: string) =>
    apiClient.get<LegalComplianceGuidance>(`/policies/${id}/non-renewal-guidance`).then((r) => r.data),

  cancel: (id: string, data: CancelPolicy) =>
    apiClient.post<Policy>(`/policies/${id}/cancel`, data).then((r) => r.data),

  issueCancellationNotice: (id: string, data: IssueCancellationNotice) =>
    apiClient.post<PolicyTransaction>(`/policies/${id}/cancellation-notice`, data).then((r) => r.data),

  completeCancellation: (id: string, txnId: string, data: CompleteCancellation) =>
    apiClient.post<Policy>(`/policies/${id}/cancellations/${txnId}/complete`, data).then((r) => r.data),

  reinstate: (id: string, data: ReinstatePolicy) =>
    apiClient.post<Policy>(`/policies/${id}/reinstate`, data).then((r) => r.data),

  startRewrite: (id: string, data: StartRewritePolicy) =>
    apiClient.post<PolicyTransaction>(`/policies/${id}/rewrite`, data).then((r) => r.data),

  completeNonRenewal: (id: string, txnId: string, data: CompleteNonRenewal) =>
    apiClient.post<Policy>(`/policies/${id}/non-renewals/${txnId}/complete`, data).then((r) => r.data),

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
