import type { PolicyLineOfBusiness } from './quote.types'

export type PolicyNumberRenewalBehavior = 'CopyBaseAndIncrementTermSuffix' | 'GenerateNewNumber'

export interface PolicyNumberSequence {
  id: string
  name: string
  format: string
  nextNumber: number
  resetAnnually: boolean
  termSuffixFormat: string
  renewalBehavior: PolicyNumberRenewalBehavior
  allowManualOverride: boolean
  isActive: boolean
  notes: string | null
}

export interface PolicyNumberSequenceUpsert {
  name: string
  format: string
  nextNumber: number
  resetAnnually: boolean
  termSuffixFormat: string
  renewalBehavior: PolicyNumberRenewalBehavior
  allowManualOverride: boolean
  isActive: boolean
  notes?: string
}

export interface PolicyNumberAssignment {
  id: string
  policyNumberSequenceId: string
  sequenceName: string
  programConfigurationId: string | null
  programName: string | null
  carrierId: string
  carrierName: string
  writingCompanyId: string | null
  lineOfBusiness: PolicyLineOfBusiness
  state: string | null
  programCarrierLineOfBusinessId: string | null
  programCarrierLobStateId: string | null
  priority: number
  isActive: boolean
}

export interface PolicyNumberAssignmentUpsert {
  policyNumberSequenceId: string
  programConfigurationId?: string
  carrierId: string
  writingCompanyId?: string
  lineOfBusiness: PolicyLineOfBusiness
  state?: string
  priority: number
  isActive: boolean
}

export interface PolicyNumberPreviewRequest {
  format: string
  nextNumber: number
  termSuffixFormat: string
  lineOfBusiness: PolicyLineOfBusiness
  state?: string
  carrierName?: string
  count: number
}

export interface PolicyNumberPreview {
  numbers: string[]
}
