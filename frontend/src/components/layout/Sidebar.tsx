import { useState } from 'react'
import { NavLink } from 'react-router-dom'
import { LayoutDashboard, Users, FileText, Building2, ShieldCheck, UserCheck, LayoutTemplate, Inbox, CheckSquare, Calendar, GitMerge, AlertOctagon, ListChecks, Receipt, Banknote, ArrowLeftRight, Landmark, Wallet, FileInput, Activity, CalendarCheck, Wifi, BarChart2, Sliders, FlaskConical, KeyRound, Database, Settings2, BookOpenCheck, FileCheck2, Hash, Bot } from 'lucide-react'
import { usePermissions } from '@/hooks/usePermissions'

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
  const perms = usePermissions()
  const { isAdmin } = perms

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
        <NavItem to="/dashboard" label="Dashboard" icon={LayoutDashboard} />
        {perms.canViewInbox && <NavItem to="/inbox" label="Inbox" icon={Inbox} />}
        <NavItem to="/tasks" label="My Tasks" icon={CheckSquare} />
        {perms.canViewInsureds && <NavItem to="/insureds" label="Insureds" icon={Building2} />}
        {perms.canViewPolicies && <NavItem to="/policies" label="Policies" icon={FileText} />}
        {perms.canViewSubmissions && <NavItem to="/submissions" label="Submissions" icon={FileText} />}
        {perms.canViewCarriers && <NavItem to="/carriers" label="Carriers" icon={ShieldCheck} />}
        {perms.canViewAgents && <NavItem to="/agents" label="Agents" icon={UserCheck} />}
        {perms.canViewReports && <NavItem to="/reports" label="Reports" icon={BarChart2} />}
        {perms.canViewComplianceDocumentation && <NavItem to="/compliance-documentation" label="Compliance Docs" icon={FileCheck2} />}

        {perms.canViewBilling && (
          <>
            <SectionLabel>Accounting</SectionLabel>
            <NavItem to="/billing/invoices" label="Invoices" icon={Receipt} />
            <NavItem to="/billing/receipts" label="Receipts" icon={Banknote} />
            <NavItem to="/billing/cash-application" label="Cash Application" icon={ArrowLeftRight} />
            <NavItem to="/billing/cash-distribution" label="Cash Distribution" icon={Landmark} />
            <NavItem to="/billing/disbursements" label="Disbursements" icon={Wallet} />
            <NavItem to="/billing/statement-reconciliation" label="Statement Recon" icon={FileInput} />
            <NavItem to="/billing/activity" label="Activity" icon={Activity} />
            <NavItem to="/billing/period-close" label="Period Close" icon={CalendarCheck} />
            <NavItem to="/billing/sync-health" label="Sync Health" icon={Wifi} />
          </>
        )}

        {(isAdmin || perms.canViewRatingAdmin || perms.canViewTaskAdmin || perms.canViewFeesAdmin || perms.canViewDocumentLibrary) && (
          <>
            <SectionLabel>Admin</SectionLabel>
            {isAdmin && <NavItem to="/users" label="Users" icon={Users} />}
            {isAdmin && <NavItem to="/admin/role-permissions" label="Role Permissions" icon={KeyRound} />}
            {isAdmin && <NavItem to="/admin/database-status" label="Database Status" icon={Database} />}
            {isAdmin && <NavItem to="/admin/jobs" label="Jobs" icon={Settings2} />}
            {isAdmin && <NavItem to="/admin/ai-settings" label="AI Settings" icon={Bot} />}
            {isAdmin && <NavItem to="/admin/legal-requirements" label="Legal Tracker" icon={BookOpenCheck} />}
            {perms.canViewDocumentLibrary && <NavItem to="/document-library" label="Doc Library" icon={LayoutTemplate} />}
            {perms.canViewDocumentLibrary && <NavItem to="/admin/policy-forms" label="Policy Forms" icon={LayoutTemplate} />}
            {perms.canViewDocumentLibrary && <NavItem to="/admin/policy-numbers" label="Policy Numbers" icon={Hash} />}
            {perms.canViewFeesAdmin && <NavItem to="/admin/fees" label="Charges & Fees" icon={Receipt} />}
            {perms.canViewRatingAdmin && <NavItem to="/admin/rating" label="Rating Plans" icon={Sliders} />}
            {perms.canViewRatingAdmin && <NavItem to="/admin/rating/shadow" label="Shadow Mode" icon={FlaskConical} />}
            {perms.canViewTaskAdmin && <NavItem to="/admin/task-types" label="Task Types" icon={ListChecks} />}
            {perms.canViewTaskAdmin && <NavItem to="/admin/workflows" label="Workflows" icon={GitMerge} />}
            {perms.canViewTaskAdmin && <NavItem to="/admin/holiday-calendar" label="Holidays" icon={Calendar} />}
            {perms.canViewTaskAdmin && <NavItem to="/admin/escalation-rules" label="Escalation" icon={AlertOctagon} />}
          </>
        )}
      </nav>
    </aside>
  )
}
