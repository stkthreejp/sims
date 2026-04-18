import { Link, useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation } from '@tanstack/react-query'
import { Edit, Trash2, Plus, ArrowLeft } from 'lucide-react'
import { toast } from 'sonner'
import { insuredsApi } from '@/api/insureds.api'
import { submissionsApi } from '@/api/submissions.api'
import { quotesApi } from '@/api/quotes.api'
import { queryClient } from '@/lib/queryClient'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { SUBMISSION_STATUS_LABELS, type SubmissionStatus } from '@/types/submission.types'
import { LOB_LABELS } from '@/types/quote.types'
import { formatCurrency } from '@/lib/utils'
import { usePermissions } from '@/hooks/usePermissions'

const SUB_STATUS_COLORS: Record<SubmissionStatus, string> = {
  New: 'bg-slate-100 text-slate-600',
  InProgress: 'bg-blue-100 text-blue-700',
  Quoted: 'bg-yellow-100 text-yellow-700',
  Bound: 'bg-green-100 text-green-700',
  Declined: 'bg-red-100 text-red-700',
  Withdrawn: 'bg-slate-100 text-slate-500',
}

export function InsuredDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { canEditInsureds, canDeleteInsureds, canCreatePolicies } = usePermissions()

  const { data: insured, isLoading } = useQuery({
    queryKey: ['insureds', id],
    queryFn: () => insuredsApi.getById(id!),
  })

  const { data: submissions } = useQuery({
    queryKey: ['submissions', 'by-insured', id],
    queryFn: () => submissionsApi.getByInsured(id!),
    enabled: !!id,
  })

  const { data: policies } = useQuery({
    queryKey: ['quotes', 'bound-by-insured', id],
    queryFn: () => quotesApi.getBoundByInsured(id!),
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
  if (!insured) return <p className="p-6 text-slate-500">Insured not found.</p>

  return (
    <div className="p-6 space-y-6">
      <div>
        <Link to="/insureds" className="inline-flex items-center gap-1 text-sm text-slate-500 hover:text-slate-900">
          <ArrowLeft className="h-3.5 w-3.5" /> Back to Insureds
        </Link>
      </div>

      <PageHeader
        title={insured.displayName}
        description={`${insured.insuredType} · ${insured.city}, ${insured.state}`}
        actions={
          <>
            {canEditInsureds && (
              <Link to={`/insureds/${id}/edit`} className="flex items-center gap-1.5 px-3 py-2 border border-slate-300 text-sm rounded-md hover:bg-slate-50">
                <Edit className="h-4 w-4" /> Edit
              </Link>
            )}
            {canDeleteInsureds && (
              <button
                onClick={() => { if (confirm('Delete this insured?')) deleteMutation.mutate() }}
                className="flex items-center gap-1.5 px-3 py-2 border border-red-300 text-red-600 text-sm rounded-md hover:bg-red-50"
              >
                <Trash2 className="h-4 w-4" /> Delete
              </button>
            )}
          </>
        }
      />

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left — contact & address */}
        <div className="space-y-4">
          <div className="bg-white rounded-lg border p-5">
            <h3 className="text-sm font-semibold text-slate-900 mb-3">Contact Information</h3>
            <dl className="space-y-2 text-sm">
              {insured.email && <><dt className="text-slate-500">Email</dt><dd>{insured.email}</dd></>}
              {insured.phone && <><dt className="text-slate-500">Phone</dt><dd>{insured.phone}</dd></>}
              {insured.phoneAlt && <><dt className="text-slate-500">Alt Phone</dt><dd>{insured.phoneAlt}</dd></>}
            </dl>
          </div>
          <div className="bg-white rounded-lg border p-5">
            <h3 className="text-sm font-semibold text-slate-900 mb-3">Address</h3>
            <address className="text-sm text-slate-600 not-italic space-y-0.5">
              <p>{insured.addressLine1}</p>
              {insured.addressLine2 && <p>{insured.addressLine2}</p>}
              <p>{insured.city}, {insured.state} {insured.zipCode}</p>
              {insured.county && <p>{insured.county} County</p>}
            </address>
          </div>
        </div>

        {/* Right — submissions + policies */}
        <div className="lg:col-span-2 space-y-4">

          {/* Policies (bound quotes) */}
          <div className="bg-white rounded-lg border">
            <div className="flex items-center justify-between px-5 py-4 border-b">
              <h3 className="text-sm font-semibold text-slate-900">
                Policies ({policies?.length ?? 0})
              </h3>
            </div>
            <div className="divide-y">
              {policies?.map((p) => (
                <Link
                  key={p.id}
                  to={`/policies/${p.id}`}
                  className="flex items-center justify-between px-5 py-3 hover:bg-slate-50"
                >
                  <div>
                    <p className="text-sm font-medium text-blue-600">{p.policyNumber}</p>
                    <p className="text-xs text-slate-500">
                      {p.carrierName} · {LOB_LABELS[p.lineOfBusiness]}
                      {' · '}Eff. {new Date(p.effectiveDate).toLocaleDateString()} – {new Date(p.expirationDate).toLocaleDateString()}
                    </p>
                  </div>
                  <span className="text-sm font-medium text-slate-700">{formatCurrency(p.totalPremium)}</span>
                </Link>
              ))}
              {!policies?.length && (
                <p className="text-sm text-slate-400 px-5 py-5 text-center">No bound policies yet.</p>
              )}
            </div>
          </div>

          {/* Submissions */}
          <div className="bg-white rounded-lg border">
            <div className="flex items-center justify-between px-5 py-4 border-b">
              <h3 className="text-sm font-semibold text-slate-900">
                Submissions ({submissions?.length ?? 0})
              </h3>
              {canCreatePolicies && (
                <Link
                  to={`/submissions/new?insuredId=${id}`}
                  className="flex items-center gap-1 text-sm text-blue-600 hover:underline"
                >
                  <Plus className="h-3.5 w-3.5" /> New Submission
                </Link>
              )}
            </div>
            <div className="divide-y">
              {submissions?.map((s) => (
                <Link
                  key={s.id}
                  to={`/submissions/${s.id}`}
                  className="flex items-center justify-between px-5 py-3 hover:bg-slate-50"
                >
                  <div>
                    <p className="text-sm font-medium text-blue-600">{s.submissionNumber}</p>
                    <p className="text-xs text-slate-500">
                      {s.underwriterName}
                      {s.agentName ? ` · ${s.agentName}` : ''}
                      {s.effectiveDate ? ` · Eff. ${new Date(s.effectiveDate).toLocaleDateString()}` : ''}
                    </p>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="text-xs text-slate-500">{s.quoteCount} quote{s.quoteCount !== 1 ? 's' : ''}</span>
                    <span className={`inline-flex px-2 py-0.5 rounded-full text-xs font-medium ${SUB_STATUS_COLORS[s.status]}`}>
                      {SUBMISSION_STATUS_LABELS[s.status]}
                    </span>
                  </div>
                </Link>
              ))}
              {!submissions?.length && (
                <p className="text-sm text-slate-400 px-5 py-5 text-center">
                  No submissions yet.{' '}
                  {canCreatePolicies && (
                    <Link to={`/submissions/new?insuredId=${id}`} className="text-blue-600 hover:underline">
                      Create the first one.
                    </Link>
                  )}
                </p>
              )}
            </div>
          </div>

        </div>
      </div>
    </div>
  )
}
