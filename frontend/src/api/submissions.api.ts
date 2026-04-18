import { apiClient } from './client'
import type { Submission, SubmissionListItem, SubmissionCreate, SubmissionUpdate } from '@/types/submission.types'
import type { PagedResult, QueryParameters } from '@/types/common.types'

export const submissionsApi = {
  getAll: (params: QueryParameters) =>
    apiClient.get<PagedResult<SubmissionListItem>>('/submissions', { params }).then((r) => r.data),

  getByInsured: (insuredId: string) =>
    apiClient.get<SubmissionListItem[]>(`/submissions/by-insured/${insuredId}`).then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<Submission>(`/submissions/${id}`).then((r) => r.data),

  create: (data: SubmissionCreate) =>
    apiClient.post<Submission>('/submissions', data).then((r) => r.data),

  update: (id: string, data: SubmissionUpdate) =>
    apiClient.put<Submission>(`/submissions/${id}`, data).then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`/submissions/${id}`),
}
