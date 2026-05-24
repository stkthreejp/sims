import type { PolicyLineOfBusiness } from './quote.types'

export interface ProgramConfiguration {
  id: string
  name: string
  code: string
  isActive: boolean
  notes: string | null
  createdAt: string
  updatedAt: string
  carriers: ProgramCarrier[]
}

export interface ProgramCarrier {
  id: string
  programConfigurationId: string
  carrierId: string
  carrierName: string
  isActive: boolean
  effectiveDate: string
  expirationDate: string | null
  notes: string | null
  linesOfBusiness: ProgramCarrierLineOfBusiness[]
}

export interface ProgramCarrierLineOfBusiness {
  id: string
  programCarrierId: string
  lineOfBusiness: PolicyLineOfBusiness
  lineOfBusinessLabel: string
  isActive: boolean
  effectiveDate: string
  expirationDate: string | null
  notes: string | null
  states: ProgramCarrierLobState[]
}

export interface ProgramCarrierLobState {
  id: string
  programCarrierLineOfBusinessId: string
  stateCode: string
  isActive: boolean
  effectiveDate: string
  expirationDate: string | null
  notes: string | null
}

export interface ProgramConfigurationUpsert {
  name: string
  code: string
  isActive: boolean
  notes?: string | null
}

export interface ProgramCarrierUpsert {
  carrierId: string
  isActive: boolean
  effectiveDate: string
  expirationDate?: string | null
  notes?: string | null
}

export interface ProgramCarrierLineOfBusinessUpsert {
  lineOfBusiness: PolicyLineOfBusiness
  isActive: boolean
  effectiveDate: string
  expirationDate?: string | null
  notes?: string | null
}

export interface ProgramCarrierLobStateUpsert {
  stateCode: string
  isActive: boolean
  effectiveDate: string
  expirationDate?: string | null
  notes?: string | null
}
