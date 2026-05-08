export type PolicyLineOfBusiness =
  | 'GeneralLiability'
  | 'InlandMarine'
  | 'AutoLiability'
  | 'AutoPhysicalDamage'
  | 'Property'
  | 'CommercialAuto'
  | 'BusinessOwners'
  | 'WorkersCompensation'
  | 'ProfessionalLiability'
  | 'Umbrella'
  | 'Cyber'
  | 'ExcessLiability'
  | 'Other'
export type QuoteStatus = 'Draft' | 'Submitted' | 'Quoted' | 'Bound' | 'Declined' | 'Cancelled' | 'Expired'
export type TransactionType = 'NewBusiness' | 'Endorsement' | 'Renewal' | 'Cancellation' | 'Reinstatement' | 'Audit'

export const LOB_LABELS: Record<PolicyLineOfBusiness, string> = {
  GeneralLiability: 'General Liability',
  InlandMarine: 'Inland Marine',
  AutoLiability: 'Auto Liability',
  AutoPhysicalDamage: 'Auto Physical Damage',
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

// Active lines SMM writes today. Use this in pickers / filters for new records.
export const ACTIVE_LOBS: PolicyLineOfBusiness[] = [
  'GeneralLiability', 'InlandMarine', 'AutoLiability', 'AutoPhysicalDamage',
]

// All values, including deprecated ones — only use when displaying historical data.
export const ALL_LOBS: PolicyLineOfBusiness[] = [
  'GeneralLiability', 'InlandMarine', 'AutoLiability', 'AutoPhysicalDamage',
  'Property', 'CommercialAuto', 'BusinessOwners',
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
  hasCommissionOverride: boolean
  createdAt: string
}

export interface CommissionOverride {
  carrierRate: number
  smmRate: number
  agentRate: number
  overrideBy: string
  overrideAt: string
  carrierCommissionAmount: number
  smmRetentionAmount: number
  agentCommissionAmount: number
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
  // Commission rates from schedules (stamped at quote creation)
  carrierCommissionRate: number
  smmRetentionRate: number
  agentCommissionRate: number
  // Computed dollar amounts
  carrierCommissionAmount: number
  smmRetentionAmount: number
  agentCommissionAmount: number
  // Give-back override (set pre-bind by UW/Admin)
  commissionOverride: CommissionOverride | null
  coverageDescription: string | null
  deductible: number | null
  limit: number | null
  uninsuredMotoristLimit: number | null
  medicalPaymentsLimit: number | null
  companyId: number | null
  producerId: number | null
  isFilingState: boolean
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
  coverageDescription?: string
  deductible?: number
  limit?: number
  uninsuredMotoristLimit?: number
  medicalPaymentsLimit?: number
  companyId?: number
  producerId?: number
  isFilingState?: boolean
}

export interface QuoteUpdate extends QuoteCreate {
  status: QuoteStatus
}

export interface QuoteBind {
  boundDate: string
  effectiveDate: string
  expirationDate: string
}

export interface CommissionOverrideRequest {
  givebackAmount?: number   // dollar amount agent gives back
  newAgentRate?: number     // new agent rate as decimal (e.g. 0.08 for 8%)
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

// Rating engine

export interface RateQuoteRequest {
  scheduleModifier: number
  scheduleModifierReason?: string
}

export interface RatingLine {
  exposureRef: string
  linePremium: number
  inputs: string         // JSON-encoded; safe-parse client-side
  factorsApplied: string // JSON-encoded
}

export interface RatingResult {
  snapshotId: string
  manualPremium: number
  scheduleModifier: number
  scheduleModifierReason: string | null
  grandTotalPremium: number
  ratedAt: string
  ratedById: string
  ratedByName: string | null
  isBoundSnapshot: boolean
  scheduleMin: number
  scheduleMax: number
  minimumPremium: number | null
  lines: RatingLine[]
}

export type AutoSafetyStatus = 'Ready' | 'MissingDot' | 'NoData'
export type AutoSafetyRiskLevel = 'Unknown' | 'Acceptable' | 'Watch' | 'High'

export interface AutoSafetyBasic {
  basic: string
  measure: number | null
  percentile: number | null
  isPrioritized: boolean
  eventCount: number
  outOfServiceCount: number
  trendDirection: string
}

export interface AutoSafetyOos {
  inspectionCount: number
  overallOosCount: number
  overallOosRate: number | null
  driverInspectionCount: number
  driverOosCount: number
  vehicleInspectionCount: number
  vehicleOosCount: number
  hazmatInspectionCount: number
  hazmatOosCount: number
  driverOosRate: number | null
  vehicleOosRate: number | null
  hazmatOosRate: number | null
}

export interface AutoSafetyAccidentSummary {
  fatalCount: number
  injuryCount: number
  towCount: number
  totalReportableCount: number
  accidentToPowerUnitRatio: number | null
}

export interface AutoSafetyHotspot {
  state: string
  inspectionCount: number
  violationCount: number
  outOfServiceCount: number
}

export interface AutoSafetyEvent {
  date: string
  eventType: string
  state: string | null
  description: string
  basic: string | null
  severityWeight: number
}

export interface AutoSafetySummary {
  status: AutoSafetyStatus
  message: string | null
  usDotNumber: string | null
  carrierName: string | null
  snapshotMonth: string | null
  methodologyVersion: string | null
  overallRiskLevel: AutoSafetyRiskLevel
  powerUnits: number | null
  driverCount: number | null
  dataRefreshedAt: string | null
  summaryFlags: string[]
  basics: AutoSafetyBasic[]
  oos: AutoSafetyOos
  accidentSummary: AutoSafetyAccidentSummary
  geographicHotspots: AutoSafetyHotspot[]
  recentSevereEvents: AutoSafetyEvent[]
}

export interface AutoSafetyRefresh {
  summary: AutoSafetySummary
  carrierRowsImported: number
  inspectionRowsImported: number
  violationRowsImported: number
  crashRowsImported: number
  refreshedAt: string
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
