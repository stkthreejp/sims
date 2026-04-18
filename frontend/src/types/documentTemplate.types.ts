import type { TemplateEntityType } from '@/lib/templateTags'

export type { TemplateEntityType }

export interface DocumentTemplate {
  id: string
  name: string
  description: string | null
  entityType: TemplateEntityType
  htmlContent: string
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
  isActive: boolean
  createdByName: string
  updatedAt: string
}

export interface DocumentTemplateCreate {
  name: string
  description?: string
  entityType: TemplateEntityType
  htmlContent: string
}

export interface DocumentTemplateUpdate extends DocumentTemplateCreate {
  isActive: boolean
}
