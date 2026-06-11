export type ClaimStatus = 'Open' | 'Closed' | 'Reopened' | 'Denied' | 'Subrogation' | 'Withdrawn'

export interface ClaimListItem {
  id: string
  claimNumber: string
  carrierClaimNumber?: string
  policyId?: string
  policyNumber?: string
  insuredId?: string
  insuredName?: string
  sourcePolicyReference?: string
  account?: string
  carrierName?: string
  dateOfLoss: string
  reportDate: string
  closedDate?: string
  status: ClaimStatus
  coverageType?: string
  claimTypeDesc?: string
  lossCause?: string
  tpaName?: string
  claimantName?: string
  adjusterName?: string
  paid: number
  reserved: number
  expense: number
  recovery: number
  incurred: number
  lastValuationDate: string
  isManualEntry: boolean
  createdAt: string
}

export interface Claim extends ClaimListItem {
  description?: string
  riskState?: string
  accidentState?: string
  tpaClaimNumber?: string
  notes?: string
  importBatchId?: string
}

export interface ClaimImportBatch {
  id: string
  fileName: string
  carrierName?: string
  tpaName?: string
  valuationDate: string
  recordCount: number
  createdCount: number
  updatedCount: number
  skippedCount: number
  errorCount: number
  status: string
  errorSummaryJson?: string
  importedByName: string
  createdAt: string
}

export interface LossRun {
  asOfDate: string
  insuredId?: string
  insuredName?: string
  policyId?: string
  policyNumber?: string
  account?: string
  claimCount: number
  openCount: number
  closedCount: number
  totalPaid: number
  totalReserved: number
  totalExpense: number
  totalIncurred: number
  claims: ClaimListItem[]
}

// Matches backend UnifiedClaimImportRow (Unified_Claims_Import column layout)
export interface UnifiedClaimImportRow {
  claimNumber?: string
  account?: string
  claimStatusDesc?: string
  adjusterName?: string
  claimTypeDesc?: string
  claimantName?: string
  dateOfClaim?: string
  dateReported?: string
  carrierName?: string
  carrierPolicyNum?: string
  carrierEffectiveDate?: string
  namedInsured?: string
  accidentCauseDesc?: string
  accidentDescription?: string
  riskState?: string
  accidentState?: string
  totalLossPaid?: number
  totalExpPaid?: number
  totalOsLoss?: number
  totalOsExp?: number
  totalRecovery?: number
  totalIncurred?: number
  lob?: string
  valueDate?: string
}

export interface ImportClaimsRequest {
  fileName: string
  carrierName?: string
  tpaName?: string
  valuationDate: string
  rows: UnifiedClaimImportRow[]
}

export interface ClaimsQuery {
  policyId?: string
  insuredId?: string
  status?: ClaimStatus
  fromDate?: string
  toDate?: string
}
