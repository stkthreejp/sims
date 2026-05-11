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
  recentEventCount: number
  recentOutOfServiceCount: number
  trendDirection: string
  scoreSource: string
}

export interface AutoSafetyIss {
  score: number | null
  status: 'Unknown' | 'Green' | 'Yellow' | 'Red'
  label: string | null
  basis: string
  explanation: string | null
  source: string
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
  overallNationalAverageRate: number | null
  driverNationalAverageRate: number | null
  vehicleNationalAverageRate: number | null
  hazmatNationalAverageRate: number | null
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

export interface AutoSafetyRadiusBand {
  label: string
  inspectionCount: number
  outOfServiceCount: number
}

export interface AutoSafetyRadiusPrecision {
  label: string
  count: number
}

export interface AutoSafetyMapPoint {
  label: string
  latitude: number
  longitude: number
  precision: string
  inspectionCount: number
  outOfServiceCount: number
}

export interface AutoSafetyRadiusSummary {
  hasBaseCoordinate: boolean
  baseLatitude: number | null
  baseLongitude: number | null
  precision: string
  note: string | null
  precisionCounts: AutoSafetyRadiusPrecision[]
  mapPoints: AutoSafetyMapPoint[]
  bands: AutoSafetyRadiusBand[]
}

export interface AutoSafetyEvent {
  date: string
  eventType: string
  state: string | null
  description: string
  basic: string | null
  severityWeight: number
}

export interface AutoSafetyDetail {
  category: string
  date: string
  reportNumber: string
  state: string | null
  city: string | null
  countyCode: string | null
  location: string | null
  agency: string | null
  conditions: string | null
  vehicleInfo: string | null
  crashEvents: string | null
  basic: string | null
  description: string
  isOutOfService: boolean
  isFatal: boolean
  isInjury: boolean
  isTow: boolean
  source: string
}

export interface AutoSafetyTrendBucket {
  label: string
  totalCount: number
  outOfServiceCount: number
  outOfServiceRate: number | null
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
  iss: AutoSafetyIss
  summaryFlags: string[]
  basics: AutoSafetyBasic[]
  oos: AutoSafetyOos
  accidentSummary: AutoSafetyAccidentSummary
  geographicHotspots: AutoSafetyHotspot[]
  radiusSummary: AutoSafetyRadiusSummary
  recentSevereEvents: AutoSafetyEvent[]
  inspectionTrend: AutoSafetyTrendBucket[]
  violationTrend: AutoSafetyTrendBucket[]
}

export interface AutoSafetyRefresh {
  summary: AutoSafetySummary
  carrierRowsImported: number
  inspectionRowsImported: number
  violationRowsImported: number
  crashRowsImported: number
  refreshedAt: string
}

export interface FmcsaAnalyticsRefresh {
  snapshotMonth: string
  carrierCount: number
  basicMeasureCount: number
  refreshedAt: string
}

export interface QuoteChecklistItem {
  id: string
  quoteId: string
  triggerKey: string
  label: string
  isBlocker: boolean
  sortOrder: number
  isCompleted: boolean
  completionSource: 'Manual' | 'System'
  completedById: string | null
  completedByName: string | null
  completedAt: string | null
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
