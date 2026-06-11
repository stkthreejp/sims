import { apiClient } from './client'
import type {
  CarrierCommission,
  CreateCarrierCommissionRequest,
  DisableCarrierCommissionRequest,
} from '@/types/carrierCommission.types'

const base = (carrierId: string) => `/carriers/${carrierId}/commissions`

export const getCarrierCommissions = (carrierId: string): Promise<CarrierCommission[]> =>
  apiClient.get<CarrierCommission[]>(base(carrierId)).then((r) => r.data)

export const createCarrierCommission = (
  carrierId: string,
  req: CreateCarrierCommissionRequest,
): Promise<CarrierCommission> =>
  apiClient.post<CarrierCommission>(base(carrierId), req).then((r) => r.data)

export const disableCarrierCommission = (
  carrierId: string,
  id: number,
  req: DisableCarrierCommissionRequest,
): Promise<CarrierCommission> =>
  apiClient.post<CarrierCommission>(`${base(carrierId)}/${id}/disable`, req).then((r) => r.data)
