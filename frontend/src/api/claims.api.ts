import { apiClient } from './client'
import type {
  Claim,
  ClaimImportBatch,
  ClaimListItem,
  ClaimsQuery,
  ImportClaimsRequest,
  LossRun,
} from '@/types/claim.types'

const BASE = '/claims'

export const getClaims = (params?: ClaimsQuery): Promise<ClaimListItem[]> =>
  apiClient.get<ClaimListItem[]>(BASE, { params }).then((r) => r.data)

export const getClaim = (id: string): Promise<Claim> =>
  apiClient.get<Claim>(`${BASE}/${id}`).then((r) => r.data)

export const importClaims = (req: ImportClaimsRequest): Promise<ClaimImportBatch> =>
  apiClient.post<ClaimImportBatch>(`${BASE}/import`, req).then((r) => r.data)

export const getImportBatches = (): Promise<ClaimImportBatch[]> =>
  apiClient.get<ClaimImportBatch[]>(`${BASE}/import-batches`).then((r) => r.data)

export const getLossRun = (params: {
  insuredId?: string
  policyId?: string
  asOfDate?: string
}): Promise<LossRun> =>
  apiClient.get<LossRun>(`${BASE}/loss-run`, { params }).then((r) => r.data)

export const downloadLossRunCsv = async (params: {
  insuredId?: string
  policyId?: string
  asOfDate?: string
}): Promise<void> => {
  const res = await apiClient.get<Blob>(`${BASE}/loss-run/csv`, { params, responseType: 'blob' })
  const disposition = res.headers['content-disposition'] as string | undefined
  const fileName = disposition?.match(/filename="?([^";]+)"?/)?.[1] ?? 'loss-run.csv'
  const url = URL.createObjectURL(res.data)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}
