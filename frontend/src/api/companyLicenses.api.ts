import { apiClient } from './client'
import type { CompanyLicense, UpsertCompanyLicense } from '@/types/companyLicense.types'

export const companyLicensesApi = {
  getAll: (includeInactive = false) =>
    apiClient.get<CompanyLicense[]>('/admin/company-licenses', { params: { includeInactive } }).then((r) => r.data),
  create: (data: UpsertCompanyLicense) =>
    apiClient.post<CompanyLicense>('/admin/company-licenses', data).then((r) => r.data),
  update: (id: string, data: UpsertCompanyLicense) =>
    apiClient.put<CompanyLicense>(`/admin/company-licenses/${id}`, data).then((r) => r.data),
  delete: (id: string) =>
    apiClient.delete(`/admin/company-licenses/${id}`),
}
