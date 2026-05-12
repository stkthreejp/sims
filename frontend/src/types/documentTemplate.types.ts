import type { TemplateEntityType } from '@/lib/templateTags'

export type { TemplateEntityType }
export type DocumentTemplateKind = 'Document' | 'Email' | 'DocumentAndEmail'

export interface DocumentTemplate {
  id: string
  name: string
  description: string | null
  entityType: TemplateEntityType
  kind: DocumentTemplateKind
  htmlContent: string
  subjectTemplate: string | null
  emailBodyHtml: string | null
  isActive: boolean
  createdByName: string
  createdAt: string
  updatedAt: string
}

export interface DocumentTemplateListItem {
  id: string
  name: string
  description: string | null
  entityType: TemplateEntityType
  kind: DocumentTemplateKind
  isActive: boolean
  createdByName: string
  updatedAt: string
}

export interface DocumentTemplateCreate {
  name: string
  description?: string
  entityType: TemplateEntityType
  kind: DocumentTemplateKind
  htmlContent: string
  subjectTemplate?: string
  emailBodyHtml?: string
}

export interface DocumentTemplateUpdate extends DocumentTemplateCreate {
  isActive: boolean
}
