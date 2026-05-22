import type { AgingBucket, AgingRow, OpenPayable, PayableAging } from './disbursement.types'

export type { AgingBucket, AgingRow, OpenPayable, PayableAging }

export interface TrustTransactionLine {
  postedAt: string
  effectiveDate: string
  sourceType: string
  memo: string | null
  debit: number
  credit: number
  runningBalance: number
}

export interface TrustReconciliation {
  asOf: string
  trustBalance: number
  unappliedReceipts: number
  openInvoices: number
  reconcilingDifference: number
  recentActivity: TrustTransactionLine[]
}

export interface OpenReceivable {
  id: number
  invoiceNumber: string
  agentName: string
  agentId: string | null
  totalAmount: number
  clearedAmount: number
  balance: number
  invoiceDate: string
  dueDate: string
  daysOutstanding: number
  status: string
}

export interface BrokerArRow {
  agentName: string
  agentId: string | null
  current: number
  days31to60: number
  days61to90: number
  over90: number
  total: number
}

export interface BrokerArAging {
  summary: AgingBucket
  rows: BrokerArRow[]
  receivables: OpenReceivable[]
}

export interface CommissionPeriod {
  year: number
  month: number
  earned: number
  agentPaid: number
  netRetained: number
  cashReceived: number
  invoiceCount: number
}

export interface CommissionSummary {
  periods: CommissionPeriod[]
  totalEarned: number
  totalAgentPaid: number
  totalNetRetained: number
  totalCashReceived: number
}

export interface InvoiceTotalsByPolicyTransactionRow {
  policyTransactionId: string | null
  policyTransactionNumber: string
  policyTransactionType: string | null
  policyVersionId: string | null
  policyVersionNumber: number | null
  invoiceCount: number
  grossPremium: number
  totalFees: number
  totalAmount: number
}

export interface InvoiceTotalsByPolicyTransaction {
  rows: InvoiceTotalsByPolicyTransactionRow[]
}

export interface InvoiceTotalsByProgramRow {
  programId: string | null
  programName: string
  programCode: string | null
  invoiceCount: number
  grossPremium: number
  totalFees: number
  totalAmount: number
  commissionAmount: number
  agentCommissionAmount: number
  netRetained: number
}

export interface InvoiceTotalsByProgram {
  rows: InvoiceTotalsByProgramRow[]
}
