import { apiClient } from './client'
import type {
  SubmissionLossClaim,
  SubmissionLossClaimCreate,
  SubmissionLossHistorySummary,
  SubmissionLossYear,
  SubmissionLossYearCreate,
} from '@/types/submissionLossHistory.types'

const base = (submissionId: string) => `/submissions/${submissionId}/loss-history`

export const submissionLossHistoryApi = {
  getSummary: (submissionId: string) =>
    apiClient.get<SubmissionLossHistorySummary>(`${base(submissionId)}/summary`).then((r) => r.data),
  getYears: (submissionId: string) =>
    apiClient.get<SubmissionLossYear[]>(`${base(submissionId)}/years`).then((r) => r.data),
  createYear: (submissionId: string, dto: SubmissionLossYearCreate) =>
    apiClient.post<SubmissionLossYear>(`${base(submissionId)}/years`, dto).then((r) => r.data),
  updateYear: (submissionId: string, yearId: string, dto: SubmissionLossYearCreate) =>
    apiClient.put<SubmissionLossYear>(`${base(submissionId)}/years/${yearId}`, dto).then((r) => r.data),
  deleteYear: (submissionId: string, yearId: string) =>
    apiClient.delete(`${base(submissionId)}/years/${yearId}`),
  createClaim: (submissionId: string, yearId: string, dto: SubmissionLossClaimCreate) =>
    apiClient.post<SubmissionLossClaim>(`${base(submissionId)}/years/${yearId}/claims`, dto).then((r) => r.data),
  updateClaim: (submissionId: string, claimId: string, dto: SubmissionLossClaimCreate) =>
    apiClient.put<SubmissionLossClaim>(`${base(submissionId)}/claims/${claimId}`, dto).then((r) => r.data),
  deleteClaim: (submissionId: string, claimId: string) =>
    apiClient.delete(`${base(submissionId)}/claims/${claimId}`),
}
