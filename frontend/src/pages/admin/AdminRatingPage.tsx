import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { ShieldCheck, ArrowRight, Users, FlaskConical } from 'lucide-react'
import { toast } from 'sonner'
import { ratingApi } from '@/api/rating.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ACTIVE_LOBS, LOB_LABELS } from '@/types/quote.types'
import type { PolicyLineOfBusiness } from '@/types/quote.types'
import type { RatingPlanListItem, ShadowRatingStatus } from '@/types/rating.types'

// Maps frontend LOB enum values to the keys returned by the settings API
const LOB_SHADOW_KEY: Record<PolicyLineOfBusiness, keyof ShadowRatingStatus> = {
  GeneralLiability:  'gl',
  InlandMarine:      'im',
  AutoLiability:     'al',
  AutoPhysicalDamage:'apd',
}

function StatusBadge({ status }: { status: RatingPlanListItem['status'] }) {
  const styles: Record<RatingPlanListItem['status'], string> = {
    Active: 'bg-emerald-100 text-emerald-700',
    Draft: 'bg-amber-100 text-amber-700',
    Retired: 'bg-slate-100 text-slate-500',
  }
  return (
    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${styles[status]}`}>
      {status}
    </span>
  )
}

function ShadowToggle({ lob, enabled, disabled }: { lob: PolicyLineOfBusiness; enabled: boolean; disabled: boolean }) {
  const qc = useQueryClient()
  const toggle = useMutation({
    mutationFn: (val: boolean) => ratingApi.updateShadowLob(lob, val),
    onSuccess: (data) => {
      qc.setQueryData(['shadow-status'], data)
      const lobLabel = LOB_LABELS[lob]
      toast.success(enabled ? `Shadow mode off for ${lobLabel}` : `Shadow mode on for ${lobLabel}`)
    },
    onError: () => toast.error('Failed to update shadow mode'),
  })

  return (
    <label className="relative inline-flex items-center gap-2 cursor-pointer" title="Allow underwriters to shadow rate quotes for this LOB">
      <FlaskConical className="h-3 w-3 text-slate-400" />
      <span className="text-xs text-slate-500">Shadow</span>
      <div className="relative">
        <input
          type="checkbox"
          className="sr-only peer"
          checked={enabled}
          disabled={disabled || toggle.isPending}
          onChange={(e) => toggle.mutate(e.target.checked)}
        />
        <div className="w-8 h-4 bg-slate-200 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-3 after:w-3 after:transition-all peer-checked:bg-blue-500 peer-disabled:opacity-50" />
      </div>
    </label>
  )
}

function PlanCard({ plan, shadowEnabled, shadowLoading }: { plan: RatingPlanListItem; shadowEnabled: boolean; shadowLoading: boolean }) {
  return (
    <div className="bg-white border rounded-lg p-4 space-y-3">
      <div className="flex items-start justify-between gap-2">
        <div>
          <p className="text-sm font-semibold text-slate-800">{plan.name}</p>
          <p className="text-xs text-slate-400 font-mono mt-0.5">{plan.formulaKey}</p>
        </div>
        <StatusBadge status={plan.status} />
      </div>

      <div className="space-y-1.5 text-xs text-slate-600">
        {plan.activeVersionNumber !== null ? (
          <>
            <div className="flex justify-between">
              <span className="text-slate-400">Active version</span>
              <span className="font-medium">v{plan.activeVersionNumber}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-400">Effective</span>
              <span>{plan.activeEffectiveDate}</span>
            </div>
          </>
        ) : (
          <p className="text-amber-600 text-xs">No active version</p>
        )}
        <div className="flex justify-between">
          <span className="text-slate-400">Versions</span>
          <span>{plan.versionCount}</span>
        </div>
        <div className="flex items-center justify-between">
          <span className="text-slate-400 flex items-center gap-1">
            <Users className="h-3 w-3" /> Carriers assigned
          </span>
          <span className={plan.assignedCarrierCount === 0 ? 'text-amber-600' : 'text-slate-700'}>
            {plan.assignedCarrierCount}
          </span>
        </div>
      </div>

      <div className="flex items-center justify-between pt-1 border-t border-slate-100">
        <ShadowToggle lob={plan.lob} enabled={shadowEnabled} disabled={shadowLoading} />
        <Link
          to={`/admin/rating/plans/${plan.id}`}
          className="flex items-center gap-1 text-xs text-blue-600 hover:text-blue-700 hover:underline"
        >
          View <ArrowRight className="h-3 w-3" />
        </Link>
      </div>
    </div>
  )
}

function LobSection({ lob, plans, shadowStatus, shadowLoading }: {
  lob: PolicyLineOfBusiness
  plans: RatingPlanListItem[]
  shadowStatus: ShadowRatingStatus | undefined
  shadowLoading: boolean
}) {
  const lobPlans = plans.filter((p) => p.lob === lob)
  const shadowKey = LOB_SHADOW_KEY[lob]
  const shadowEnabled = shadowStatus ? shadowStatus[shadowKey] : false

  return (
    <div>
      <h2 className="text-sm font-semibold text-slate-700 mb-3 flex items-center gap-2">
        <span className="px-2 py-0.5 bg-blue-50 text-blue-700 rounded text-xs border border-blue-100">
          {LOB_LABELS[lob]}
        </span>
      </h2>

      {lobPlans.length === 0 ? (
        <div className="border border-dashed rounded-lg px-4 py-6 text-center">
          <ShieldCheck className="h-7 w-7 text-slate-200 mx-auto mb-2" />
          <p className="text-sm text-slate-400">No rating plan for {LOB_LABELS[lob]}.</p>
          <p className="text-xs text-slate-300 mt-0.5">Create one via a seed migration.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {lobPlans.map((plan) => (
            <PlanCard key={plan.id} plan={plan} shadowEnabled={shadowEnabled} shadowLoading={shadowLoading} />
          ))}
        </div>
      )}
    </div>
  )
}

export function AdminRatingPage() {
  const { data: plans = [], isLoading } = useQuery({
    queryKey: ['rating-plans'],
    queryFn: () => ratingApi.getPlans(),
  })

  const { data: shadowStatus, isLoading: shadowLoading } = useQuery({
    queryKey: ['shadow-status'],
    queryFn: () => ratingApi.getShadowStatus(),
  })

  if (isLoading) return <LoadingSpinner />

  return (
    <div className="p-6 space-y-8 max-w-5xl">
      <div>
        <h1 className="text-xl font-semibold text-slate-900 flex items-center gap-2">
          <ShieldCheck className="h-5 w-5 text-slate-400" />
          Rating Engine
        </h1>
        <p className="text-sm text-slate-500 mt-1">
          Rating plans and factor tables by line of business.
        </p>
      </div>

      {ACTIVE_LOBS.map((lob) => (
        <LobSection key={lob} lob={lob} plans={plans} shadowStatus={shadowStatus} shadowLoading={shadowLoading} />
      ))}
    </div>
  )
}
