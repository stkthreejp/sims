import type { DocumentType } from './attachment.types'
import type { PolicyLineOfBusiness } from './quote.types'

export type PolicyFormType = 'Mandatory' | 'Conditional' | 'AdHoc'

export interface PolicyFormFieldMapping {
  id: string
  pdfFieldName: string
  dataPath: string
  format: string | null
}

export interface PolicyFormFieldMappingUpsert {
  pdfFieldName: string
  dataPath: string
  format?: string
}

export interface DocumentTag {
  tag: string
  label: string
  category: string
  dataType: string
  defaultFormat: string | null
  isRepeatable: boolean
  repeatBlock: string | null
}

export interface PolicyFormTemplate {
  id: string
  formNumber: string
  name: string
  editionDate: string | null
  documentType: DocumentType
  fileName: string | null
  contentType: string | null
  storagePath: string | null
  isFillable: boolean
  isActive: boolean
  notes: string | null
  fieldMappings: PolicyFormFieldMapping[]
  updatedAt: string
}

export interface PolicyFormTemplateUpsert {
  formNumber: string
  name: string
  editionDate?: string
  documentType: DocumentType
  fileName?: string
  contentType?: string
  storagePath?: string
  isFillable: boolean
  isActive: boolean
  notes?: string
}

export interface PolicyPackageForm {
  id: string
  policyFormTemplateId: string
  formNumber: string
  formName: string
  editionDate: string | null
  sequenceOrder: number
  formType: PolicyFormType
  triggerConditionJson: string | null
  notes: string | null
}

export interface PolicyPackageConfiguration {
  id: string
  programConfigurationId: string | null
  programName: string | null
  carrierId: string
  carrierName: string
  lineOfBusiness: PolicyLineOfBusiness
  state: string | null
  programCarrierLineOfBusinessId: string | null
  programCarrierLobStateId: string | null
  name: string
  isActive: boolean
  forms: PolicyPackageForm[]
  updatedAt: string
}

export interface PolicyPackageConfigurationUpsert {
  programConfigurationId?: string | null
  carrierId: string
  lineOfBusiness: PolicyLineOfBusiness
  state: string | null
  name: string
  isActive: boolean
}

export interface PolicyPackageFormUpsert {
  policyFormTemplateId: string
  sequenceOrder: number
  formType: PolicyFormType
  triggerConditionJson?: string
  notes?: string
}
