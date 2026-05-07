export type LossPremiumBasis = 'Actual' | 'Projected'
export type LossClaimStatus = 'Open' | 'Closed'

export interface SubmissionLossHistorySummary {
  yearCount: number
  claimCount: number
  totalPremium: number
  totalPaid: number
  totalReserved: number
  totalExpense: number
  totalIncurred: number
  lossRatio: number | null
  averageSeverity: number | null
  largestLoss: number
  openReserve: number
  years: SubmissionLossYear[]
}

export interface SubmissionLossYear {
  id: string
  submissionId: string
  policyYear: number
  lineOfBusiness: string | null
  carrierName: string | null
  policyNumber: string | null
  premiumAmount: number
  premiumBasis: LossPremiumBasis
  isSmmWritten: boolean
  source: string | null
  asOfDate: string | null
  paidOverride: number | null
  reservedOverride: number | null
  expenseOverride: number | null
  notes: string | null
  paid: number
  reserved: number
  expense: number
  incurred: number
  lossRatio: number | null
  claimCount: number
  createdAt: string
  claims: SubmissionLossClaim[]
}

export interface SubmissionLossYearCreate {
  policyYear: number
  lineOfBusiness?: string
  carrierName?: string
  policyNumber?: string
  premiumAmount: number
  premiumBasis: LossPremiumBasis
  isSmmWritten: boolean
  source?: string
  asOfDate?: string
  paidOverride?: number
  reservedOverride?: number
  expenseOverride?: number
  notes?: string
}

export interface SubmissionLossClaim {
  id: string
  submissionLossYearId: string
  dateOfLoss: string | null
  claimNumber: string | null
  status: LossClaimStatus
  description: string | null
  coverageType: string | null
  paid: number
  reserved: number
  expense: number
  incurred: number
  createdAt: string
}

export interface SubmissionLossClaimCreate {
  dateOfLoss?: string
  claimNumber?: string
  status: LossClaimStatus
  description?: string
  coverageType?: string
  paid: number
  reserved: number
  expense: number
}
