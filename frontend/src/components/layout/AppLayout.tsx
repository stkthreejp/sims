import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'

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
          <Outlet />
        </main>
      </div>
    </div>
  )
}
