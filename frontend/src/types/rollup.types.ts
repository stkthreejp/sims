export interface RollupSummary {
  id: number
  periodYear: number
  periodMonth: number
  driverType: string
  status: 'Pending' | 'Exported' | 'Posted' | 'Failed'
  transactionCount: number
  externalId: string | null
  blobUri: string | null
  errorMessage: string | null
  createdAt: string
  completedAt: string | null
}

export interface Rollup extends RollupSummary {
  lineCount: number
}
