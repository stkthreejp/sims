export type SubmissionStatus = 'New' | 'InProgress' | 'Quoted' | 'Bound' | 'Declined' | 'Withdrawn'

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
