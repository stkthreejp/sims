import { apiClient } from './client'
import type {
  TrustReconciliation,
  PayableAging,
  BrokerArAging,
  CommissionSummary,
  InvoiceTotalsByPolicyTransaction,
  InvoiceTotalsByProgram,
} from '@/types/report.types'

const BASE = '/reports'

export const getTrustReconciliation = (asOf?: string): Promise<TrustReconciliation> =>
  apiClient.get(`${BASE}/accounting/trust-reconciliation`, { params: asOf ? { asOf } : {} }).then(r => r.data)

export const getCarrierPayableAging = (): Promise<PayableAging> =>
  apiClient.get(`${BASE}/accounting/carrier-payable-aging`).then(r => r.data)

export const getSlTaxAging = (): Promise<PayableAging> =>
  apiClient.get(`${BASE}/accounting/sl-tax-aging`).then(r => r.data)

export const getBrokerArAging = (): Promise<BrokerArAging> =>
  apiClient.get(`${BASE}/accounting/broker-ar-aging`).then(r => r.data)

export const getCommissionSummary = (months = 12): Promise<CommissionSummary> =>
  apiClient.get(`${BASE}/accounting/commission-summary`, { params: { months } }).then(r => r.data)

export const getInvoiceTotalsByPolicyTransaction = (): Promise<InvoiceTotalsByPolicyTransaction> =>
  apiClient.get(`${BASE}/accounting/invoice-totals-by-policy-transaction`).then(r => r.data)

export const getInvoiceTotalsByProgram = (programId?: string | null): Promise<InvoiceTotalsByProgram> =>
  apiClient.get(`${BASE}/accounting/invoice-totals-by-program`, { params: programId ? { programId } : {} }).then(r => r.data)
