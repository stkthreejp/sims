import { apiClient } from './client'
import type { InboundEmail, InboundEmailListItem } from '@/types/inboundEmail.types'
import type { Submission } from '@/types/submission.types'

export const inboundEmailsApi = {
  getUnprocessed: () =>
    apiClient.get<InboundEmailListItem[]>('/inbound-emails').then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<InboundEmail>(`/inbound-emails/${id}`).then((r) => r.data),

  createSubmission: (id: string) =>
    apiClient.post<Submission>(`/inbound-emails/${id}/create-submission`).then((r) => r.data),
}
