import type {
  NettedPayee,
  BatchSummary,
  BatchDetail,
  CreateBatchRequest,
  MarkExecutedRequest,
} from '@/types/cashDistribution.types'

const BASE = '/api/v1/billing/cash-distribution'

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

export const getPendingInstructions = (): Promise<NettedPayee[]> =>
  apiFetch(`${BASE}/pending`)

export const getBatches = (): Promise<BatchSummary[]> =>
  apiFetch(`${BASE}/batches`)

export const getBatch = (id: number): Promise<BatchDetail> =>
  apiFetch(`${BASE}/batches/${id}`)

export const createBatch = (req: CreateBatchRequest): Promise<BatchDetail> =>
  apiFetch(`${BASE}/batches`, { method: 'POST', body: JSON.stringify(req) })

export const markExecuted = (id: number, req: MarkExecutedRequest): Promise<BatchDetail> =>
  apiFetch(`${BASE}/batches/${id}/mark-executed`, { method: 'POST', body: JSON.stringify(req) })

export const getBatchPdfUrl = (id: number): Promise<{ url: string }> =>
  apiFetch(`${BASE}/batches/${id}/pdf-url`)
