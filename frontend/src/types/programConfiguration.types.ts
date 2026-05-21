export interface ProgramConfiguration {
  id: string
  name: string
  code: string
  isActive: boolean
  notes: string | null
  createdAt: string
  updatedAt: string
}

export interface ProgramConfigurationUpsert {
  name: string
  code: string
  isActive: boolean
  notes?: string | null
}
