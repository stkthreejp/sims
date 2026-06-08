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
      setAuth(res.user, res.accessToken)
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
      setAuth(res.user, res.accessToken)
      navigate('/dashboard')
    } catch (err: unknown) {
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
    <div className="sd-card" style={{ padding: '28px 32px' }}>
      <h2 style={{ margin: '0 0 20px', fontSize: 17, fontWeight: 600, color: 'var(--ink)' }}>
        Sign in to your account
      </h2>

      {/* Microsoft sign-in */}
      <button
        type="button"
        onClick={handleMicrosoftLogin}
        disabled={msLoading}
        className="sd-btn outline"
        style={{ width: '100%', marginBottom: 16, height: 38 }}
      >
        {msLoading ? (
          <span style={{ width: 14, height: 14, borderRadius: '50%', border: '2px solid var(--line)', borderTopColor: 'var(--ink-3)', animation: 'spin 0.7s linear infinite', display: 'inline-block' }} />
        ) : (
          <MicrosoftLogo />
        )}
        {msLoading ? 'Signing in…' : 'Sign in with Microsoft'}
      </button>

      {/* Divider */}
      <div style={{ position: 'relative', margin: '4px 0 18px', display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{ flex: 1, height: 1, background: 'var(--line)' }} />
        <span style={{ fontSize: 11.5, color: 'var(--ink-4)', whiteSpace: 'nowrap' }}>or sign in with username</span>
        <div style={{ flex: 1, height: 1, background: 'var(--line)' }} />
      </div>

      {/* Username / password form */}
      <form onSubmit={handleSubmit(onSubmit)} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
        <div className="sd-form-group">
          <label className="sd-label">Username</label>
          <input
            {...register('userName', { required: 'Username is required' })}
            className={`sd-input${errors.userName ? ' error' : ''}`}
            placeholder="Enter your username"
            autoComplete="username"
          />
          {errors.userName && <p className="sd-form-error">{errors.userName.message}</p>}
        </div>

        <div className="sd-form-group">
          <label className="sd-label">Password</label>
          <input
            {...register('password', { required: 'Password is required' })}
            type="password"
            className={`sd-input${errors.password ? ' error' : ''}`}
            placeholder="Enter your password"
            autoComplete="current-password"
          />
          {errors.password && <p className="sd-form-error">{errors.password.message}</p>}
        </div>

        <button
          type="submit"
          disabled={loading}
          className="sd-btn primary"
          style={{ width: '100%', marginTop: 4, height: 38 }}
        >
          {loading ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </div>
  )
}

function MicrosoftLogo() {
  return (
    <svg width="17" height="17" viewBox="0 0 21 21" xmlns="http://www.w3.org/2000/svg">
      <rect x="1" y="1" width="9" height="9" fill="#f25022" />
      <rect x="11" y="1" width="9" height="9" fill="#7fba00" />
      <rect x="1" y="11" width="9" height="9" fill="#00a4ef" />
      <rect x="11" y="11" width="9" height="9" fill="#ffb900" />
    </svg>
  )
}
