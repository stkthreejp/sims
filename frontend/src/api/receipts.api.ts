import { apiClient } from './client'
import type { ReceiptSummary, ReceiptDetail, CreateReceiptRequest, OpenInvoice, ApplyCashRequest, ApplyCashResult } from '@/types/receipt.types'

export const getReceipts = (): Promise<ReceiptSummary[]> =>
  apiClient.get('/billing/receipts').then(r => r.data)

export const getReceipt = (id: number): Promise<ReceiptDetail> =>
  apiClient.get(`/billing/receipts/${id}`).then(r => r.data)

export const createReceipt = (req: CreateReceiptRequest): Promise<ReceiptDetail> =>
  apiClient.post('/billing/receipts', req).then(r => r.data)

export const getOpenInvoices = (): Promise<OpenInvoice[]> =>
  apiClient.get('/billing/cash-application/open-invoices').then(r => r.data)

export const applyCash = (req: ApplyCashRequest): Promise<ApplyCashResult> =>
  apiClient.post('/billing/cash-application/apply', req).then(r => r.data)
