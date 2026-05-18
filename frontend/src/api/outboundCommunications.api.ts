import { apiClient } from './client'
import type {
  OutboundCommunication,
  OutboundCommunicationCreate,
  OutboundCommunicationEntityType,
  OutboundCommunicationListItem,
  OutboundCommunicationStatus,
  OutboundCommunicationUpdate,
} from '@/types/outboundCommunication.types'

export const outboundCommunicationsApi = {
  getForEntity: (entityType: OutboundCommunicationEntityType, entityId: string, policyTransactionId?: string) =>
    apiClient
      .get<OutboundCommunicationListItem[]>('/outbound-communications', { params: { entityType, entityId, policyTransactionId } })
      .then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<OutboundCommunication>(`/outbound-communications/${id}`).then((r) => r.data),

  createDraft: (data: OutboundCommunicationCreate) =>
    apiClient.post<OutboundCommunication>('/outbound-communications', data).then((r) => r.data),

  updateDraft: (id: string, data: OutboundCommunicationUpdate) =>
    apiClient.put<OutboundCommunication>(`/outbound-communications/${id}`, data).then((r) => r.data),

  updateStatus: (id: string, status: OutboundCommunicationStatus, failureReason?: string, graphMessageId?: string) =>
    apiClient
      .post<OutboundCommunication>(`/outbound-communications/${id}/status`, {
        status,
        failureReason,
        graphMessageId,
      })
      .then((r) => r.data),

  send: (id: string) =>
    apiClient.post<OutboundCommunication>(`/outbound-communications/${id}/send`).then((r) => r.data),
}
