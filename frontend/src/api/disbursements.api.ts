import type {
  PayableAging,
  OpenPayable,
  DisbursementSummary,
  DisbursementDetail,
  CreateDisbursementRequest,
} from '@/types/disbursement.types'

const BASE = '/api/v1/billing/disbursements'

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

export const getAging = (): Promise<PayableAging> =>
  apiFetch(`${BASE}/aging`)

export const getOpenPayables = (): Promise<OpenPayable[]> =>
  apiFetch(`${BASE}/open-payables`)

export const getDisbursements = (): Promise<DisbursementSummary[]> =>
  apiFetch(`${BASE}`)

export const getDisbursement = (id: number): Promise<DisbursementDetail> =>
  apiFetch(`${BASE}/${id}`)

export const createDisbursement = (req: CreateDisbursementRequest): Promise<DisbursementDetail> =>
  apiFetch(`${BASE}`, { method: 'POST', body: JSON.stringify(req) })

export const postDisbursement = (id: number): Promise<DisbursementDetail> =>
  apiFetch(`${BASE}/${id}/post`, { method: 'POST', body: '{}' })

export const voidDisbursement = (id: number, reason?: string): Promise<DisbursementDetail> =>
  apiFetch(`${BASE}/${id}/void`, { method: 'POST', body: JSON.stringify({ reason }) })
