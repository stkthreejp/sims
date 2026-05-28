import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, CheckCircle2, Download, FileSpreadsheet, RefreshCcw, Save, Search, Settings2, X } from 'lucide-react'
import { toast } from 'sonner'
import {
  createBordereauxPremiumRun,
  generateBordereauxExportPackage,
  getAccountCurrentDownloadUrl,
  getBordereauxPremiumPreview,
  getBordereauxProfiles,
  getBordereauxRun,
  getBordereauxRuns,
  getLondonBordereauxDownloadUrl,
  reconcileBordereauxRun,
  updateBordereauxProfile,
} from '@/api/bordereaux.api'
import type {
  BordereauxPremiumPreview,
  BordereauxPremiumPreviewRow,
  BordereauxProfile,
  BordereauxProfileSetupItem,
  BordereauxRun,
  ReconcileBordereauxRunRequest,
  UpsertBordereauxProfileRequest,
} from '@/types/bordereaux.types'

function money(value: number) {
  return value.toLocaleString('en-US', { style: 'currency', currency: 'USD' })
}

function formatDate(value?: string | null) {
  if (!value) return '-'
  return new Date(`${value}T00:00:00`).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

function currentMonthValue() {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`
}

function monthBounds(month: string) {
  const [yearText, monthText] = month.split('-')
  const year = Number(yearText)
  const monthIndex = Number(monthText) - 1
  const lastDay = new Date(year, monthIndex + 1, 0).getDate()
  return {
    periodStart: `${yearText}-${monthText}-01`,
    periodEnd: `${yearText}-${monthText}-${String(lastDay).padStart(2, '0')}`,
  }
}

function safeRows(run?: BordereauxRun | null): BordereauxPremiumPreviewRow[] {
  if (!run?.sourceRowsSnapshotJson) return []
  try {
    return JSON.parse(run.sourceRowsSnapshotJson) as BordereauxPremiumPreviewRow[]
  } catch {
    return []
  }
}

function summarizeRows(rows: BordereauxPremiumPreviewRow[]) {
  return rows.reduce(
    (sum, row) => ({
      rowCount: sum.rowCount + 1,
      grossPremium: sum.grossPremium + row.grossPremium,
      grossCommission: sum.grossCommission + row.grossCommission,
      fees: sum.fees + row.fees,
      netDueCarrier: sum.netDueCarrier + row.netDueCarrier,
    }),
    { rowCount: 0, grossPremium: 0, grossCommission: 0, fees: 0, netDueCarrier: 0 },
  )
}

interface ValidationSummary {
  status?: string
  errors?: number
  warnings?: number
  missingLondonLobSetupRows?: number
  missingSurplusLinesSetupRows?: number
  items?: Array<{
    code?: string
    policyNumber?: string
    transactionNumber?: string
    lineOfBusiness?: string
    insuredState?: string
    reportingDate?: string
    message?: string
  }>
}

function parseValidationSummary(run?: BordereauxRun | null): ValidationSummary | null {
  if (!run?.validationSummaryJson) return null
  try {
    return JSON.parse(run.validationSummaryJson) as ValidationSummary
  } catch {
    return null
  }
}

function profileLabel(profile: BordereauxProfile) {
  const scope = [profile.programName, profile.carrierName, profile.lineOfBusiness, profile.stateCode]
    .filter(Boolean)
    .join(' / ')
  return scope ? `${profile.name} (${scope})` : profile.name
}

function parseStringArray(json: string): string[] {
  try {
    const parsed = JSON.parse(json)
    return Array.isArray(parsed)
      ? parsed.filter((value): value is string => typeof value === 'string' && value.trim().length > 0).map((value) => value.trim())
      : []
  } catch {
    return []
  }
}

function parseStringRecord(json: string): Record<string, string> {
  try {
    const parsed = JSON.parse(json)
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return {}
    return Object.fromEntries(
      Object.entries(parsed)
        .filter(([, value]) => typeof value === 'string')
        .map(([key, value]) => [key, String(value)]),
    )
  } catch {
    return {}
  }
}

function profileToRequest(profile: BordereauxProfile): UpsertBordereauxProfileRequest {
  return {
    name: profile.name,
    programConfigurationId: profile.programConfigurationId,
    carrierId: profile.carrierId,
    lineOfBusiness: profile.lineOfBusiness,
    stateCode: profile.stateCode,
    reportType: profile.reportType,
    frequency: profile.frequency,
    outputFormat: profile.outputFormat,
    dateBasis: profile.dateBasis,
    requiresAccountCurrent: profile.requiresAccountCurrent,
    isActive: profile.isActive,
    requiredTabsJson: profile.requiredTabsJson,
    requiredColumnsJson: profile.requiredColumnsJson,
    mappingRulesJson: profile.mappingRulesJson,
    staticValuesJson: profile.staticValuesJson,
    validationRulesJson: profile.validationRulesJson,
    includedTransactionTypesJson: profile.includedTransactionTypesJson,
    notes: profile.notes,
  }
}

function setupBadgeColors(status: string) {
  if (status === 'Configured') return { background: '#f0fdf4', color: '#166534', border: '#bbf7d0' }
  if (status === 'Default') return { background: '#eff6ff', color: '#1d4ed8', border: '#bfdbfe' }
  if (status === 'Missing') return { background: '#fffbeb', color: '#92400e', border: '#fde68a' }
  return { background: 'var(--surface-2)', color: 'var(--ink-3)', border: 'var(--line)' }
}

function SetupBadge({ status }: { status: string }) {
  const colors = setupBadgeColors(status)
  return (
    <span style={{
      border: `1px solid ${colors.border}`,
      borderRadius: 6,
      background: colors.background,
      color: colors.color,
      padding: '2px 6px',
      fontSize: 10.5,
      fontWeight: 750,
      lineHeight: 1.2,
    }}>
      {status}
    </span>
  )
}

function SetupItemList({ title, items }: { title: string; items: BordereauxProfileSetupItem[] }) {
  return (
    <div style={setupGroupStyle}>
      <div style={setupGroupTitle}>{title}</div>
      <div style={{ display: 'grid', gap: 7 }}>
        {items.map((item) => (
          <div key={item.key} style={setupRowStyle}>
            <span style={{ color: 'var(--ink)', fontSize: 12.5, fontWeight: 650 }}>{item.label}</span>
            <SetupBadge status={item.status} />
          </div>
        ))}
      </div>
    </div>
  )
}

function ProfileSetupPanel({
  profile,
  isSaving,
  onSave,
}: {
  profile: BordereauxProfile
  isSaving: boolean
  onSave: (profile: BordereauxProfile) => void
}) {
  const [isEditing, setIsEditing] = useState(false)
  const [selectedTabs, setSelectedTabs] = useState<Set<string>>(new Set())
  const [staticForm, setStaticForm] = useState<Record<string, string>>({})

  useEffect(() => {
    setSelectedTabs(new Set(parseStringArray(profile.requiredTabsJson).map((tab) => tab.toLowerCase())))
    const staticValues = parseStringRecord(profile.staticValuesJson)
    setStaticForm(Object.fromEntries(profile.setupStatus.staticValues.map((item) => [item.key, staticValues[item.key] ?? ''])))
    setIsEditing(false)
  }, [profile.id, profile.requiredTabsJson, profile.staticValuesJson, profile.setupStatus.staticValues])

  function toggleTab(tab: string) {
    const key = tab.toLowerCase()
    setSelectedTabs((current) => {
      const next = new Set(current)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  function saveSetup() {
    const knownTabs = new Set(profile.setupStatus.requiredTabs.map((item) => item.key.toLowerCase()))
    const existingTabs = parseStringArray(profile.requiredTabsJson)
    const otherTabs = existingTabs.filter((tab) => !knownTabs.has(tab.toLowerCase()))
    const requiredTabs = [
      ...otherTabs,
      ...profile.setupStatus.requiredTabs.filter((item) => selectedTabs.has(item.key.toLowerCase())).map((item) => item.key),
    ]

    const staticValues = parseStringRecord(profile.staticValuesJson)
    profile.setupStatus.staticValues.forEach((item) => {
      const value = staticForm[item.key]?.trim() ?? ''
      if (value) staticValues[item.key] = value
      else delete staticValues[item.key]
    })

    onSave({
      ...profile,
      requiredTabsJson: JSON.stringify(requiredTabs),
      staticValuesJson: JSON.stringify(staticValues),
    })
  }

  const hasIssues = !profile.setupStatus.isReadyForExport

  return (
    <section style={section}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, alignItems: 'center', marginBottom: 12 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {hasIssues ? <AlertTriangle size={16} color="#92400e" /> : <CheckCircle2 size={16} color="#166534" />}
          <div>
            <div style={sectionTitle}>Profile Setup</div>
            <div style={{ marginTop: 2, color: hasIssues ? '#92400e' : '#166534', fontSize: 12, fontWeight: 700 }}>
              {hasIssues ? `${profile.setupStatus.missingItems} missing setup item${profile.setupStatus.missingItems === 1 ? '' : 's'}` : 'Ready for export'}
            </div>
          </div>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          {isEditing && (
            <button type="button" className="sd-btn outline" disabled={isSaving} onClick={() => setIsEditing(false)}>
              <X size={14} />
              Cancel
            </button>
          )}
          <button
            type="button"
            className={isEditing ? 'sd-btn primary' : 'sd-btn outline'}
            disabled={isSaving}
            onClick={() => (isEditing ? saveSetup() : setIsEditing(true))}
          >
            {isEditing ? <Save size={14} /> : <Settings2 size={14} />}
            {isEditing ? 'Save Setup' : 'Edit Setup'}
          </button>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: 14 }}>
        <div style={setupGroupStyle}>
          <div style={setupGroupTitle}>Required Tabs</div>
          <div style={{ display: 'grid', gap: 7 }}>
            {profile.setupStatus.requiredTabs.map((item) => (
              <label key={item.key} style={setupRowStyle}>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8, minWidth: 0 }}>
                  {isEditing && (
                    <input
                      type="checkbox"
                      checked={selectedTabs.has(item.key.toLowerCase())}
                      onChange={() => toggleTab(item.key)}
                    />
                  )}
                  <span style={{ color: 'var(--ink)', fontSize: 12.5, fontWeight: 650 }}>{item.label}</span>
                </span>
                <SetupBadge status={selectedTabs.has(item.key.toLowerCase()) ? 'Configured' : 'Missing'} />
              </label>
            ))}
          </div>
        </div>

        <div style={setupGroupStyle}>
          <div style={setupGroupTitle}>Static Values</div>
          <div style={{ display: 'grid', gap: 8 }}>
            {profile.setupStatus.staticValues.map((item) => (
              <div key={item.key} style={{ display: 'grid', gap: 5 }}>
                <div style={setupRowStyle}>
                  <span style={{ color: 'var(--ink)', fontSize: 12.5, fontWeight: 650 }}>{item.label}</span>
                  <SetupBadge status={item.status} />
                </div>
                {isEditing ? (
                  <input
                    value={staticForm[item.key] ?? ''}
                    placeholder={item.defaultValue ?? ''}
                    onChange={(event) => setStaticForm((form) => ({ ...form, [item.key]: event.target.value }))}
                    style={inputStyle}
                  />
                ) : (
                  <div style={{ color: 'var(--ink-3)', fontSize: 12, overflowWrap: 'anywhere' }}>
                    {item.value ?? item.defaultValue ?? '-'}
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>

        <SetupItemList title="Required Columns" items={profile.setupStatus.requiredColumns} />
        <SetupItemList title="Mapping Rules" items={profile.setupStatus.mappingRules} />
      </div>
    </section>
  )
}

function ValidationPanel({ run }: { run: BordereauxRun }) {
  const summary = parseValidationSummary(run)
  if (!summary) return null

  const warningCount = summary.warnings ?? summary.items?.length ?? 0
  const errorCount = summary.errors ?? 0
  const hasIssues = warningCount > 0 || errorCount > 0

  return (
    <div style={{
      border: `1px solid ${hasIssues ? '#f59e0b' : '#bbf7d0'}`,
      borderRadius: 8,
      background: hasIssues ? '#fffbeb' : '#f0fdf4',
      padding: 12,
      marginBottom: 12,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: hasIssues ? '#92400e' : '#166534', fontSize: 13, fontWeight: 750 }}>
        {hasIssues ? <AlertTriangle size={15} /> : <CheckCircle2 size={15} />}
        {hasIssues ? `${warningCount} setup warning${warningCount === 1 ? '' : 's'}` : 'No setup warnings recorded'}
      </div>
      {hasIssues && (
        <>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 8, fontSize: 12, color: '#92400e' }}>
            <span>Missing London LOB setup: {summary.missingLondonLobSetupRows ?? 0}</span>
            <span>Missing surplus lines setup: {summary.missingSurplusLinesSetupRows ?? 0}</span>
          </div>
          <div style={{ display: 'grid', gap: 6, marginTop: 10 }}>
            {(summary.items ?? []).slice(0, 8).map((item, index) => (
              <div key={`${item.code}-${item.policyNumber}-${item.transactionNumber}-${index}`} style={{ borderTop: '1px solid #fde68a', paddingTop: 6, fontSize: 12, color: '#78350f' }}>
                <strong>{item.policyNumber ?? 'Policy'} {item.transactionNumber ? `/ ${item.transactionNumber}` : ''}</strong>
                <span> - {item.message ?? item.code}</span>
                <div style={{ marginTop: 2, color: '#92400e' }}>
                  {[item.lineOfBusiness, item.insuredState, formatDate(item.reportingDate)].filter(Boolean).join(' / ')}
                </div>
              </div>
            ))}
            {(summary.items?.length ?? 0) > 8 && (
              <div style={{ fontSize: 12, color: '#92400e' }}>{(summary.items?.length ?? 0) - 8} more warning rows in the audit JSON.</div>
            )}
          </div>
        </>
      )}
    </div>
  )
}

function StatusPill({ status }: { status: string }) {
  const matched = status === 'Matched'
  const mismatch = status === 'Mismatch'
  const bg = matched ? 'var(--green-soft, #f0fdf4)' : mismatch ? 'var(--red-soft, #fef2f2)' : 'var(--surface-2)'
  const color = matched ? '#166534' : mismatch ? 'var(--red, #b91c1c)' : 'var(--ink-3)'
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5, borderRadius: 6, padding: '3px 7px', background: bg, color, fontSize: 11, fontWeight: 700 }}>
      {matched ? <CheckCircle2 size={12} /> : mismatch ? <AlertTriangle size={12} /> : null}
      {status}
    </span>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ border: '1px solid var(--line)', borderRadius: 8, background: 'var(--surface)', padding: '12px 14px', minWidth: 150 }}>
      <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--ink-3)', textTransform: 'uppercase' }}>{label}</div>
      <div style={{ marginTop: 5, fontSize: 18, fontWeight: 700, color: 'var(--ink)' }}>{value}</div>
    </div>
  )
}

function PreviewTable({ preview }: { preview?: BordereauxPremiumPreview }) {
  const rows = preview?.rows ?? []
  return (
    <div style={{ overflowX: 'auto', border: '1px solid var(--line)', borderRadius: 8, background: 'var(--surface)' }}>
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
        <thead>
          <tr style={{ background: 'var(--surface-2)', borderBottom: '1px solid var(--line)' }}>
            {['Reporting Date', 'Policy', 'Transaction', 'Insured', 'State', 'Gross Premium', 'Commission', 'Fees', 'Net Due'].map((h) => (
              <th key={h} style={{ ...th, textAlign: ['Gross Premium', 'Commission', 'Fees', 'Net Due'].includes(h) ? 'right' : 'left' }}>{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={`${row.policyTransactionId}-${row.invoiceId}`} style={{ borderBottom: '1px solid var(--line-2)' }}>
              <td style={td}>{formatDate(row.reportingDate)}</td>
              <td style={{ ...td, fontWeight: 600 }}>{row.policyNumber}</td>
              <td style={td}>{row.transactionType}</td>
              <td style={td}>{row.insuredName}</td>
              <td style={td}>{row.insuredState}</td>
              <td style={{ ...td, textAlign: 'right' }}>{money(row.grossPremium)}</td>
              <td style={{ ...td, textAlign: 'right' }}>{money(row.grossCommission)}</td>
              <td style={{ ...td, textAlign: 'right' }}>{money(row.fees)}</td>
              <td style={{ ...td, textAlign: 'right', fontWeight: 700 }}>{money(row.netDueCarrier)}</td>
            </tr>
          ))}
          {rows.length === 0 && (
            <tr>
              <td colSpan={9} style={{ ...td, textAlign: 'center', color: 'var(--ink-4)', padding: 22 }}>No rows for this selection</td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  )
}

function ReconciliationPanel({ run, onReconciled }: { run?: BordereauxRun; onReconciled: (run: BordereauxRun) => void }) {
  const rows = useMemo(() => safeRows(run), [run])
  const totals = useMemo(() => summarizeRows(rows), [rows])
  const [form, setForm] = useState({
    rowCount: 0,
    grossPremium: 0,
    grossCommission: 0,
    fees: 0,
    netDueCarrier: 0,
  })

  useEffect(() => {
    setForm({
      rowCount: totals.rowCount,
      grossPremium: totals.grossPremium,
      grossCommission: totals.grossCommission,
      fees: totals.fees,
      netDueCarrier: totals.netDueCarrier,
    })
  }, [totals])

  const mutation = useMutation({
    mutationFn: (request: ReconcileBordereauxRunRequest) => reconcileBordereauxRun(run!.id, request),
    onSuccess: (updated) => {
      toast.success(updated.reconciliationStatus === 'Matched' ? 'BDX and Account Current match' : 'Reconciliation mismatch recorded')
      onReconciled(updated)
    },
    onError: () => toast.error('Could not reconcile this run'),
  })

  if (!run) return null

  return (
    <section style={section}>
      <div style={sectionTitle}>Account Current Check</div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: 10 }}>
        <NumberField label="Rows" value={form.rowCount} onChange={(value) => setForm((f) => ({ ...f, rowCount: value }))} />
        <NumberField label="Gross Premium" value={form.grossPremium} onChange={(value) => setForm((f) => ({ ...f, grossPremium: value }))} />
        <NumberField label="Commission" value={form.grossCommission} onChange={(value) => setForm((f) => ({ ...f, grossCommission: value }))} />
        <NumberField label="Fees" value={form.fees} onChange={(value) => setForm((f) => ({ ...f, fees: value }))} />
        <NumberField label="Net Due" value={form.netDueCarrier} onChange={(value) => setForm((f) => ({ ...f, netDueCarrier: value }))} />
      </div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 12 }}>
        <button
          className="sd-btn primary"
          disabled={mutation.isPending}
          onClick={() => mutation.mutate({
            accountCurrentRowCount: form.rowCount,
            accountCurrentGrossPremiumTotal: form.grossPremium,
            accountCurrentGrossCommissionTotal: form.grossCommission,
            accountCurrentFeesTotal: form.fees,
            accountCurrentNetDueCarrierTotal: form.netDueCarrier,
          })}
        >
          <CheckCircle2 size={14} />
          Reconcile
        </button>
      </div>
    </section>
  )
}

function NumberField({ label, value, onChange }: { label: string; value: number; onChange: (value: number) => void }) {
  return (
    <label style={{ display: 'grid', gap: 5 }}>
      <span style={labelStyle}>{label}</span>
      <input
        type="number"
        step="0.01"
        value={value}
        onChange={(event) => onChange(Number(event.target.value))}
        style={inputStyle}
      />
    </label>
  )
}

function openDownload(url: string, fileName?: string | null) {
  const link = document.createElement('a')
  link.href = url
  if (fileName) link.download = fileName
  link.rel = 'noopener'
  document.body.appendChild(link)
  link.click()
  link.remove()
}

export function BordereauxWorkbenchPage() {
  const queryClient = useQueryClient()
  const [profileId, setProfileId] = useState('')
  const [month, setMonth] = useState(currentMonthValue)
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null)
  const period = useMemo(() => monthBounds(month), [month])

  const profilesQuery = useQuery({
    queryKey: ['bordereaux', 'profiles'],
    queryFn: getBordereauxProfiles,
  })
  const premiumProfiles = useMemo(
    () => (profilesQuery.data ?? []).filter((profile) => profile.reportType === 'Premium'),
    [profilesQuery.data],
  )

  useEffect(() => {
    if (!profileId && premiumProfiles.length > 0) setProfileId(premiumProfiles[0].id)
  }, [premiumProfiles, profileId])

  const previewQuery = useQuery({
    queryKey: ['bordereaux', 'preview', profileId, period.periodStart, period.periodEnd],
    queryFn: () => getBordereauxPremiumPreview(profileId, period.periodStart, period.periodEnd),
    enabled: Boolean(profileId),
  })

  const runsQuery = useQuery({
    queryKey: ['bordereaux', 'runs', profileId],
    queryFn: () => getBordereauxRuns(profileId),
    enabled: Boolean(profileId),
  })

  const selectedRunQuery = useQuery({
    queryKey: ['bordereaux', 'run', selectedRunId],
    queryFn: () => getBordereauxRun(selectedRunId!),
    enabled: Boolean(selectedRunId),
  })

  const createRun = useMutation({
    mutationFn: () => createBordereauxPremiumRun(profileId, period.periodStart, period.periodEnd),
    onSuccess: (run) => {
      toast.success(`Run ${run.runNumber} created`)
      setSelectedRunId(run.id)
      queryClient.invalidateQueries({ queryKey: ['bordereaux', 'runs', profileId] })
      queryClient.invalidateQueries({ queryKey: ['bordereaux', 'run', run.id] })
    },
    onError: () => toast.error('Could not create the monthly run'),
  })

  const generatePackage = useMutation({
    mutationFn: (runId: string) => generateBordereauxExportPackage(runId),
    onSuccess: (run) => {
      toast.success('Export package generated')
      queryClient.setQueryData(['bordereaux', 'run', run.id], run)
      queryClient.invalidateQueries({ queryKey: ['bordereaux', 'runs', profileId] })
    },
    onError: () => toast.error('Could not generate the export package'),
  })

  const downloadFile = useMutation({
    mutationFn: async ({ run, kind }: { run: BordereauxRun; kind: 'london' | 'accountCurrent' }) => {
      const url = kind === 'london'
        ? await getLondonBordereauxDownloadUrl(run.id)
        : await getAccountCurrentDownloadUrl(run.id)
      return {
        url,
        fileName: kind === 'london' ? run.londonBordereauxFileName : run.accountCurrentFileName,
      }
    },
    onSuccess: ({ url, fileName }) => openDownload(url, fileName),
    onError: () => toast.error('Could not get the download link'),
  })

  const updateProfile = useMutation({
    mutationFn: (profile: BordereauxProfile) => updateBordereauxProfile(profile.id, profileToRequest(profile)),
    onSuccess: (updated) => {
      toast.success('BDX profile setup saved')
      queryClient.setQueryData(['bordereaux', 'profiles'], (current: BordereauxProfile[] | undefined) =>
        current?.map((profile) => (profile.id === updated.id ? updated : profile)) ?? [updated])
    },
    onError: () => toast.error('Could not save BDX profile setup'),
  })

  const selectedProfile = premiumProfiles.find((profile) => profile.id === profileId)
  const selectedRun = selectedRunQuery.data
  const rows = previewQuery.data?.rows ?? []

  return (
    <div style={{ padding: '22px 28px', maxWidth: 1320 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16, marginBottom: 18 }}>
        <div>
          <h1 style={{ margin: 0, fontSize: 20, fontWeight: 750, color: 'var(--ink)' }}>Bordereaux Workbench</h1>
          <div style={{ marginTop: 4, fontSize: 12.5, color: 'var(--ink-3)' }}>London BDX and Account Current runs</div>
        </div>
        <button className="sd-btn outline" onClick={() => { previewQuery.refetch(); runsQuery.refetch() }}>
          <RefreshCcw size={14} />
          Refresh
        </button>
      </div>

      <section style={section}>
        <div style={{ display: 'grid', gridTemplateColumns: 'minmax(260px, 1fr) 170px auto', gap: 12, alignItems: 'end' }}>
          <label style={{ display: 'grid', gap: 5 }}>
            <span style={labelStyle}>Profile</span>
            <select value={profileId} onChange={(event) => { setProfileId(event.target.value); setSelectedRunId(null) }} style={inputStyle}>
              {premiumProfiles.map((profile) => (
                <option key={profile.id} value={profile.id}>{profileLabel(profile)}</option>
              ))}
            </select>
          </label>
          <label style={{ display: 'grid', gap: 5 }}>
            <span style={labelStyle}>Month</span>
            <input type="month" value={month} onChange={(event) => setMonth(event.target.value)} style={inputStyle} />
          </label>
          <button
            className="sd-btn primary"
            disabled={!profileId || createRun.isPending || previewQuery.isLoading}
            onClick={() => createRun.mutate()}
          >
            <Save size={14} />
            Create Run
          </button>
        </div>
        {selectedProfile && (
          <div style={{ marginTop: 10, display: 'flex', gap: 8, flexWrap: 'wrap', color: 'var(--ink-3)', fontSize: 12 }}>
            <span>{selectedProfile.programName}</span>
            <span>{selectedProfile.carrierName}</span>
            {selectedProfile.lineOfBusiness && <span>{selectedProfile.lineOfBusiness}</span>}
            {selectedProfile.stateCode && <span>{selectedProfile.stateCode}</span>}
          </div>
        )}
      </section>

      {selectedProfile && (
        <ProfileSetupPanel
          profile={selectedProfile}
          isSaving={updateProfile.isPending}
          onSave={(profile) => updateProfile.mutate(profile)}
        />
      )}

      <section style={section}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, marginBottom: 12 }}>
          <div style={sectionTitle}>Monthly Preview</div>
          {previewQuery.isFetching && <span style={{ color: 'var(--ink-4)', fontSize: 12 }}>Refreshing</span>}
        </div>
        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 12 }}>
          <Metric label="Rows" value={String(rows.length)} />
          <Metric label="Gross Premium" value={money(previewQuery.data?.grossPremiumTotal ?? 0)} />
          <Metric label="Commission" value={money(previewQuery.data?.grossCommissionTotal ?? 0)} />
          <Metric label="Net Due" value={money(previewQuery.data?.netDueCarrierTotal ?? 0)} />
        </div>
        <PreviewTable preview={previewQuery.data} />
      </section>

      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(280px, 370px) minmax(0, 1fr)', gap: 14, alignItems: 'start' }}>
        <section style={section}>
          <div style={sectionTitle}>Run History</div>
          <div style={{ display: 'grid', gap: 7, marginTop: 10 }}>
            {(runsQuery.data ?? []).map((run) => (
              <button
                key={run.id}
                onClick={() => setSelectedRunId(run.id)}
                style={{
                  border: '1px solid var(--line)',
                  borderRadius: 8,
                  padding: '10px 11px',
                  textAlign: 'left',
                  background: selectedRunId === run.id ? 'var(--accent-soft)' : 'var(--surface)',
                  color: 'var(--ink)',
                  cursor: 'pointer',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8 }}>
                  <strong style={{ fontSize: 13 }}>Run {run.runNumber}</strong>
                  <StatusPill status={run.reconciliationStatus} />
                </div>
                <div style={{ marginTop: 5, color: 'var(--ink-3)', fontSize: 12 }}>
                  {formatDate(run.periodStart)} - {formatDate(run.periodEnd)}
                </div>
              </button>
            ))}
            {(runsQuery.data ?? []).length === 0 && (
              <div style={{ color: 'var(--ink-4)', fontSize: 12.5, padding: '18px 4px' }}>No runs for this profile</div>
            )}
          </div>
        </section>

        <section style={section}>
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, alignItems: 'center', marginBottom: 12 }}>
            <div style={sectionTitle}>Audit Detail</div>
            {selectedRun && (
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', justifyContent: 'flex-end' }}>
                <StatusPill status={selectedRun.reconciliationStatus} />
                <button className="sd-btn outline" disabled={generatePackage.isPending} onClick={() => generatePackage.mutate(selectedRun.id)}>
                  <FileSpreadsheet size={14} />
                  Generate Files
                </button>
              </div>
            )}
          </div>
          {!selectedRun && (
            <div style={{ display: 'grid', placeItems: 'center', gap: 8, minHeight: 180, color: 'var(--ink-4)', fontSize: 13 }}>
              <Search size={22} />
              Select a run
            </div>
          )}
          {selectedRun && (
            <>
              <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 12 }}>
                <Metric label="Run Rows" value={String(selectedRun.bordereauxRowCount)} />
                <Metric label="AC Rows" value={String(selectedRun.accountCurrentRowCount)} />
                <Metric label="Status" value={selectedRun.status} />
              </div>
              {(selectedRun.londonBordereauxFileName || selectedRun.accountCurrentFileName) && (
                <div style={{ display: 'grid', gap: 7, marginBottom: 12, color: 'var(--ink-3)', fontSize: 12 }}>
                  {selectedRun.londonBordereauxFileName && (
                    <FileDownloadRow
                      label={selectedRun.londonBordereauxFileName}
                      disabled={downloadFile.isPending}
                      onClick={() => downloadFile.mutate({ run: selectedRun, kind: 'london' })}
                    />
                  )}
                  {selectedRun.accountCurrentFileName && (
                    <FileDownloadRow
                      label={selectedRun.accountCurrentFileName}
                      disabled={downloadFile.isPending}
                      onClick={() => downloadFile.mutate({ run: selectedRun, kind: 'accountCurrent' })}
                    />
                  )}
                </div>
              )}
              <ValidationPanel run={selectedRun} />
              <div style={{ display: 'grid', gap: 10 }}>
                <SnapshotBlock title="Validation Summary" json={selectedRun.validationSummaryJson} />
                <SnapshotBlock title="Profile Snapshot" json={selectedRun.profileSnapshotJson} />
                <SnapshotBlock title="Reconciliation" json={selectedRun.reconciliationSummaryJson} />
                <SnapshotBlock title="Source Rows" json={selectedRun.sourceRowsSnapshotJson} compact />
              </div>
            </>
          )}
        </section>
      </div>

      <ReconciliationPanel
        run={selectedRun}
        onReconciled={(run) => {
          queryClient.setQueryData(['bordereaux', 'run', run.id], run)
          queryClient.invalidateQueries({ queryKey: ['bordereaux', 'runs', profileId] })
        }}
      />
    </div>
  )
}

function FileDownloadRow({ label, disabled, onClick }: { label: string; disabled: boolean; onClick: () => void }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10, border: '1px solid var(--line-2)', borderRadius: 8, padding: '7px 9px', background: 'var(--surface-2)' }}>
      <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{label}</span>
      <button type="button" className="sd-btn outline sm" disabled={disabled} onClick={onClick}>
        <Download size={13} />
        Download
      </button>
    </div>
  )
}

function SnapshotBlock({ title, json, compact = false }: { title: string; json: string; compact?: boolean }) {
  return (
    <div style={{ border: '1px solid var(--line)', borderRadius: 8, overflow: 'hidden', background: 'var(--surface)' }}>
      <div style={{ padding: '8px 10px', borderBottom: '1px solid var(--line-2)', background: 'var(--surface-2)', display: 'flex', alignItems: 'center', gap: 7, fontSize: 12, fontWeight: 700, color: 'var(--ink-2)' }}>
        <FileSpreadsheet size={14} />
        {title}
      </div>
      <pre style={{ margin: 0, maxHeight: compact ? 180 : 130, overflow: 'auto', padding: 10, fontSize: 11.5, lineHeight: 1.45, color: 'var(--ink-3)', whiteSpace: 'pre-wrap' }}>
        {formatJson(json)}
      </pre>
    </div>
  )
}

function formatJson(json: string) {
  try {
    return JSON.stringify(JSON.parse(json), null, 2)
  } catch {
    return json
  }
}

const section: React.CSSProperties = {
  border: '1px solid var(--line)',
  borderRadius: 8,
  background: 'var(--surface)',
  padding: 14,
  marginBottom: 14,
}

const sectionTitle: React.CSSProperties = {
  color: 'var(--ink)',
  fontSize: 13,
  fontWeight: 750,
}

const labelStyle: React.CSSProperties = {
  color: 'var(--ink-3)',
  fontSize: 11,
  fontWeight: 700,
  textTransform: 'uppercase',
}

const inputStyle: React.CSSProperties = {
  height: 34,
  border: '1px solid var(--line)',
  borderRadius: 6,
  background: 'var(--surface)',
  color: 'var(--ink)',
  padding: '0 10px',
  fontSize: 12.5,
}

const setupGroupStyle: React.CSSProperties = {
  borderTop: '1px solid var(--line-2)',
  paddingTop: 10,
}

const setupGroupTitle: React.CSSProperties = {
  color: 'var(--ink-3)',
  fontSize: 11,
  fontWeight: 800,
  marginBottom: 8,
  textTransform: 'uppercase',
}

const setupRowStyle: React.CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'space-between',
  gap: 10,
  minHeight: 24,
}

const th: React.CSSProperties = {
  padding: '9px 10px',
  color: 'var(--ink-3)',
  fontSize: 11,
  fontWeight: 700,
  textTransform: 'uppercase',
  whiteSpace: 'nowrap',
}

const td: React.CSSProperties = {
  padding: '8px 10px',
  color: 'var(--ink)',
  whiteSpace: 'nowrap',
}
