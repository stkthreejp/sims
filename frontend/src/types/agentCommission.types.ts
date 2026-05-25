export interface AgentCommission {
  id: number
  programConfigurationId: string | null
  programName: string | null
  carrierId: string | null
  carrierName: string | null
  lineOfBusiness: string | null
  lineOfBusinessLabel: string | null
  stateCode: string | null
  commissionRate: number
  effectiveDate: string
  disabledDate: string | null
  isActive: boolean
  createdAt: string
}

export interface CreateAgentCommissionRequest {
  programConfigurationId?: string | null
  carrierId?: string | null
  lineOfBusiness: string | null
  stateCode?: string | null
  commissionRate: number
  effectiveDate: string
}

export interface DisableAgentCommissionRequest {
  disabledDate: string | null
}
