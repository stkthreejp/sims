export type EmailAttachmentDocumentType =
  | 'Unknown'
  | 'Acord125'
  | 'Acord126'
  | 'LossRun'
  | 'DecPage'
  | 'ScheduleOfValues'
  | 'SignedApplication'
  | 'Other'

export interface EmailAttachment {
  id: string
  fileName: string
  contentType: string | null
  blobUrl: string
  fileSizeBytes: number
  documentType: EmailAttachmentDocumentType
}

export interface InboundEmailListItem {
  id: string
  fromAddress: string
  fromName: string | null
  subject: string
  receivedAt: string
  isProcessed: boolean
  linkedSubmissionId: string | null
  attachmentCount: number
  createdAt: string
}

export interface InboundEmail {
  id: string
  fromAddress: string
  fromName: string | null
  subject: string
  bodyText: string | null
  receivedAt: string
  processedAt: string | null
  isProcessed: boolean
  linkedSubmissionId: string | null
  attachments: EmailAttachment[]
  createdAt: string
}
