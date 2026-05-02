export interface PendingInstruction {
  id: number
  receiptId: number
  receiptNumber: string
  cashApplicationId: number
  invoiceLineId: number
  feeCode: string
  feeDisplayName: string
  amount: number
  createdAt: string
}

export interface NettedPayee {
  payeeId: number
  payeeName: string
  payeeType: string
  totalAmount: number
  instructionCount: number
  instructions: PendingInstruction[]
}

export interface CreateBatchRequest {
  payeeIds: number[]
}

export interface BatchInstruction {
  id: number
  receiptId: number
  receiptNumber: string
  feeDisplayName: string
  amount: number
  status: string
  ledgerTransactionId: string | null
}

export interface BatchWire {
  payeeId: number
  payeeName: string
  netAmount: number
  instructions: BatchInstruction[]
}

export interface BatchSummary {
  id: number
  batchNumber: string
  status: string
  totalInstructions: number
  totalWires: number
  totalAmount: number
  pdfBlobPath: string | null
  executedAt: string | null
  bankReference: string | null
  createdAt: string
}

export interface BatchDetail extends BatchSummary {
  wires: BatchWire[]
}

export interface MarkExecutedRequest {
  bankReference?: string
}
