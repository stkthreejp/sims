import { apiClient } from './client'
import type { Insured, InsuredCreate, InsuredListItem, InsuredSummaryStats, InsuredUpdate } from '@/types/insured.types'
import type { PagedResult, QueryParameters } from '@/types/common.types'

export const insuredsApi = {
  getAll: (params: QueryParameters) =>
    apiClient.get<PagedResult<InsuredListItem>>('/insureds', { params }).then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<Insured>(`/insureds/${id}`).then((r) => r.data),

  create: (data: InsuredCreate) =>
    apiClient.post<Insured>('/insureds', data).then((r) => r.data),

  update: (id: string, data: InsuredUpdate) =>
    apiClient.put<Insured>(`/insureds/${id}`, data).then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`/insureds/${id}`),

  getSummaryStats: () =>
    apiClient.get<InsuredSummaryStats>('/insureds/summary-stats').then((r) => r.data),
}
