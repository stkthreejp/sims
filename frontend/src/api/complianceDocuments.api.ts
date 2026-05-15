import { apiClient } from './client'
import type {
  ComplianceDocumentCreate,
  ComplianceDocumentDetail,
  ComplianceDocumentListItem,
  ComplianceDocumentSummary,
  ComplianceDocumentUpdate,
  ComplianceAttestationCampaign,
  ComplianceAuditLog,
  ComplianceEvidence,
  ComplianceVersionCompare,
} from '@/types/compliance.types'

export const complianceDocumentsApi = {
  getSummary: () =>
    apiClient.get<ComplianceDocumentSummary>('/compliance-documents/summary').then((r) => r.data),

  getAll: (filters: { status?: string; category?: string; search?: string }) =>
    apiClient.get<ComplianceDocumentListItem[]>('/compliance-documents', { params: filters }).then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<ComplianceDocumentDetail>(`/compliance-documents/${id}`).then((r) => r.data),

  getAuditLog: (id: string) =>
    apiClient.get<ComplianceAuditLog[]>(`/compliance-documents/${id}/audit-log`).then((r) => r.data),

  create: (data: ComplianceDocumentCreate) =>
    apiClient.post<ComplianceDocumentDetail>('/compliance-documents', data).then((r) => r.data),

  update: (id: string, data: ComplianceDocumentUpdate) =>
    apiClient.put<ComplianceDocumentDetail>(`/compliance-documents/${id}`, data).then((r) => r.data),

  saveDraft: (id: string, data: { htmlContent: string; changeSummary?: string | null }) =>
    apiClient.put<ComplianceDocumentDetail>(`/compliance-documents/${id}/draft`, data).then((r) => r.data),

  publishDraft: (id: string, data: { notes?: string | null; effectiveDate?: string | null }) =>
    apiClient.post<ComplianceDocumentDetail>(`/compliance-documents/${id}/publish`, data).then((r) => r.data),

  addReview: (id: string, data: { status: string; notes?: string | null; nextReviewDate?: string | null }) =>
    apiClient.post(`/compliance-documents/${id}/reviews`, data).then((r) => r.data),

  addEvidence: (id: string, data: { title: string; evidenceType: string; description?: string | null; url?: string | null }) =>
    apiClient.post<ComplianceEvidence>(`/compliance-documents/${id}/evidence`, data).then((r) => r.data),

  getAttestationCampaigns: (documentId?: string | null) =>
    apiClient.get<ComplianceAttestationCampaign[]>('/compliance-documents/attestations', {
      params: { documentId },
    }).then((r) => r.data),

  createAttestationCampaign: (id: string, data: { versionId: string; name: string; statement: string; dueDate: string; userIds: string[] }) =>
    apiClient.post<ComplianceAttestationCampaign>(`/compliance-documents/${id}/attestations`, data).then((r) => r.data),

  submitAttestation: (campaignId: string, data: { status: 'Attested' | 'Declined'; comment?: string | null }) =>
    apiClient.post(`/compliance-documents/attestations/${campaignId}/submit`, data).then((r) => r.data),

  compare: (id: string, fromVersionId?: string | null, toVersionId?: string | null) =>
    apiClient.get<ComplianceVersionCompare>(`/compliance-documents/${id}/compare`, {
      params: { fromVersionId, toVersionId },
    }).then((r) => r.data),
}
