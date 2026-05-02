import { NavLink } from 'react-router-dom'
import {
  LayoutDashboard, Users, FileText, Building2,
  ShieldCheck, UserCheck, LayoutTemplate, Inbox,
} from 'lucide-react'
import { useAuthStore } from '@/store/authStore'

const navItems = [
  { to: '/dashboard',        label: 'Dashboard',   icon: LayoutDashboard },
  { to: '/inbox',            label: 'Inbox',        icon: Inbox,          roles: ['Underwriter', 'Admin'] },
  { to: '/submissions',      label: 'Submissions',  icon: FileText },
  { to: '/insureds',         label: 'Insureds',     icon: Building2 },
  { to: '/policies',         label: 'Policies',     icon: FileText },
  { to: '/carriers',         label: 'Carriers',     icon: ShieldCheck,    adminOnly: true },
  { to: '/agents',           label: 'Agents',       icon: UserCheck,      adminOnly: true },
  { to: '/users',            label: 'Users',        icon: Users,          adminOnly: true },
  { to: '/document-library', label: 'Doc Library',  icon: LayoutTemplate, adminOnly: true },
]

export function Sidebar() {
  const hasRole = useAuthStore((s) => s.hasRole)

  return (
    <>
      <style>{`
        .sims-nav-link {
          display: flex; align-items: center; gap: 10px;
          padding: 7px 10px; border-radius: 7px;
          color: var(--ink-2); font-size: 13px; font-weight: 500;
          text-decoration: none; transition: background 0.1s;
        }
        .sims-nav-link:hover { background: var(--hover); }
        .sims-nav-link.active {
          background: var(--accent-soft); color: var(--accent-ink); font-weight: 600;
        }
        .sims-nav-link .sims-nav-icon { color: var(--ink-3); }
        .sims-nav-link.active .sims-nav-icon { color: var(--accent); }
      `}</style>

      <aside
        className="flex flex-col shrink-0"
        style={{
          width: 'var(--sidebar-w)',
          background: 'var(--surface)',
          borderRight: '1px solid var(--line)',
          padding: '16px 12px',
          position: 'sticky',
          top: 0,
          height: '100vh',
        }}
      >
        {/* Brand */}
        <div
          className="flex items-center gap-2"
          style={{ padding: '6px 10px 18px', fontSize: 14, fontWeight: 600, letterSpacing: '-0.01em', color: 'var(--ink)' }}
        >
          <span style={{ width: 26, height: 26, display: 'grid', placeItems: 'center', flexShrink: 0 }}>
            <img src="/smm-symbol.png" alt="SMM" style={{ width: '100%', height: '100%', objectFit: 'contain' }} />
          </span>
          SIMS
        </div>

        {/* Nav */}
        <nav style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
          {navItems.map(({ to, label, icon: Icon, adminOnly, roles }) => {
            if (adminOnly && !hasRole('Admin')) return null
            if (roles && !roles.some((r) => hasRole(r))) return null
            return (
              <NavLink
                key={to}
                to={to}
                className={({ isActive }) => `sims-nav-link${isActive ? ' active' : ''}`}
              >
                <Icon className="sims-nav-icon" style={{ width: 15, height: 15, flexShrink: 0 }} />
                {label}
              </NavLink>
            )
          })}
        </nav>
      </aside>
    </>
  )
}
