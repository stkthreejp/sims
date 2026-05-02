import { apiClient } from './client'
import type { InvoiceSummary, InvoiceDetail, CreateInvoiceRequest } from '@/types/invoice.types'

export const getInvoices = (): Promise<InvoiceSummary[]> =>
  apiClient.get('/billing/invoices').then(r => r.data)

export const getInvoice = (id: number): Promise<InvoiceDetail> =>
  apiClient.get(`/billing/invoices/${id}`).then(r => r.data)

export const createInvoice = (req: CreateInvoiceRequest): Promise<InvoiceDetail> =>
  apiClient.post('/billing/invoices', req).then(r => r.data)
