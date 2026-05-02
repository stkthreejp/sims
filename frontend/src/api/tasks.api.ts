import { apiClient } from './client'
import type { TaskInstanceListItem, TaskInstance, TaskAuditEntry, TaskInstanceStatus } from '@/types/task.types'

export const tasksApi = {
  getMyQueue: () =>
    apiClient.get<TaskInstanceListItem[]>('/tasks/my-queue').then((r) => r.data),

  getByEntity: (entityType: string, entityId: string) =>
    apiClient.get<TaskInstanceListItem[]>(`/tasks/${entityType}/${entityId}`).then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<TaskInstance>(`/tasks/${id}`).then((r) => r.data),

  updateStatus: (id: string, newStatus: TaskInstanceStatus, notes?: string) =>
    apiClient.patch<TaskInstance>(`/tasks/${id}/status`, { newStatus, notes }).then((r) => r.data),

  reassign: (id: string, newUserId: string) =>
    apiClient.patch<TaskInstance>(`/tasks/${id}/reassign`, { newUserId }).then((r) => r.data),

  getAudit: (id: string) =>
    apiClient.get<TaskAuditEntry[]>(`/tasks/${id}/audit`).then((r) => r.data),
}
