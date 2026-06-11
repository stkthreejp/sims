import { apiClient } from './client'
import type {
  PayableAging,
  OpenPayable,
  DisbursementSummary,
  DisbursementDetail,
  CreateDisbursementRequest,
} from '@/types/disbursement.types'

const BASE = '/billing/disbursements'

export const getAging = (): Promise<PayableAging> =>
  apiClient.get<PayableAging>(`${BASE}/aging`).then((r) => r.data)

export const getOpenPayables = (): Promise<OpenPayable[]> =>
  apiClient.get<OpenPayable[]>(`${BASE}/open-payables`).then((r) => r.data)

export const getDisbursements = (): Promise<DisbursementSummary[]> =>
  apiClient.get<DisbursementSummary[]>(BASE).then((r) => r.data)

export const getDisbursement = (id: number): Promise<DisbursementDetail> =>
  apiClient.get<DisbursementDetail>(`${BASE}/${id}`).then((r) => r.data)

export const createDisbursement = (req: CreateDisbursementRequest): Promise<DisbursementDetail> =>
  apiClient.post<DisbursementDetail>(BASE, req).then((r) => r.data)

export const postDisbursement = (id: number): Promise<DisbursementDetail> =>
  apiClient.post<DisbursementDetail>(`${BASE}/${id}/post`, {}).then((r) => r.data)

export const voidDisbursement = (id: number, reason?: string): Promise<DisbursementDetail> =>
  apiClient.post<DisbursementDetail>(`${BASE}/${id}/void`, { reason }).then((r) => r.data)
