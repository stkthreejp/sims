export interface RollupSummary {
  id: number
  periodYear: number
  periodMonth: number
  driverType: string
  status: 'Pending' | 'Exported' | 'Posted' | 'Failed' | 'Divergent'
  transactionCount: number
  externalId: string | null
  blobUri: string | null
  errorMessage: string | null
  createdAt: string
  completedAt: string | null
}

export interface PendingQboSync {
  id: number
  rollupId: number
  period: string
  status: 'Pending' | 'Retrying' | 'Done' | 'Failed'
  attemptCount: number
  nextRetryAt: string | null
  lastError: string | null
  createdAt: string
}

export interface QboStatus {
  connected: boolean
  pending: PendingQboSync[]
}

export interface Rollup extends RollupSummary {
  lineCount: number
}
