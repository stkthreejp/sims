import { apiClient } from './client'
import type { LoginRequest, LoginResponse, UserInfo } from '@/types/auth.types'

export const authApi = {
  login: (data: LoginRequest) =>
    apiClient.post<LoginResponse>('/auth/login', data).then((r) => r.data),

  loginWithMicrosoft: (idToken: string) =>
    apiClient.post<LoginResponse>('/auth/microsoft', { idToken }).then((r) => r.data),

  refresh: (refreshToken: string) =>
    apiClient.post<LoginResponse>('/auth/refresh', { refreshToken }).then((r) => r.data),

  logout: (refreshToken: string) =>
    apiClient.post('/auth/logout', { refreshToken }),

  me: () =>
    apiClient.get<UserInfo>('/auth/me').then((r) => r.data),

  changePassword: (currentPassword: string, newPassword: string) =>
    apiClient.put('/auth/me/password', { currentPassword, newPassword }),
}
