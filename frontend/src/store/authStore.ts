import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'
import type { UserInfo } from '@/types/auth.types'

interface AuthState {
  user: UserInfo | null
  accessToken: string | null
  isAuthenticated: boolean
  setAuth: (user: UserInfo, accessToken: string) => void
  clearAuth: () => void
  hasPermission: (permission: string) => boolean
  hasRole: (role: string) => boolean
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user: null,
      accessToken: null,
      isAuthenticated: false,

      setAuth: (user, accessToken) =>
        set({ user, accessToken, isAuthenticated: true }),

      clearAuth: () =>
        set({ user: null, accessToken: null, isAuthenticated: false }),

      hasPermission: (permission) =>
        get().user?.permissions.includes(permission) ?? false,

      hasRole: (role) =>
        get().user?.roles.includes(role) ?? false,
    }),
    {
      name: 'ims-auth',
      version: 2,
      storage: createJSONStorage(() => sessionStorage),
      partialize: (state) => ({
        user: state.user,
        isAuthenticated: state.isAuthenticated,
      }),
      migrate: (persistedState) => {
        const state = persistedState as Partial<AuthState>
        return {
          user: state.user ?? null,
          accessToken: null,
          isAuthenticated: Boolean(state.isAuthenticated),
        }
      },
    }
  )
)
