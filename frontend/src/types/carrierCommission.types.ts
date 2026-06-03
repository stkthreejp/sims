export interface CarrierCommission {
  id: number
  programConfigurationId: string | null
  programName: string | null
  lineOfBusiness: string | null
  lineOfBusinessLabel: string | null
  programCarrierId: string | null
  programCarrierLineOfBusinessId: string | null
  commissionRate: number
  smmRetentionRate: number
  effectiveDate: string
  disabledDate: string | null
  isActive: boolean
  createdAt: string
}

export interface CreateCarrierCommissionRequest {
  programConfigurationId?: string | null
  lineOfBusiness: string | null
  commissionRate: number
  smmRetentionRate: number
  effectiveDate: string
}

export interface DisableCarrierCommissionRequest {
  disabledDate: string | null
}
