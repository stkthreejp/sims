import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useParams, Link } from 'react-router-dom'
import { ArrowLeft, ArrowRight, CheckCircle, XCircle, Users } from 'lucide-react'
import { toast } from 'sonner'
import { ratingApi } from '@/api/rating.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { useAuthStore } from '@/store/authStore'
import type { RatingPlanVersionSummary, PlanStatus } from '@/types/rating.types'

function StatusBadge({ status }: { status: PlanStatus }) {
  const map: Record<PlanStatus, { label: string; cls: string }> = {
    Active:  { label: 'Active',  cls: 'bg-emerald-100 text-emerald-700' },
    Draft:   { label: 'Draft',   cls: 'bg-amber-100 text-amber-700' },
    Retired: { label: 'Retired', cls: 'bg-slate-100 text-slate-500' },
  }
  const { label, cls } = map[status]
  return <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${cls}`}>{label}</span>
}

function EffectiveRange({ v }: { v: RatingPlanVersionSummary }) {
  if (v.expirationDate) return <span>{v.effectiveDate} — {v.expirationDate}</span>
  if (v.status === 'Active') return <span>{v.effectiveDate} <span className="text-slate-400">onward</span></span>
  return <span>{v.effectiveDate}</span>
}

export function AdminRatingPlanDetailPage() {
  const { planId } = useParams<{ planId: string }>()
  const qc = useQueryClient()
  const currentUserId = useAuthStore((s) => s.user?.id)

  const { data: plan, isLoading } = useQuery({
    queryKey: ['rating-plan', planId],
    queryFn: () => ratingApi.getPlan(planId!),
    enabled: !!planId,
  })

  const promoteMutation = useMutation({
    mutationFn: (versionId: string) => ratingApi.promoteVersion(versionId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['rating-plan', planId] })
      qc.invalidateQueries({ queryKey: ['rating-plans'] })
      toast.success('Version promoted to Active')
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to promote version'),
  })

  const retireMutation = useMutation({
    mutationFn: (versionId: string) => ratingApi.retireVersion(versionId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['rating-plan', planId] })
      qc.invalidateQueries({ queryKey: ['rating-plans'] })
      toast.success('Version retired')
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to retire version'),
  })

  if (isLoading) return <LoadingSpinner />
  if (!plan) return <div className="p-6 text-sm text-slate-500">Plan not found.</div>

  const activeVersion = plan.versions.find((v) => v.status === 'Active')

  return (
    <div className="p-6 space-y-6 max-w-4xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-slate-500">
        <Link to="/admin/rating" className="hover:text-slate-700 flex items-center gap-1">
          <ArrowLeft className="h-4 w-4" /> Rating Engine
        </Link>
        <span>/</span>
        <span className="text-slate-800 font-medium">{plan.name}</span>
      </div>

      {/* Header */}
      <div className="bg-white border rounded-lg p-5">
        <div className="flex items-start justify-between">
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <h1 className="text-xl font-semibold text-slate-900">{plan.name}</h1>
              <StatusBadge status={plan.status} />
            </div>
            <div className="flex items-center gap-3 text-sm text-slate-500">
              <span className="px-2 py-0.5 bg-blue-50 text-blue-700 rounded text-xs border border-blue-100">{plan.lobLabel}</span>
              <span className="font-mono text-xs text-slate-400">{plan.formulaKey}</span>
            </div>
          </div>
        </div>
      </div>

      {/* Versions timeline */}
      <div className="bg-white border rounded-lg p-5 space-y-4">
        <h2 className="text-base font-semibold text-slate-800">Versions</h2>

        {plan.versions.length === 0 ? (
          <p className="text-sm text-slate-400 text-center py-4">No versions yet.</p>
        ) : (
          <div className="space-y-3">
            {plan.versions.map((v) => (
              <div
                key={v.id}
                className={`border rounded-lg p-4 ${v.status === 'Active' ? 'border-emerald-200 bg-emerald-50/30' : 'border-slate-200'}`}
              >
                <div className="flex items-start justify-between gap-4">
                  <div className="space-y-1.5">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-semibold text-slate-700 font-mono">v{v.versionNumber}</span>
                      <StatusBadge status={v.status} />
                      <span className="text-xs text-slate-500">
                        <EffectiveRange v={v} />
                      </span>
                    </div>

                    <div className="flex items-center gap-4 text-xs text-slate-500">
                      {v.promotedByName && v.promotedAt && (
                        <span className="flex items-center gap-1">
                          <CheckCircle className="h-3 w-3 text-emerald-500" />
                          Promoted by {v.promotedByName} on {new Date(v.promotedAt).toLocaleDateString()}
                        </span>
                      )}
                      <span className="flex items-center gap-1">
                        <Users className="h-3 w-3 text-slate-400" />
                        {v.assignedCarrierCount} carrier{v.assignedCarrierCount !== 1 ? 's' : ''} assigned
                      </span>
                    </div>

                    {v.notes && (
                      <p className="text-xs text-slate-500 italic">{v.notes}</p>
                    )}
                  </div>

                  <div className="flex items-center gap-2 shrink-0">
                    <Link
                      to={`/admin/rating/versions/${v.id}`}
                      className="flex items-center gap-1 px-2.5 py-1.5 text-xs border rounded hover:bg-slate-50 text-slate-600"
                    >
                      View <ArrowRight className="h-3 w-3" />
                    </Link>

                    {v.status === 'Draft' && (() => {
                      const blockedByMakerChecker =
                        (v.createdById && v.createdById === currentUserId) ||
                        (v.lastEditedById && v.lastEditedById === currentUserId)
                      return (
                        <div title={blockedByMakerChecker ? 'You edited this draft — a different admin must promote it.' : undefined}>
                          <button
                            onClick={() => {
                              const priorLabel = activeVersion ? `, retiring v${activeVersion.versionNumber} effective ${activeVersion.effectiveDate}` : ''
                              if (confirm(`Promote v${v.versionNumber} to Active for ${plan.name}?${priorLabel}`))
                                promoteMutation.mutate(v.id)
                            }}
                            disabled={promoteMutation.isPending || !!blockedByMakerChecker}
                            className="flex items-center gap-1 px-2.5 py-1.5 text-xs bg-emerald-600 text-white rounded hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed"
                          >
                            <CheckCircle className="h-3 w-3" /> Promote
                          </button>
                        </div>
                      )
                    })()}

                    {(v.status === 'Draft' || v.status === 'Active') && (
                      <button
                        onClick={() => {
                          if (confirm(`Retire v${v.versionNumber} of ${plan.name}? This cannot be undone.`))
                            retireMutation.mutate(v.id)
                        }}
                        disabled={retireMutation.isPending}
                        className="flex items-center gap-1 px-2.5 py-1.5 text-xs border border-red-200 text-red-600 rounded hover:bg-red-50 disabled:opacity-50"
                      >
                        <XCircle className="h-3 w-3" /> Retire
                      </button>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Carrier assignments */}
      <div className="bg-white border rounded-lg p-5 space-y-3">
        <h2 className="text-base font-semibold text-slate-800 flex items-center gap-2">
          <Users className="h-4 w-4 text-slate-400" />
          Assigned Carriers
          <span className="text-xs font-normal text-slate-400">({plan.assignments.length})</span>
        </h2>

        {plan.assignments.length === 0 ? (
          <div className="text-center py-4 border border-dashed rounded-lg">
            <p className="text-sm text-slate-400">No carriers assigned to this plan.</p>
            <p className="text-xs text-slate-300 mt-0.5">Assign carriers from the Carrier detail page.</p>
          </div>
        ) : (
          <div className="border rounded-lg overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-slate-500 border-b bg-slate-50">
                  <th className="px-4 py-2 font-medium">Carrier</th>
                  <th className="px-4 py-2 font-medium">Assigned Version</th>
                  <th className="px-4 py-2" />
                </tr>
              </thead>
              <tbody className="divide-y">
                {plan.assignments.map((a) => (
                  <tr key={a.assignmentId} className="hover:bg-slate-50">
                    <td className="px-4 py-2.5 font-medium text-slate-800">{a.carrierName}</td>
                    <td className="px-4 py-2.5 text-slate-600 font-mono text-xs">v{a.versionNumber}</td>
                    <td className="px-4 py-2.5 text-right">
                      <Link
                        to={`/carriers/${a.carrierId}`}
                        className="flex items-center justify-end gap-1 text-xs text-blue-600 hover:underline"
                      >
                        Carrier detail <ArrowRight className="h-3 w-3" />
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}
