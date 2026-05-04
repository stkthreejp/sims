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
