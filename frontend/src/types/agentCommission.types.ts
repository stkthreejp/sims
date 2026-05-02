export interface AgentCommission {
  id: number
  lineOfBusiness: string | null
  lineOfBusinessLabel: string | null
  commissionRate: number
  effectiveDate: string
  disabledDate: string | null
  isActive: boolean
  createdAt: string
}

export interface CreateAgentCommissionRequest {
  lineOfBusiness: string | null
  commissionRate: number
  effectiveDate: string
}

export interface DisableAgentCommissionRequest {
  disabledDate: string | null
}
