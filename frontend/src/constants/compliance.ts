// Single source of truth for all compliance status and category values.
// Must stay in sync with SIMS.Domain.Constants.ComplianceStatuses.cs on the backend.

export const DOCUMENT_STATUS = {
  DRAFT: 'Draft',
  UNDER_REVIEW: 'Under Review',
  NEEDS_UPDATE: 'Needs Update',
  ACTIVE: 'Active',
  RETIRED: 'Retired',
} as const

export type DocumentStatus = typeof DOCUMENT_STATUS[keyof typeof DOCUMENT_STATUS]

export const DOCUMENT_STATUS_LIST = Object.values(DOCUMENT_STATUS) satisfies DocumentStatus[]

export const VERSION_STATUS = {
  DRAFT: 'Draft',
  PUBLISHED: 'Published',
} as const

export const REVIEW_STATUS = {
  COMPLETED: 'Completed',
  APPROVED: 'Approved',
} as const

export const ATTESTATION_STATUS = {
  PENDING: 'Pending',
  ATTESTED: 'Attested',
  DECLINED: 'Declined',
} as const

export type AttestationStatus = typeof ATTESTATION_STATUS[keyof typeof ATTESTATION_STATUS]

export const CAMPAIGN_STATUS = {
  ACTIVE: 'Active',
  CLOSED: 'Closed',
} as const

export const DOCUMENT_CATEGORIES = [
  'IT',
  'Security',
  'Business Continuity',
  'Privacy',
  'Operations',
  'Vendor Management',
  'HR',
  'Finance',
] as const

export type DocumentCategory = typeof DOCUMENT_CATEGORIES[number]

export const REVIEW_CADENCES = [
  'Annual',
  'Semiannual',
  'Quarterly',
  'Biennial',
  'Manual',
] as const
