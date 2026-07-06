export interface InvoiceSummary {
  id: number
  invoiceNumber: string
  invoiceDate: string
  effectiveDate: string
  grossPremium: number
  totalFees: number
  totalAmount: number
  openBalance: number
  dueDate: string
  status: string
  policyTransactionId: string | null
  policyTransactionNumber: string | null
  policyTransactionType: string | null
  policyVersionId: string | null
  policyVersionNumber: number | null
}

export interface InvoiceLineItem {
  id: number
  feeCode: string
  feeDisplayName: string
  feeCategory: string
  amount: number
  isTaxable: boolean
  accountCode: string
  accountLabel: string
}

export interface LedgerEntry {
  id: number
  accountCode: string
  accountLabel: string
  debit: number
  credit: number
  memo: string | null
}

export interface InvoiceDetail extends InvoiceSummary {
  ledgerTransactionId: string
  lines: InvoiceLineItem[]
  ledgerEntries: LedgerEntry[]
}

export interface CreateInvoiceRequest {
  effectiveDate: string
  grossPremium: number
  stateCode: string
  isEndorsement: boolean
  isFilingState: boolean
  companyId?: number
  producerId?: number
  lineOfBusiness?: string
  city?: string
  licenseType?: string
  locationCount: number
  vehicleCount: number
  policyTransactionId?: string
  policyVersionId?: string
}
