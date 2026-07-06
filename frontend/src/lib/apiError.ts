import axios from 'axios'

/**
 * Single source of truth for turning an API/network error into a user-facing
 * message. Supersedes the ~7 private copies that used to live in individual
 * pages (audit X4). Resolution order:
 *   1. ASP.NET model-validation `errors` dict → first "field: message"
 *   2. `errorMessage` (SIMS Result failures) → `detail`/`title` (ProblemDetails)
 *   3. a plain-string response body
 *   4. the JS Error message
 *   5. the caller's fallback
 */
export function getApiErrorMessage(err: unknown, fallback = 'Something went wrong. Please try again.'): string {
  const data = axios.isAxiosError(err) ? err.response?.data : (err as any)?.response?.data

  if (typeof data === 'string' && data.trim()) return data

  if (data && typeof data === 'object') {
    const errors = (data as any).errors
    if (errors && typeof errors === 'object') {
      const first = Object.entries(errors).flatMap(([field, messages]) =>
        Array.isArray(messages) ? messages.map((m) => `${field}: ${m}`) : [`${field}: ${messages}`]
      )[0]
      if (first) return first
    }
    const msg = (data as any).errorMessage ?? (data as any).detail ?? (data as any).title ?? (data as any).message
    if (msg) return msg
  }

  if (err instanceof Error && err.message) return err.message
  return fallback
}
