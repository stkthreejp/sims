import { apiClient } from './client'
import type { UWWriteupDto, SaveWriteupDto, SubmitWriteupDto } from '@/types/uwWriteup.types'

export const uwWriteupApi = {
  get: (quoteId: string) =>
    apiClient.get<UWWriteupDto>(`/quotes/${quoteId}/writeup`).then((r) => r.data),

  save: (quoteId: string, dto: SaveWriteupDto) =>
    apiClient.put<UWWriteupDto>(`/quotes/${quoteId}/writeup`, dto).then((r) => r.data),

  submit: (quoteId: string, dto: SubmitWriteupDto) =>
    apiClient.post<UWWriteupDto>(`/quotes/${quoteId}/writeup/submit`, dto).then((r) => r.data),

  approve: (quoteId: string) =>
    apiClient.post<UWWriteupDto>(`/quotes/${quoteId}/writeup/approve`).then((r) => r.data),
}
