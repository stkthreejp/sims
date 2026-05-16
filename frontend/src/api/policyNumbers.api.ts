import { apiClient } from './client'
import type {
  PolicyNumberAssignment,
  PolicyNumberAssignmentUpsert,
  PolicyNumberPreview,
  PolicyNumberPreviewRequest,
  PolicyNumberSequence,
  PolicyNumberSequenceUpsert,
} from '@/types/policyNumber.types'

export const policyNumbersApi = {
  getSequences: (includeInactive = true) =>
    apiClient.get<PolicyNumberSequence[]>('/policy-numbers/sequences', { params: { includeInactive } }).then((r) => r.data),

  createSequence: (data: PolicyNumberSequenceUpsert) =>
    apiClient.post<PolicyNumberSequence>('/policy-numbers/sequences', data).then((r) => r.data),

  updateSequence: (id: string, data: PolicyNumberSequenceUpsert) =>
    apiClient.put<PolicyNumberSequence>(`/policy-numbers/sequences/${id}`, data).then((r) => r.data),

  deleteSequence: (id: string) =>
    apiClient.delete(`/policy-numbers/sequences/${id}`),

  getAssignments: (includeInactive = true) =>
    apiClient.get<PolicyNumberAssignment[]>('/policy-numbers/assignments', { params: { includeInactive } }).then((r) => r.data),

  createAssignment: (data: PolicyNumberAssignmentUpsert) =>
    apiClient.post<PolicyNumberAssignment>('/policy-numbers/assignments', data).then((r) => r.data),

  updateAssignment: (id: string, data: PolicyNumberAssignmentUpsert) =>
    apiClient.put<PolicyNumberAssignment>(`/policy-numbers/assignments/${id}`, data).then((r) => r.data),

  deleteAssignment: (id: string) =>
    apiClient.delete(`/policy-numbers/assignments/${id}`),

  preview: (data: PolicyNumberPreviewRequest) =>
    apiClient.post<PolicyNumberPreview>('/policy-numbers/preview', data).then((r) => r.data),
}
