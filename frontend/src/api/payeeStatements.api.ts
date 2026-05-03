import { apiClient } from './client'
import type { PayeeStatement, PayeeStatementSummary, ImportPayeeStatementRequest } from '@/types/payeeStatement.types'

export const payeeStatementsApi = {
  getAll: (): Promise<PayeeStatementSummary[]> =>
    apiClient.get('/billing/payee-statements').then(r => r.data),

  getById: (id: number): Promise<PayeeStatement> =>
    apiClient.get(`/billing/payee-statements/${id}`).then(r => r.data),

  import: (req: ImportPayeeStatementRequest, file: File): Promise<PayeeStatement> => {
    const form = new FormData()
    form.append('file', file)
    form.append('payeeName', req.payeeName)
    form.append('statementDate', req.statementDate)
    form.append('apLedgerAccountId', String(req.apLedgerAccountId))
    if (req.referenceNumber) form.append('referenceNumber', req.referenceNumber)
    return apiClient.post('/billing/payee-statements/import', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then(r => r.data)
  },

  setLineMatch: (statementId: number, lineId: number, invoiceLineId: number | null): Promise<PayeeStatement> =>
    apiClient.put(`/billing/payee-statements/${statementId}/lines/${lineId}/match`, { invoiceLineId }).then(r => r.data),

  post: (id: number): Promise<PayeeStatement> =>
    apiClient.post(`/billing/payee-statements/${id}/post`).then(r => r.data),
}
