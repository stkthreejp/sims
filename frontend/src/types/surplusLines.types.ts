import type { PolicyLineOfBusiness } from './quote.types'

export interface SurplusLinesStateSetup {
  id: string
  stateCode: string
  programConfigurationId: string | null
  programName: string | null
  carrierId: string | null
  carrierName: string | null
  lineOfBusiness: PolicyLineOfBusiness | null
  lineOfBusinessLabel: string | null
  effectiveDate: string
  expirationDate: string | null
  isActive: boolean
  filingRequired: boolean
  licenseHolderType: string
  filingBrokerName: string
  licenseNumber: string
  licenseState: string
  brokerAddressLine1: string
  brokerAddressLine2: string | null
  brokerCity: string
  brokerState: string
  brokerZipCode: string
  brokerCountry: string
  stampingWording: string | null
  requiredNoticeText: string | null
  paperworkNotes: string | null
  filingNotes: string | null
  surplusLinesTaxFeeDefinitionId: number | null
  surplusLinesTaxFeeName: string | null
  stampingFeeDefinitionId: number | null
  stampingFeeName: string | null
  filingFeeDefinitionId: number | null
  filingFeeName: string | null
  feeValidationMessages: string[]
  createdAt: string
  updatedAt: string
}

export interface SurplusLinesStateSetupUpsert {
  stateCode: string
  programConfigurationId: string | null
  carrierId: string | null
  lineOfBusiness: PolicyLineOfBusiness | null
  effectiveDate: string
  expirationDate: string | null
  isActive: boolean
  filingRequired: boolean
  licenseHolderType: string
  filingBrokerName: string
  licenseNumber: string
  licenseState: string
  brokerAddressLine1: string
  brokerAddressLine2: string | null
  brokerCity: string
  brokerState: string
  brokerZipCode: string
  brokerCountry: string
  stampingWording: string | null
  requiredNoticeText: string | null
  paperworkNotes: string | null
  filingNotes: string | null
  surplusLinesTaxFeeDefinitionId: number | null
  stampingFeeDefinitionId: number | null
  filingFeeDefinitionId: number | null
}
