export interface UserInfo {
  id: string
  userName: string
  email: string
  fullName: string
  roles: string[]
  permissions: string[]
  mustChangePassword: boolean
}

export interface LoginRequest {
  userName: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  expiresAt: string
  user: UserInfo
}
