export interface OpenPayable {
  id: number
  invoiceId: number
  invoiceNumber: string
  payeeName: string
  payeeId: number | null
  carrierId: string | null
  amount: number
  paidAmount: number
  balance: number
  invoiceDate: string
  dueDate: string
  daysOutstanding: number
  status: string
}

export interface AgingBucket {
  current: number
  days31to60: number
  days61to90: number
  over90: number
  total: number
}

export interface AgingRow {
  payeeName: string
  payeeId: number | null
  carrierId: string | null
  current: number
  days31to60: number
  days61to90: number
  over90: number
  total: number
}

export interface PayableAging {
  summary: AgingBucket
  rows: AgingRow[]
  payables: OpenPayable[]
}

export interface CreateDisbursementRequest {
  lines: { payableId: number; amount: number }[]
  paymentDate: string
  paymentMethod: string
  reference?: string
  notes?: string
}

export interface DisbursementLineSummary {
  id: number
  payableId: number
  invoiceNumber: string
  payeeName: string
  amount: number
}

export interface DisbursementSummary {
  id: number
  disbursementNumber: string
  payeeName: string
  carrierId: string | null
  totalAmount: number
  paymentDate: string
  paymentMethod: string
  reference: string | null
  status: string
  createdAt: string
}

export interface DisbursementDetail extends DisbursementSummary {
  ledgerTransactionId: string | null
  notes: string | null
  lines: DisbursementLineSummary[]
}
