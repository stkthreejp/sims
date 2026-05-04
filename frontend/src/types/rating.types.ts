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
