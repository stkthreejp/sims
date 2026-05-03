import { apiClient } from './client'
import type { AccountingPeriod } from '@/types/periodClose.types'

export async function getPeriods(): Promise<AccountingPeriod[]> {
  const { data } = await apiClient.get<AccountingPeriod[]>('/billing/periods')
  return data
}

export async function getOrCreatePeriod(year: number, month: number): Promise<AccountingPeriod> {
  const { data } = await apiClient.post<AccountingPeriod>(`/billing/periods/${year}/${month}`)
  return data
}

export async function evaluateChecklist(id: number): Promise<AccountingPeriod> {
  const { data } = await apiClient.post<AccountingPeriod>(`/billing/periods/${id}/evaluate`)
  return data
}

export async function closePeriod(id: number, notes?: string): Promise<AccountingPeriod> {
  const { data } = await apiClient.post<{ period: AccountingPeriod }>(`/billing/periods/${id}/close`, { notes })
  return data.period
}

export async function reopenPeriod(id: number, reason?: string): Promise<AccountingPeriod> {
  const { data } = await apiClient.post<{ period: AccountingPeriod }>(`/billing/periods/${id}/reopen`, { reason })
  return data.period
}
