import { apiClient } from './client'
import type { ActivityEvent, ActivityFilter } from '@/types/activity.types'

export async function getActivity(filter: ActivityFilter = {}): Promise<ActivityEvent[]> {
  const params: Record<string, string> = {}
  if (filter.fromDate) params.fromDate = filter.fromDate
  if (filter.toDate) params.toDate = filter.toDate
  if (filter.sourceType) params.sourceType = filter.sourceType
  if (filter.postingStatus) params.postingStatus = filter.postingStatus
  const { data } = await apiClient.get<ActivityEvent[]>('/billing/activity', { params })
  return data
}

export async function getActivityEvent(transactionId: string): Promise<ActivityEvent> {
  const { data } = await apiClient.get<ActivityEvent>(`/billing/activity/${transactionId}`)
  return data
}

export async function voidReceipt(id: number, reason?: string): Promise<void> {
  await apiClient.post(`/billing/void/receipts/${id}`, { reason })
}

export async function voidCashApplication(id: number, reason?: string): Promise<void> {
  await apiClient.post(`/billing/void/cash-applications/${id}`, { reason })
}

export async function voidInvoice(id: number, reason?: string): Promise<void> {
  await apiClient.post(`/billing/void/invoices/${id}`, { reason })
}

export async function voidDisbursement(id: number, reason?: string): Promise<void> {
  await apiClient.post(`/billing/void/disbursements/${id}`, { reason })
}
