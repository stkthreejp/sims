import { apiClient } from './client'
import type {
  ProgramCarrier,
  ProgramCarrierLineOfBusiness,
  ProgramCarrierLineOfBusinessUpsert,
  ProgramCarrierLobState,
  ProgramCarrierLobStateUpsert,
  ProgramCarrierUpsert,
  ProgramConfiguration,
  ProgramConfigurationUpsert,
  ProgramOrphanAudit,
} from '@/types/programConfiguration.types'

export const programConfigurationsApi = {
  getAll: (includeInactive = false) =>
    apiClient.get<ProgramConfiguration[]>('/admin/program-configurations', { params: { includeInactive } }).then((r) => r.data),

  // Read-only lookup for pickers — reachable by any authenticated user, unlike
  // the admin-gated getAll (which silently 403s for underwriters).
  getOptions: (includeInactive = false) =>
    apiClient.get<ProgramConfiguration[]>('/program-configurations/options', { params: { includeInactive } }).then((r) => r.data),

  getOrphanAudit: () =>
    apiClient.get<ProgramOrphanAudit>('/admin/program-configurations/orphan-audit').then((r) => r.data),

  create: (data: ProgramConfigurationUpsert) =>
    apiClient.post<ProgramConfiguration>('/admin/program-configurations', data).then((r) => r.data),

  update: (id: string, data: ProgramConfigurationUpsert) =>
    apiClient.put<ProgramConfiguration>(`/admin/program-configurations/${id}`, data).then((r) => r.data),

  addCarrier: (programId: string, data: ProgramCarrierUpsert) =>
    apiClient.post<ProgramCarrier>(`/admin/program-configurations/${programId}/carriers`, data).then((r) => r.data),

  updateCarrier: (programId: string, programCarrierId: string, data: ProgramCarrierUpsert) =>
    apiClient.put<ProgramCarrier>(`/admin/program-configurations/${programId}/carriers/${programCarrierId}`, data).then((r) => r.data),

  addLineOfBusiness: (programId: string, programCarrierId: string, data: ProgramCarrierLineOfBusinessUpsert) =>
    apiClient.post<ProgramCarrierLineOfBusiness>(`/admin/program-configurations/${programId}/carriers/${programCarrierId}/lines-of-business`, data).then((r) => r.data),

  updateLineOfBusiness: (programId: string, programCarrierId: string, programCarrierLobId: string, data: ProgramCarrierLineOfBusinessUpsert) =>
    apiClient.put<ProgramCarrierLineOfBusiness>(`/admin/program-configurations/${programId}/carriers/${programCarrierId}/lines-of-business/${programCarrierLobId}`, data).then((r) => r.data),

  addState: (programId: string, programCarrierId: string, programCarrierLobId: string, data: ProgramCarrierLobStateUpsert) =>
    apiClient.post<ProgramCarrierLobState>(`/admin/program-configurations/${programId}/carriers/${programCarrierId}/lines-of-business/${programCarrierLobId}/states`, data).then((r) => r.data),

  updateState: (programId: string, programCarrierId: string, programCarrierLobId: string, stateId: string, data: ProgramCarrierLobStateUpsert) =>
    apiClient.put<ProgramCarrierLobState>(`/admin/program-configurations/${programId}/carriers/${programCarrierId}/lines-of-business/${programCarrierLobId}/states/${stateId}`, data).then((r) => r.data),

  copyState: (programId: string, programCarrierId: string, programCarrierLobId: string, sourceStateCode: string, targetStateCode: string) =>
    apiClient.post<ProgramCarrierLobState>(`/admin/program-configurations/${programId}/carriers/${programCarrierId}/lines-of-business/${programCarrierLobId}/states/copy`, { sourceStateCode, targetStateCode }).then((r) => r.data),
}
