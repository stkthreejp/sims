import { apiClient } from './client'
import type { PolicyLineOfBusiness } from '@/types/quote.types'
import type {
  CarrierRatingAssignment,
  CarrierRatingAssignmentCreate,
  CarrierRatingAssignmentUpdate,
  RatingPlanVersionPicker,
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
}
