import { NavLink } from 'react-router-dom'
import { LayoutDashboard, Users, FileText, Building2, ShieldCheck, UserCheck, LayoutTemplate } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useAuthStore } from '@/store/authStore'

const navItems = [
  { to: '/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/insureds', label: 'Insureds', icon: Building2 },
  { to: '/policies', label: 'Policies', icon: FileText },
  { to: '/carriers', label: 'Carriers', icon: ShieldCheck, adminOnly: true },
  { to: '/agents', label: 'Agents', icon: UserCheck, adminOnly: true },
  { to: '/users', label: 'Users', icon: Users, adminOnly: true },
  { to: '/document-library', label: 'Doc Library', icon: LayoutTemplate, adminOnly: true },
]

export function Sidebar() {
  const hasRole = useAuthStore((s) => s.hasRole)

  return (
    <aside className="w-56 bg-slate-900 flex flex-col shrink-0">
      <div className="px-4 py-4 border-b border-slate-700 flex justify-center">
        <img src="/logo.png" alt="Specialty Market Managers" className="h-10 w-auto brightness-0 invert" />
      </div>

      <nav className="flex-1 px-2 py-4 space-y-0.5">
        {navItems.map(({ to, label, icon: Icon, adminOnly }) => {
          if (adminOnly && !hasRole('Admin')) return null
          return (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors',
                  isActive
                    ? 'bg-blue-600 text-white'
                    : 'text-slate-300 hover:bg-slate-800 hover:text-white'
                )
              }
            >
              <Icon className="h-4 w-4 shrink-0" />
              {label}
            </NavLink>
          )
        })}
      </nav>
    </aside>
  )
}
