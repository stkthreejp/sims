import { apiClient } from './client'
import type { User, UserCreate, UserUpdate } from '@/types/user.types'
import type { PagedResult, QueryParameters } from '@/types/common.types'

export const usersApi = {
  getAll: (params: QueryParameters) =>
    apiClient.get<PagedResult<User>>('/users', { params }).then((r) => r.data),

  getById: (id: string) =>
    apiClient.get<User>(`/users/${id}`).then((r) => r.data),

  create: (data: UserCreate) =>
    apiClient.post<User>('/users', data).then((r) => r.data),

  update: (id: string, data: UserUpdate) =>
    apiClient.put<User>(`/users/${id}`, data).then((r) => r.data),

  delete: (id: string) =>
    apiClient.delete(`/users/${id}`),
}
