import { apiClient } from './client'
import type {
  Intermediary,
  IntermediaryBrokerageSetup,
  IntermediaryBrokerageSetupUpsert,
  IntermediaryListItem,
  IntermediaryUpsert,
} from '@/types/intermediary.types'

export const intermediariesApi = {
  getAll: (includeInactive = false) =>
    apiClient.get<IntermediaryListItem[]>('/admin/intermediaries', { params: { includeInactive } }).then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<Intermediary>(`/admin/intermediaries/${id}`).then((r) => r.data),

  create: (data: IntermediaryUpsert) =>
    apiClient.post<Intermediary>('/admin/intermediaries', data).then((r) => r.data),

  update: (id: string, data: IntermediaryUpsert) =>
    apiClient.put<Intermediary>(`/admin/intermediaries/${id}`, data).then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`/admin/intermediaries/${id}`),

  createBrokerageSetup: (intermediaryId: string, data: IntermediaryBrokerageSetupUpsert) =>
    apiClient.post<IntermediaryBrokerageSetup>(`/admin/intermediaries/${intermediaryId}/brokerage-setups`, data).then((r) => r.data),

  updateBrokerageSetup: (intermediaryId: string, setupId: string, data: IntermediaryBrokerageSetupUpsert) =>
    apiClient.put<IntermediaryBrokerageSetup>(`/admin/intermediaries/${intermediaryId}/brokerage-setups/${setupId}`, data).then((r) => r.data),

  deleteBrokerageSetup: (intermediaryId: string, setupId: string) =>
    apiClient.delete(`/admin/intermediaries/${intermediaryId}/brokerage-setups/${setupId}`),
}
