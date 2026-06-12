import { apiClient } from './client'
import type {
  Agent,
  AgentListItem,
  AgentCreate,
  AgentUpdate,
  AgentLocation,
  AgentContact,
  AgentLocationInput,
  AgentContactInput,
  AgentComplianceDoc,
  AgentComplianceDocUpsert,
  AgentComplianceStatusResult,
  AgentContactLog,
  AgentContactLogCreate,
  AgentKpi,
  AgentSummaryStats,
} from '@/types/agent.types'

export const agentsApi = {
  // Core
  getAll: (activeOnly = false) =>
    apiClient.get<AgentListItem[]>('/agents', { params: { activeOnly } }).then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<Agent>(`/agents/${id}`).then((r) => r.data),

  create: (data: AgentCreate) =>
    apiClient.post<Agent>('/agents', data).then((r) => r.data),

  update: (id: string, data: AgentUpdate) =>
    apiClient.put<Agent>(`/agents/${id}`, data).then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`/agents/${id}`),

  // Locations
  addLocation: (agentId: string, data: AgentLocationInput) =>
    apiClient.post<AgentLocation>(`/agents/${agentId}/locations`, data).then((r) => r.data),

  updateLocation: (agentId: string, locationId: string, data: AgentLocationInput) =>
    apiClient.put<AgentLocation>(`/agents/${agentId}/locations/${locationId}`, data).then((r) => r.data),

  deleteLocation: (agentId: string, locationId: string) =>
    apiClient.delete(`/agents/${agentId}/locations/${locationId}`),

  // Contacts
  addContact: (agentId: string, locationId: string, data: AgentContactInput) =>
    apiClient.post<AgentContact>(`/agents/${agentId}/locations/${locationId}/contacts`, data).then((r) => r.data),

  updateContact: (agentId: string, locationId: string, contactId: string, data: AgentContactInput) =>
    apiClient
      .put<AgentContact>(`/agents/${agentId}/locations/${locationId}/contacts/${contactId}`, data)
      .then((r) => r.data),

  deleteContact: (agentId: string, locationId: string, contactId: string) =>
    apiClient.delete(`/agents/${agentId}/locations/${locationId}/contacts/${contactId}`),

  // Compliance docs
  getCompliance: (agentId: string) =>
    apiClient.get<AgentComplianceStatusResult>(`/agents/${agentId}/compliance`).then((r) => r.data),

  upsertComplianceDoc: (agentId: string, docType: string, data: AgentComplianceDocUpsert) =>
    apiClient.put<AgentComplianceDoc>(`/agents/${agentId}/compliance/${docType}`, data).then((r) => r.data),

  deleteComplianceDoc: (agentId: string, docType: string) =>
    apiClient.delete(`/agents/${agentId}/compliance/${docType}`),

  // Contact log
  getContactLog: (agentId: string) =>
    apiClient.get<AgentContactLog[]>(`/agents/${agentId}/contact-log`).then((r) => r.data),

  createContactLog: (agentId: string, data: AgentContactLogCreate) =>
    apiClient.post<AgentContactLog>(`/agents/${agentId}/contact-log`, data).then((r) => r.data),

  deleteContactLog: (agentId: string, logId: string) =>
    apiClient.delete(`/agents/${agentId}/contact-log/${logId}`),

  // KPIs and summary
  getKpi: (agentId: string) =>
    apiClient.get<AgentKpi>(`/agents/${agentId}/kpi`).then((r) => r.data),

  getSummaryStats: () =>
    apiClient.get<AgentSummaryStats>('/agents/summary-stats').then((r) => r.data),
}
