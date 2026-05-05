export interface Permission {
  id: number
  name: string
  displayName: string
  category: string
}

export interface Role {
  id: string
  name: string
  description: string | null
  isSystemRole: boolean
  permissions: string[]
}
