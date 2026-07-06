import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, AlertTriangle, Plus, FileText } from 'lucide-react'
import { toast } from 'sonner'
import {
  getBordereauxProfiles,
  createBordereauxProfile,
  updateBordereauxProfile,
} from '@/api/bordereaux.api'
import { carriersApi } from '@/api/carriers.api'
import { programConfigurationsApi } from '@/api/programConfigurations.api'
import {
  BordereauxProfileSetupPanel,
  bordereauxProfileToRequest,
} from '@/components/bordereaux/BordereauxProfileSetupPanel'
import { ErrorState } from '@/components/common/ErrorState'
import { getApiErrorMessage } from '@/lib/apiError'
import { US_STATES } from '@/constants/usStates'
import { ACTIVE_LOBS, LOB_LABELS } from '@/types/quote.types'
import { DEFAULT_BDX_TXN_TYPES, type BordereauxProfile, type UpsertBordereauxProfileRequest } from '@/types/bordereaux.types'

// ── Constants ─────────────────────────────────────────────────────────────────
// Values must match the backend enums exactly (BordereauxReportType /
// BordereauxFrequency / BordereauxOutputFormat) — extend the backend first
// before offering new options here.

const REPORT_TYPES = ['Premium'] as const
const FREQUENCIES  = ['Monthly'] as const
const OUTPUT_FMTS  = ['Xlsx'] as const
const DATE_BASES   = ['EffectiveOrBoundDateGreater', 'EffectiveDate', 'BoundDate'] as const

function blankRequest(): UpsertBordereauxProfileRequest {
  return {
    name: '',
    programConfigurationId: '',
    carrierId: '',
    lineOfBusiness: null,
    stateCode: null,
    reportType: 'Premium',
    frequency: 'Monthly',
    outputFormat: 'Xlsx',
    dateBasis: 'EffectiveOrBoundDateGreater',
    requiresAccountCurrent: true,
    isActive: true,
    requiredTabsJson: '[]',
    requiredColumnsJson: '[]',
    mappingRulesJson: '{}',
    staticValuesJson: '{}',
    validationRulesJson: '{}',
    includedTransactionTypesJson: DEFAULT_BDX_TXN_TYPES,
    notes: null,
  }
}

// ── Sub-components ────────────────────────────────────────────────────────────

function ReadinessBadge({ profile }: { profile: BordereauxProfile }) {
  const ready = profile.setupStatus.isReadyForExport
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 4,
      padding: '2px 8px', borderRadius: 999, fontSize: 11, fontWeight: 700,
      background: ready ? 'var(--good-soft, #f0fdf4)' : 'var(--warn-soft, #fffbeb)',
      color: ready ? 'var(--good-fg, #16a34a)' : 'var(--warn-fg, #92400e)',
    }}>
      {ready ? <CheckCircle2 size={10} /> : <AlertTriangle size={10} />}
      {ready ? 'Ready' : `${profile.setupStatus.missingItems} missing`}
    </span>
  )
}

function ActiveBadge({ active }: { active: boolean }) {
  return (
    <span style={{
      padding: '1px 7px', borderRadius: 999, fontSize: 11, fontWeight: 600,
      background: active ? 'var(--accent-soft)' : 'var(--surface-2)',
      color: active ? 'var(--accent-ink)' : 'var(--ink-4)',
    }}>
      {active ? 'Active' : 'Inactive'}
    </span>
  )
}

// ── Create / Edit form ────────────────────────────────────────────────────────

function ProfileForm({
  initial,
  onSave,
  onCancel,
  isSaving,
}: {
  initial: UpsertBordereauxProfileRequest
  onSave: (req: UpsertBordereauxProfileRequest) => void
  onCancel: () => void
  isSaving: boolean
}) {
  const [form, setForm] = useState<UpsertBordereauxProfileRequest>(initial)
  const set = <K extends keyof UpsertBordereauxProfileRequest>(k: K, v: UpsertBordereauxProfileRequest[K]) =>
    setForm(prev => ({ ...prev, [k]: v }))

  const { data: programs = [] } = useQuery({
    queryKey: ['admin', 'program-configurations', 'active'],
    queryFn: () => programConfigurationsApi.getAll(false),
  })
  const { data: carriers = [] } = useQuery({
    queryKey: ['carriers', 'active'],
    queryFn: () => carriersApi.getAll(true),
  })

  const lobOptions = ACTIVE_LOBS.map((lob) => [lob, LOB_LABELS[lob]] as [string, string])

  const field: React.CSSProperties = { display: 'flex', flexDirection: 'column', gap: 4 }
  const label: React.CSSProperties = { fontSize: 11, fontWeight: 700, color: 'var(--ink-3)', textTransform: 'uppercase', letterSpacing: '0.04em' }

  return (
    <div style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-md)', padding: 20, background: 'var(--surface)' }}>
      <h3 style={{ margin: '0 0 16px', fontSize: 14, fontWeight: 700, color: 'var(--ink)' }}>
        {initial.name ? `Edit: ${initial.name}` : 'New Bordereaux Profile'}
      </h3>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14, marginBottom: 16 }}>
        <div style={{ ...field, gridColumn: '1 / -1' }}>
          <label style={label}>Name</label>
          <input className="sd-input" value={form.name} onChange={e => set('name', e.target.value)} placeholder="e.g. Lloyd's GL – DALE Monthly" />
        </div>

        <div style={field}>
          <label style={label}>Program</label>
          <select className="sd-input" value={form.programConfigurationId} onChange={e => set('programConfigurationId', e.target.value)}>
            <option value="">— select —</option>
            {programs.map(p => <option key={p.id} value={p.id}>{p.name}{p.code ? ` (${p.code})` : ''}</option>)}
          </select>
        </div>

        <div style={field}>
          <label style={label}>Carrier</label>
          <select className="sd-input" value={form.carrierId} onChange={e => set('carrierId', e.target.value)}>
            <option value="">— select —</option>
            {carriers.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </div>

        <div style={field}>
          <label style={label}>Line of Business (optional)</label>
          <select className="sd-input" value={form.lineOfBusiness ?? ''} onChange={e => set('lineOfBusiness', e.target.value || null)}>
            <option value="">All lines</option>
            {lobOptions.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
          </select>
        </div>

        <div style={field}>
          <label style={label}>State Code (optional)</label>
          <select className="sd-input" value={form.stateCode ?? ''} onChange={e => set('stateCode', e.target.value ? e.target.value.toUpperCase() : null)}>
            <option value="">All states</option>
            {US_STATES.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>

        <div style={field}>
          <label style={label}>Report Type</label>
          <select className="sd-input" value={form.reportType} onChange={e => set('reportType', e.target.value)}>
            {REPORT_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
          </select>
        </div>

        <div style={field}>
          <label style={label}>Frequency</label>
          <select className="sd-input" value={form.frequency} onChange={e => set('frequency', e.target.value)}>
            {FREQUENCIES.map(f => <option key={f} value={f}>{f}</option>)}
          </select>
        </div>

        <div style={field}>
          <label style={label}>Output Format</label>
          <select className="sd-input" value={form.outputFormat} onChange={e => set('outputFormat', e.target.value)}>
            {OUTPUT_FMTS.map(f => <option key={f} value={f}>{f}</option>)}
          </select>
        </div>

        <div style={field}>
          <label style={label}>Date Basis</label>
          <select className="sd-input" value={form.dateBasis} onChange={e => set('dateBasis', e.target.value)}>
            {DATE_BASES.map(b => <option key={b} value={b}>{b}</option>)}
          </select>
        </div>

        <div style={{ ...field, gridColumn: '1 / -1' }}>
          <label style={label}>Notes</label>
          <textarea className="sd-input" value={form.notes ?? ''} onChange={e => set('notes', e.target.value || null)} rows={2} style={{ resize: 'vertical' }} />
        </div>

        <div style={{ ...field, gridColumn: '1 / -1', flexDirection: 'row', gap: 20 }}>
          <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, cursor: 'pointer' }}>
            <input type="checkbox" checked={form.requiresAccountCurrent} onChange={e => set('requiresAccountCurrent', e.target.checked)} />
            Requires Account Current
          </label>
          <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, cursor: 'pointer' }}>
            <input type="checkbox" checked={form.isActive} onChange={e => set('isActive', e.target.checked)} />
            Active
          </label>
        </div>
      </div>

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
        <button className="sd-btn outline" onClick={onCancel} disabled={isSaving}>Cancel</button>
        <button
          className="sd-btn primary"
          disabled={isSaving || !form.name || !form.programConfigurationId || !form.carrierId}
          onClick={() => onSave(form)}
        >
          {isSaving ? 'Saving…' : 'Save Profile'}
        </button>
      </div>
    </div>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function AdminBordereauxProfilesPage() {
  const qc = useQueryClient()
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [showEdit, setShowEdit] = useState(false)
  const [showInactive, setShowInactive] = useState(false)

  const { data: profiles = [], isLoading, isError, error, refetch } = useQuery({
    queryKey: ['bordereaux', 'profiles', showInactive],
    queryFn: () => getBordereauxProfiles({ includeInactive: showInactive }),
  })

  const selected = profiles.find(p => p.id === selectedId) ?? null

  const create = useMutation({
    mutationFn: (req: UpsertBordereauxProfileRequest) => createBordereauxProfile(req),
    onSuccess: (profile) => {
      toast.success('Profile created')
      qc.invalidateQueries({ queryKey: ['bordereaux', 'profiles'] })
      setShowCreate(false)
      setSelectedId(profile.id)
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed to create profile')),
  })

  const update = useMutation({
    mutationFn: ({ id, req }: { id: string; req: UpsertBordereauxProfileRequest }) =>
      updateBordereauxProfile(id, req),
    onSuccess: (profile) => {
      toast.success('Profile saved')
      qc.invalidateQueries({ queryKey: ['bordereaux', 'profiles'] })
      qc.setQueryData(['bordereaux', 'profile', profile.id], profile)
      setShowEdit(false)
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed to save profile')),
  })

  function handleSetupSave(patch: BordereauxProfile) {
    update.mutate({ id: patch.id, req: bordereauxProfileToRequest(patch) })
  }

  function handleToggleActive(profile: BordereauxProfile) {
    const req = { ...bordereauxProfileToRequest(profile), isActive: !profile.isActive }
    update.mutate({ id: profile.id, req })
  }

  return (
    <div style={{ display: 'flex', height: '100%', minHeight: 0 }}>
      {/* Left: profile list */}
      <div style={{ width: 300, flexShrink: 0, borderRight: '1px solid var(--line)', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
        <div style={{ padding: '16px 16px 12px', borderBottom: '1px solid var(--line)', flexShrink: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
            <h1 style={{ margin: 0, fontSize: 16, fontWeight: 700, color: 'var(--ink)' }}>Bordereaux Profiles</h1>
            <button className="sd-btn primary sm" onClick={() => { setShowCreate(true); setShowEdit(false); setSelectedId(null) }}>
              <Plus size={13} /> New
            </button>
          </div>
          <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, color: 'var(--ink-3)', cursor: 'pointer' }}>
            <input type="checkbox" checked={showInactive} onChange={e => setShowInactive(e.target.checked)} />
            Show inactive
          </label>
        </div>

        <div style={{ flex: 1, overflowY: 'auto' }}>
          {isLoading && <p style={{ padding: 16, fontSize: 13, color: 'var(--ink-3)' }}>Loading…</p>}
          {isError && <ErrorState error={error} onRetry={refetch} />}
          {!isLoading && !isError && profiles.length === 0 && (
            <div style={{ padding: 24, textAlign: 'center', color: 'var(--ink-4)', fontSize: 13 }}>
              <FileText size={24} style={{ margin: '0 auto 8px', opacity: 0.4 }} />
              No profiles yet. Click New to create one.
            </div>
          )}
          {profiles.map(p => (
            <button
              key={p.id}
              onClick={() => { setSelectedId(p.id); setShowCreate(false); setShowEdit(false) }}
              style={{
                width: '100%', textAlign: 'left', padding: '10px 14px',
                border: 'none', borderBottom: '1px solid var(--line-2)', cursor: 'pointer',
                background: selectedId === p.id ? 'var(--accent-soft)' : 'transparent',
                opacity: p.isActive ? 1 : 0.55,
              }}
            >
              <div style={{ fontSize: 13, fontWeight: 600, color: selectedId === p.id ? 'var(--accent-ink)' : 'var(--ink)', marginBottom: 3, lineHeight: 1.3 }}>
                {p.name}
              </div>
              <div style={{ fontSize: 11, color: 'var(--ink-3)', marginBottom: 5 }}>
                {p.programName} · {p.carrierName}
                {p.lineOfBusiness ? ` · ${LOB_LABELS[p.lineOfBusiness as keyof typeof LOB_LABELS] ?? p.lineOfBusiness}` : ''}
              </div>
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                <ReadinessBadge profile={p} />
                <ActiveBadge active={p.isActive} />
              </div>
            </button>
          ))}
        </div>
      </div>

      {/* Right: detail / create */}
      <div style={{ flex: 1, overflowY: 'auto', padding: 24 }}>
        {showCreate && (
          <ProfileForm
            initial={blankRequest()}
            onSave={req => create.mutate(req)}
            onCancel={() => setShowCreate(false)}
            isSaving={create.isPending}
          />
        )}

        {!showCreate && showEdit && selected && (
          <ProfileForm
            initial={bordereauxProfileToRequest(selected)}
            onSave={req => update.mutate({ id: selected.id, req })}
            onCancel={() => setShowEdit(false)}
            isSaving={update.isPending}
          />
        )}

        {!showCreate && !showEdit && selected && (
          <div>
            <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: 20, gap: 12, flexWrap: 'wrap' }}>
              <div>
                <h2 style={{ margin: '0 0 4px', fontSize: 17, fontWeight: 700, color: 'var(--ink)' }}>{selected.name}</h2>
                <div style={{ fontSize: 12.5, color: 'var(--ink-3)' }}>
                  {selected.programName} · {selected.carrierName}
                  {selected.lineOfBusiness ? ` · ${LOB_LABELS[selected.lineOfBusiness as keyof typeof LOB_LABELS] ?? selected.lineOfBusiness}` : ''}
                  {selected.stateCode ? ` · ${selected.stateCode}` : ''}
                </div>
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                <button
                  className="sd-btn outline sm"
                  disabled={update.isPending}
                  onClick={() => setShowEdit(true)}
                >
                  Edit details
                </button>
                <button
                  className="sd-btn outline sm"
                  disabled={update.isPending}
                  onClick={() => handleToggleActive(selected)}
                >
                  {selected.isActive ? 'Deactivate' : 'Activate'}
                </button>
              </div>
            </div>

            {/* Profile metadata */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(140px, 1fr))', gap: 12, marginBottom: 24 }}>
              {[
                ['Report Type', selected.reportType],
                ['Frequency', selected.frequency],
                ['Output Format', selected.outputFormat],
                ['Date Basis', selected.dateBasis],
                ['Account Current', selected.requiresAccountCurrent ? 'Yes' : 'No'],
              ].map(([lbl, val]) => (
                <div key={lbl} style={{ background: 'var(--surface-2)', borderRadius: 'var(--r-sm)', padding: '8px 12px' }}>
                  <div style={{ fontSize: 10, fontWeight: 700, color: 'var(--ink-4)', textTransform: 'uppercase', letterSpacing: '0.04em', marginBottom: 3 }}>{lbl}</div>
                  <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink)' }}>{val}</div>
                </div>
              ))}
            </div>

            {selected.notes && (
              <p style={{ marginBottom: 20, fontSize: 13, color: 'var(--ink-3)', fontStyle: 'italic' }}>{selected.notes}</p>
            )}

            <BordereauxProfileSetupPanel
              profile={selected}
              isSaving={update.isPending}
              onSave={handleSetupSave}
              lineOfBusinessOptions={Object.entries(LOB_LABELS).map(([v, l]) => ({ value: v, label: l }))}
            />
          </div>
        )}

        {!showCreate && !selected && (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: '100%', color: 'var(--ink-4)', gap: 8 }}>
            <FileText size={32} style={{ opacity: 0.3 }} />
            <p style={{ margin: 0, fontSize: 13 }}>Select a profile to view or edit its setup</p>
          </div>
        )}
      </div>
    </div>
  )
}
