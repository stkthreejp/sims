import { apiClient } from './client'
import type {
  PolicyFormTemplate,
  PolicyFormTemplateUpsert,
  PolicyPackageConfiguration,
  PolicyPackageConfigurationUpsert,
  PolicyPackageFormUpsert,
} from '@/types/policyForm.types'
import type { PolicyLineOfBusiness } from '@/types/quote.types'

export const policyFormsApi = {
  getTemplates: (includeInactive = false) =>
    apiClient.get<PolicyFormTemplate[]>('/policy-forms/templates', { params: { includeInactive } }).then((r) => r.data),

  createTemplate: (data: PolicyFormTemplateUpsert) =>
    apiClient.post<PolicyFormTemplate>('/policy-forms/templates', data).then((r) => r.data),

  updateTemplate: (id: string, data: PolicyFormTemplateUpsert) =>
    apiClient.put<PolicyFormTemplate>(`/policy-forms/templates/${id}`, data).then((r) => r.data),

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
