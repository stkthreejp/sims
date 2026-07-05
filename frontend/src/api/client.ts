import axios, { type AxiosRequestConfig } from 'axios'
import { useAuthStore } from '@/store/authStore'

export const apiClient = axios.create({
  baseURL: '/api/v1',
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true,
})

// Attach access token to every request
apiClient.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken
  if (token) config.headers.Authorization = `Bearer ${token}`

  // Stamp a stable Idempotency-Key on mutating requests so the backend can
  // dedupe a replay (e.g. the 401-refresh retry below re-sends this same config
  // object) instead of double-posting money (audit B4). Only set it once — the
  // interceptor runs again on replay, and we must keep the original key.
  const method = config.method?.toUpperCase()
  if (method && method !== 'GET' && method !== 'HEAD' && !config.headers['Idempotency-Key']) {
    config.headers['Idempotency-Key'] = crypto.randomUUID()
  }
  return config
})

let isRefreshing = false
let failedQueue: Array<{ resolve: (v: unknown) => void; reject: (e: unknown) => void }> = []

const processQueue = (error: unknown, token: string | null = null) => {
  failedQueue.forEach(({ resolve, reject }) => {
    if (error) reject(error)
    else resolve(token)
  })
  failedQueue = []
}

// Auto-refresh on 401
apiClient.interceptors.response.use(
  (res) => res,
  async (error) => {
    const originalRequest = error.config as AxiosRequestConfig & { _retry?: boolean }

    if (error.response?.status !== 401 || originalRequest._retry) {
      return Promise.reject(error)
    }

    if (isRefreshing) {
      return new Promise((resolve, reject) => {
        failedQueue.push({ resolve, reject })
      }).then((token) => {
        if (originalRequest.headers)
          originalRequest.headers['Authorization'] = `Bearer ${token}`
        return apiClient(originalRequest)
      })
    }

    originalRequest._retry = true
    isRefreshing = true

    const { setAuth, clearAuth } = useAuthStore.getState()

    try {
      const { data } = await axios.post('/api/v1/auth/refresh', null, { withCredentials: true })
      setAuth(data.user, data.accessToken)
      processQueue(null, data.accessToken)
      if (originalRequest.headers)
        originalRequest.headers['Authorization'] = `Bearer ${data.accessToken}`
      return apiClient(originalRequest)
    } catch (refreshError) {
      processQueue(refreshError, null)
      const status = axios.isAxiosError(refreshError) ? refreshError.response?.status : undefined
      if (status === 401) {
        clearAuth()
        window.location.href = '/login'
      }
      return Promise.reject(refreshError)
    } finally {
      isRefreshing = false
    }
  }
)
