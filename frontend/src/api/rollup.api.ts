import { apiClient } from './client'
import type { Rollup, RollupSummary, SyncStatus } from '@/types/rollup.types'

export async function getRollups(): Promise<RollupSummary[]> {
  const { data } = await apiClient.get<RollupSummary[]>('/billing/rollups')
  return data
}

export async function getRollup(id: number): Promise<Rollup> {
  const { data } = await apiClient.get<Rollup>(`/billing/rollups/${id}`)
  return data
}

export async function triggerRollup(periodYear: number, periodMonth: number, driverType: string): Promise<Rollup> {
  const { data } = await apiClient.post<Rollup>('/billing/rollups', { periodYear, periodMonth, driverType })
  return data
}

export async function resyncRollup(id: number): Promise<Rollup> {
  const { data } = await apiClient.post<Rollup>(`/billing/rollups/${id}/resync`)
  return data
}

export async function getRollupDownloadUrl(id: number): Promise<string> {
  const { data } = await apiClient.get<{ url: string }>(`/billing/rollups/${id}/download-url`)
  return data.url
}

export async function getXeroStatus(): Promise<SyncStatus> {
  const { data } = await apiClient.get<SyncStatus>('/billing/xero/status')
  return data
}
