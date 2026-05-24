import type { PolicyLineOfBusiness } from './quote.types'

export type ProposalDocumentRole = 'Proposal' | 'StateNotice'

export interface ProposalDocumentConfiguration {
  id: string
  programConfigurationId: string | null
  programName: string | null
  carrierId: string
  carrierName: string
  lineOfBusiness: PolicyLineOfBusiness
  lineOfBusinessLabel: string
  state: string | null
  role: ProposalDocumentRole
  documentTemplateId: string
  documentTemplateName: string
  sequenceOrder: number
  isActive: boolean
  effectiveDate: string | null
  expirationDate: string | null
  notes: string | null
}

export interface ProposalDocumentConfigurationUpsert {
  programConfigurationId?: string | null
  carrierId: string
  lineOfBusiness: PolicyLineOfBusiness
  state?: string | null
  role: ProposalDocumentRole
  documentTemplateId: string
  sequenceOrder: number
  isActive: boolean
  effectiveDate?: string | null
  expirationDate?: string | null
  notes?: string | null
}

export interface ProposalDocumentSelectionItem {
  configurationId: string
  documentTemplateId: string
  documentTemplateName: string
  role: ProposalDocumentRole
  state: string | null
  sequenceOrder: number
}

export interface ProposalDocumentSelection {
  quoteId: string
  state: string | null
  proposal: ProposalDocumentSelectionItem
  notices: ProposalDocumentSelectionItem[]
}
