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

export interface PendingJournalSync {
  id: number
  rollupId: number
  period: string
  status: 'Pending' | 'Retrying' | 'Done' | 'Failed'
  attemptCount: number
  nextRetryAt: string | null
  lastError: string | null
  createdAt: string
}

export interface SyncStatus {
  connected: boolean
  pending: PendingJournalSync[]
}

export interface Rollup extends RollupSummary {
  lineCount: number
}
