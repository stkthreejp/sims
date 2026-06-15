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
  programId: string | null
  programName: string | null
  programCode: string | null
  lineOfBusiness: string | null
  state: string | null
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

export interface DeclineReasonSummary {
  reason: string
  count: number
  share: number
}

export interface DeclineReasonRow {
  quoteId: string
  quoteNumber: string
  submissionId: string
  submissionNumber: string
  insuredName: string
  carrierName: string
  lineOfBusiness: string
  programId: string | null
  programName: string | null
  programCode: string | null
  state: string | null
  reason: string
  declinedAt: string
  actionUrl: string
}

export interface DeclineReasonReport {
  totalDeclines: number
  withReasonCount: number
  unspecifiedCount: number
  reasons: DeclineReasonSummary[]
  rows: DeclineReasonRow[]
}

export interface ClearanceOverrideSummary {
  checkType: string
  count: number
}

export interface ClearanceOverrideRow {
  id: string
  submissionId: string
  submissionNumber: string
  insuredName: string
  programId: string | null
  programName: string | null
  programCode: string | null
  state: string | null
  lineOfBusiness: string | null
  checkType: string
  status: string
  matchedRecordId: string | null
  matchedRecordLabel: string | null
  explanation: string
  overriddenById: string | null
  overriddenByName: string | null
  overriddenAt: string | null
  overrideReason: string
  reviewedAt: string
  actionUrl: string
}

// Production reports

export interface RenewalsUpcomingRow {
  policyId: string
  policyNumber: string
  insuredName: string
  agentName: string | null
  programId: string | null
  programCode: string | null
  programName: string | null
  carrierId: string
  carrierName: string
  lineOfBusiness: string
  effectiveDate: string
  expirationDate: string
  daysUntilExpiry: number
  premiumAmount: number
  hasRenewalSubmission: boolean
}

export interface RenewalsUpcoming {
  daysAhead: number
  totalCount: number
  rows: RenewalsUpcomingRow[]
}

export interface BoundByPeriodPeriodRow {
  year: number
  month: number
  policyCount: number
  grossPremium: number
  totalPremium: number
}

export interface BoundByPeriodBreakdownRow {
  programId: string | null
  programCode: string | null
  programName: string
  carrierId: string
  carrierName: string
  lineOfBusiness: string
  policyCount: number
  grossPremium: number
  totalPremium: number
}

export interface BoundByPeriod {
  dateFrom: string
  dateTo: string
  totalPolicies: number
  totalGrossPremium: number
  periods: BoundByPeriodPeriodRow[]
  breakdown: BoundByPeriodBreakdownRow[]
}

export interface HitRatioByCarrierRow {
  carrierId: string
  carrierName: string
  totalQuotes: number
  boundCount: number
  declinedCount: number
  expiredCount: number
  openCount: number
  hitRatio: number
}

export interface HitRatioByCarrier {
  dateFrom: string
  dateTo: string
  totalQuotes: number
  totalBound: number
  overallHitRatio: number
  rows: HitRatioByCarrierRow[]
}

export interface ClearanceOverrideReport {
  totalOverrides: number
  blockedOverrideCount: number
  warningOverrideCount: number
  checkTypes: ClearanceOverrideSummary[]
  rows: ClearanceOverrideRow[]
}

// WS9 production reports

export interface WrittenPremiumRow {
  programId: string | null
  programCode: string | null
  programName: string
  carrierId: string
  carrierName: string
  lineOfBusiness: string
  state: string
  policyCount: number
  grossPremium: number
  totalPremium: number
}

export interface WrittenPremium {
  dateFrom: string
  dateTo: string
  totalPolicies: number
  totalGrossPremium: number
  rows: WrittenPremiumRow[]
}

export interface SubmissionPipelineAgentRow {
  agentId: string | null
  agentName: string
  received: number
  quoted: number
  bound: number
  declined: number
  open: number
  quoteRate: number
  bindRate: number
}

export interface SubmissionPipeline {
  dateFrom: string
  dateTo: string
  totalReceived: number
  totalQuoted: number
  totalBound: number
  totalDeclined: number
  totalOpen: number
  quoteRate: number
  bindRate: number
  overallConversion: number
  byAgent: SubmissionPipelineAgentRow[]
}

export interface UwWorkloadRow {
  underwriterId: string
  underwriterName: string
  openSubmissions: number
  pendingQuotes: number
  openTasks: number
  overdueTasks: number
  referralsPending: number
  authApprovalsPending: number
  pipelinePremium: number
}

export interface UwWorkload {
  totalOpenSubmissions: number
  totalPendingQuotes: number
  totalOpenTasks: number
  totalPipelinePremium: number
  rows: UwWorkloadRow[]
}
