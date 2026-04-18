import { apiClient } from './client'
import type { Carrier, CarrierListItem, CarrierCreate, CarrierUpdate, CarrierContact, CarrierContactInput } from '@/types/carrier.types'

export const carriersApi = {
  // Core
  getAll: (activeOnly = false) =>
    apiClient.get<CarrierListItem[]>('/carriers', { params: { activeOnly } }).then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<Carrier>(`/carriers/${id}`).then((r) => r.data),

  create: (data: CarrierCreate) =>
    apiClient.post<Carrier>('/carriers', data).then((r) => r.data),

  update: (id: string, data: CarrierUpdate) =>
    apiClient.put<Carrier>(`/carriers/${id}`, data).then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`/carriers/${id}`),

  // Contacts
  addContact: (carrierId: string, data: CarrierContactInput) =>
    apiClient.post<CarrierContact>(`/carriers/${carrierId}/contacts`, data).then((r) => r.data),

  updateContact: (carrierId: string, contactId: string, data: CarrierContactInput) =>
    apiClient.put<CarrierContact>(`/carriers/${carrierId}/contacts/${contactId}`, data).then((r) => r.data),

  deleteContact: (carrierId: string, contactId: string) =>
    apiClient.delete(`/carriers/${carrierId}/contacts/${contactId}`),
}
