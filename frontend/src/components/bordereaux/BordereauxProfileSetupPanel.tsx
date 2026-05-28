import { useEffect, useState } from 'react'
import { AlertTriangle, CheckCircle2, Save, Settings2, X } from 'lucide-react'
import type {
  BordereauxProfile,
  BordereauxProfileSetupItem,
  UpsertBordereauxProfileRequest,
} from '@/types/bordereaux.types'

export function parseBordereauxStringArray(json: string): string[] {
  try {
    const parsed = JSON.parse(json)
    return Array.isArray(parsed)
      ? parsed.filter((value): value is string => typeof value === 'string' && value.trim().length > 0).map((value) => value.trim())
      : []
  } catch {
    return []
  }
}

export function parseBordereauxStringRecord(json: string): Record<string, string> {
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

export function bordereauxProfileToRequest(profile: BordereauxProfile): UpsertBordereauxProfileRequest {
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

export function BordereauxProfileSetupPanel({
  profile,
  isSaving,
  onSave,
  lineOfBusinessOptions = [],
}: {
  profile: BordereauxProfile
  isSaving: boolean
  onSave: (profile: BordereauxProfile) => void
  lineOfBusinessOptions?: Array<{ value: string; label: string }>
}) {
  const [isEditing, setIsEditing] = useState(false)
  const [lineOfBusiness, setLineOfBusiness] = useState(profile.lineOfBusiness ?? '')
  const [selectedTabs, setSelectedTabs] = useState<Set<string>>(new Set())
  const [staticForm, setStaticForm] = useState<Record<string, string>>({})

  useEffect(() => {
    setLineOfBusiness(profile.lineOfBusiness ?? '')
    setSelectedTabs(new Set(parseBordereauxStringArray(profile.requiredTabsJson).map((tab) => tab.toLowerCase())))
    const staticValues = parseBordereauxStringRecord(profile.staticValuesJson)
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
    const existingTabs = parseBordereauxStringArray(profile.requiredTabsJson)
    const otherTabs = existingTabs.filter((tab) => !knownTabs.has(tab.toLowerCase()))
    const requiredTabs = [
      ...otherTabs,
      ...profile.setupStatus.requiredTabs.filter((item) => selectedTabs.has(item.key.toLowerCase())).map((item) => item.key),
    ]

    const staticValues = parseBordereauxStringRecord(profile.staticValuesJson)
    profile.setupStatus.staticValues.forEach((item) => {
      const value = staticForm[item.key]?.trim() ?? ''
      if (value) staticValues[item.key] = value
      else delete staticValues[item.key]
    })

    onSave({
      ...profile,
      lineOfBusiness: lineOfBusiness || null,
      requiredTabsJson: JSON.stringify(requiredTabs),
      staticValuesJson: JSON.stringify(staticValues),
    })
  }

  const hasIssues = !profile.setupStatus.isReadyForExport

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          {hasIssues ? <AlertTriangle className="h-4 w-4 text-amber-700" /> : <CheckCircle2 className="h-4 w-4 text-emerald-700" />}
          <div>
            <div className="text-sm font-semibold text-slate-800">Profile Setup</div>
            <div className={`mt-0.5 text-xs font-semibold ${hasIssues ? 'text-amber-700' : 'text-emerald-700'}`}>
              {hasIssues ? `${profile.setupStatus.missingItems} missing setup item${profile.setupStatus.missingItems === 1 ? '' : 's'}` : 'Ready for export'}
            </div>
          </div>
        </div>
        <div className="flex gap-2">
          {isEditing && (
            <button type="button" className="sd-btn outline sm" disabled={isSaving} onClick={() => setIsEditing(false)}>
              <X className="h-3.5 w-3.5" />
              Cancel
            </button>
          )}
          <button
            type="button"
            className={isEditing ? 'sd-btn primary sm' : 'sd-btn outline sm'}
            disabled={isSaving}
            onClick={() => (isEditing ? saveSetup() : setIsEditing(true))}
          >
            {isEditing ? <Save className="h-3.5 w-3.5" /> : <Settings2 className="h-3.5 w-3.5" />}
            {isEditing ? 'Save Setup' : 'Edit Setup'}
          </button>
        </div>
      </div>

      <div className="rounded-lg border border-slate-200 bg-slate-50 p-3">
        <div className="mb-2 text-[11px] font-bold uppercase text-slate-500">Profile Scope</div>
        {isEditing ? (
          <div className="max-w-sm">
            <label className="sims-field-label">Line of Business</label>
            <select
              value={lineOfBusiness}
              onChange={(event) => setLineOfBusiness(event.target.value)}
              className="sims-select"
            >
              <option value="">All lines</option>
              {lineOfBusinessOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </div>
        ) : (
          <div className="text-sm font-medium text-slate-800">
            {lineOfBusinessOptions.find((option) => option.value === profile.lineOfBusiness)?.label ?? profile.lineOfBusiness ?? 'All lines'}
          </div>
        )}
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <div className="border-t border-slate-100 pt-3">
          <div className="mb-2 text-[11px] font-bold uppercase text-slate-500">Required Tabs</div>
          <div className="grid gap-2">
            {profile.setupStatus.requiredTabs.map((item) => (
              <label key={item.key} className="flex min-h-6 items-center justify-between gap-3">
                <span className="inline-flex min-w-0 items-center gap-2">
                  {isEditing && (
                    <input
                      type="checkbox"
                      checked={selectedTabs.has(item.key.toLowerCase())}
                      onChange={() => toggleTab(item.key)}
                      className="rounded"
                    />
                  )}
                  <span className="text-sm font-medium text-slate-800">{item.label}</span>
                </span>
                <SetupBadge status={selectedTabs.has(item.key.toLowerCase()) ? 'Configured' : 'Missing'} />
              </label>
            ))}
          </div>
        </div>

        <div className="border-t border-slate-100 pt-3">
          <div className="mb-2 text-[11px] font-bold uppercase text-slate-500">Static Values</div>
          <div className="grid gap-3">
            {profile.setupStatus.staticValues.map((item) => (
              <div key={item.key} className="grid gap-1.5">
                <div className="flex min-h-6 items-center justify-between gap-3">
                  <span className="text-sm font-medium text-slate-800">{item.label}</span>
                  <SetupBadge status={item.status} />
                </div>
                {isEditing ? (
                  <input
                    value={staticForm[item.key] ?? ''}
                    placeholder={item.defaultValue ?? ''}
                    onChange={(event) => setStaticForm((form) => ({ ...form, [item.key]: event.target.value }))}
                    className="sims-input"
                  />
                ) : (
                  <div className="break-words text-xs text-slate-600">
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
    </div>
  )
}

function SetupItemList({ title, items }: { title: string; items: BordereauxProfileSetupItem[] }) {
  return (
    <div className="border-t border-slate-100 pt-3">
      <div className="mb-2 text-[11px] font-bold uppercase text-slate-500">{title}</div>
      <div className="grid gap-2">
        {items.map((item) => (
          <div key={item.key} className="flex min-h-6 items-center justify-between gap-3">
            <span className="text-sm font-medium text-slate-800">{item.label}</span>
            <SetupBadge status={item.status} />
          </div>
        ))}
      </div>
    </div>
  )
}

function SetupBadge({ status }: { status: string }) {
  const classes = status === 'Configured'
    ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
    : status === 'Default'
      ? 'border-blue-200 bg-blue-50 text-blue-700'
      : status === 'Missing'
        ? 'border-amber-200 bg-amber-50 text-amber-700'
        : 'border-slate-200 bg-slate-50 text-slate-500'

  return (
    <span className={`rounded-md border px-1.5 py-0.5 text-[10.5px] font-bold leading-tight ${classes}`}>
      {status}
    </span>
  )
}
