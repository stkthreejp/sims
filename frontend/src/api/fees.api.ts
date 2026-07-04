import { apiClient } from './client'
import type { FeeDefinition, FeeRuleVersion, FeeAuditLogEntry, LedgerAccountOption, PayeeOption } from '@/types/fee.types'

export const feesApi = {
  getLedgerAccounts: () =>
    apiClient.get<LedgerAccountOption[]>('/admin/fees/ledger-accounts').then((r) => r.data),

  getPayees: (includeInactive = false) =>
    apiClient.get<PayeeOption[]>('/admin/fees/payees', { params: { includeInactive } }).then((r) => r.data),

  createPayee: (data: { name: string; payeeType: string; externalReference?: string | null; isActive?: boolean }) =>
    apiClient.post<PayeeOption>('/admin/fees/payees', data).then((r) => r.data),

  updatePayee: (id: number, data: { name: string; payeeType: string; externalReference?: string | null; isActive?: boolean }) =>
    apiClient.put<PayeeOption>(`/admin/fees/payees/${id}`, data).then((r) => r.data),

  // Definitions
  getDefinitions: () =>
    apiClient.get<FeeDefinition[]>('/admin/fees/definitions').then((r) => r.data),

  getDefinition: (id: number) =>
    apiClient.get<FeeDefinition>(`/admin/fees/definitions/${id}`).then((r) => r.data),

  createDefinition: (data: Omit<FeeDefinition, 'id'>) =>
    apiClient.post<FeeDefinition>('/admin/fees/definitions', data).then((r) => r.data),

  // Versions
  getVersions: (feeDefinitionId: number) =>
    apiClient.get<FeeRuleVersion[]>(`/admin/fees/definitions/${feeDefinitionId}/versions`).then((r) => r.data),

  getVersion: (id: number) =>
    apiClient.get<FeeRuleVersion>(`/admin/fees/versions/${id}`).then((r) => r.data),

  createVersion: (data: Omit<FeeRuleVersion, 'id' | 'feeCode' | 'feeDisplayName' | 'nonTaxableStates'>) =>
    apiClient.post<FeeRuleVersion>('/admin/fees/versions', data).then((r) => r.data),

  newVersionFromExisting: (existingId: number, data: Omit<FeeRuleVersion, 'id' | 'feeCode' | 'feeDisplayName' | 'nonTaxableStates'>) =>
    apiClient.post<FeeRuleVersion>(`/admin/fees/versions/${existingId}/new-version`, data).then((r) => r.data),

  disableVersion: (id: number, disabledDate: string, notes?: string) =>
    apiClient.post(`/admin/fees/versions/${id}/disable`, { disabledDate, notes }),

  // State taxability
  setStateTaxability: (feeDefinitionId: number, nonTaxableStateCodes: string[]) =>
    apiClient.put(`/admin/fees/definitions/${feeDefinitionId}/state-taxability`, { nonTaxableStateCodes }),

  // Audit log
  getAuditLog: (versionId: number) =>
    apiClient.get<FeeAuditLogEntry[]>(`/admin/fees/versions/${versionId}/audit-log`).then((r) => r.data),
}
