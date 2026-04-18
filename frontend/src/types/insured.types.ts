export type InsuredType = 'Individual' | 'Commercial'

export interface InsuredListItem {
  id: string
  insuredType: InsuredType
  displayName: string
  email: string | null
  phone: string | null
  city: string
  state: string
  isActive: boolean
  policyCount: number
  createdAt: string
}

export interface Insured {
  id: string
  insuredType: InsuredType
  displayName: string
  firstName: string | null
  lastName: string | null
  dateOfBirth: string | null
  companyName: string | null
  taxId: string | null
  email: string | null
  phone: string | null
  phoneAlt: string | null
  addressLine1: string
  addressLine2: string | null
  city: string
  state: string
  zipCode: string
  county: string | null
  isActive: boolean
  createdAt: string
  policyCount: number
}

export interface InsuredCreate {
  insuredType: InsuredType
  firstName?: string
  lastName?: string
  dateOfBirth?: string
  companyName?: string
  taxId?: string
  email?: string
  phone?: string
  phoneAlt?: string
  addressLine1: string
  addressLine2?: string
  city: string
  state: string
  zipCode: string
  county?: string
}

export interface InsuredUpdate extends InsuredCreate {
  isActive: boolean
}
