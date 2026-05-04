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
  CreateRatingPlanVersionDto,
  UpdateVersionMetaDto,
  UpdateFactorTableDto,
  RatingImpactPreview,
  CsvImportResult,
  ShadowRatingDashboard,
  ShadowRatingStatus,
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

  createVersion: (planId: string, dto: CreateRatingPlanVersionDto) =>
    apiClient.post<{ versionId: string; versionNumber: number }>(`/rating-plans/${planId}/versions`, dto).then((r) => r.data),

  updateVersionMeta: (id: string, dto: UpdateVersionMetaDto) =>
    apiClient.put(`/rating-plan-versions/${id}`, dto).then((r) => r.data),

  updateFactorTable: (versionId: string, tableCode: string, dto: UpdateFactorTableDto) =>
    apiClient.put(`/rating-plan-versions/${versionId}/factors/${tableCode}`, dto).then((r) => r.data),

  importCsv: (versionId: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return apiClient.post<CsvImportResult>(`/rating-plan-versions/${versionId}/import-csv`, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then((r) => r.data)
  },

  computeImpactPreview: (versionId: string) =>
    apiClient.post<RatingImpactPreview>(`/rating-plan-versions/${versionId}/preview-impact`).then((r) => r.data),

  getImpactPreview: (versionId: string) =>
    apiClient.get<RatingImpactPreview>(`/rating-plan-versions/${versionId}/preview-impact`).then((r) => r.data),

  getShadowResults: (days = 30) =>
    apiClient.get<ShadowRatingDashboard>('/rating/shadow/results', { params: { days } }).then((r) => r.data),

  getShadowStatus: () =>
    apiClient.get<ShadowRatingStatus>('/rating/shadow/settings').then((r) => r.data),

  updateShadowLob: (lob: string, enabled: boolean) =>
    apiClient.put<ShadowRatingStatus>(`/rating/shadow/settings/${lob}`, { enabled }).then((r) => r.data),
}
