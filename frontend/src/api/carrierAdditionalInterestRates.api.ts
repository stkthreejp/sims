import { apiClient } from './client'
import type { CarrierAdditionalInterestRate, CarrierAdditionalInterestRateCreate } from '@/types/submissionLob.types'

const base = (carrierId: string) => `/carriers/${carrierId}/additional-interest-rates`

export const carrierAdditionalInterestRatesApi = {
  getAll: (carrierId: string) =>
    apiClient.get<CarrierAdditionalInterestRate[]>(base(carrierId)).then((r) => r.data),
  create: (carrierId: string, dto: CarrierAdditionalInterestRateCreate) =>
    apiClient.post<CarrierAdditionalInterestRate>(base(carrierId), dto).then((r) => r.data),
  update: (carrierId: string, id: string, dto: CarrierAdditionalInterestRateCreate) =>
    apiClient.put<CarrierAdditionalInterestRate>(`${base(carrierId)}/${id}`, dto).then((r) => r.data),
  delete: (carrierId: string, id: string) =>
    apiClient.delete(`${base(carrierId)}/${id}`),
}
