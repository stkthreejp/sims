export type PolicyLineOfBusiness = 'GeneralLiability' | 'Property' | 'CommercialAuto' | 'BusinessOwners' | 'WorkersCompensation' | 'ProfessionalLiability' | 'Umbrella' | 'Cyber' | 'ExcessLiability' | 'Other'
export type QuoteStatus = 'Draft' | 'Submitted' | 'Quoted' | 'Bound' | 'Declined' | 'Cancelled' | 'Expired'
export type TransactionType = 'NewBusiness' | 'Endorsement' | 'Renewal' | 'Cancellation' | 'Reinstatement' | 'Audit'

export const LOB_LABELS: Record<PolicyLineOfBusiness, string> = {
  GeneralLiability: 'General Liability',
  Property: 'Property',
  CommercialAuto: 'Commercial Auto',
  BusinessOwners: 'Business Owners (BOP)',
  WorkersCompensation: 'Workers Compensation',
  ProfessionalLiability: 'Professional Liability',
  Umbrella: 'Umbrella',
  Cyber: 'Cyber',
  ExcessLiability: 'Excess Liability',
  Other: 'Other',
}

export const ALL_LOBS: PolicyLineOfBusiness[] = [
  'GeneralLiability', 'Property', 'CommercialAuto', 'BusinessOwners',
  'WorkersCompensation', 'ProfessionalLiability', 'Umbrella', 'Cyber',
  'ExcessLiability', 'Other',
]

export const QUOTE_STATUS_LABELS: Record<QuoteStatus, string> = {
  Draft: 'Draft',
  Submitted: 'Submitted',
  Quoted: 'Quoted',
  Bound: 'Bound',
  Declined: 'Declined',
  Cancelled: 'Cancelled',
  Expired: 'Expired',
}

export interface QuoteListItem {
  id: string
  quoteNumber: string
  submissionId: string
  submissionNumber: string
  insuredName: string
  carrierId: string
  carrierName: string
  lineOfBusiness: PolicyLineOfBusiness
  status: QuoteStatus
  policyNumber: string | null
  effectiveDate: string
  expirationDate: string
  totalPremium: number
  createdAt: string
}

export interface Quote {
  id: string
  quoteNumber: string
  submissionId: string
  submissionNumber: string
  insuredId: string
  insuredName: string
  carrierId: string
  carrierName: string
  lineOfBusiness: PolicyLineOfBusiness
  status: QuoteStatus
  policyNumber: string | null
  boundDate: string | null
  issuedDate: string | null
  cancelledDate: string | null
  effectiveDate: string
  expirationDate: string
  premiumAmount: number
  taxesAndFees: number
  totalPremium: number
  commissionRate: number
  commissionAmount: number
  coverageDescription: string | null
  deductible: number | null
  limit: number | null
  createdAt: string
}

export interface QuoteCreate {
  submissionId: string
  carrierId: string
  lineOfBusiness: PolicyLineOfBusiness
  effectiveDate: string
  expirationDate: string
  premiumAmount: number
  taxesAndFees: number
  commissionRate: number
  coverageDescription?: string
  deductible?: number
  limit?: number
}

export interface QuoteUpdate extends QuoteCreate {
  status: QuoteStatus
}

export interface QuoteBind {
  boundDate: string
  effectiveDate: string
  expirationDate: string
}

export interface Note {
  id: string
  quoteId: string
  subject: string | null
  body: string
  isPinned: boolean
  createdById: string
  createdByName: string
  createdAt: string
  updatedAt: string
}

export interface Attachment {
  id: string
  quoteId: string
  fileName: string
  contentType: string
  fileSizeBytes: number
  description: string | null
  uploadedById: string
  uploadedByName: string
  createdAt: string
}
