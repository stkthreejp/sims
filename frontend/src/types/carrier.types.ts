import type { PolicyLineOfBusiness } from './quote.types'

export interface CarrierContact {
  id: string
  firstName: string
  lastName: string | null
  title: string | null
  email: string | null
  phone: string | null
  isPrimary: boolean
}

export interface Carrier {
  id: string
  name: string
  naic: string | null
  amBestRating: string | null
  addressLine1: string | null
  addressLine2: string | null
  city: string | null
  state: string | null
  zipCode: string | null
  website: string | null
  defaultCurrencyCode: string
  isActive: boolean
  linesOfBusiness: PolicyLineOfBusiness[]
  contacts: CarrierContact[]
  createdAt: string
}

export interface CarrierListItem {
  id: string
  name: string
  naic: string | null
  amBestRating: string | null
  city: string | null
  state: string | null
  isActive: boolean
  linesOfBusiness: PolicyLineOfBusiness[]
  contactCount: number
}

export interface CarrierCreate {
  name: string
  naic?: string
  amBestRating?: string
  addressLine1?: string
  addressLine2?: string
  city?: string
  state?: string
  zipCode?: string
  website?: string
  defaultCurrencyCode?: string
  linesOfBusiness: PolicyLineOfBusiness[]
}

export interface CarrierUpdate {
  name: string
  naic?: string
  amBestRating?: string
  addressLine1?: string
  addressLine2?: string
  city?: string
  state?: string
  zipCode?: string
  website?: string
  defaultCurrencyCode?: string
  isActive: boolean
  linesOfBusiness: PolicyLineOfBusiness[]
}

export interface CarrierContactInput {
  firstName: string
  lastName?: string
  title?: string
  email?: string
  phone?: string
  isPrimary: boolean
}
