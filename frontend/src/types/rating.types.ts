import type { PolicyLineOfBusiness } from './quote.types'

export interface CarrierRatingAssignment {
  id: string
  carrierId: string
  carrierName: string
  lineOfBusiness: PolicyLineOfBusiness
  lineOfBusinessLabel: string
  ratingPlanVersionId: string
  planName: string
  versionNumber: number
  effectiveDate: string
}

export interface CarrierRatingAssignmentCreate {
  carrierId: string
  lineOfBusiness: PolicyLineOfBusiness
  ratingPlanVersionId: string
}

export interface CarrierRatingAssignmentUpdate {
  ratingPlanVersionId: string
}

export interface RatingPlanVersionPicker {
  id: string
  planName: string
  versionNumber: number
  effectiveDate: string
  lob: PolicyLineOfBusiness
}

export type PlanStatus = 'Draft' | 'Active' | 'Retired'

// ─── Plan detail ──────────────────────────────────────────────────────────────

export interface RatingPlanVersionSummary {
  id: string
  versionNumber: number
  status: PlanStatus
  effectiveDate: string
  expirationDate: string | null
  notes: string | null
  promotedAt: string | null
  promotedByName: string | null
  assignedCarrierCount: number
  createdById: string | null
  lastEditedById: string | null
}

export interface PlanCarrierAssignment {
  assignmentId: string
  carrierId: string
  carrierName: string
  versionId: string
  versionNumber: number
}

export interface RatingPlanDetail {
  id: string
  lob: PolicyLineOfBusiness
  lobLabel: string
  name: string
  formulaKey: string
  status: PlanStatus
  versions: RatingPlanVersionSummary[]
  assignments: PlanCarrierAssignment[]
}

// ─── Version detail ───────────────────────────────────────────────────────────

export interface RatingPlanVersionDetail {
  id: string
  ratingPlanId: string
  planName: string
  lob: PolicyLineOfBusiness
  lobLabel: string
  versionNumber: number
  status: PlanStatus
  effectiveDate: string
  expirationDate: string | null
  scheduleMin: number
  scheduleMax: number
  minimumPremium: number | null
  notes: string | null
  promotedAt: string | null
  promotedByName: string | null
  promotedById: string | null
  createdById: string | null
  lastEditedById: string | null
  impactPreviewComputedAt: string | null
}

// ─── Mutating DTOs ────────────────────────────────────────────────────────────

export interface CreateRatingPlanVersionDto {
  effectiveDate: string
  cloneFromVersionId?: string | null
  notes?: string | null
}

export interface UpdateVersionMetaDto {
  effectiveDate: string
  notes: string | null
  scheduleMin: number
  scheduleMax: number
  minimumPremium: number | null
}

export interface FactorRowInputDto {
  dimensionValues: Record<string, string>
  factor: number
}

export interface UpdateFactorTableDto {
  rows: FactorRowInputDto[]
}

// ─── Impact preview ───────────────────────────────────────────────────────────

export interface DistributionBucket {
  rangeLabel: string
  count: number
}

export interface TopMover {
  quoteId: string
  quoteNumber: string
  insuredName: string
  currentPremium: number
  newPremium: number
  deltaPct: number
}

export interface RatingImpactPreview {
  computedAt: string
  quoteCount: number
  totalCurrentPremium: number
  totalNewPremium: number
  totalDeltaPct: number
  quotesUp: number
  quotesDown: number
  quotesFlat: number
  distributionBuckets: DistributionBucket[]
  topMovers: TopMover[]
}

export interface CsvImportResult {
  tablesUpdated: string[]
  rowCountByTable: Record<string, number>
  warnings: string[]
}

// ─── Factor tables ────────────────────────────────────────────────────────────

export type FactorKind = 'Multiplier' | 'RatePer100' | 'FlatAmount'

export interface FactorRow {
  id: string
  dimensionValues: Record<string, string>
  factor: number
}

export interface FactorTable {
  id: string
  code: string
  dimensionNames: string[]
  valueSemantics: FactorKind
  rows: FactorRow[]
}

// ─── Eligibility rules ────────────────────────────────────────────────────────

export interface EligibilityRule {
  id: string
  equipmentTypeId: string
  equipmentTypeName: string
  typeNumber: number
  accepted: boolean
}

// ─── Plan list (4B) ───────────────────────────────────────────────────────────

export interface RatingPlanListItem {
  id: string
  lob: PolicyLineOfBusiness
  lobLabel: string
  name: string
  formulaKey: string
  status: PlanStatus
  activeVersionNumber: number | null
  activeEffectiveDate: string | null
  activeVersionId: string | null
  versionCount: number
  assignedCarrierCount: number
}
