import type { PolicyLineOfBusiness } from './quote.types'

export interface ProgramConfiguration {
  id: string
  name: string
  code: string
  carrierId: string | null
  carrierName: string | null
  lineOfBusiness: PolicyLineOfBusiness
  stateCode: string
  isActive: boolean
  notes: string | null
  createdAt: string
  updatedAt: string
}

export interface ProgramConfigurationUpsert {
  name: string
  code: string
  carrierId: string | null
  lineOfBusiness: PolicyLineOfBusiness
  stateCode: string
  isActive: boolean
  notes?: string | null
}
