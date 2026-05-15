import { apiClient } from './client'
import type {
  PolicyFormTemplate,
  DocumentTag,
  PolicyFormFieldMapping,
  PolicyFormFieldMappingUpsert,
  PolicyFormTemplateUpsert,
  PolicyPackageConfiguration,
  PolicyPackageConfigurationUpsert,
  PolicyPackageFormUpsert,
} from '@/types/policyForm.types'
import type { PolicyLineOfBusiness } from '@/types/quote.types'
import type { GenerateDocumentResponse } from './documentGeneration.api'

export const policyFormsApi = {
  getTemplates: (includeInactive = false) =>
    apiClient.get<PolicyFormTemplate[]>('/policy-forms/templates', { params: { includeInactive } }).then((r) => r.data),

  createTemplate: (data: PolicyFormTemplateUpsert) =>
    apiClient.post<PolicyFormTemplate>('/policy-forms/templates', data).then((r) => r.data),

  updateTemplate: (id: string, data: PolicyFormTemplateUpsert) =>
    apiClient.put<PolicyFormTemplate>(`/policy-forms/templates/${id}`, data).then((r) => r.data),

  uploadTemplateFile: (id: string, file: File) => {
    const formData = new FormData()
    formData.append('file', file)
    return apiClient.post<PolicyFormTemplate>(`/policy-forms/templates/${id}/file`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then((r) => r.data)
  },

  getTemplateDownloadUrl: (id: string) =>
    apiClient.get<{ url: string }>(`/policy-forms/templates/${id}/download-url`).then((r) => r.data),

  testMergeTemplate: (id: string, policyId: string) =>
    apiClient.post<GenerateDocumentResponse>(`/policy-forms/templates/${id}/test-merge`, { policyId }).then((r) => r.data),

  replaceMappings: (id: string, mappings: PolicyFormFieldMappingUpsert[]) =>
    apiClient.put<PolicyFormFieldMapping[]>(`/policy-forms/templates/${id}/mappings`, mappings).then((r) => r.data),

  getTags: () =>
    apiClient.get<DocumentTag[]>('/policy-forms/tags').then((r) => r.data),

  deleteTemplate: (id: string) =>
    apiClient.delete(`/policy-forms/templates/${id}`),

  getPackages: (params?: { carrierId?: string; lineOfBusiness?: PolicyLineOfBusiness; state?: string; includeInactive?: boolean }) =>
    apiClient.get<PolicyPackageConfiguration[]>('/policy-forms/packages', { params }).then((r) => r.data),

  createPackage: (data: PolicyPackageConfigurationUpsert) =>
    apiClient.post<PolicyPackageConfiguration>('/policy-forms/packages', data).then((r) => r.data),

  updatePackage: (id: string, data: PolicyPackageConfigurationUpsert) =>
    apiClient.put<PolicyPackageConfiguration>(`/policy-forms/packages/${id}`, data).then((r) => r.data),

  deletePackage: (id: string) =>
    apiClient.delete(`/policy-forms/packages/${id}`),

  replacePackageForms: (id: string, forms: PolicyPackageFormUpsert[]) =>
    apiClient.put<PolicyPackageConfiguration>(`/policy-forms/packages/${id}/forms`, forms).then((r) => r.data),
}
