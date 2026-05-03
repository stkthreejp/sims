export interface ChecklistItem {
  checkKey: string
  label: string
  issueCount: number
  isBlocking: boolean
  passed: boolean
  lastCheckedAt: string | null
}

export interface AccountingPeriod {
  id: number
  periodYear: number
  periodMonth: number
  status: 'Open' | 'Closing' | 'Closed' | 'Reopened'
  closedAt: string | null
  reopenedAt: string | null
  notes: string | null
  checklist: ChecklistItem[]
}
