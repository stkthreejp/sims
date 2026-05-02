import { Outlet } from 'react-router-dom'

export function AuthLayout() {
  return (
    <div className="min-h-screen flex items-center justify-center p-4" style={{ background: 'var(--bg)' }}>
      <div className="w-full max-w-md">
        <div className="text-center mb-8">
          <img src="/logo.png" alt="Specialty Market Managers" className="h-14 w-auto mx-auto mb-2" />
          <p className="text-sm text-slate-500">Policy Administration</p>
        </div>
        <Outlet />
      </div>
    </div>
  )
}
