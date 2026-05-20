export interface DocumentAiExtractedField {
  name: string
  value: string
  confidence: number
  pageNumber: number
  requiresReview: boolean
}

export interface DocumentAiSubmissionDataPreview {
  descriptionOfOperations?: string | null
  dba?: string | null
  entityType?: string | null
  imCoverages?: unknown | null
}

export interface DocumentAiLossYearPreview {
  policyYear: number
  lineOfBusiness?: string | null
  carrierName?: string | null
  policyNumber?: string | null
  premiumAmount: number
  premiumBasis: string
  isSmmWritten: boolean
  source?: string | null
  asOfDate?: string | null
  paidOverride?: number | null
  reservedOverride?: number | null
  expenseOverride?: number | null
  notes?: string | null
}

export interface DocumentAiNormalizationPreview {
  submissionData: DocumentAiSubmissionDataPreview
  lossYears: DocumentAiLossYearPreview[]
  fieldsRequiringReview: DocumentAiExtractedField[]
  warnings: string[]
}
