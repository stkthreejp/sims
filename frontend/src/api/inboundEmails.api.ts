import { apiClient } from './client'
import type { InboundEmail, InboundEmailListItem } from '@/types/inboundEmail.types'
import type { Submission } from '@/types/submission.types'

export interface CreateSubmissionResult {
  /** One submission per detected line of business. Always contains at least one entry. */
  submissions: Submission[]
  extractionStatus: 'NotApplicable' | 'Completed' | 'Failed'
  emailId: string
}

export const inboundEmailsApi = {
  getUnprocessed: () =>
    apiClient.get<InboundEmailListItem[]>('/inbound-emails').then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<InboundEmail>(`/inbound-emails/${id}`).then((r) => r.data),

  createSubmission: (id: string, insuredId?: string, attachmentIds?: string[], lineOfBusiness?: string) =>
    apiClient
      .post<CreateSubmissionResult>(`/inbound-emails/${id}/create-submission`, {
        ...(insuredId ? { insuredId } : {}),
        ...(attachmentIds ? { attachmentIds } : {}),
        ...(lineOfBusiness ? { lineOfBusiness } : {}),
      })
      .then((r) => r.data),

  reExtract: (emailId: string) =>
    apiClient
      .post<{ extractionStatus: string }>(`/inbound-emails/${emailId}/re-extract`, {})
      .then((r) => r.data),
}
