import { apiClient } from './client'
import type { CarrierAdditionalInterestRate, CarrierAdditionalInterestRateCreate } from '@/types/submissionLob.types'

const base = '/admin/premium-charges/additional-interest-rates'

export const premiumChargesApi = {
  getAdditionalInterestRates: () =>
    apiClient.get<CarrierAdditionalInterestRate[]>(base).then((r) => r.data),
  createAdditionalInterestRate: (dto: CarrierAdditionalInterestRateCreate) =>
    apiClient.post<CarrierAdditionalInterestRate>(base, dto).then((r) => r.data),
  updateAdditionalInterestRate: (id: string, dto: CarrierAdditionalInterestRateCreate) =>
    apiClient.put<CarrierAdditionalInterestRate>(`${base}/${id}`, dto).then((r) => r.data),
  deleteAdditionalInterestRate: (id: string) =>
    apiClient.delete(`${base}/${id}`),
}
