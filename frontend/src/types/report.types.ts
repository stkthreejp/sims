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
  availablePrograms: Array<{
    id: string
    name: string
    code: string
  }>
}

export interface PostBindFollowUpRow {
  policyId: string
  policyNumber: string
  boundQuoteId: string
  insuredName: string
  carrierName: string
  lineOfBusiness: string
  programId: string | null
  programName: string | null
  programCode: string | null
  state: string | null
  boundDate: string
  issuedDate: string | null
  daysSinceBind: number
  daysSinceIssue: number | null
  ownerId: string | null
  ownerName: string | null
  dueDate: string
  daysUntilDue: number
  slaStatus: string
  openRequiredItemCount: number
  openRequiredItems: string[]
}

export interface PostBindFollowUp {
  rows: PostBindFollowUpRow[]
}

export interface ManagerQueueRow {
  id: string
  workType: 'Referral' | 'AuthorityApproval' | 'PostBind' | string
  title: string
  detail: string
  priority: string
  referenceNumber: string
  insuredName: string | null
  submissionId: string | null
  quoteId: string | null
  policyId: string | null
  ownerId: string | null
  ownerName: string | null
  createdAt: string
  dueDate: string | null
  daysOpen: number
  slaStatus: string
  actionUrl: string
}

export interface ManagerQueue {
  pendingReferralCount: number
  pendingAuthorityApprovalCount: number
  postBindFollowUpCount: number
  rows: ManagerQueueRow[]
}

export interface UnassignedProgramCleanupRow {
  id: string
  recordType: 'Quote' | 'Policy' | string
  referenceNumber: string
  insuredName: string
  carrierName: string
  lineOfBusiness: string
  state: string | null
  status: string
  effectiveDate: string
  expirationDate: string
  submissionId: string | null
  quoteId: string | null
  policyId: string | null
  actionUrl: string
}

export interface UnassignedProgramCleanup {
  openQuoteCount: number
  activePolicyCount: number
  rows: UnassignedProgramCleanupRow[]
}

export interface AuthorityApprovalActivityRow {
  id: string
  targetType: string
  targetId: string
  actionCode: string
  actionLabel: string
  approvalType: string
  isOverride: boolean
  reason: string
  status: string
  referenceNumber: string
  insuredName: string | null
  requestedById: string
  requestedByName: string | null
  ownerId: string | null
  ownerName: string | null
  decisionById: string | null
  decisionByName: string | null
  requestedAt: string
  dueAt: string | null
  decisionAt: string | null
  decisionHours: number | null
  hoursUntilDue: number | null
  slaStatus: string
  actionUrl: string
}

export interface AuthorityApprovalActivity {
  pendingCount: number
  approvedCount: number
  declinedCount: number
  cancelledCount: number
  overrideCount: number
  overduePendingCount: number
  averageDecisionHours: number | null
  rows: AuthorityApprovalActivityRow[]
}
