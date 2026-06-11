import { useState } from 'react'
import { Link, useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation } from '@tanstack/react-query'
import { Edit, Trash2, Plus, ArrowLeft, Download, Mail, Copy, ExternalLink } from 'lucide-react'
import { toast } from 'sonner'
import { insuredsApi } from '@/api/insureds.api'
import { submissionsApi } from '@/api/submissions.api'
import { policiesApi } from '@/api/policies.api'
import { downloadLossRunCsv } from '@/api/claims.api'
import { queryClient } from '@/lib/queryClient'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { DocumentsSection } from '@/components/documents/DocumentsSection'
import { SUBMISSION_STATUS_LABELS, type SubmissionStatus } from '@/types/submission.types'
import { LOB_LABELS } from '@/types/quote.types'
import { POLICY_STATUS_LABELS, type PolicyListItem } from '@/types/policy.types'
import { usePermissions } from '@/hooks/usePermissions'
import { getGoogleMapsApiKey } from '@/lib/clientConfig'

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

function staticMapUrl(lat: number, lng: number) {
  const key = getGoogleMapsApiKey()
  if (!key) return null
  const center = `${lat},${lng}`
  return `https://maps.googleapis.com/maps/api/staticmap?center=${center}&zoom=14&size=520x160&scale=2&markers=color:blue%7C${center}&key=${key}`
}

function initials(name: string): string {
  return name.split(' ').map((w) => w[0]).join('').slice(0, 2).toUpperCase()
}

function daysUntil(dateStr: string): number {
  const today = new Date(); today.setHours(0, 0, 0, 0)
  const target = new Date(dateStr); target.setHours(0, 0, 0, 0)
  return Math.round((target.getTime() - today.getTime()) / 86400000)
}

// ─── status pills ────────────────────────────────────────────────────────────

const SUB_PILL: Record<SubmissionStatus, string> = {
  New:        'new',
  InProgress: 'inprogress',
  Quoted:     'quoted',
  Bound:      'bound',
  Declined:   'declined',
  Withdrawn:  'withdrawn',
}

function policyPillVariant(p: PolicyListItem): { variant: string; label: string } {
  const days = daysUntil(p.expirationDate)
  if (p.status === 'Active' && days > 30) return { variant: 'good', label: 'Active' }
  if (p.status === 'Active' && days >= 0) return { variant: 'expiring', label: 'Expiring' }
  return { variant: 'withdrawn', label: POLICY_STATUS_LABELS[p.status] }
}

// ─── shared card primitives ──────────────────────────────────────────────────

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

function LobChip({ label }: { label: string }) {
  return (
    <span style={{
      fontSize: 10.5, padding: '2px 7px', borderRadius: 'var(--r-xs)',
      background: 'var(--surface-2)', color: 'var(--ink-2)', fontWeight: 500, lineHeight: 1.4,
    }}>{label}</span>
  )
}

// ─── policy table ────────────────────────────────────────────────────────────

function PolicyTable({ policies }: { policies: PolicyListItem[] }) {
  return (
    <table className="subs-table">
      <thead>
        <tr>
          {['Policy #', 'Line', 'Carrier', 'Term', 'Status', 'Premium', ''].map((h, i) => (
            <th key={i} className="subs-th" style={{ textAlign: i === 5 ? 'right' : 'left', width: i === 6 ? 32 : undefined }}>
              {h}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {policies.map((p) => {
          const days = daysUntil(p.expirationDate)
          const { variant, label } = policyPillVariant(p)
          return (
            <tr key={p.id} className="subs-row" onClick={() => window.location.href = `/policies/${p.id}`}>
              <td><span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--ink-3)' }}>{p.policyNumber}</span></td>
              <td><LobChip label={LOB_LABELS[p.lineOfBusiness]} /></td>
              <td style={{ color: 'var(--ink-2)' }}>{p.carrierName}</td>
              <td>
                <div style={{ fontVariantNumeric: 'tabular-nums', fontSize: 12.5 }}>
                  {new Date(p.effectiveDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })} →{' '}
                  {new Date(p.expirationDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}
                </div>
                {days >= 0 && days <= 60 && (
                  <div style={{ fontSize: 11, color: 'var(--warn-fg)', fontFamily: 'var(--font-mono)', marginTop: 2 }}>{days}d to renewal</div>
                )}
              </td>
              <td><span className={`sd-pill ${variant}`}>{label}</span></td>
              <td style={{ textAlign: 'right', fontWeight: 600, fontVariantNumeric: 'tabular-nums' }}>{fmtMoney(p.totalPremium)}</td>
              <td />
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}

// ─── submission table ────────────────────────────────────────────────────────

type useSubmissions = Awaited<ReturnType<typeof submissionsApi.getByInsured>>

function SubmissionTable({ subs, onOpen }: { subs: useSubmissions; onOpen: (id: string) => void }) {
  return (
    <table className="subs-table">
      <thead>
        <tr>
          {['Submission #', 'Lines', 'Status', 'Effective', 'Underwriter', 'Quotes', 'Created'].map((h) => (
            <th key={h} className="subs-th">{h}</th>
          ))}
        </tr>
      </thead>
      <tbody>
        {subs.map((s) => (
          <tr key={s.id} className="subs-row" onClick={() => onOpen(s.id)}>
            <td><span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--ink-3)' }}>{s.submissionNumber}</span></td>
            <td style={{ color: 'var(--ink-3)' }}>—</td>
            <td><span className={`sd-pill ${SUB_PILL[s.status]}`}>{SUBMISSION_STATUS_LABELS[s.status]}</span></td>
            <td style={{ fontVariantNumeric: 'tabular-nums', fontSize: 12.5 }}>
              {s.effectiveDate ? new Date(s.effectiveDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : '—'}
            </td>
            <td style={{ color: 'var(--ink-2)' }}>{s.underwriterName}</td>
            <td style={{ color: 'var(--ink-3)', fontSize: 12 }}>{s.quoteCount}</td>
            <td style={{ color: 'var(--ink-3)', fontSize: 12 }}>
              {new Date(s.createdAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

// ─── main component ──────────────────────────────────────────────────────────

type Tab = 'overview' | 'policies' | 'submissions' | 'documents'

export function InsuredDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { canEditInsureds, canDeleteInsureds, canCreatePolicies, canViewClaims } = usePermissions()
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
  ]

  return (
    <>
      {/* Back */}
      <button
        onClick={() => navigate('/insureds')}
        className="sd-btn outline sm"
        style={{ marginBottom: 14 }}
      >
        <ArrowLeft style={{ width: 13, height: 13 }} />
        Back to Insureds
      </button>

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
              <span className={`sd-pill ${insured.isActive ? 'good' : 'withdrawn'}`}>
                {insured.isActive ? 'Active client' : 'Inactive'}
              </span>
            </h1>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', fontSize: 12.5, color: 'var(--ink-3)' }}>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5 }}>{insured.id.slice(0, 8).toUpperCase()}</span>
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
            <a href={`mailto:${insured.email}`} className="sd-btn outline sm">
              <Mail style={{ width: 13, height: 13 }} /> Email
            </a>
          )}
          {canViewClaims && (
            <button
              className="sd-btn outline sm"
              onClick={() =>
                downloadLossRunCsv({ insuredId: id }).catch((err) =>
                  toast.error(err?.response?.status === 403
                    ? 'You do not have access to this insured’s loss run'
                    : 'Could not generate loss run'))
              }
            >
              <Download style={{ width: 13, height: 13 }} /> Loss Run
            </button>
          )}
          {canEditInsureds && (
            <Link to={`/insureds/${id}/edit`} className="sd-btn outline sm">
              <Edit style={{ width: 13, height: 13 }} /> Edit
            </Link>
          )}
          {canCreatePolicies && (
            <button className="sd-btn primary sm" onClick={() => navigate(`/submissions/new?insuredId=${id}`)}>
              <Plus style={{ width: 13, height: 13 }} /> New submission
            </button>
          )}
          {canDeleteInsureds && (
            <button
              className="sd-btn danger sm"
              onClick={() => { if (confirm('Delete this insured?')) deleteMutation.mutate() }}
            >
              <Trash2 style={{ width: 13, height: 13 }} />
            </button>
          )}
        </div>
      </header>

      {/* Metric strip */}
      <div className="sd-metrics five" style={{ marginBottom: 20 }}>
        <div className="sd-metric accent">
          <p className="k">In-force premium</p>
          <p className="v">{fmtMoneyK(inForcePremium)}</p>
          <p className="s">{activePolicies.length} active {activePolicies.length === 1 ? 'policy' : 'policies'}</p>
        </div>
        <div className="sd-metric">
          <p className="k">Open submissions</p>
          <p className="v">{openSubs.length}</p>
          <p className="s">{openSubs[0]?.submissionNumber ?? 'None active'}</p>
        </div>
        <div className="sd-metric">
          <p className="k">Lifetime premium</p>
          <p className="v">{fmtMoneyK(lifetimePremium)}</p>
          <p className="s">Across {policies.length} bound {policies.length === 1 ? 'policy' : 'policies'}</p>
        </div>
        <div className="sd-metric">
          <p className="k">3-yr loss ratio</p>
          <p className="v">—</p>
          <p className="s">No loss run data</p>
        </div>
        <div className="sd-metric">
          <p className="k">Renewal in</p>
          <p className="v" style={nearestExpiry != null && nearestExpiry <= 30 ? { color: 'var(--warn-fg)' } : {}}>
            {nearestExpiry != null ? `${nearestExpiry}d` : '—'}
          </p>
          <p className="s">
            {nearestExpiry != null ? `${activePolicies.length} ${activePolicies.length === 1 ? 'policy' : 'policies'} expiring` : 'No active policies'}
          </p>
        </div>
      </div>

      {/* 3-col info row */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gap: 14, marginBottom: 18 }}>
        {/* Contact */}
        <div className="sd-card">
          <div className="sd-card-head">
            <h3>Contact</h3>
            {insured.email && (
              <a href={`mailto:${insured.email}`} className="sd-btn sm outline">
                <Mail style={{ width: 12, height: 12 }} />
              </a>
            )}
          </div>
          <div className="sd-card-body">
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
        </div>

        {/* Address */}
        <div className="sd-card">
          <div className="sd-card-head">
            <h3>Address</h3>
            <div style={{ display: 'flex', gap: 6 }}>
              <button className="sd-btn sm"><Copy style={{ width: 12, height: 12 }} /></button>
              <button className="sd-btn sm"><ExternalLink style={{ width: 12, height: 12 }} /></button>
            </div>
          </div>
          <div className="sd-card-body">
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
              {insured.latitude != null && insured.longitude != null && (
                <div style={{ border: '1px solid var(--line-2)', borderRadius: 'var(--r)', overflow: 'hidden', background: 'var(--surface-2)' }}>
                  {staticMapUrl(insured.latitude, insured.longitude) ? (
                    <img
                      src={staticMapUrl(insured.latitude, insured.longitude)!}
                      alt="Insured location map"
                      style={{ width: '100%', height: 120, objectFit: 'cover', display: 'block' }}
                    />
                  ) : (
                    <div style={{ height: 96, display: 'grid', placeItems: 'center', color: 'var(--ink-4)', fontSize: 12.5 }}>
                      Location cached
                    </div>
                  )}
                  <div style={{ display: 'flex', justifyContent: 'space-between', gap: 10, padding: '8px 10px', fontSize: 11.5, color: 'var(--ink-3)' }}>
                    <span>{insured.geocodePrecision ?? insured.geocodeProvider ?? 'Geocoded'}</span>
                    <a
                      href={`https://www.google.com/maps/search/?api=1&query=${insured.latitude},${insured.longitude}`}
                      target="_blank"
                      rel="noreferrer"
                      style={{ color: 'var(--accent-ink)', fontWeight: 600, textDecoration: 'none' }}
                    >
                      Open map
                    </a>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Business profile */}
        <div className="sd-card">
          <div className="sd-card-head"><h3>Business profile</h3></div>
          <div className="sd-card-body">
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px 16px' }}>
              {insured.taxId && <Field label="FEIN" mono>{insured.taxId}</Field>}
              {insured.usDotNumber && <Field label="USDOT #" mono>{insured.usDotNumber}</Field>}
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
              {insured.entityType && <Field label="Entity type" colSpan>{insured.entityType}</Field>}
              {insured.dba && <Field label="DBA" colSpan>{insured.dba}</Field>}
              {!insured.taxId && !insured.usDotNumber && !insured.yearsInBusiness && !insured.entityType && (
                <p style={{ color: 'var(--ink-4)', fontSize: 12.5, gridColumn: '1 / -1' }}>No business profile on file.</p>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Tabbed area */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
        {/* Tab strip */}
        <div className="sd-tabs">
          {tabs.map(([key, label, count]) => (
            <button
              key={key}
              className={`sd-tab${tab === key ? ' active' : ''}`}
              onClick={() => setTab(key)}
            >
              {label}
              {count != null && <span className="cnt">{count}</span>}
            </button>
          ))}
        </div>

        {/* OVERVIEW */}
        {tab === 'overview' && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div className="sd-card" style={{ overflow: 'hidden' }}>
              <div className="sd-card-head">
                <h3>Policies in force <span className="cnt">{activePolicies.length}</span></h3>
                <div style={{ display: 'flex', gap: 6 }}>
                  <button className="sd-btn sm outline">
                    <Download style={{ width: 12, height: 12 }} />COI
                  </button>
                  <button className="sd-btn sm outline" onClick={() => setTab('policies')}>View all</button>
                </div>
              </div>
              {activePolicies.length === 0 ? (
                <div style={{ padding: '36px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>
                  No active policies.
                </div>
              ) : (
                <PolicyTable policies={activePolicies} />
              )}
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
              <div className="sd-card">
                <div className="sd-card-head"><h3>Loss history (5 yrs)</h3></div>
                <div style={{ padding: '36px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>
                  No loss run data on file.
                </div>
              </div>

              <div className="sd-card">
                <div className="sd-card-head">
                  <h3>Recent submissions</h3>
                  <button className="sd-btn sm outline" onClick={() => setTab('submissions')}>View all</button>
                </div>
                {submissions.length === 0 ? (
                  <div style={{ padding: '36px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>No submissions yet.</div>
                ) : (
                  <div>
                    {submissions.slice(0, 4).map((s, i) => (
                      <div
                        key={s.id}
                        onClick={() => navigate(`/submissions/${s.id}`)}
                        className="subs-row"
                        style={{
                          display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between',
                          padding: '10px 16px',
                          borderBottom: i < Math.min(submissions.length, 4) - 1 ? '1px solid var(--line-2)' : 'none',
                        }}
                      >
                        <div>
                          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--ink-3)' }}>{s.submissionNumber}</div>
                          <div style={{ fontSize: 12, color: 'var(--ink-3)', marginTop: 1 }}>{s.underwriterName}</div>
                        </div>
                        <span className={`sd-pill ${SUB_PILL[s.status]}`}>{SUBMISSION_STATUS_LABELS[s.status]}</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          </div>
        )}

        {/* POLICIES */}
        {tab === 'policies' && (
          <div className="sd-card" style={{ overflow: 'hidden' }}>
            <div className="sd-card-head">
              <h3>All policies <span className="cnt">{policies.length}</span></h3>
              <div style={{ display: 'flex', gap: 6 }}>
                <button className="sd-btn sm outline">
                  <Download style={{ width: 12, height: 12 }} />Export
                </button>
                {canCreatePolicies && (
                  <button className="sd-btn sm primary" onClick={() => navigate(`/submissions/new?insuredId=${id}`)}>
                    <Plus style={{ width: 12, height: 12 }} />New submission
                  </button>
                )}
              </div>
            </div>
            {policies.length === 0 ? (
              <div style={{ padding: '36px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>No bound policies yet.</div>
            ) : (
              <PolicyTable policies={policies} />
            )}
          </div>
        )}

        {/* SUBMISSIONS */}
        {tab === 'submissions' && (
          <div className="sd-card" style={{ overflow: 'hidden' }}>
            <div className="sd-card-head">
              <h3>All submissions <span className="cnt">{submissions.length}</span></h3>
              {canCreatePolicies && (
                <button className="sd-btn sm primary" onClick={() => navigate(`/submissions/new?insuredId=${id}`)}>
                  <Plus style={{ width: 12, height: 12 }} />New submission
                </button>
              )}
            </div>
            {submissions.length === 0 ? (
              <div style={{ padding: '36px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>
                No submissions yet.{' '}
                {canCreatePolicies && (
                  <span onClick={() => navigate(`/submissions/new?insuredId=${id}`)} style={{ color: 'var(--accent)', cursor: 'pointer' }}>
                    Create the first one.
                  </span>
                )}
              </div>
            ) : (
              <SubmissionTable subs={submissions} onOpen={(submissionId) => navigate(`/submissions/${submissionId}`)} />
            )}
          </div>
        )}

        {/* DOCUMENTS */}
        {tab === 'documents' && (
          <div className="sd-card">
            <div className="sd-card-head"><h3>Documents</h3></div>
            <div className="sd-card-body">
              <DocumentsSection entityId={id!} entityType="Insured" />
            </div>
          </div>
        )}

      </div>
    </>
  )
}
