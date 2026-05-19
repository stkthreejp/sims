export interface AiModelRegistry {
  id: string
  provider: string
  modelId: string
  displayName: string
  active: boolean
  allowedUseCases: string[]
  defaultUseCases: string[]
  costNotes: string | null
  retirementReviewDate: string | null
}

export interface AiUseCaseModelSetting {
  useCase: string
  model: AiModelRegistry
  promptVersion: string
  updatedByUserId: string | null
  updatedAt: string
}

export interface AiModelSettingAuditLog {
  id: string
  useCase: string
  previousAiModelRegistryId: string | null
  newAiModelRegistryId: string
  previousPromptVersion: string | null
  newPromptVersion: string
  changedByUserId: string
  changeReason: string
  changedAt: string
}

export interface UpdateAiUseCaseModelSettingRequest {
  aiModelRegistryId: string
  promptVersion: string
  changeReason: string
}
