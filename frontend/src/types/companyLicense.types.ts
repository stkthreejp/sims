export interface CompanyLicense {
  id: string
  holderName: string
  licenseNumber: string
  licenseState: string
  licenseType: string
  effectiveDate: string | null
  expirationDate: string | null
  addressLine1: string | null
  addressLine2: string | null
  city: string | null
  state: string | null
  zipCode: string | null
  country: string
  isActive: boolean
  notes: string | null
}

export interface UpsertCompanyLicense {
  holderName: string
  licenseNumber: string
  licenseState: string
  licenseType: string
  effectiveDate?: string | null
  expirationDate?: string | null
  addressLine1?: string | null
  addressLine2?: string | null
  city?: string | null
  state?: string | null
  zipCode?: string | null
  country?: string | null
  isActive: boolean
  notes?: string | null
}
