export interface ComplianceDocumentSummary {
  totalDocuments: number
  activeDocuments: number
  draftDocuments: number
  dueSoon: number
  overdue: number
  pendingAttestations: number
  activeAttestationCampaigns: number
}

export interface ComplianceDocumentListItem {
  id: string
  title: string
  category: string
  documentType: string
  status: string
  ownerName: string | null
  approverName: string | null
  effectiveDate: string | null
  lastReviewedDate: string | null
  nextReviewDate: string | null
  reviewCadence: string
  tags: string[]
  currentPublishedVersionNumber: number | null
  currentDraftVersionNumber: number | null
  updatedAt: string
}

export interface ComplianceDocumentDetail extends ComplianceDocumentListItem {
  ownerId: string | null
  approverId: string | null
  currentPublishedVersion: ComplianceDocumentVersion | null
  currentDraftVersion: ComplianceDocumentVersion | null
  versions: ComplianceDocumentVersion[]
  reviews: ComplianceDocumentReview[]
  evidenceItems: ComplianceEvidence[]
}

export interface ComplianceDocumentVersion {
  id: string
  versionNumber: number
  status: string
  htmlContent: string
  plainText: string
  changeSummary: string | null
  createdByName: string
  approvedByName: string | null
  createdAt: string
  approvedAt: string | null
  effectiveDate: string | null
}

export interface ComplianceDocumentReview {
  id: string
  versionId: string | null
  status: string
  notes: string | null
  reviewedByName: string
  reviewedAt: string
  nextReviewDate: string | null
}

export interface ComplianceEvidence {
  id: string
  title: string
  evidenceType: string
  description: string | null
  url: string | null
  createdByName: string
  createdAt: string
}

export interface ComplianceAttestationCampaign {
  id: string
  documentId: string
  versionId: string
  documentTitle: string
  versionNumber: number
  name: string
  statement: string
  dueDate: string
  status: string
  createdByName: string
  createdAt: string
  recipientCount: number
  pendingCount: number
  attestedCount: number
  declinedCount: number
  recipients: ComplianceAttestationRecipient[]
}

export interface ComplianceAttestationRecipient {
  id: string
  userId: string
  userName: string
  email: string
  status: string
  attestedAt: string | null
  comment: string | null
}

export interface ComplianceAuditLog {
  id: string
  documentId: string
  versionId: string | null
  action: string
  fieldName: string | null
  oldValue: string | null
  newValue: string | null
  comment: string | null
  userName: string
  createdAt: string
}

export interface ComplianceDocumentCreate {
  title: string
  category: string
  documentType: string
  ownerId?: string | null
  approverId?: string | null
  effectiveDate?: string | null
  nextReviewDate?: string | null
  reviewCadence: string
  tags: string[]
  htmlContent: string
}

export interface ComplianceDocumentUpdate {
  title: string
  category: string
  documentType: string
  status: string
  ownerId?: string | null
  approverId?: string | null
  effectiveDate?: string | null
  nextReviewDate?: string | null
  reviewCadence: string
  tags: string[]
}

export interface ComplianceVersionCompare {
  fromVersionId: string | null
  toVersionId: string | null
  fromTitle: string
  toTitle: string
  parts: { text: string; kind: 'Same' | 'Added' | 'Removed' }[]
}
