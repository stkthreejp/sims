import { useState } from 'react'
import { NavLink } from 'react-router-dom'
import { LayoutDashboard, Users, FileText, Building2, ShieldCheck, UserCheck, LayoutTemplate, Inbox, CheckSquare, Calendar, GitMerge, AlertOctagon, ListChecks, Receipt, Banknote, ArrowLeftRight, Landmark, Wallet, FileInput, Activity, CalendarCheck, Wifi, BarChart2, Sliders, FlaskConical } from 'lucide-react'
import { useAuthStore } from '@/store/authStore'

const navItems = [
  { to: '/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/inbox', label: 'Inbox', icon: Inbox, roles: ['Underwriter', 'Admin'] },
  { to: '/tasks', label: 'My Tasks', icon: CheckSquare },
  { to: '/insureds', label: 'Insureds', icon: Building2 },
  { to: '/policies', label: 'Policies', icon: FileText },
  { to: '/carriers', label: 'Carriers', icon: ShieldCheck, adminOnly: true },
  { to: '/agents', label: 'Agents', icon: UserCheck, adminOnly: true },
  { to: '/users', label: 'Users', icon: Users, adminOnly: true },
  { to: '/document-library', label: 'Doc Library', icon: LayoutTemplate, adminOnly: true },
  { to: '/reports', label: 'Reports', icon: BarChart2, roles: ['Admin', 'Underwriter'] },
]

const ratingAdminItems = [
  { to: '/admin/rating', label: 'Rating Plans', icon: Sliders },
  { to: '/admin/rating/shadow', label: 'Shadow Mode', icon: FlaskConical },
]

const adminTaskItems = [
  { to: '/admin/task-types', label: 'Task Types', icon: ListChecks },
  { to: '/admin/workflows', label: 'Workflows', icon: GitMerge },
  { to: '/admin/holiday-calendar', label: 'Holidays', icon: Calendar },
  { to: '/admin/escalation-rules', label: 'Escalation', icon: AlertOctagon },
]

const accountingItems = [
  { to: '/admin/fees', label: 'Fee Rules', icon: Receipt },
  { to: '/billing/invoices', label: 'Invoices', icon: Receipt },
  { to: '/billing/receipts', label: 'Receipts', icon: Banknote },
  { to: '/billing/cash-application', label: 'Cash Application', icon: ArrowLeftRight },
  { to: '/billing/cash-distribution', label: 'Cash Distribution', icon: Landmark },
  { to: '/billing/disbursements', label: 'Disbursements', icon: Wallet },
  { to: '/billing/statement-reconciliation', label: 'Statement Recon', icon: FileInput },
  { to: '/billing/activity', label: 'Activity', icon: Activity },
  { to: '/billing/period-close', label: 'Period Close', icon: CalendarCheck },
  { to: '/billing/sync-health', label: 'Sync Health', icon: Wifi },
]

function NavItem({ to, label, icon: Icon }: { to: string; label: string; icon: React.ElementType }) {
  const [hovered, setHovered] = useState(false)
  return (
    <NavLink
      to={to}
      style={({ isActive }) => ({
        display: 'flex',
        alignItems: 'center',
        gap: 9,
        padding: '6px 10px',
        borderRadius: 'var(--r-sm)',
        fontSize: 12.5,
        fontWeight: isActive ? 600 : 500,
        color: isActive ? 'var(--accent-ink)' : hovered ? 'var(--ink)' : 'var(--ink-3)',
        background: isActive ? 'var(--accent-soft)' : hovered ? 'var(--hover)' : 'transparent',
        textDecoration: 'none',
        transition: 'background .1s, color .1s',
      })}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
    >
      <Icon style={{ width: 14, height: 14, flexShrink: 0, opacity: .85 }} />
      {label}
    </NavLink>
  )
}

function SectionLabel({ children }: { children: React.ReactNode }) {
  return (
    <div style={{
      padding: '14px 10px 4px',
      fontSize: 9.5,
      fontWeight: 700,
      letterSpacing: '.07em',
      textTransform: 'uppercase',
      color: 'var(--ink-4)',
    }}>
      {children}
    </div>
  )
}

export function Sidebar() {
  const hasRole = useAuthStore((s) => s.hasRole)
  const isAdmin = hasRole('Admin')

  return (
    <aside style={{
      width: 'var(--sidebar-w)',
      background: 'var(--surface)',
      borderRight: '1px solid var(--line)',
      display: 'flex',
      flexDirection: 'column',
      flexShrink: 0,
    }}>
      {/* Logo */}
      <div style={{
        padding: '14px 16px',
        borderBottom: '1px solid var(--line)',
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        minHeight: 'var(--topbar-h)',
      }}>
        <img src="/logo.png" alt="Specialty Market Managers" style={{ height: 32, width: 'auto' }} />
      </div>

      {/* Nav */}
      <nav style={{ flex: 1, padding: '8px 8px', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 1 }}>
        {navItems.map(({ to, label, icon, adminOnly, roles }) => {
          if (adminOnly && !isAdmin) return null
          if (roles && !roles.some((r) => hasRole(r))) return null
          return <NavItem key={to} to={to} label={label} icon={icon} />
        })}

        {isAdmin && (
          <>
            <SectionLabel>Accounting</SectionLabel>
            {accountingItems.map(({ to, label, icon }) => (
              <NavItem key={to} to={to} label={label} icon={icon} />
            ))}

            <SectionLabel>Rating Engine</SectionLabel>
            {ratingAdminItems.map(({ to, label, icon }) => (
              <NavItem key={to} to={to} label={label} icon={icon} />
            ))}

            <SectionLabel>Task Engine</SectionLabel>
            {adminTaskItems.map(({ to, label, icon }) => (
              <NavItem key={to} to={to} label={label} icon={icon} />
            ))}
          </>
        )}
      </nav>
    </aside>
  )
}
