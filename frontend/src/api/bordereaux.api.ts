import { apiClient } from './client'
import type {
  BordereauxPremiumPreview,
  BordereauxProfile,
  BordereauxRun,
  ReconcileBordereauxRunRequest,
  UpsertBordereauxProfileRequest,
} from '@/types/bordereaux.types'

const BASE = '/admin/bordereaux-profiles'

export interface BordereauxProfileQuery {
  includeInactive?: boolean
  programId?: string
  carrierId?: string
  reportType?: string
  outputFormat?: string
}

export const getBordereauxProfiles = (params?: BordereauxProfileQuery): Promise<BordereauxProfile[]> =>
  apiClient.get<BordereauxProfile[]>(BASE, { params }).then((r) => r.data)

export const createBordereauxProfile = (
  request: UpsertBordereauxProfileRequest,
): Promise<BordereauxProfile> =>
  apiClient.post<BordereauxProfile>(BASE, request).then((r) => r.data)

export const updateBordereauxProfile = (
  profileId: string,
  request: UpsertBordereauxProfileRequest,
): Promise<BordereauxProfile> =>
  apiClient.put<BordereauxProfile>(`${BASE}/${profileId}`, request).then((r) => r.data)

export const getBordereauxPremiumPreview = (
  profileId: string,
  periodStart: string,
  periodEnd: string,
): Promise<BordereauxPremiumPreview> =>
  apiClient.get<BordereauxPremiumPreview>(`${BASE}/${profileId}/premium-preview`, {
    params: { periodStart, periodEnd },
  }).then((r) => r.data)

export const createBordereauxPremiumRun = (
  profileId: string,
  periodStart: string,
  periodEnd: string,
): Promise<BordereauxRun> =>
  apiClient.post<BordereauxRun>(`${BASE}/${profileId}/premium-runs`, {
    periodStart,
    periodEnd,
  }).then((r) => r.data)

export const getBordereauxRuns = (profileId?: string | null): Promise<BordereauxRun[]> =>
  apiClient.get<BordereauxRun[]>(`${BASE}/premium-runs`, {
    params: profileId ? { profileId } : {},
  }).then((r) => r.data)

export const getBordereauxRun = (runId: string): Promise<BordereauxRun> =>
  apiClient.get<BordereauxRun>(`${BASE}/premium-runs/${runId}`).then((r) => r.data)

export const reconcileBordereauxRun = (
  runId: string,
  request: ReconcileBordereauxRunRequest,
): Promise<BordereauxRun> =>
  apiClient.post<BordereauxRun>(`${BASE}/premium-runs/${runId}/reconcile`, request).then((r) => r.data)

export const generateBordereauxExportPackage = (runId: string): Promise<BordereauxRun> =>
  apiClient.post<BordereauxRun>(`${BASE}/premium-runs/${runId}/export-package`).then((r) => r.data)

export const getLondonBordereauxDownloadUrl = (runId: string): Promise<string> =>
  apiClient.get<{ url: string }>(`${BASE}/premium-runs/${runId}/london-bordereaux/download-url`).then((r) => r.data.url)

export const getAccountCurrentDownloadUrl = (runId: string): Promise<string> =>
  apiClient.get<{ url: string }>(`${BASE}/premium-runs/${runId}/account-current/download-url`).then((r) => r.data.url)
