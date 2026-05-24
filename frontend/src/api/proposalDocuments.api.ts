import { apiClient } from './client'
import type {
  ProposalDocumentConfiguration,
  ProposalDocumentConfigurationUpsert,
  ProposalDocumentSelection,
} from '@/types/proposalDocument.types'

export const proposalDocumentsApi = {
  getAll: (includeInactive = false) =>
    apiClient
      .get<ProposalDocumentConfiguration[]>('/proposal-document-configurations', { params: { includeInactive } })
      .then((r) => r.data),

  create: (data: ProposalDocumentConfigurationUpsert) =>
    apiClient
      .post<ProposalDocumentConfiguration>('/proposal-document-configurations', data)
      .then((r) => r.data),

  update: (id: string, data: ProposalDocumentConfigurationUpsert) =>
    apiClient
      .put<ProposalDocumentConfiguration>(`/proposal-document-configurations/${id}`, data)
      .then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`/proposal-document-configurations/${id}`),

  resolveForQuote: (quoteId: string) =>
    apiClient
      .get<ProposalDocumentSelection>(`/proposal-document-configurations/quotes/${quoteId}/selection`)
      .then((r) => r.data),
}
