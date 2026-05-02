export interface ReceiptSummary {
  id: number
  receiptNumber: string
  receivedDate: string
  payerName: string
  amount: number
  appliedAmount: number
  status: string
}

export interface ReceiptApplication {
  id: number
  invoiceId: number
  invoiceNumber: string
  grossApplied: number
  commissionAmount: number
  netApplied: number
  createdAt: string
}

export interface ReceiptDetail {
  id: number
  receiptNumber: string
  receivedDate: string
  payerName: string
  amount: number
  appliedAmount: number
  status: string
  ledgerTransactionId: string
  applications: ReceiptApplication[]
}

export interface CreateReceiptRequest {
  receivedDate: string
  amount: number
  payerName: string
  reference?: string
}

export interface OpenInvoice {
  id: number
  invoiceNumber: string
  invoiceDate: string
  totalAmount: number
  clearedAmount: number
  openBalance: number
  status: string
}

export interface ApplicationLineRequest {
  invoiceId: number
  grossApplied: number
  commissionAmount: number
}

export interface ApplyCashRequest {
  receiptId: number
  lines: ApplicationLineRequest[]
}

export interface ApplyCashResult {
  receiptId: number
  receiptNumber: string
  receiptAmount: number
  appliedAmount: number
  remainingAmount: number
  receiptStatus: string
  applications: ReceiptApplication[]
}
