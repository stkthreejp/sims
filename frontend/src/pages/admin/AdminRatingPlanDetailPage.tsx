import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useParams, Link } from 'react-router-dom'
import { ArrowLeft, ArrowRight, CheckCircle, XCircle, Users, Plus } from 'lucide-react'
import { toast } from 'sonner'
import { ratingApi } from '@/api/rating.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { StatusBadge } from '@/components/common/StatusBadge'
import { useAuthStore } from '@/store/authStore'
import { todayLocal } from '@/lib/utils'
import type { RatingPlanVersionSummary, PlanStatus } from '@/types/rating.types'

// Nearest .sd-pill variant per status, chosen to preserve the prior colors:
// Active (emerald) → good (green); Draft (amber) → expiring (amber/warn);
// Retired (slate) → expired (neutral gray).
const STATUS_PILL_VARIANT: Record<PlanStatus, string> = {
  Active: 'good',
  Draft: 'expiring',
  Retired: 'expired',
}

function EffectiveRange({ v }: { v: RatingPlanVersionSummary }) {
  if (v.expirationDate) return <span>{v.effectiveDate} — {v.expirationDate}</span>
  if (v.status === 'Active') return <span>{v.effectiveDate} <span className="text-slate-400">onward</span></span>
  return <span>{v.effectiveDate}</span>
}

function CreateDraftModal({
  planId,
  planName,
  versions,
  onClose,
}: {
  planId: string
  planName: string
  versions: RatingPlanVersionSummary[]
  onClose: () => void
}) {
  const qc = useQueryClient()
  const [effectiveDate, setEffectiveDate] = useState(todayLocal())
  const [cloneFrom, setCloneFrom] = useState<string>('none')
  const [notes, setNotes] = useState('')

  const cloneablVersions = versions.filter((v) => v.status === 'Active' || v.status === 'Retired')

  const createMutation = useMutation({
    mutationFn: () =>
      ratingApi.createVersion(planId, {
        effectiveDate,
        cloneFromVersionId: cloneFrom !== 'none' ? cloneFrom : null,
        notes: notes.trim() || null,
      }),
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: ['rating-plan', planId] })
      qc.invalidateQueries({ queryKey: ['rating-plans'] })
      toast.success(`Draft v${data.versionNumber} created`)
      onClose()
    },
    onError: (err: any) =>
      toast.error(err?.response?.data?.errorMessage ?? 'Failed to create draft'),
  })

  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-md p-6 space-y-4">
        <h2 className="text-base font-semibold text-slate-900">Create Draft Version — {planName}</h2>

        <div className="space-y-3">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Effective Date</label>
            <input
              type="date"
              value={effectiveDate}
              onChange={(e) => setEffectiveDate(e.target.value)}
              className="w-full px-3 py-1.5 text-sm border rounded focus:ring-1 focus:ring-blue-500 outline-none"
            />
          </div>

          {cloneablVersions.length > 0 && (
            <div>
              <label className="block text-xs font-medium text-slate-600 mb-1">Clone factor tables from</label>
              <select
                value={cloneFrom}
                onChange={(e) => setCloneFrom(e.target.value)}
                className="w-full px-3 py-1.5 text-sm border rounded focus:ring-1 focus:ring-blue-500 outline-none"
              >
                <option value="none">— blank (no factor tables) —</option>
                {cloneablVersions.map((v) => (
                  <option key={v.id} value={v.id}>
                    v{v.versionNumber} ({v.status}, {v.effectiveDate})
                  </option>
                ))}
              </select>
            </div>
          )}

          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Notes (optional)</label>
            <textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              rows={2}
              placeholder="What changed in this version?"
              className="w-full px-3 py-1.5 text-sm border rounded focus:ring-1 focus:ring-blue-500 outline-none resize-none"
            />
          </div>
        </div>

        <div className="flex justify-end gap-2 pt-1">
          <button
            onClick={onClose}
            className="px-4 py-1.5 text-sm border rounded hover:bg-slate-50"
          >
            Cancel
          </button>
          <button
            onClick={() => createMutation.mutate()}
            disabled={!effectiveDate || createMutation.isPending}
            className="px-4 py-1.5 text-sm bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50"
          >
            {createMutation.isPending ? 'Creating…' : 'Create Draft'}
          </button>
        </div>
      </div>
    </div>
  )
}

export function AdminRatingPlanDetailPage() {
  const { planId } = useParams<{ planId: string }>()
  const qc = useQueryClient()
  const currentUserId = useAuthStore((s) => s.user?.id)
  const [showCreateModal, setShowCreateModal] = useState(false)

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
  const hasDraft = plan.versions.some((v) => v.status === 'Draft')

  return (
    <div className="p-6 space-y-6 max-w-4xl">
      {showCreateModal && (
        <CreateDraftModal
          planId={plan.id}
          planName={plan.name}
          versions={plan.versions}
          onClose={() => setShowCreateModal(false)}
        />
      )}

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
              <StatusBadge status={plan.status} variant={STATUS_PILL_VARIANT[plan.status]} />
            </div>
            <div className="flex items-center gap-3 text-sm text-slate-500">
              <span className="px-2 py-0.5 bg-blue-50 text-blue-700 rounded text-xs border border-blue-100">{plan.lobLabel}</span>
              <span className="font-mono text-xs text-slate-400">{plan.formulaKey}</span>
            </div>
          </div>

          {!hasDraft && (
            <button
              onClick={() => setShowCreateModal(true)}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm bg-blue-600 text-white rounded hover:bg-blue-700"
            >
              <Plus className="h-3.5 w-3.5" /> New Draft Version
            </button>
          )}
        </div>
      </div>

      {/* Versions timeline */}
      <div className="bg-white border rounded-lg p-5 space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-base font-semibold text-slate-800">Versions</h2>
          {hasDraft && (
            <span className="text-xs text-amber-600 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded">
              Draft exists — complete or retire it to create another
            </span>
          )}
        </div>

        {plan.versions.length === 0 ? (
          <div className="text-center py-8 border border-dashed rounded-lg">
            <p className="text-sm text-slate-400">No versions yet.</p>
            <button
              onClick={() => setShowCreateModal(true)}
              className="mt-2 text-xs text-blue-600 hover:underline"
            >
              Create the first draft
            </button>
          </div>
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
                      <StatusBadge status={v.status} variant={STATUS_PILL_VARIANT[v.status]} />
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
                          const msg = v.status === 'Active'
                            ? `Retire the ACTIVE version v${v.versionNumber} of ${plan.name}?\n\nThis plan will have NO active version. New quotes for ${plan.lobLabel} on the ${v.assignedCarrierCount} assigned carrier${v.assignedCarrierCount !== 1 ? 's' : ''} will STOP rating until another version is promoted. This cannot be undone.`
                            : `Retire v${v.versionNumber} of ${plan.name}? This cannot be undone.`
                          if (confirm(msg))
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
