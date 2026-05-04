import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { authApi } from '@/api/auth.api'
import { useAuthStore } from '@/store/authStore'
import type { LoginRequest } from '@/types/auth.types'
import { ensureMsalInitialized, loginRequest, msalInstance } from '@/lib/msalConfig'

export function LoginPage() {
  const navigate = useNavigate()
  const setAuth = useAuthStore((s) => s.setAuth)
  const [loading, setLoading] = useState(false)
  const [msLoading, setMsLoading] = useState(false)

  const { register, handleSubmit, formState: { errors } } = useForm<LoginRequest>()

  const onSubmit = async (data: LoginRequest) => {
    setLoading(true)
    try {
      const res = await authApi.login(data)
      setAuth(res.user, res.accessToken, res.refreshToken)
      navigate('/dashboard')
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { errorMessage?: string } } })
        ?.response?.data?.errorMessage ?? 'Login failed'
      toast.error(msg)
    } finally {
      setLoading(false)
    }
  }

  const handleMicrosoftLogin = async () => {
    setMsLoading(true)
    try {
      await ensureMsalInitialized()
      const result = await msalInstance.loginPopup(loginRequest)
      const idToken = result.idToken
      if (!idToken) {
        toast.error('No ID token returned from Microsoft.')
        return
      }
      const res = await authApi.loginWithMicrosoft(idToken)
      setAuth(res.user, res.accessToken, res.refreshToken)
      navigate('/dashboard')
    } catch (err: unknown) {
      // Ignore user-cancelled popup (BrowserAuthError with errorCode 'user_cancelled')
      const code = (err as { errorCode?: string })?.errorCode
      if (code === 'user_cancelled' || code === 'popup_window_error') return

      const msg =
        (err as { response?: { data?: { errorMessage?: string } } })?.response?.data?.errorMessage ??
        (err as { message?: string })?.message ??
        'Microsoft sign-in failed'
      toast.error(msg)
    } finally {
      setMsLoading(false)
    }
  }

  return (
    <div className="bg-white rounded-xl border border-slate-200 shadow-sm p-8">
      <h2 className="text-lg font-semibold text-slate-900 mb-6">Sign in to your account</h2>

      {/* Microsoft sign-in */}
      <button
        type="button"
        onClick={handleMicrosoftLogin}
        disabled={msLoading}
        className="w-full flex items-center justify-center gap-3 py-2.5 px-4 border border-slate-300 rounded-md text-sm font-medium text-slate-700 bg-white hover:bg-slate-50 disabled:opacity-50 transition-colors mb-5"
      >
        {msLoading ? (
          <span className="h-4 w-4 rounded-full border-2 border-slate-400 border-t-transparent animate-spin" />
        ) : (
          <MicrosoftLogo />
        )}
        {msLoading ? 'Signing in…' : 'Sign in with Microsoft'}
      </button>

      {/* Divider */}
      <div className="relative mb-5">
        <div className="absolute inset-0 flex items-center">
          <div className="w-full border-t border-slate-200" />
        </div>
        <div className="relative flex justify-center text-xs">
          <span className="bg-white px-3 text-slate-400">or sign in with username</span>
        </div>
      </div>

      {/* Username / password form */}
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">Username</label>
          <input
            {...register('userName', { required: 'Username is required' })}
            className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            placeholder="Enter your username"
            autoComplete="username"
          />
          {errors.userName && <p className="text-xs text-red-600 mt-1">{errors.userName.message}</p>}
        </div>

        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">Password</label>
          <input
            {...register('password', { required: 'Password is required' })}
            type="password"
            className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            placeholder="Enter your password"
            autoComplete="current-password"
          />
          {errors.password && <p className="text-xs text-red-600 mt-1">{errors.password.message}</p>}
        </div>

        <button
          type="submit"
          disabled={loading}
          className="w-full py-2 px-4 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white text-sm font-medium rounded-md transition-colors"
        >
          {loading ? 'Signing in…' : 'Sign in'}
        </button>
      </form>

    </div>
  )
}

// Microsoft "four squares" logo SVG
function MicrosoftLogo() {
  return (
    <svg width="18" height="18" viewBox="0 0 21 21" xmlns="http://www.w3.org/2000/svg">
      <rect x="1" y="1" width="9" height="9" fill="#f25022" />
      <rect x="11" y="1" width="9" height="9" fill="#7fba00" />
      <rect x="1" y="11" width="9" height="9" fill="#00a4ef" />
      <rect x="11" y="11" width="9" height="9" fill="#ffb900" />
    </svg>
  )
}
