export type UserStatus = 'Active' | 'Inactive' | 'Locked'

export interface User {
  id: string
  userName: string
  email: string
  firstName: string
  lastName: string
  fullName: string
  phoneNumber: string | null
  status: UserStatus
  lastLoginAt: string | null
  mustChangePassword: boolean
  createdAt: string
  roles: string[]
}

export interface UserCreate {
  userName: string
  email: string
  firstName: string
  lastName: string
  phoneNumber?: string
  password: string
  roles: string[]
}

export interface UserUpdate {
  email: string
  firstName: string
  lastName: string
  phoneNumber?: string
  status: UserStatus
  roles: string[]
}
