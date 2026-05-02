export interface CarrierCommission {
  id: number
  lineOfBusiness: string | null
  lineOfBusinessLabel: string | null
  commissionRate: number
  effectiveDate: string
  disabledDate: string | null
  isActive: boolean
  createdAt: string
}

export interface CreateCarrierCommissionRequest {
  lineOfBusiness: string | null
  commissionRate: number
  effectiveDate: string
}

export interface DisableCarrierCommissionRequest {
  disabledDate: string | null
}
