import { apiClient } from './client'
import type { Attachment, DocumentType } from '@/types/attachment.types'

export type DocumentTemplate = {
  id: string
  name: string
  entityType: string
  kind: 'Document' | 'Email' | 'DocumentAndEmail'
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

export type ProposalSendDraftResponse = {
  generatedDocument: GenerateDocumentResponse
  communicationId: string
}

export const documentGenerationApi = {
  getTemplates: (entityType: string) =>
    apiClient
      .get<DocumentTemplate[]>('/document-templates', { params: { entityType } })
      .then((r) => r.data.filter((t) => t.kind !== 'Email')),

  generate: (data: GenerateDocumentRequest) =>
    apiClient.post<GenerateDocumentResponse>('/document-generation', data).then((r) => r.data),

  getInlandMarineProposalHtml: (quoteId: string) =>
    apiClient
      .get<string>(`/quotes/${quoteId}/proposal/inland-marine/html`, { responseType: 'text' as any })
      .then((r) => r.data),

  saveInlandMarineProposalHtml: (quoteId: string) =>
    apiClient
      .post<GenerateDocumentResponse>(`/quotes/${quoteId}/proposal/inland-marine/html`)
      .then((r) => r.data),

  saveInlandMarineProposalPdf: (quoteId: string) =>
    apiClient
      .post<GenerateDocumentResponse>(`/quotes/${quoteId}/proposal/inland-marine/pdf`)
      .then((r) => r.data),

  createInlandMarineProposalSendDraft: (quoteId: string) =>
    apiClient
      .post<ProposalSendDraftResponse>(`/quotes/${quoteId}/proposal/inland-marine/send-draft`)
      .then((r) => r.data),
}
