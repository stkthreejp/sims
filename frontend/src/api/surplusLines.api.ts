import { apiClient } from './client'
import type { SurplusLinesStateSetup, SurplusLinesStateSetupUpsert } from '@/types/surplusLines.types'

export const surplusLinesApi = {
  getSetups: (includeInactive = true) =>
    apiClient.get<SurplusLinesStateSetup[]>('/admin/surplus-lines/setups', { params: { includeInactive } }).then((r) => r.data),

  getSetup: (id: string) =>
    apiClient.get<SurplusLinesStateSetup>(`/admin/surplus-lines/setups/${id}`).then((r) => r.data),

  createSetup: (data: SurplusLinesStateSetupUpsert) =>
    apiClient.post<SurplusLinesStateSetup>('/admin/surplus-lines/setups', data).then((r) => r.data),

  updateSetup: (id: string, data: SurplusLinesStateSetupUpsert) =>
    apiClient.put<SurplusLinesStateSetup>(`/admin/surplus-lines/setups/${id}`, data).then((r) => r.data),

  copySetup: (id: string, targetStateCode: string) =>
    apiClient.post<SurplusLinesStateSetup>(`/admin/surplus-lines/setups/${id}/copy`, { targetStateCode }).then((r) => r.data),
}
