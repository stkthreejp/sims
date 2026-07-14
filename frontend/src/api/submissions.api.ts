import { apiClient } from './client'
import type { Submission, SubmissionListItem, SubmissionCreate, SubmissionUpdate, UnderwritingClearanceEvaluation, UnderwritingReferral, UnderwritingReferralSummary, UnderwritingReferralStatus, IntakeJob } from '@/types/submission.types'
import type { PagedResult, QueryParameters } from '@/types/common.types'

export const submissionsApi = {
  getAll: (params: QueryParameters) =>
    apiClient.get<PagedResult<SubmissionListItem>>('/submissions', { params }).then((r) => r.data),

  getByInsured: (insuredId: string) =>
    apiClient.get<SubmissionListItem[]>(`/submissions/by-insured/${insuredId}`).then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<Submission>(`/submissions/${id}`).then((r) => r.data),

  getClearance: (id: string) =>
    apiClient.get<UnderwritingClearanceEvaluation>(`/submissions/${id}/clearance`).then((r) => r.data),

  evaluateClearance: (id: string) =>
    apiClient.post<UnderwritingClearanceEvaluation>(`/submissions/${id}/clearance/evaluate`).then((r) => r.data),

  overrideClearance: (id: string, reason: string) =>
    apiClient.post<UnderwritingClearanceEvaluation>(`/submissions/${id}/clearance/override`, { reason }).then((r) => r.data),

  getUnderwritingReferrals: (id: string) =>
    apiClient.get<UnderwritingReferralSummary>(`/submissions/${id}/underwriting-referrals`).then((r) => r.data),

  getIntake: (id: string) =>
    apiClient.get<IntakeJob | null>(`/submissions/${id}/intake`).then((r) => r.data),

  reintake: (id: string) =>
    apiClient.post<{ jobId: string }>(`/submissions/${id}/reintake`).then((r) => r.data),

  decideUnderwritingReferral: (referralId: string, decision: Exclude<UnderwritingReferralStatus, 'Open'>, notes?: string) =>
    apiClient.post<UnderwritingReferral>(`/underwriting/referrals/${referralId}/${decision.toLowerCase()}`, { notes }).then((r) => r.data),

  create: (data: SubmissionCreate) =>
    apiClient.post<Submission>('/submissions', data).then((r) => r.data),

  update: (id: string, data: SubmissionUpdate) =>
    apiClient.put<Submission>(`/submissions/${id}`, data).then((r) => r.data),

  setLinesOfBusiness: (id: string, linesOfBusiness: string[]) =>
    apiClient.patch<Submission>(`/submissions/${id}/lines-of-business`, { linesOfBusiness }).then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`/submissions/${id}`),
}
