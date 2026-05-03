export type MatchStatus = 'Unmatched' | 'AutoMatched' | 'ManualMatched'

export interface PayeeStatementSummary {
  id: number
  payeeName: string
  statementDate: string
  referenceNumber: string | null
  statementTotal: number
  totalLines: number
  matchedLines: number
  status: string
  createdAt: string
}

export interface PayeeStatementLine {
  id: number
  policyNumber: string
  stateCode: string
  amount: number
  description: string | null
  matchStatus: MatchStatus
  matchedInvoiceLineId: number | null
  matchedFeeCode: string | null
  matchedFeeDisplayName: string | null
}

export interface PayeeStatement {
  id: number
  payeeName: string
  statementDate: string
  referenceNumber: string | null
  apLedgerAccountId: number
  apLedgerAccountName: string
  statementTotal: number
  status: string
  lines: PayeeStatementLine[]
  createdAt: string
}

export interface ImportPayeeStatementRequest {
  payeeName: string
  statementDate: string
  referenceNumber?: string
  apLedgerAccountId: number
}
