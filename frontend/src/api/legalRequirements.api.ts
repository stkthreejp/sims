import { apiClient } from './client'

export interface LegalRequirementSection {
  id: string
  state: string
  lineOfBusiness: string
  action: string
  category: string
  topic: string
  requirementText: string
  citations: string[]
  sourceName: string
  sourceDocument: string
  sourceCreatedAt: string
  reviewStatus: string
  lastVerifiedAt: string
  sortOrder: number
}

export interface LegalRequirementsSummary {
  states: string[]
  categories: string[]
  sectionCount: number
  sectionsByState: Record<string, number>
  sectionsByReviewStatus: Record<string, number>
  scanRunCount: number
  pendingScanResultCount: number
  changeLogCount: number
  sourceName: string
  sourceDocument: string
  sourceCreatedAt: string | null
}

export interface LegalSourceScanRun {
  id: string
  sourceName: string
  sourceType: string
  status: string
  startedAt: string
  completedAt: string | null
  resultsFound: number
  possibleChanges: number
  errorMessage: string | null
  startedByName: string | null
}

export interface LegalSourceScanResult {
  id: string
  scanRunId: string
  sourceName: string
  requirementSectionId: string | null
  state: string
  category: string
  topic: string
  matchStatus: string
  sourceUrl: string
  sourceCitation: string
  sourceText: string
  suggestedRequirementText: string | null
  confidenceScore: number | null
  reviewStatus: string
  reviewedByName: string | null
  reviewedAt: string | null
  createdAt: string
}

export interface LegalRequirementChangeLog {
  id: string
  requirementSectionId: string
  state: string
  category: string
  topic: string
  scanResultId: string | null
  changeType: string
  fieldName: string
  oldValue: string | null
  newValue: string | null
  comment: string | null
  changedByName: string
  changedAt: string
}

export const legalRequirementsApi = {
  async getSummary() {
    const { data } = await apiClient.get<LegalRequirementsSummary>('/legal-requirements/summary')
    return data
  },

  async getSections(filters: { state?: string; category?: string; search?: string }) {
    const { data } = await apiClient.get<LegalRequirementSection[]>('/legal-requirements', {
      params: {
        state: filters.state || undefined,
        category: filters.category || undefined,
        search: filters.search || undefined,
      },
    })
    return data
  },

  async getScanRuns() {
    const { data } = await apiClient.get<LegalSourceScanRun[]>('/legal-requirements/scan-runs')
    return data
  },

  async getScanResults(filters: { state?: string; reviewStatus?: string }) {
    const { data } = await apiClient.get<LegalSourceScanResult[]>('/legal-requirements/scan-results', {
      params: {
        state: filters.state || undefined,
        reviewStatus: filters.reviewStatus || undefined,
      },
    })
    return data
  },

  async getChangeLog(filters: { state?: string; requirementSectionId?: string }) {
    const { data } = await apiClient.get<LegalRequirementChangeLog[]>('/legal-requirements/change-log', {
      params: {
        state: filters.state || undefined,
        requirementSectionId: filters.requirementSectionId || undefined,
      },
    })
    return data
  },

  async importOden(file: File) {
    const form = new FormData()
    form.append('file', file)
    const { data } = await apiClient.post<LegalSourceScanRun>('/legal-requirements/imports/oden', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },

  async approveScanResult(scanResultId: string, comment?: string) {
    await apiClient.post(`/legal-requirements/scan-results/${scanResultId}/approve`, { comment: comment || null })
  },

  async rejectScanResult(scanResultId: string, comment?: string) {
    await apiClient.post(`/legal-requirements/scan-results/${scanResultId}/reject`, { comment: comment || null })
  },

  async simulateChange() {
    const { data } = await apiClient.post<LegalSourceScanResult>('/legal-requirements/scan-runs/simulate-change')
    return data
  },
}
