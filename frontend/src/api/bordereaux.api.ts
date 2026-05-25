import { apiClient } from './client'
import type {
  BordereauxPremiumPreview,
  BordereauxProfile,
  BordereauxRun,
  ReconcileBordereauxRunRequest,
} from '@/types/bordereaux.types'

const BASE = '/admin/bordereaux-profiles'

export const getBordereauxProfiles = (): Promise<BordereauxProfile[]> =>
  apiClient.get<BordereauxProfile[]>(BASE).then((r) => r.data)

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
