import type { PolicyLineOfBusiness } from './quote.types'

export interface IntermediaryListItem {
  id: string
  name: string
  referenceNumber: string | null
  email: string | null
  phone: string | null
  city: string | null
  state: string | null
  isActive: boolean
  brokerageSetupCount: number
  activeBrokerageSetupCount: number
}

export interface IntermediaryBrokerageSetup {
  id: string
  intermediaryId: string
  programConfigurationId: string
  programName: string
  carrierId: string
  carrierName: string
  lineOfBusiness: PolicyLineOfBusiness | null
  lineOfBusinessLabel: string
  effectiveDate: string
  expirationDate: string | null
  brokerageRate: number | null
  createPayable: boolean
  payablePayeeId: number | null
  payablePayeeName: string | null
  isActive: boolean
  notes: string | null
}

export interface Intermediary {
  id: string
  name: string
  referenceNumber: string | null
  email: string | null
  phone: string | null
  addressLine1: string | null
  addressLine2: string | null
  city: string | null
  state: string | null
  zipCode: string | null
  country: string | null
  bankName: string | null
  bankAccountName: string | null
  bankAccountLast4: string | null
  bankRoutingNumber: string | null
  bankSwiftCode: string | null
  bankInstructions: string | null
  isActive: boolean
  notes: string | null
  createdAt: string
  updatedAt: string
  brokerageSetups: IntermediaryBrokerageSetup[]
}

export interface IntermediaryUpsert {
  name: string
  referenceNumber?: string | null
  email?: string | null
  phone?: string | null
  addressLine1?: string | null
  addressLine2?: string | null
  city?: string | null
  state?: string | null
  zipCode?: string | null
  country?: string | null
  bankName?: string | null
  bankAccountName?: string | null
  bankAccountLast4?: string | null
  bankRoutingNumber?: string | null
  bankSwiftCode?: string | null
  bankInstructions?: string | null
  isActive: boolean
  notes?: string | null
}

export interface IntermediaryBrokerageSetupUpsert {
  programConfigurationId: string
  carrierId: string
  lineOfBusiness?: PolicyLineOfBusiness | null
  effectiveDate: string
  expirationDate?: string | null
  brokerageRate?: number | null
  createPayable: boolean
  payablePayeeId?: number | null
  isActive: boolean
  notes?: string | null
}
