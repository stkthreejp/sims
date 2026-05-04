import { Suspense } from 'react'
import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ErrorBoundary } from '@/components/common/ErrorBoundary'

export function AppLayout() {
  return (
    <div className="flex h-screen" style={{ background: 'var(--bg)' }}>
      <Sidebar />
      <div className="flex-1 flex flex-col min-w-0">
        <Topbar />
        <main
          className="flex-1 overflow-auto"
          style={{ padding: 'var(--container-pad)' }}
        >
          <ErrorBoundary>
            <Suspense fallback={<div className="flex items-center justify-center h-full"><LoadingSpinner /></div>}>
              <Outlet />
            </Suspense>
          </ErrorBoundary>
        </main>
      </div>
    </div>
  )
}
