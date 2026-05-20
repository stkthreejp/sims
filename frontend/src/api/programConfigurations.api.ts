import { apiClient } from './client'
import type { ProgramConfiguration, ProgramConfigurationUpsert } from '@/types/programConfiguration.types'

export const programConfigurationsApi = {
  getAll: (includeInactive = false) =>
    apiClient.get<ProgramConfiguration[]>('/admin/program-configurations', { params: { includeInactive } }).then((r) => r.data),

  create: (data: ProgramConfigurationUpsert) =>
    apiClient.post<ProgramConfiguration>('/admin/program-configurations', data).then((r) => r.data),

  update: (id: string, data: ProgramConfigurationUpsert) =>
    apiClient.put<ProgramConfiguration>(`/admin/program-configurations/${id}`, data).then((r) => r.data),
}
