export interface ActivityLedgerLine {
  id: number
  accountCode: string
  accountName: string
  debit: number
  credit: number
  memo: string | null
  postingStatus: string
}

export interface ActivityEvent {
  transactionId: string
  sourceType: string
  sourceId: number
  sourceNumber: string
  sourceDescription: string | null
  sourcePolicyTransactionId: string | null
  sourcePolicyTransactionNumber: string | null
  sourcePolicyTransactionType: string | null
  sourcePolicyVersionId: string | null
  sourcePolicyVersionNumber: number | null
  effectiveDate: string
  postedAt: string
  totalDebits: number
  totalCredits: number
  postingStatus: 'Posted' | 'Voided' | 'Reversal'
  voidedByTransactionId: string | null
  reversesTransactionId: string | null
  voidReason: string | null
  voidedAt: string | null
  canVoid: boolean
  voidBlockReason: string | null
  lines: ActivityLedgerLine[]
}

export interface ActivityFilter {
  fromDate?: string
  toDate?: string
  sourceType?: string
  postingStatus?: string
}
