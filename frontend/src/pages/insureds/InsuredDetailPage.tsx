import { useState } from 'react'
import { Link, useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation } from '@tanstack/react-query'
import { Edit, Trash2, Plus, ArrowLeft, Download, Mail, Copy, ExternalLink } from 'lucide-react'
import { toast } from 'sonner'
import { insuredsApi } from '@/api/insureds.api'
import { submissionsApi } from '@/api/submissions.api'
import { policiesApi } from '@/api/policies.api'
import { queryClient } from '@/lib/queryClient'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { DocumentsSection } from '@/components/documents/DocumentsSection'
import { SUBMISSION_STATUS_LABELS, type SubmissionStatus } from '@/types/submission.types'
import { LOB_LABELS } from '@/types/quote.types'
import { POLICY_STATUS_LABELS, type PolicyListItem } from '@/types/policy.types'
import { usePermissions } from '@/hooks/usePermissions'

// ─── helpers ────────────────────────────────────────────────────────────────

function fmtMoney(n: number | null | undefined): string {
  if (n == null) return '—'
  return '$' + n.toLocaleString('en-US', { maximumFractionDigits: 0 })
}

function fmtMoneyK(n: number | null | undefined): string {
  if (n == null) return '—'
  if (Math.abs(n) >= 1e6) return '$' + (n / 1e6).toFixed(2).replace(/\.?0+$/, '') + 'M'
  if (Math.abs(n) >= 1000) return '$' + Math.round(n / 1000) + 'K'
  return '$' + n.toLocaleString('en-US', { maximumFractionDigits: 0 })
}

function initials(name: string): string {
  return name.split(' ').map((w) => w[0]).join('').slice(0, 2).toUpperCase()
}

function daysUntil(dateStr: string): number {
  const today = new Date(); today.setHours(0, 0, 0, 0)
  const target = new Date(dateStr); target.setHours(0, 0, 0, 0)
  return Math.round((target.getTime() - today.getTime()) / 86400000)
}

// ─── status pill ────────────────────────────────────────────────────────────

const SUB_PILL: Record<SubmissionStatus, { bg: string; fg: string }> = {
  New:        { bg: 'var(--pill-draft-bg)',    fg: 'var(--pill-draft-fg)' },
  InProgress: { bg: 'var(--pill-inprog-bg)',   fg: 'var(--pill-inprog-fg)' },
  Quoted:     { bg: 'var(--pill-quoted-bg)',   fg: 'var(--pill-quoted-fg)' },
  Bound:      { bg: 'var(--pill-bound-bg)',    fg: 'var(--pill-bound-fg)' },
  Declined:   { bg: 'var(--pill-declined-bg)', fg: 'var(--pill-declined-fg)' },
  Withdrawn:  { bg: 'var(--pill-draft-bg)',    fg: 'var(--pill-draft-fg)' },
}

function Pill({ label, bg, fg }: { label: string; bg: string; fg: string }) {
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 6,
      padding: '3px 9px', borderRadius: 'var(--r-pill)',
      fontSize: 'var(--fs-sm)', fontWeight: 600, letterSpacing: '.005em',
      background: bg, color: fg,
    }}>
      <span style={{ width: 5, height: 5, borderRadius: '50%', background: 'currentColor', opacity: .85 }} />
      {label}
    </span>
  )
}

// ─── shared card primitives ──────────────────────────────────────────────────

function Card({ children, style }: { children: React.ReactNode; style?: React.CSSProperties }) {
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--line)',
      borderRadius: 'var(--r-xl)', boxShadow: 'var(--shadow-sm)', overflow: 'hidden', ...style,
    }}>{children}</div>
  )
}

function CardHead({ children }: { children: React.ReactNode }) {
  return (
    <div style={{
      display: 'flex', justifyContent: 'space-between', alignItems: 'center',
      padding: '13px 16px', borderBottom: '1px solid var(--line-2)',
    }}>{children}</div>
  )
}

function CardH3({ children }: { children: React.ReactNode }) {
  return (
    <h3 style={{ margin: 0, fontSize: 13, fontWeight: 600, color: 'var(--ink)', letterSpacing: '-.005em', display: 'flex', alignItems: 'center', gap: 8 }}>
      {children}
    </h3>
  )
}

function HeadCount({ n }: { n: number }) {
  return <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--ink-4)', fontWeight: 500 }}>{n}</span>
}

function BtnSm({ children, onClick, variant = 'ghost' }: {
  children: React.ReactNode; onClick?: () => void; variant?: 'ghost' | 'outline' | 'primary'
}) {
  const base: React.CSSProperties = {
    display: 'inline-flex', alignItems: 'center', gap: 5, height: 28, padding: '0 10px',
    fontSize: 12, fontWeight: 500, borderRadius: 'var(--r-sm)', cursor: 'pointer',
    border: 'none', background: 'none', color: 'var(--ink-2)',
  }
  if (variant === 'outline') return (
    <button onClick={onClick} style={{ ...base, border: '1px solid var(--line)', background: 'var(--surface)' }}
      onMouseEnter={(e) => { e.currentTarget.style.borderColor = 'var(--accent-light)'; e.currentTarget.style.background = 'var(--hover)' }}
      onMouseLeave={(e) => { e.currentTarget.style.borderColor = 'var(--line)'; e.currentTarget.style.background = 'var(--surface)' }}
    >{children}</button>
  )
  if (variant === 'primary') return (
    <button onClick={onClick} style={{ ...base, background: 'var(--accent)', color: '#fff', border: 'none' }}>{children}</button>
  )
  return <button onClick={onClick} style={{ ...base, color: 'var(--ink-3)' }}>{children}</button>
}

function Field({ label, children, mono = false, colSpan = false }: {
  label: string; children: React.ReactNode; mono?: boolean; colSpan?: boolean
}) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 2, ...(colSpan ? { gridColumn: '1 / -1' } : {}) }}>
      <span style={{ fontSize: 10.5, letterSpacing: '.06em', textTransform: 'uppercase', color: 'var(--ink-4)', fontWeight: 600 }}>
        {label}
      </span>
      <span style={{
        fontSize: 13, color: 'var(--ink)', lineHeight: 1.4,
        ...(mono ? { fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--ink-2)' } : {}),
      }}>
        {children}
      </span>
    </div>
  )
}

// ─── LOB chip ────────────────────────────────────────────────────────────────

function LobChip({ label }: { label: string }) {
  return (
    <span style={{
      fontSize: 10.5, padding: '2px 7px', borderRadius: 'var(--r-xs)',
      background: 'var(--surface-2)', color: 'var(--ink-2)', fontWeight: 500, lineHeight: 1.4,
    }}>{label}</span>
  )
}

// ─── policy rows  ────────────────────────────────────────────────────────────

function policyPill(p: PolicyListItem) {
  const days = daysUntil(p.expirationDate)
  if (p.status === 'Active' && days > 30) return { label: 'Active', bg: 'var(--good-bg)', fg: 'var(--good-fg)' }
  if (p.status === 'Active' && days >= 0) return { label: 'Expiring', bg: 'var(--warn-bg)', fg: 'var(--warn-fg)' }
  return { label: POLICY_STATUS_LABELS[p.status], bg: 'var(--pill-draft-bg)', fg: 'var(--pill-draft-fg)' }
}

function PolicyTable({ policies }: { policies: PolicyListItem[] }) {
  return (
    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
      <thead>
        <tr>
          {['Policy #', 'Line', 'Carrier', 'Term', 'Status', 'Premium', ''].map((h, i) => (
            <th key={i} className="id-th" style={{ textAlign: i === 5 ? 'right' : 'left', width: i === 6 ? 32 : undefined }}>
              {h}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {policies.map((p) => {
          const days = daysUntil(p.expirationDate)
          const pill = policyPill(p)
          return (
            <tr key={p.id} className="id-tr" onClick={() => window.location.href = `/policies/${p.id}`}>
              <td className="id-td"><span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--ink-3)' }}>{p.policyNumber}</span></td>
              <td className="id-td"><LobChip label={LOB_LABELS[p.lineOfBusiness]} /></td>
              <td className="id-td" style={{ color: 'var(--ink-2)' }}>{p.carrierName}</td>
              <td className="id-td">
                <div style={{ fontVariantNumeric: 'tabular-nums', fontSize: 12.5 }}>
                  {new Date(p.effectiveDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })} →{' '}
                  {new Date(p.expirationDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}
                </div>
                {days >= 0 && days <= 60 && (
                  <div style={{ fontSize: 11, color: '#b33a2a', fontFamily: 'var(--font-mono)', marginTop: 2 }}>{days}d to renewal</div>
                )}
              </td>
              <td className="id-td"><Pill label={pill.label} bg={pill.bg} fg={pill.fg} /></td>
              <td className="id-td" style={{ textAlign: 'right', fontWeight: 600, fontVariantNumeric: 'tabular-nums' }}>{fmtMoney(p.totalPremium)}</td>
              <td className="id-td" />
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}

// ─── submission table ────────────────────────────────────────────────────────

function SubmissionTable({ subs, onNew }: { subs: useSubmissions; onNew: () => void }) {
  return (
    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
      <thead>
        <tr>
          {['Submission #', 'Lines', 'Status', 'Effective', 'Underwriter', 'Quotes', 'Created'].map((h, i) => (
            <th key={i} className="id-th" style={{ textAlign: 'left' }}>{h}</th>
          ))}
        </tr>
      </thead>
      <tbody>
        {subs.map((s) => {
          const pill = SUB_PILL[s.status]
          return (
            <tr key={s.id} className="id-tr" onClick={() => window.location.href = `/submissions/${s.id}`}>
              <td className="id-td"><span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--ink-3)' }}>{s.submissionNumber}</span></td>
              <td className="id-td">—</td>
              <td className="id-td"><Pill label={SUBMISSION_STATUS_LABELS[s.status]} bg={pill.bg} fg={pill.fg} /></td>
              <td className="id-td" style={{ fontVariantNumeric: 'tabular-nums', fontSize: 12.5 }}>
                {s.effectiveDate ? new Date(s.effectiveDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : '—'}
              </td>
              <td className="id-td" style={{ color: 'var(--ink-2)' }}>{s.underwriterName}</td>
              <td className="id-td" style={{ color: 'var(--ink-3)', fontSize: 12 }}>{s.quoteCount}</td>
              <td className="id-td" style={{ color: 'var(--ink-3)', fontSize: 12 }}>
                {new Date(s.createdAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}
              </td>
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}

type useSubmissions = Awaited<ReturnType<typeof submissionsApi.getByInsured>>

// ─── main component ──────────────────────────────────────────────────────────

type Tab = 'overview' | 'policies' | 'submissions' | 'documents' | 'activity'

export function InsuredDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { canEditInsureds, canDeleteInsureds, canCreatePolicies } = usePermissions()
  const [tab, setTab] = useState<Tab>('overview')

  const { data: insured, isLoading } = useQuery({
    queryKey: ['insureds', id],
    queryFn: () => insuredsApi.getById(id!),
  })

  const { data: submissions = [] } = useQuery({
    queryKey: ['submissions', 'by-insured', id],
    queryFn: () => submissionsApi.getByInsured(id!),
    enabled: !!id,
  })

  const { data: policies = [] } = useQuery({
    queryKey: ['policies', 'by-insured', id],
    queryFn: () => policiesApi.getByInsured(id!),
    enabled: !!id,
  })

  const deleteMutation = useMutation({
    mutationFn: () => insuredsApi.delete(id!),
    onSuccess: () => {
      toast.success('Insured deleted')
      queryClient.invalidateQueries({ queryKey: ['insureds'] })
      navigate('/insureds')
    },
    onError: () => toast.error('Failed to delete insured'),
  })

  if (isLoading) return <LoadingSpinner />
  if (!insured) return <p style={{ padding: 24, color: 'var(--ink-3)' }}>Insured not found.</p>

  // ── computed metrics ──────────────────────────────────────────────────────
  const activePolicies = policies.filter((p) => daysUntil(p.expirationDate) >= 0)
  const inForcePremium = activePolicies.reduce((s, p) => s + p.totalPremium, 0)
  const lifetimePremium = policies.reduce((s, p) => s + p.totalPremium, 0)
  const openSubs = submissions.filter((s) => !['Bound', 'Declined', 'Withdrawn'].includes(s.status))
  const nearestExpiry = activePolicies.length
    ? Math.min(...activePolicies.map((p) => daysUntil(p.expirationDate)))
    : null

  const mark = initials(insured.displayName)

  const tabs: [Tab, string, number | null][] = [
    ['overview', 'Overview', null],
    ['policies', 'Policies', policies.length],
    ['submissions', 'Submissions', submissions.length],
    ['documents', 'Documents', null],
    ['activity', 'Activity', null],
  ]

  return (
    <>
      <style>{`
        .id-th { text-align: left; font-weight: 500; color: var(--ink-3); font-size: 11px; padding: 9px 14px; background: var(--surface-2); border-bottom: 1px solid var(--line); letter-spacing: .02em; text-transform: uppercase; white-space: nowrap; }
        .id-tr { border-bottom: 1px solid var(--line-2); transition: background .1s; cursor: pointer; }
        .id-tr:last-child { border-bottom: 0; }
        .id-tr:hover { background: var(--hover); }
        .id-td { padding: 11px 14px; vertical-align: middle; }
        .id-back:hover { color: var(--accent-ink); }
        .id-tab { padding: 8px 12px; font-size: 12.5px; color: var(--ink-3); cursor: pointer; font-weight: 500; display: inline-flex; align-items: center; gap: 6px; border-bottom: 2px solid transparent; margin-bottom: -1px; transition: color .1s; }
        .id-tab:hover { color: var(--ink-2); }
        .id-tab.active { color: var(--accent-ink); font-weight: 600; border-bottom-color: var(--accent); }
        .id-tab .c { font-family: var(--font-mono); font-size: 11px; color: var(--ink-4); font-weight: 500; }
        .id-tab.active .c { color: var(--accent); }
        .id-metric { background: var(--surface); border: 1px solid var(--line); border-radius: var(--r-lg); padding: 12px 14px; box-shadow: var(--shadow-sm); }
        .id-metric.accent { background: var(--accent-soft); border-color: #cfe0ef; }
        .id-doc:hover { background: var(--hover); cursor: pointer; }
        .id-activity li::before { content: ""; position: absolute; left: 22px; top: 24px; bottom: -6px; width: 1px; background: var(--line); }
        .id-activity li:last-child::before { display: none; }
      `}</style>

      {/* Back */}
      <a
        className="id-back"
        onClick={() => navigate('/insureds')}
        style={{ display: 'inline-flex', alignItems: 'center', gap: 6, color: 'var(--ink-3)', fontSize: 12.5, marginBottom: 14, cursor: 'pointer', fontWeight: 500 }}
      >
        <ArrowLeft style={{ width: 13, height: 13 }} /> Back to Insureds
      </a>

      {/* Page header */}
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 24, marginBottom: 20 }}>
        <div style={{ display: 'flex', gap: 16, alignItems: 'flex-start', minWidth: 0 }}>
          {/* initials mark */}
          <div style={{
            width: 52, height: 52, borderRadius: 12, background: 'var(--accent-soft)', color: 'var(--accent-ink)',
            display: 'grid', placeItems: 'center', fontSize: 18, fontWeight: 700, letterSpacing: '-.02em',
            flexShrink: 0, border: '1px solid #cfe0ef',
          }}>
            {mark}
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6, minWidth: 0 }}>
            <h1 style={{ margin: 0, fontSize: 24, fontWeight: 600, letterSpacing: '-.015em', lineHeight: 1.15, display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap', color: 'var(--ink)' }}>
              {insured.displayName}
              <Pill
                label={insured.isActive ? 'Active client' : 'Inactive'}
                bg={insured.isActive ? 'var(--good-bg)' : 'var(--pill-draft-bg)'}
                fg={insured.isActive ? 'var(--good-fg)' : 'var(--pill-draft-fg)'}
              />
            </h1>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', fontSize: 12.5, color: 'var(--ink-3)' }}>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--ink-3)' }}>{insured.id.slice(0, 8).toUpperCase()}</span>
              <span style={{ color: 'var(--ink-4)' }}>·</span>
              <span>{insured.insuredType}</span>
              {insured.dba && <>
                <span style={{ color: 'var(--ink-4)' }}>·</span>
                <span>DBA {insured.dba}</span>
              </>}
              <span style={{ color: 'var(--ink-4)' }}>·</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, padding: '2px 6px', borderRadius: 'var(--r-xs)', background: 'var(--surface-2)', color: 'var(--ink-3)', fontWeight: 600 }}>
                {insured.state}
              </span>
              <span style={{ color: 'var(--ink-4)' }}>·</span>
              <span>{insured.city}, {insured.state}</span>
              {insured.createdAt && <>
                <span style={{ color: 'var(--ink-4)' }}>·</span>
                <span>Client since {new Date(insured.createdAt).toLocaleDateString('en-US', { month: 'short', year: 'numeric' })}</span>
              </>}
            </div>
          </div>
        </div>

        {/* Actions */}
        <div style={{ display: 'flex', gap: 8, flexShrink: 0 }}>
          {insured.email && (
            <a href={`mailto:${insured.email}`} style={{
              display: 'inline-flex', alignItems: 'center', gap: 6, padding: '0 12px', height: 32,
              borderRadius: 'var(--r)', fontSize: 12.5, fontWeight: 500, color: 'var(--ink-2)',
              border: '1px solid var(--line)', background: 'var(--surface)', cursor: 'pointer', textDecoration: 'none',
            }}>
              <Mail style={{ width: 13, height: 13 }} /> Email
            </a>
          )}
          {canEditInsureds && (
            <Link to={`/insureds/${id}/edit`} style={{
              display: 'inline-flex', alignItems: 'center', gap: 6, padding: '0 12px', height: 32,
              borderRadius: 'var(--r)', fontSize: 12.5, fontWeight: 500, color: 'var(--ink-2)',
              border: '1px solid var(--line)', background: 'var(--surface)', textDecoration: 'none',
            }}>
              <Edit style={{ width: 13, height: 13 }} /> Edit
            </Link>
          )}
          {canCreatePolicies && (
            <button
              onClick={() => navigate(`/submissions/new?insuredId=${id}`)}
              style={{
                display: 'inline-flex', alignItems: 'center', gap: 6, padding: '0 12px', height: 32,
                borderRadius: 'var(--r)', fontSize: 12.5, fontWeight: 500, color: '#fff',
                background: 'var(--accent)', border: 'none', cursor: 'pointer',
              }}
            >
              <Plus style={{ width: 13, height: 13 }} /> New submission
            </button>
          )}
          {canDeleteInsureds && (
            <button
              onClick={() => { if (confirm('Delete this insured?')) deleteMutation.mutate() }}
              style={{
                display: 'inline-flex', alignItems: 'center', justifyContent: 'center', width: 32, height: 32,
                borderRadius: 'var(--r)', fontSize: 12.5, color: 'var(--bad-fg)',
                border: '1px solid var(--line)', background: 'var(--surface)', cursor: 'pointer',
              }}
            >
              <Trash2 style={{ width: 13, height: 13 }} />
            </button>
          )}
        </div>
      </header>

      {/* Metric strip */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5,1fr)', gap: 10, marginBottom: 20 }}>
        <div className="id-metric accent">
          <div style={{ fontSize: 10.5, letterSpacing: '.04em', textTransform: 'uppercase', color: 'var(--accent-ink)', opacity: .75, fontWeight: 600, margin: '0 0 4px' }}>In-force premium</div>
          <div style={{ fontSize: 20, fontWeight: 600, letterSpacing: '-.015em', lineHeight: 1, color: 'var(--accent-ink)', fontVariantNumeric: 'tabular-nums' }}>{fmtMoneyK(inForcePremium)}</div>
          <div style={{ color: 'var(--ink-3)', fontSize: 11.5, marginTop: 5 }}>{activePolicies.length} active {activePolicies.length === 1 ? 'policy' : 'policies'}</div>
        </div>
        <div className="id-metric">
          <div style={{ fontSize: 10.5, letterSpacing: '.04em', textTransform: 'uppercase', color: 'var(--ink-3)', fontWeight: 600, margin: '0 0 4px' }}>Open submissions</div>
          <div style={{ fontSize: 20, fontWeight: 600, letterSpacing: '-.015em', lineHeight: 1, fontVariantNumeric: 'tabular-nums' }}>{openSubs.length}</div>
          <div style={{ color: 'var(--ink-3)', fontSize: 11.5, marginTop: 5 }}>{openSubs[0]?.submissionNumber ?? 'None active'}</div>
        </div>
        <div className="id-metric">
          <div style={{ fontSize: 10.5, letterSpacing: '.04em', textTransform: 'uppercase', color: 'var(--ink-3)', fontWeight: 600, margin: '0 0 4px' }}>Lifetime premium</div>
          <div style={{ fontSize: 20, fontWeight: 600, letterSpacing: '-.015em', lineHeight: 1, fontVariantNumeric: 'tabular-nums' }}>{fmtMoneyK(lifetimePremium)}</div>
          <div style={{ color: 'var(--ink-3)', fontSize: 11.5, marginTop: 5 }}>Across {policies.length} bound {policies.length === 1 ? 'policy' : 'policies'}</div>
        </div>
        <div className="id-metric">
          <div style={{ fontSize: 10.5, letterSpacing: '.04em', textTransform: 'uppercase', color: 'var(--ink-3)', fontWeight: 600, margin: '0 0 4px' }}>3-yr loss ratio</div>
          <div style={{ fontSize: 20, fontWeight: 600, letterSpacing: '-.015em', lineHeight: 1 }}>—</div>
          <div style={{ color: 'var(--ink-3)', fontSize: 11.5, marginTop: 5 }}>No loss run data</div>
        </div>
        <div className="id-metric">
          <div style={{ fontSize: 10.5, letterSpacing: '.04em', textTransform: 'uppercase', color: 'var(--ink-3)', fontWeight: 600, margin: '0 0 4px' }}>Renewal in</div>
          <div style={{ fontSize: 20, fontWeight: 600, letterSpacing: '-.015em', lineHeight: 1, ...(nearestExpiry != null && nearestExpiry <= 30 ? { color: '#b33a2a' } : {}) }}>
            {nearestExpiry != null ? `${nearestExpiry}d` : '—'}
          </div>
          <div style={{ color: 'var(--ink-3)', fontSize: 11.5, marginTop: 5 }}>
            {nearestExpiry != null ? `${activePolicies.length} ${activePolicies.length === 1 ? 'policy' : 'policies'} expiring` : 'No active policies'}
          </div>
        </div>
      </div>

      {/* 3-col info row */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gap: 14, marginBottom: 18 }}>
        {/* Contact */}
        <Card>
          <CardHead>
            <CardH3>Contact</CardH3>
            <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
              {insured.email && <a href={`mailto:${insured.email}`}><BtnSm><Mail style={{ width: 12, height: 12 }} /></BtnSm></a>}
            </div>
          </CardHead>
          <div style={{ padding: '14px 16px' }}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              {insured.email && (
                <Field label="Email">
                  <a href={`mailto:${insured.email}`} style={{ color: 'var(--accent-ink)', fontWeight: 500 }}>{insured.email}</a>
                </Field>
              )}
              {insured.phone && (
                <Field label="Phone" mono>
                  {insured.phone}
                  <span style={{ color: 'var(--ink-4)', fontSize: 11, marginLeft: 6 }}>main</span>
                  {insured.phoneAlt && (
                    <div style={{ marginTop: 2 }}>
                      {insured.phoneAlt}
                      <span style={{ color: 'var(--ink-4)', fontSize: 11, marginLeft: 6 }}>alt</span>
                    </div>
                  )}
                </Field>
              )}
              {!insured.email && !insured.phone && (
                <p style={{ color: 'var(--ink-4)', fontSize: 12.5 }}>No contact info on file.</p>
              )}
            </div>
          </div>
        </Card>

        {/* Address */}
        <Card>
          <CardHead>
            <CardH3>Address</CardH3>
            <div style={{ display: 'flex', gap: 6 }}>
              <BtnSm><Copy style={{ width: 12, height: 12 }} /></BtnSm>
              <BtnSm><ExternalLink style={{ width: 12, height: 12 }} /></BtnSm>
            </div>
          </CardHead>
          <div style={{ padding: '14px 16px' }}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              <Field label="Physical">
                {insured.addressLine1}
                {insured.addressLine2 && <><br />{insured.addressLine2}</>}
                <br />{insured.city}, {insured.state} {insured.zipCode}
                {insured.county && <><br />{insured.county} County</>}
              </Field>
              <Field label="Mailing">
                <span style={{ color: 'var(--ink-3)' }}>Same as above</span>
              </Field>
            </div>
          </div>
        </Card>

        {/* Business profile */}
        <Card>
          <CardHead><CardH3>Business profile</CardH3></CardHead>
          <div style={{ padding: '14px 16px' }}>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px 16px' }}>
              {insured.taxId && (
                <Field label="FEIN" mono>{insured.taxId}</Field>
              )}
              {insured.usDotNumber && (
                <Field label="USDOT #" mono>{insured.usDotNumber}</Field>
              )}
              {!insured.usDotNumber && (
                <Field label="USDOT #">
                  <Link to={`/insureds/${insured.id}/edit`} style={{ color: 'var(--accent-ink)', fontWeight: 500 }}>
                    Add USDOT number
                  </Link>
                </Field>
              )}
              {insured.yearsInBusiness != null && (
                <Field label="Years in business">{insured.yearsInBusiness} years</Field>
              )}
              {insured.entityType && (
                <Field label="Entity type" colSpan>{insured.entityType}</Field>
              )}
              {insured.dba && (
                <Field label="DBA" colSpan>{insured.dba}</Field>
              )}
              {!insured.taxId && !insured.usDotNumber && !insured.yearsInBusiness && !insured.entityType && (
                <p style={{ color: 'var(--ink-4)', fontSize: 12.5, gridColumn: '1 / -1' }}>No business profile on file.</p>
              )}
            </div>
          </div>
        </Card>
      </div>

      {/* Tabbed area */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
        {/* Tab strip */}
        <div style={{ display: 'flex', gap: 2, borderBottom: '1px solid var(--line)', padding: '0 4px' }}>
          {tabs.map(([key, label, count]) => (
            <button
              key={key}
              className={`id-tab${tab === key ? ' active' : ''}`}
              onClick={() => setTab(key)}
            >
              {label}
              {count != null && <span className="c">{count}</span>}
            </button>
          ))}
        </div>

        {/* OVERVIEW */}
        {tab === 'overview' && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <Card>
              <CardHead>
                <CardH3>Policies in force <HeadCount n={activePolicies.length} /></CardH3>
                <div style={{ display: 'flex', gap: 6 }}>
                  <BtnSm variant="outline"><Download style={{ width: 12, height: 12 }} />COI</BtnSm>
                  <BtnSm variant="outline" onClick={() => setTab('policies')}>View all</BtnSm>
                </div>
              </CardHead>
              {activePolicies.length === 0 ? (
                <div style={{ padding: '36px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>
                  No active policies.
                </div>
              ) : (
                <PolicyTable policies={activePolicies} />
              )}
            </Card>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
              {/* Loss history stub */}
              <Card>
                <CardHead><CardH3>Loss history (5 yrs)</CardH3></CardHead>
                <div style={{ padding: '36px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>
                  No loss run data on file.
                </div>
              </Card>

              {/* Recent submissions */}
              <Card>
                <CardHead>
                  <CardH3>Recent submissions</CardH3>
                  <BtnSm variant="outline" onClick={() => setTab('submissions')}>View all</BtnSm>
                </CardHead>
                {submissions.length === 0 ? (
                  <div style={{ padding: '36px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>No submissions yet.</div>
                ) : (
                  <div>
                    {submissions.slice(0, 4).map((s, i) => {
                      const pill = SUB_PILL[s.status]
                      return (
                        <div
                          key={s.id}
                          onClick={() => navigate(`/submissions/${s.id}`)}
                          style={{
                            display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between',
                            padding: '10px 16px', borderBottom: i < Math.min(submissions.length, 4) - 1 ? '1px solid var(--line-2)' : 'none',
                            cursor: 'pointer',
                          }}
                          onMouseEnter={(e) => e.currentTarget.style.background = 'var(--hover)'}
                          onMouseLeave={(e) => e.currentTarget.style.background = 'transparent'}
                        >
                          <div>
                            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--ink-3)' }}>{s.submissionNumber}</div>
                            <div style={{ fontSize: 12, color: 'var(--ink-3)', marginTop: 1 }}>{s.underwriterName}</div>
                          </div>
                          <Pill label={SUBMISSION_STATUS_LABELS[s.status]} bg={pill.bg} fg={pill.fg} />
                        </div>
                      )
                    })}
                  </div>
                )}
              </Card>
            </div>
          </div>
        )}

        {/* POLICIES */}
        {tab === 'policies' && (
          <Card>
            <CardHead>
              <CardH3>All policies <HeadCount n={policies.length} /></CardH3>
              <div style={{ display: 'flex', gap: 6 }}>
                <BtnSm variant="outline"><Download style={{ width: 12, height: 12 }} />Export</BtnSm>
                {canCreatePolicies && (
                  <BtnSm variant="primary" onClick={() => navigate(`/submissions/new?insuredId=${id}`)}>
                    <Plus style={{ width: 12, height: 12 }} />New submission
                  </BtnSm>
                )}
              </div>
            </CardHead>
            {policies.length === 0 ? (
              <div style={{ padding: '36px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>No bound policies yet.</div>
            ) : (
              <PolicyTable policies={policies} />
            )}
          </Card>
        )}

        {/* SUBMISSIONS */}
        {tab === 'submissions' && (
          <Card>
            <CardHead>
              <CardH3>All submissions <HeadCount n={submissions.length} /></CardH3>
              <div style={{ display: 'flex', gap: 6 }}>
                {canCreatePolicies && (
                  <BtnSm variant="primary" onClick={() => navigate(`/submissions/new?insuredId=${id}`)}>
                    <Plus style={{ width: 12, height: 12 }} />New submission
                  </BtnSm>
                )}
              </div>
            </CardHead>
            {submissions.length === 0 ? (
              <div style={{ padding: '36px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>
                No submissions yet.{' '}
                {canCreatePolicies && (
                  <span
                    onClick={() => navigate(`/submissions/new?insuredId=${id}`)}
                    style={{ color: 'var(--accent)', cursor: 'pointer' }}
                  >
                    Create the first one.
                  </span>
                )}
              </div>
            ) : (
              <SubmissionTable subs={submissions} onNew={() => navigate(`/submissions/new?insuredId=${id}`)} />
            )}
          </Card>
        )}

        {/* DOCUMENTS */}
        {tab === 'documents' && (
          <Card>
            <CardHead>
              <CardH3>Documents</CardH3>
            </CardHead>
            <div style={{ padding: '16px' }}>
              <DocumentsSection entityId={id!} entityType="Insured" />
            </div>
          </Card>
        )}

        {/* ACTIVITY */}
        {tab === 'activity' && (
          <Card>
            <CardHead><CardH3>Activity</CardH3></CardHead>
            <div style={{ padding: '36px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>
              Activity log coming soon.
            </div>
          </Card>
        )}
      </div>
    </>
  )
}
