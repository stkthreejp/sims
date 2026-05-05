import { apiClient } from './client'
import type { Role, Permission } from '@/types/role.types'

export const rolesApi = {
  getAll: () =>
    apiClient.get<Role[]>('/roles').then((r) => r.data),

  getPermissions: () =>
    apiClient.get<Permission[]>('/roles/permissions').then((r) => r.data),

  updatePermissions: (roleId: string, permissionIds: number[]) =>
    apiClient.put(`/roles/${roleId}/permissions`, { permissionIds }),
}
