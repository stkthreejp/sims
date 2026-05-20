import { apiClient } from './client'
import type {
  AddProposedUnderwritingControlsRequest,
  AiGuidelineControlProposalFromAttachmentRequest,
  AiGuidelineControlProposalResult,
  CreateUnderwritingGuidelineDocumentRequest,
  UnderwritingGuidelineAuditLog,
  UnderwritingGuidelineControl,
  UnderwritingGuidelineDocument,
  UnderwritingControlEvaluationSummary,
  UnderwritingControlTargetType,
  UpdateUnderwritingGuidelineControlRequest,
} from '@/types/underwritingGuidelines.types'

export const underwritingGuidelinesApi = {
  getDocuments: () =>
    apiClient.get<UnderwritingGuidelineDocument[]>('/admin/underwriting-guidelines/documents').then((r) => r.data),

  createDocument: (data: CreateUnderwritingGuidelineDocumentRequest) =>
    apiClient.post<UnderwritingGuidelineDocument>('/admin/underwriting-guidelines/documents', data).then((r) => r.data),

  proposeFromAttachment: (data: AiGuidelineControlProposalFromAttachmentRequest) =>
    apiClient.post<AiGuidelineControlProposalResult>('/admin/ai-guideline-control-proposals/from-attachment', data).then((r) => r.data),

  getControls: (documentId: string) =>
    apiClient.get<UnderwritingGuidelineControl[]>(`/admin/underwriting-guidelines/documents/${documentId}/controls`).then((r) => r.data),

  addProposedControls: (documentId: string, data: AddProposedUnderwritingControlsRequest) =>
    apiClient.post<UnderwritingGuidelineControl[]>(`/admin/underwriting-guidelines/documents/${documentId}/proposed-controls`, data).then((r) => r.data),

  updateControl: (controlId: string, data: UpdateUnderwritingGuidelineControlRequest) =>
    apiClient.put<UnderwritingGuidelineControl>(`/admin/underwriting-guidelines/controls/${controlId}`, data).then((r) => r.data),

  approveControl: (controlId: string, notes?: string) =>
    apiClient.post<UnderwritingGuidelineControl>(`/admin/underwriting-guidelines/controls/${controlId}/approve`, { notes }).then((r) => r.data),

  rejectControl: (controlId: string, notes?: string) =>
    apiClient.post<UnderwritingGuidelineControl>(`/admin/underwriting-guidelines/controls/${controlId}/reject`, { notes }).then((r) => r.data),

  publishControl: (controlId: string, notes?: string) =>
    apiClient.post<UnderwritingGuidelineControl>(`/admin/underwriting-guidelines/controls/${controlId}/publish`, { notes }).then((r) => r.data),

  retireControl: (controlId: string, notes?: string) =>
    apiClient.post<UnderwritingGuidelineControl>(`/admin/underwriting-guidelines/controls/${controlId}/retire`, { notes }).then((r) => r.data),

  getAuditLog: (params?: { documentId?: string; controlId?: string }) =>
    apiClient.get<UnderwritingGuidelineAuditLog[]>('/admin/underwriting-guidelines/audit-log', { params }).then((r) => r.data),

  getEnforcementResults: (targetType: UnderwritingControlTargetType, targetId: string) =>
    apiClient.get<UnderwritingControlEvaluationSummary>(`/underwriting/control-enforcement/${targetType}/${targetId}`).then((r) => r.data),

  overrideEnforcementResult: (resultId: string, reason: string) =>
    apiClient.post<UnderwritingControlEvaluationSummary>(`/underwriting/control-enforcement/results/${resultId}/override`, { reason }).then((r) => r.data),
}
