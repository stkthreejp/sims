import { apiClient } from './client'
import type {
  TaskType, TaskTypeListItem,
  WorkflowTemplate, WorkflowTemplateListItem, WorkflowStep,
  SystemEvent, HolidayCalendar, EscalationRule,
} from '@/types/task.types'

export interface DatabaseTableStatus {
  name: string
  exists: boolean
}

export interface DatabaseStatus {
  canConnect: boolean
  providerName: string | null
  databaseName: string | null
  dataSource: string | null
  latestAppliedMigration: string | null
  appliedMigrations: string[]
  pendingMigrations: string[]
  expectedTables: DatabaseTableStatus[]
}

export interface FmcsaAnalyticsImportBatch {
  snapshotMonth: string
  sourceName: string
  status: string
  rowsImported: number
  startedAt: string
  completedAt: string | null
  errorMessage: string | null
}

export interface FmcsaAnalyticsStatus {
  isConfigured: boolean
  carrierPeerSnapshotCount: number
  basicPeerMeasureCount: number
  hasRunningImport: boolean
  latestBatches: FmcsaAnalyticsImportBatch[]
}

export interface FmcsaAnalyticsRefresh {
  snapshotMonth: string
  carrierCount: number
  basicMeasureCount: number
  refreshedAt: string
}

// ── Task Types ────────────────────────────────────────────────────────────────

export const adminTaskTypesApi = {
  getAll: (activeOnly = false) =>
    apiClient.get<TaskTypeListItem[]>('/admin/task-types', { params: { activeOnly } }).then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<TaskType>(`/admin/task-types/${id}`).then((r) => r.data),

  create: (data: Partial<TaskType>) =>
    apiClient.post<TaskType>('/admin/task-types', data).then((r) => r.data),

  update: (id: string, data: Partial<TaskType>) =>
    apiClient.put<TaskType>(`/admin/task-types/${id}`, data).then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`/admin/task-types/${id}`),
}

// ── Workflow Templates ────────────────────────────────────────────────────────

export const adminWorkflowsApi = {
  getAll: () =>
    apiClient.get<WorkflowTemplateListItem[]>('/admin/workflow-templates').then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<WorkflowTemplate>(`/admin/workflow-templates/${id}`).then((r) => r.data),

  create: (data: Partial<WorkflowTemplate>) =>
    apiClient.post<WorkflowTemplate>('/admin/workflow-templates', data).then((r) => r.data),

  update: (id: string, data: Partial<WorkflowTemplate>) =>
    apiClient.put<WorkflowTemplate>(`/admin/workflow-templates/${id}`, data).then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`/admin/workflow-templates/${id}`),

  setSteps: (id: string, steps: Partial<WorkflowStep>[]) =>
    apiClient.put<WorkflowTemplate>(`/admin/workflow-templates/${id}/steps`, steps).then((r) => r.data),
}

// ── System Events ─────────────────────────────────────────────────────────────

export const adminSystemEventsApi = {
  getAll: () =>
    apiClient.get<SystemEvent[]>('/admin/system-events').then((r) => r.data),
}

export const adminDatabaseApi = {
  getStatus: () =>
    apiClient.get<DatabaseStatus>('/admin/database/status').then((r) => r.data),
}

export const adminJobsApi = {
  getSafetyStatus: () =>
    apiClient.get<FmcsaAnalyticsStatus>('/fmcsa/analytics/status').then((r) => r.data),

  refreshImportedSafety: () =>
    apiClient.post<FmcsaAnalyticsRefresh>('/fmcsa/analytics/refresh-imported').then((r) => r.data),

  importSmsSample: () =>
    apiClient.post<FmcsaAnalyticsRefresh>('/fmcsa/analytics/refresh-official-sms', null, { params: { maxRowsPerDataset: 5000 } }).then((r) => r.data),

  importSmsFull: () =>
    apiClient.post<FmcsaAnalyticsRefresh>('/fmcsa/analytics/refresh-official-sms').then((r) => r.data),
}

// ── Holiday Calendar ──────────────────────────────────────────────────────────

export const adminHolidayCalendarApi = {
  getAll: () =>
    apiClient.get<HolidayCalendar[]>('/admin/holiday-calendar').then((r) => r.data),

  create: (data: { date: string; name: string }) =>
    apiClient.post<HolidayCalendar>('/admin/holiday-calendar', data).then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`/admin/holiday-calendar/${id}`),
}

// ── Escalation Rules ──────────────────────────────────────────────────────────

export const adminEscalationRulesApi = {
  getAll: () =>
    apiClient.get<EscalationRule[]>('/admin/escalation-rules').then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<EscalationRule>(`/admin/escalation-rules/${id}`).then((r) => r.data),

  create: (data: Partial<EscalationRule>) =>
    apiClient.post<EscalationRule>('/admin/escalation-rules', data).then((r) => r.data),

  update: (id: string, data: Partial<EscalationRule>) =>
    apiClient.put<EscalationRule>(`/admin/escalation-rules/${id}`, data).then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`/admin/escalation-rules/${id}`),
}
