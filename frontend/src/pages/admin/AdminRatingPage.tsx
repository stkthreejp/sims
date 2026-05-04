import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { ShieldCheck, ArrowRight, Users } from 'lucide-react'
import { ratingApi } from '@/api/rating.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ACTIVE_LOBS, LOB_LABELS } from '@/types/quote.types'
import type { PolicyLineOfBusiness } from '@/types/quote.types'
import type { RatingPlanListItem } from '@/types/rating.types'

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

function PlanCard({ plan }: { plan: RatingPlanListItem }) {
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

      <Link
        to={`/admin/rating/plans/${plan.id}`}
        className="flex items-center justify-end gap-1 text-xs text-blue-600 hover:text-blue-700 hover:underline"
      >
        View <ArrowRight className="h-3 w-3" />
      </Link>
    </div>
  )
}

function LobSection({ lob, plans }: { lob: PolicyLineOfBusiness; plans: RatingPlanListItem[] }) {
  const lobPlans = plans.filter((p) => p.lob === lob)

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
            <PlanCard key={plan.id} plan={plan} />
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
        <LobSection key={lob} lob={lob} plans={plans} />
      ))}
    </div>
  )
}
