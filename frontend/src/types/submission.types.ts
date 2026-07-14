export type SubmissionStatus = 'New' | 'InProgress' | 'Quoted' | 'Bound' | 'Declined' | 'Withdrawn'
export type UnderwritingClearanceStatus = 'Clear' | 'Warning' | 'Blocked'
export type UnderwritingClearanceCheckType = 'DuplicateSubmission' | 'ActivePolicyOverlap'
export type UnderwritingReferralStatus = 'Open' | 'Approved' | 'Declined' | 'Waived'

export const SUBMISSION_STATUS_LABELS: Record<SubmissionStatus, string> = {
  New: 'New',
  InProgress: 'In Progress',
  Quoted: 'Quoted',
  Bound: 'Bound',
  Declined: 'Declined',
  Withdrawn: 'Withdrawn',
}

export interface SubmissionListItem {
  id: string
  submissionNumber: string
  insuredId: string
  insuredName: string
  agentName: string | null
  agencyName: string | null
  underwriterName: string
  effectiveDate: string | null
  status: SubmissionStatus
  linesOfBusiness: string[]
  quoteCount: number
  createdAt: string
}

export interface Submission {
  id: string
  submissionNumber: string
  insuredId: string
  insuredName: string
  agentId: string | null
  agentName: string | null
  agencyName: string | null
  underwriterId: string
  underwriterName: string
  assistantUWId: string | null
  assistantUWName: string | null
  effectiveDate: string | null
  expirationDate: string | null
  status: SubmissionStatus
  descriptionOfOperations: string | null
  linesOfBusiness: string[]
  quoteCount: number
  createdAt: string
}

export interface SubmissionCreate {
  insuredId: string
  agentId?: string
  underwriterId: string
  assistantUWId?: string
  effectiveDate?: string
  expirationDate?: string
  descriptionOfOperations?: string
  linesOfBusiness: string[]
}

export interface SubmissionUpdate extends SubmissionCreate {
  status: SubmissionStatus
}

export interface UnderwritingClearanceResult {
  checkType: UnderwritingClearanceCheckType
  status: UnderwritingClearanceStatus
  matchedRecordId: string | null
  matchedRecordLabel: string | null
  explanation: string
  isOverridden: boolean
  overriddenById: string | null
  overriddenAt: string | null
  overrideReason: string | null
}

export interface UnderwritingClearanceEvaluation {
  submissionId: string
  overallStatus: UnderwritingClearanceStatus
  results: UnderwritingClearanceResult[]
}

export interface UnderwritingAppetiteResult {
  id: string
  submissionId: string
  quoteId: string | null
  quoteNumber: string | null
  ruleCode: string
  ruleName: string
  triggered: boolean
  referralRequired: boolean
  explanation: string
  evaluatedById: string
  evaluatedByName: string
  evaluatedAt: string
}

export interface UnderwritingReferral {
  id: string
  submissionId: string
  quoteId: string | null
  quoteNumber: string | null
  referralType: string
  status: UnderwritingReferralStatus
  required: boolean
  reason: string
  requestedById: string
  requestedByName: string
  requestedAt: string
  decisionById: string | null
  decisionByName: string | null
  decisionAt: string | null
  decisionNotes: string | null
}

export interface UnderwritingReferralSummary {
  submissionId: string
  hasOpenRequiredReferrals: boolean
  appetiteResults: UnderwritingAppetiteResult[]
  referrals: UnderwritingReferral[]
}

export type IntakeJobStatus = 'Queued' | 'Running' | 'NeedsReview' | 'Completed' | 'Failed'

export interface IntakeJob {
  id: string
  submissionId: string
  status: IntakeJobStatus
  stage: string | null
  attemptCount: number
  startedAt: string | null
  completedAt: string | null
  errorMessage: string | null
  createdAt: string
}
