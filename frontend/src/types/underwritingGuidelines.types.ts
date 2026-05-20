import type { PolicyLineOfBusiness } from './quote.types'

export type UnderwritingControlItemType =
  | 'AppetiteRule'
  | 'ReferralTrigger'
  | 'AuthorityLimit'
  | 'DocumentChecklistItem'
  | 'AppetiteNote'

export type UnderwritingControlStage =
  | 'Submission'
  | 'Quote'
  | 'Bind'
  | 'Issue'
  | 'PostBind'
  | 'Renewal'

export type UnderwritingControlSeverity =
  | 'Informational'
  | 'Warning'
  | 'ReferralRequired'
  | 'HardBlock'

export type UnderwritingControlStatus =
  | 'AiSuggested'
  | 'Draft'
  | 'Approved'
  | 'Published'
  | 'Rejected'
  | 'Retired'

export interface UnderwritingGuidelineDocument {
  id: string
  programName: string
  carrierId: string | null
  carrierName: string | null
  lineOfBusiness: PolicyLineOfBusiness
  stateCode: string
  title: string
  sourceFileName: string | null
  sourceBlobName: string | null
  notes: string | null
  version: number
  createdByUserId: string
  createdAt: string
  controlCount: number
}

export interface UnderwritingGuidelineControl {
  id: string
  guidelineDocumentId: string
  programName: string
  carrierId: string | null
  carrierName: string | null
  lineOfBusiness: PolicyLineOfBusiness
  stateCode: string
  itemType: UnderwritingControlItemType
  stage: UnderwritingControlStage
  severity: UnderwritingControlSeverity
  status: UnderwritingControlStatus
  ruleKey: string
  label: string
  description: string | null
  conditionJson: string | null
  isBlocking: boolean
  overrideAllowed: boolean
  overridePermission: string | null
  sourceCitation: string | null
  aiConfidence: number | null
  version: number
  sortOrder: number
  reviewedByUserId: string | null
  reviewedAt: string | null
  reviewNotes: string | null
  publishedByUserId: string | null
  publishedAt: string | null
  retiredByUserId: string | null
  retiredAt: string | null
  retirementReason: string | null
}

export interface UnderwritingGuidelineAuditLog {
  id: string
  guidelineDocumentId: string | null
  guidelineControlId: string | null
  action: string
  actorUserId: string
  notes: string | null
  beforeJson: string | null
  afterJson: string | null
  createdAt: string
}

export interface CreateUnderwritingGuidelineDocumentRequest {
  programName: string
  carrierId: string | null
  lineOfBusiness: PolicyLineOfBusiness
  stateCode: string
  title: string
  sourceFileName?: string | null
  sourceBlobName?: string | null
  notes?: string | null
}

export interface CreateUnderwritingGuidelineControlRequest {
  itemType: UnderwritingControlItemType
  stage: UnderwritingControlStage
  severity: UnderwritingControlSeverity
  ruleKey: string
  label: string
  description?: string | null
  conditionJson?: string | null
  isBlocking: boolean
  overrideAllowed: boolean
  overridePermission?: string | null
  sourceCitation?: string | null
  aiConfidence?: number | null
  sortOrder: number
}

export interface AddProposedUnderwritingControlsRequest {
  controls: CreateUnderwritingGuidelineControlRequest[]
}

export interface UpdateUnderwritingGuidelineControlRequest extends CreateUnderwritingGuidelineControlRequest {
  changeNotes?: string | null
}

export interface AiGuidelineControlProposalFromAttachmentRequest {
  attachmentId: string
  document: CreateUnderwritingGuidelineDocumentRequest
}

export interface AiGuidelineControlProposalResult {
  document: UnderwritingGuidelineDocument
  controls: UnderwritingGuidelineControl[]
  warnings: string[]
}
