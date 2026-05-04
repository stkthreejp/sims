import { apiClient } from './client'
import type { PolicyLineOfBusiness } from '@/types/quote.types'
import type {
  CarrierRatingAssignment,
  CarrierRatingAssignmentCreate,
  CarrierRatingAssignmentUpdate,
  RatingPlanVersionPicker,
  RatingPlanListItem,
  RatingPlanDetail,
  RatingPlanVersionDetail,
  FactorTable,
  EligibilityRule,
} from '@/types/rating.types'

export const ratingApi = {
  getAssignments: (carrierId?: string) =>
    apiClient
      .get<CarrierRatingAssignment[]>('/carrier-rating-assignments', {
        params: carrierId ? { carrierId } : undefined,
      })
      .then((r) => r.data),

  createAssignment: (dto: CarrierRatingAssignmentCreate) =>
    apiClient
      .post<CarrierRatingAssignment>('/carrier-rating-assignments', dto)
      .then((r) => r.data),

  updateAssignment: (id: string, dto: CarrierRatingAssignmentUpdate) =>
    apiClient
      .put<CarrierRatingAssignment>(`/carrier-rating-assignments/${id}`, dto)
      .then((r) => r.data),

  deleteAssignment: (id: string) =>
    apiClient.delete(`/carrier-rating-assignments/${id}`),

  getVersionsForLob: (lob: PolicyLineOfBusiness) =>
    apiClient
      .get<RatingPlanVersionPicker[]>('/rating-plan-versions', { params: { lob } })
      .then((r) => r.data),

  getPlans: () =>
    apiClient.get<RatingPlanListItem[]>('/rating-plans').then((r) => r.data),

  getPlan: (id: string) =>
    apiClient.get<RatingPlanDetail>(`/rating-plans/${id}`).then((r) => r.data),

  getVersion: (id: string) =>
    apiClient.get<RatingPlanVersionDetail>(`/rating-plan-versions/${id}`).then((r) => r.data),

  getVersionFactors: (id: string) =>
    apiClient.get<FactorTable[]>(`/rating-plan-versions/${id}/factors`).then((r) => r.data),

  getVersionEligibilityRules: (id: string) =>
    apiClient.get<EligibilityRule[]>(`/rating-plan-versions/${id}/eligibility-rules`).then((r) => r.data),

  promoteVersion: (id: string) =>
    apiClient.post(`/rating-plan-versions/${id}/promote`).then((r) => r.data),

  retireVersion: (id: string) =>
    apiClient.post(`/rating-plan-versions/${id}/retire`).then((r) => r.data),
}
