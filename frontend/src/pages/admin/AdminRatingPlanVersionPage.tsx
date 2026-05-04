import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useParams, Link } from 'react-router-dom'
import { ArrowLeft, CheckCircle, XCircle, Search, ChevronDown, ChevronRight } from 'lucide-react'
import { toast } from 'sonner'
import { ratingApi } from '@/api/rating.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { useAuthStore } from '@/store/authStore'
import type { PlanStatus, FactorTable } from '@/types/rating.types'

type Tab = 'schedule' | 'factors' | 'eligibility' | 'audit'

function StatusBadge({ status }: { status: PlanStatus }) {
  const map: Record<PlanStatus, { label: string; cls: string }> = {
    Active:  { label: 'Active',  cls: 'bg-emerald-100 text-emerald-700' },
    Draft:   { label: 'Draft',   cls: 'bg-amber-100 text-amber-700' },
    Retired: { label: 'Retired', cls: 'bg-slate-100 text-slate-500' },
  }
  const { label, cls } = map[status]
  return <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${cls}`}>{label}</span>
}

function FactorTablePanel({ table }: { table: FactorTable }) {
  const [open, setOpen] = useState(true)
  const [search, setSearch] = useState('')

  const filteredRows = useMemo(() => {
    if (!search.trim()) return table.rows
    const q = search.toLowerCase()
    return table.rows.filter((r) =>
      Object.values(r.dimensionValues).some((v) => v.toLowerCase().includes(q)) ||
      String(r.factor).includes(q)
    )
  }, [table.rows, search])

  return (
    <div className="border rounded-lg overflow-hidden">
      <button
        onClick={() => setOpen((o) => !o)}
        className="w-full flex items-center justify-between px-4 py-3 bg-slate-50 hover:bg-slate-100 text-left"
      >
        <div className="flex items-center gap-2">
          {open ? <ChevronDown className="h-4 w-4 text-slate-400" /> : <ChevronRight className="h-4 w-4 text-slate-400" />}
          <span className="text-sm font-semibold text-slate-700 font-mono">{table.code}</span>
          <span className="text-xs text-slate-400">{table.rows.length} rows · {table.dimensionNames.join(', ')}</span>
          <span className="text-xs px-1.5 py-0.5 rounded bg-slate-200 text-slate-600">{table.valueSemantics}</span>
        </div>
      </button>

      {open && (
        <div>
          <div className="px-4 py-2 border-b bg-white">
            <div className="relative">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-400" />
              <input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Filter rows…"
                className="w-full pl-8 pr-3 py-1.5 text-xs border rounded"
              />
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-xs">
              <thead>
                <tr className="text-left text-slate-500 border-b bg-slate-50">
                  {table.dimensionNames.map((d) => (
                    <th key={d} className="px-4 py-2 font-medium">{d}</th>
                  ))}
                  <th className="px-4 py-2 font-medium text-right">Factor</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {filteredRows.map((row) => (
                  <tr key={row.id} className="hover:bg-slate-50">
                    {table.dimensionNames.map((d) => (
                      <td key={d} className="px-4 py-1.5 text-slate-700">{row.dimensionValues[d] ?? '—'}</td>
                    ))}
                    <td className="px-4 py-1.5 text-right font-mono font-medium text-slate-800">
                      {row.factor.toFixed(4)}
                    </td>
                  </tr>
                ))}
                {filteredRows.length === 0 && (
                  <tr>
                    <td colSpan={table.dimensionNames.length + 1} className="px-4 py-4 text-center text-slate-400">
                      No rows match filter.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  )
}

export function AdminRatingPlanVersionPage() {
  const { versionId } = useParams<{ versionId: string }>()
  const qc = useQueryClient()
  const currentUserId = useAuthStore((s) => s.user?.id)
  const [activeTab, setActiveTab] = useState<Tab>('schedule')

  const { data: version, isLoading: vLoading } = useQuery({
    queryKey: ['rating-plan-version', versionId],
    queryFn: () => ratingApi.getVersion(versionId!),
    enabled: !!versionId,
  })

  const { data: factors = [], isLoading: fLoading } = useQuery({
    queryKey: ['rating-plan-version-factors', versionId],
    queryFn: () => ratingApi.getVersionFactors(versionId!),
    enabled: !!versionId && activeTab === 'factors',
  })

  const { data: eligibility = [], isLoading: eLoading } = useQuery({
    queryKey: ['rating-plan-version-eligibility', versionId],
    queryFn: () => ratingApi.getVersionEligibilityRules(versionId!),
    enabled: !!versionId && activeTab === 'eligibility',
  })

  const promoteMutation = useMutation({
    mutationFn: () => ratingApi.promoteVersion(versionId!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['rating-plan-version', versionId] })
      qc.invalidateQueries({ queryKey: ['rating-plan', version?.ratingPlanId] })
      qc.invalidateQueries({ queryKey: ['rating-plans'] })
      toast.success('Version promoted to Active')
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to promote version'),
  })

  const retireMutation = useMutation({
    mutationFn: () => ratingApi.retireVersion(versionId!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['rating-plan-version', versionId] })
      qc.invalidateQueries({ queryKey: ['rating-plan', version?.ratingPlanId] })
      qc.invalidateQueries({ queryKey: ['rating-plans'] })
      toast.success('Version retired')
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to retire version'),
  })

  if (vLoading) return <LoadingSpinner />
  if (!version) return <div className="p-6 text-sm text-slate-500">Version not found.</div>

  const effectiveRange = version.expirationDate
    ? `${version.effectiveDate} — ${version.expirationDate}`
    : version.status === 'Active'
      ? `${version.effectiveDate} onward`
      : version.effectiveDate

  const tabs: { id: Tab; label: string }[] = [
    { id: 'schedule', label: 'Schedule & Limits' },
    { id: 'factors', label: 'Factor Tables' },
    { id: 'eligibility', label: 'Eligibility Rules' },
    { id: 'audit', label: 'Audit' },
  ]

  return (
    <div className="p-6 space-y-5 max-w-5xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-slate-500">
        <Link to="/admin/rating" className="hover:text-slate-700 flex items-center gap-1">
          <ArrowLeft className="h-4 w-4" /> Rating Engine
        </Link>
        <span>/</span>
        <Link to={`/admin/rating/plans/${version.ratingPlanId}`} className="hover:text-slate-700">
          {version.planName}
        </Link>
        <span>/</span>
        <span className="text-slate-800 font-medium">v{version.versionNumber}</span>
      </div>

      {/* Header */}
      <div className="bg-white border rounded-lg p-5">
        <div className="flex items-start justify-between">
          <div className="space-y-1.5">
            <div className="flex items-center gap-2">
              <h1 className="text-xl font-semibold text-slate-900">{version.planName}</h1>
              <span className="text-slate-400 font-mono text-sm">v{version.versionNumber}</span>
              <StatusBadge status={version.status} />
            </div>
            <div className="flex items-center gap-3 text-sm text-slate-500">
              <span className="px-2 py-0.5 bg-blue-50 text-blue-700 rounded text-xs border border-blue-100">{version.lobLabel}</span>
              <span className="text-xs text-slate-400">{effectiveRange}</span>
            </div>
          </div>

          <div className="flex items-center gap-2">
            {version.status === 'Draft' && (() => {
              const blockedByMakerChecker =
                (version.createdById && version.createdById === currentUserId) ||
                (version.lastEditedById && version.lastEditedById === currentUserId)
              return (
                <div title={blockedByMakerChecker ? 'You edited this draft — a different admin must promote it.' : undefined}>
                  <button
                    onClick={() => {
                      if (confirm(`Promote v${version.versionNumber} to Active for ${version.planName}?`))
                        promoteMutation.mutate()
                    }}
                    disabled={promoteMutation.isPending || !!blockedByMakerChecker}
                    className="flex items-center gap-1.5 px-3 py-1.5 bg-emerald-600 text-white rounded text-sm hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    <CheckCircle className="h-3.5 w-3.5" /> Promote
                  </button>
                </div>
              )
            })()}
            {(version.status === 'Draft' || version.status === 'Active') && (
              <button
                onClick={() => {
                  if (confirm(`Retire v${version.versionNumber} of ${version.planName}? This cannot be undone.`))
                    retireMutation.mutate()
                }}
                disabled={retireMutation.isPending}
                className="flex items-center gap-1.5 px-3 py-1.5 border border-red-200 text-red-600 rounded text-sm hover:bg-red-50 disabled:opacity-50"
              >
                <XCircle className="h-3.5 w-3.5" /> Retire
              </button>
            )}
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="border-b flex gap-1">
        {tabs.map((t) => (
          <button
            key={t.id}
            onClick={() => setActiveTab(t.id)}
            className={`px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors ${
              activeTab === t.id
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-slate-500 hover:text-slate-700'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {/* Tab content */}
      {activeTab === 'schedule' && (
        <div className="bg-white border rounded-lg p-5">
          <dl className="grid grid-cols-2 gap-x-8 gap-y-4 text-sm">
            <div>
              <dt className="text-xs font-medium text-slate-500 mb-0.5">Schedule Rating Min</dt>
              <dd className="text-slate-800 font-mono">{(version.scheduleMin * 100).toFixed(0)}%</dd>
            </div>
            <div>
              <dt className="text-xs font-medium text-slate-500 mb-0.5">Schedule Rating Max</dt>
              <dd className="text-slate-800 font-mono">{(version.scheduleMax * 100).toFixed(0)}%</dd>
            </div>
            <div>
              <dt className="text-xs font-medium text-slate-500 mb-0.5">Minimum Premium</dt>
              <dd className="text-slate-800">{version.minimumPremium != null ? `$${version.minimumPremium.toLocaleString()}` : '—'}</dd>
            </div>
            <div>
              <dt className="text-xs font-medium text-slate-500 mb-0.5">Effective Date</dt>
              <dd className="text-slate-800">{version.effectiveDate}</dd>
            </div>
            {version.notes && (
              <div className="col-span-2">
                <dt className="text-xs font-medium text-slate-500 mb-0.5">Notes</dt>
                <dd className="text-slate-700 whitespace-pre-wrap">{version.notes}</dd>
              </div>
            )}
          </dl>
        </div>
      )}

      {activeTab === 'factors' && (
        <div className="space-y-3">
          {fLoading ? (
            <LoadingSpinner />
          ) : factors.length === 0 ? (
            <div className="text-center py-8 border border-dashed rounded-lg">
              <p className="text-sm text-slate-400">No factor tables in this version.</p>
            </div>
          ) : (
            factors.map((t) => <FactorTablePanel key={t.id} table={t} />)
          )}
        </div>
      )}

      {activeTab === 'eligibility' && (
        <div className="bg-white border rounded-lg overflow-hidden">
          {eLoading ? (
            <LoadingSpinner />
          ) : eligibility.length === 0 ? (
            <div className="text-center py-8">
              <p className="text-sm text-slate-400">No eligibility rules in this version.</p>
            </div>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-slate-500 border-b bg-slate-50">
                  <th className="px-4 py-2 font-medium">#</th>
                  <th className="px-4 py-2 font-medium">Equipment Type</th>
                  <th className="px-4 py-2 font-medium">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {eligibility.map((r) => (
                  <tr key={r.id} className="hover:bg-slate-50">
                    <td className="px-4 py-2.5 text-slate-400 font-mono text-xs">{r.typeNumber}</td>
                    <td className="px-4 py-2.5 text-slate-800">{r.equipmentTypeName}</td>
                    <td className="px-4 py-2.5">
                      {r.accepted ? (
                        <span className="flex items-center gap-1 text-emerald-700 text-xs">
                          <CheckCircle className="h-3.5 w-3.5" /> Accepted
                        </span>
                      ) : (
                        <span className="flex items-center gap-1 text-red-600 text-xs">
                          <XCircle className="h-3.5 w-3.5" /> Excluded
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {activeTab === 'audit' && (
        <div className="bg-white border rounded-lg p-5">
          <dl className="space-y-4 text-sm">
            <div>
              <dt className="text-xs font-medium text-slate-500 mb-0.5">Status</dt>
              <dd><StatusBadge status={version.status} /></dd>
            </div>
            {version.promotedAt && (
              <>
                <div>
                  <dt className="text-xs font-medium text-slate-500 mb-0.5">Promoted at</dt>
                  <dd className="text-slate-800">{new Date(version.promotedAt).toLocaleString()}</dd>
                </div>
                {version.promotedByName && (
                  <div>
                    <dt className="text-xs font-medium text-slate-500 mb-0.5">Promoted by</dt>
                    <dd className="text-slate-800">{version.promotedByName}</dd>
                  </div>
                )}
              </>
            )}
            {!version.promotedAt && (
              <p className="text-slate-400 text-xs italic">No promotion history yet.</p>
            )}
          </dl>
        </div>
      )}
    </div>
  )
}
