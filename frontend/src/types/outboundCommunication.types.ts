export type OutboundCommunicationEntityType =
  | 'Submission'
  | 'Quote'
  | 'Policy'
  | 'Carrier'
  | 'Agent'
  | 'Insured'

export type OutboundCommunicationStatus =
  | 'Draft'
  | 'Queued'
  | 'Sent'
  | 'Failed'
  | 'Cancelled'

export type OutboundCommunicationSenderType =
  | 'CurrentUser'
  | 'SharedMailbox'
  | 'System'

export interface OutboundCommunicationListItem {
  id: string
  entityType: OutboundCommunicationEntityType
  entityId: string
  toAddress: string
  toName: string | null
  fromAddress: string
  subject: string
  status: OutboundCommunicationStatus
  graphMessageWebLink: string | null
  sentAt: string | null
  createdByName: string
  attachmentCount: number
  createdAt: string
}

export interface OutboundCommunicationAttachment {
  attachmentId: string
  fileName: string
}

export interface OutboundCommunication {
  id: string
  entityType: OutboundCommunicationEntityType
  entityId: string
  templateId: string | null
  toAddress: string
  toName: string | null
  ccAddresses: string | null
  bccAddresses: string | null
  fromAddress: string
  fromName: string | null
  senderType: OutboundCommunicationSenderType
  subject: string
  bodyHtml: string
  status: OutboundCommunicationStatus
  failureReason: string | null
  graphMessageId: string | null
  graphMessageWebLink: string | null
  createdByName: string
  sentByName: string | null
  sentAt: string | null
  attachments: OutboundCommunicationAttachment[]
  createdAt: string
  updatedAt: string
}

export interface OutboundCommunicationCreate {
  entityType: OutboundCommunicationEntityType
  entityId: string
  templateId?: string
  toAddress: string
  toName?: string
  ccAddresses?: string
  bccAddresses?: string
  fromAddress: string
  fromName?: string
  senderType: OutboundCommunicationSenderType
  subject: string
  bodyHtml: string
  attachmentIds: string[]
}

export interface OutboundCommunicationUpdate {
  toAddress: string
  toName?: string
  ccAddresses?: string
  bccAddresses?: string
  fromAddress: string
  fromName?: string
  senderType: OutboundCommunicationSenderType
  subject: string
  bodyHtml: string
  attachmentIds: string[]
}
