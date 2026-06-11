import { apiClient } from './client'
import type {
  NettedPayee,
  BatchSummary,
  BatchDetail,
  CreateBatchRequest,
  MarkExecutedRequest,
} from '@/types/cashDistribution.types'

const BASE = '/billing/cash-distribution'

export const getPendingInstructions = (): Promise<NettedPayee[]> =>
  apiClient.get<NettedPayee[]>(`${BASE}/pending`).then((r) => r.data)

export const getBatches = (): Promise<BatchSummary[]> =>
  apiClient.get<BatchSummary[]>(`${BASE}/batches`).then((r) => r.data)

export const getBatch = (id: number): Promise<BatchDetail> =>
  apiClient.get<BatchDetail>(`${BASE}/batches/${id}`).then((r) => r.data)

export const createBatch = (req: CreateBatchRequest): Promise<BatchDetail> =>
  apiClient.post<BatchDetail>(`${BASE}/batches`, req).then((r) => r.data)

export const markExecuted = (id: number, req: MarkExecutedRequest): Promise<BatchDetail> =>
  apiClient.post<BatchDetail>(`${BASE}/batches/${id}/mark-executed`, req).then((r) => r.data)

export const getBatchPdfUrl = (id: number): Promise<{ url: string }> =>
  apiClient.get<{ url: string }>(`${BASE}/batches/${id}/pdf-url`).then((r) => r.data)
