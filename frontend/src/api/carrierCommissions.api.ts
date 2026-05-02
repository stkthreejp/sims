import type {
  CarrierCommission,
  CreateCarrierCommissionRequest,
  DisableCarrierCommissionRequest,
} from '@/types/carrierCommission.types'

async function apiFetch<T>(url: string, options?: RequestInit): Promise<T> {
  const token = localStorage.getItem('token')
  const res = await fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options?.headers,
    },
  })
  if (!res.ok) {
    const err = await res.json().catch(() => ({}))
    throw new Error(err.errorMessage ?? `Request failed: ${res.status}`)
  }
  return res.json()
}

const base = (carrierId: string) => `/api/v1/carriers/${carrierId}/commissions`

export const getCarrierCommissions = (carrierId: string): Promise<CarrierCommission[]> =>
  apiFetch(base(carrierId))

export const createCarrierCommission = (
  carrierId: string,
  req: CreateCarrierCommissionRequest,
): Promise<CarrierCommission> =>
  apiFetch(base(carrierId), { method: 'POST', body: JSON.stringify(req) })

export const disableCarrierCommission = (
  carrierId: string,
  id: number,
  req: DisableCarrierCommissionRequest,
): Promise<CarrierCommission> =>
  apiFetch(`${base(carrierId)}/${id}/disable`, { method: 'POST', body: JSON.stringify(req) })
