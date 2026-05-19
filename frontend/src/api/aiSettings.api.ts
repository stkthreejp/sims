import { apiClient } from './client'
import type {
  AiModelRegistry,
  AiModelSettingAuditLog,
  AiUseCaseModelSetting,
  UpdateAiUseCaseModelSettingRequest,
} from '@/types/aiSettings.types'

export const aiSettingsApi = {
  getModels: () =>
    apiClient.get<AiModelRegistry[]>('/admin/ai-settings/models').then((r) => r.data),

  getSettings: () =>
    apiClient.get<AiUseCaseModelSetting[]>('/admin/ai-settings/settings').then((r) => r.data),

  updateSetting: (useCase: string, data: UpdateAiUseCaseModelSettingRequest) =>
    apiClient.put<AiUseCaseModelSetting>(`/admin/ai-settings/settings/${useCase}`, data).then((r) => r.data),

  getAuditLog: () =>
    apiClient.get<AiModelSettingAuditLog[]>('/admin/ai-settings/audit-log').then((r) => r.data),
}
