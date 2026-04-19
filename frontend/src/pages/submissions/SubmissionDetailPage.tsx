import { useState } from 'react'
import { Link, useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Plus, Pencil, Trash2, CheckCircle, X, Check, FileText } from 'lucide-react'
import { toast } from 'sonner'
import { submissionsApi } from '@/api/submissions.api'
import { quotesApi } from '@/api/quotes.api'
import { carriersApi } from '@/api/carriers.api'
import { usersApi } from '@/api/users.api'
import { agentsApi } from '@/api/agents.api'
import { SUBMISSION_STATUS_LABELS, type SubmissionStatus, type SubmissionUpdate } from '@/types/submission.types'
import { LOB_LABELS, ALL_LOBS, QUOTE_STATUS_LABELS, type PolicyLineOfBusiness, type QuoteStatus, type QuoteCreate, type QuoteBind } from '@/types/quote.types'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { formatCurrency } from '@/lib/utils'
import { DocumentsSection } from '@/components/documents/DocumentsSection'
import { GenerateDocumentModal } from '@/components/documents/GenerateDocumentModal'
import { usePermissions } from '@/hooks/usePermissions'

const STATUS_COLORS: Record<SubmissionStatus, string> = {
  New: 'bg-slate-100 text-slate-600',
  InProgress: 'bg-blue-100 text-blue-700',
  Quoted: 'bg-yellow-100 text-yellow-700',
  Bound: 'bg-green-100 text-green-700',
  Declined: 'bg-red-100 text-red-700',
  Withdrawn: 'bg-slate-100 text-slate-500',
}

const QUOTE_STATUS_COLORS: Record<QuoteStatus, string> = {
  Draft: 'bg-slate-100 text-slate-600',
  Submitted: 'bg-blue-100 text-blue-700',
  Quoted: 'bg-yellow-100 text-yellow-700',
  Bound: 'bg-green-100 text-green-700',
  Declined: 'bg-red-100 text-red-700',
  Cancelled: 'bg-red-100 text-red-600',
  Expired: 'bg-slate-100 text-slate-500',
}

type QuoteForm = {
  carrierId: string
  lineOfBusiness: PolicyLineOfBusiness | ''
  effectiveDate: string
  expirationDate: string
  premiumAmount: string
  taxesAndFees: string
  commissionRate: string
  coverageDescription: string
  deductible: string
  limit: string
}

const emptyQuoteForm = (): QuoteForm => ({
  carrierId: '', lineOfBusiness: '', effectiveDate: '', expirationDate: '',
  premiumAmount: '', taxesAndFees: '0', commissionRate: '0',
  coverageDescription: '', deductible: '', limit: '',
})

export function SubmissionDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { canUploadAttachments, canDeleteAttachments, canCreatePolicies } = usePermissions()

  const [showGenerateModal, setShowGenerateModal] = useState(false)

  const [showQuoteForm, setShowQuoteForm] = useState(false)
  const [quoteForm, setQuoteForm] = useState<QuoteForm>(emptyQuoteForm())
  const [bindingQuoteId, setBindingQuoteId] = useState<string | null>(null)
  const [bindForm, setBindForm] = useState({ boundDate: '', effectiveDate: '', expirationDate: '' })

  const { data: submission, isLoading } = useQuery({
    queryKey: ['submissions', id],
    queryFn: () => submissionsApi.getById(id!),
  })

  const { data: quotes = [] } = useQuery({
    queryKey: ['quotes', 'by-submission', id],
    queryFn: () => quotesApi.getBySubmission(id!),
    enabled: !!id,
  })

  const { data: carriers = [] } = useQuery({
    queryKey: ['carriers', 'active'],
    queryFn: () => carriersApi.getAll(true),
  })

  const selectedCarrier = carriers.find((c) => c.id === quoteForm.carrierId)
  const availableLobs = selectedCarrier ? selectedCarrier.linesOfBusiness : ALL_LOBS

  const createQuoteMutation = useMutation({
    mutationFn: (data: QuoteCreate) => quotesApi.create(data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quotes', 'by-submission', id] })
      qc.invalidateQueries({ queryKey: ['submissions', id] })
      setShowQuoteForm(false)
      setQuoteForm(emptyQuoteForm())
      toast.success('Quote created')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to create quote'),
  })

  const deleteQuoteMutation = useMutation({
    mutationFn: (quoteId: string) => quotesApi.delete(quoteId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quotes', 'by-submission', id] })
      toast.success('Quote deleted')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to delete quote'),
  })

  const bindMutation = useMutation({
    mutationFn: ({ quoteId, data }: { quoteId: string; data: QuoteBind }) => quotesApi.bind(quoteId, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quotes', 'by-submission', id] })
      qc.invalidateQueries({ queryKey: ['submissions', id] })
      setBindingQuoteId(null)
      toast.success('Quote bound — policy created')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to bind quote'),
  })

  const handleCreateQuote = () => {
    if (!quoteForm.carrierId || !quoteForm.lineOfBusiness || !quoteForm.effectiveDate || !quoteForm.expirationDate) {
      toast.error('Carrier, line of business, and dates are required')
      return
    }
    createQuoteMutation.mutate({
      submissionId: id!,
      carrierId: quoteForm.carrierId,
      lineOfBusiness: quoteForm.lineOfBusiness as PolicyLineOfBusiness,
      effectiveDate: quoteForm.effectiveDate,
      expirationDate: quoteForm.expirationDate,
      premiumAmount: parseFloat(quoteForm.premiumAmount) || 0,
      taxesAndFees: parseFloat(quoteForm.taxesAndFees) || 0,
      commissionRate: parseFloat(quoteForm.commissionRate) || 0,
      coverageDescription: quoteForm.coverageDescription || undefined,
      deductible: quoteForm.deductible ? parseFloat(quoteForm.deductible) : undefined,
      limit: quoteForm.limit ? parseFloat(quoteForm.limit) : undefined,
    })
  }

  const handleBind = () => {
    if (!bindingQuoteId || !bindForm.boundDate || !bindForm.effectiveDate || !bindForm.expirationDate) {
      toast.error('All bind dates are required')
      return
    }
    bindMutation.mutate({ quoteId: bindingQuoteId, data: bindForm })
  }

  const setQF = (k: keyof QuoteForm) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    const val = e.target.value
    setQuoteForm((prev) => {
      const next = { ...prev, [k]: val }
      if (k === 'carrierId') next.lineOfBusiness = ''
      return next
    })
  }

  if (isLoading) return <LoadingSpinner />
  if (!submission) return <p className="p-6 text-slate-500">Submission not found.</p>

  return (
    <div className="p-6 space-y-6">
      {/* Breadcrumb */}
      <Link
        to={`/insureds/${submission.insuredId}`}
        className="inline-flex items-center gap-1 text-sm text-slate-500 hover:text-slate-900"
      >
        <ArrowLeft className="h-3.5 w-3.5" /> {submission.insuredName}
      </Link>

      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-xl font-semibold text-slate-900">{submission.submissionNumber}</h1>
          <p className="text-sm text-slate-500 mt-0.5">{submission.insuredName}</p>
        </div>
        <div className="flex items-center gap-2">
          {canCreatePolicies && (
            <button
              onClick={() => setShowGenerateModal(true)}
              className="flex items-center gap-1.5 px-3 py-1.5 border rounded text-sm text-slate-700 hover:bg-slate-50"
            >
              <FileText className="h-3.5 w-3.5" /> Generate Document
            </button>
          )}
          <span className={`inline-flex px-2.5 py-1 rounded-full text-xs font-medium ${STATUS_COLORS[submission.status]}`}>
            {SUBMISSION_STATUS_LABELS[submission.status]}
          </span>
        </div>
      </div>

      {showGenerateModal && (
        <GenerateDocumentModal
          entityType="Submission"
          entityId={id!}
          onClose={() => setShowGenerateModal(false)}
        />
      )}

      {/* Submission info */}
      <div className="bg-white border rounded-lg p-5 grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Underwriter</p>
          <p className="font-medium">{submission.underwriterName}</p>
        </div>
        {submission.assistantUWName && (
          <div>
            <p className="text-xs text-slate-500 mb-0.5">Asst. Underwriter</p>
            <p className="font-medium">{submission.assistantUWName}</p>
          </div>
        )}
        {submission.agentName && (
          <div>
            <p className="text-xs text-slate-500 mb-0.5">Agent</p>
            <p className="font-medium">{submission.agentName}</p>
            {submission.agencyName && <p className="text-xs text-slate-400">{submission.agencyName}</p>}
          </div>
        )}
        {submission.effectiveDate && (
          <div>
            <p className="text-xs text-slate-500 mb-0.5">Target Eff. Date</p>
            <p className="font-medium">{new Date(submission.effectiveDate).toLocaleDateString()}</p>
          </div>
        )}
        {submission.expirationDate && (
          <div>
            <p className="text-xs text-slate-500 mb-0.5">Target Exp. Date</p>
            <p className="font-medium">{new Date(submission.expirationDate).toLocaleDateString()}</p>
          </div>
        )}
      </div>

      {/* Quotes section */}
      <div className="bg-white border rounded-lg">
        <div className="flex items-center justify-between px-5 py-4 border-b">
          <h2 className="text-sm font-semibold text-slate-900">Quotes ({quotes.length})</h2>
          {!showQuoteForm && (
            <button
              onClick={() => { setShowQuoteForm(true); setQuoteForm(emptyQuoteForm()) }}
              className="flex items-center gap-1 text-sm text-blue-600 hover:underline"
            >
              <Plus className="h-3.5 w-3.5" /> Add Quote
            </button>
          )}
        </div>

        {/* New quote form */}
        {showQuoteForm && (
          <div className="px-5 py-4 border-b bg-slate-50 space-y-4">
            <h3 className="text-sm font-medium text-slate-700">New Quote</h3>
            <div className="grid grid-cols-2 md:grid-cols-3 gap-3 text-sm">
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Carrier *</label>
                <select value={quoteForm.carrierId} onChange={setQF('carrierId')} className="w-full border rounded px-2 py-1.5">
                  <option value="">— Select carrier —</option>
                  {carriers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Line of Business *</label>
                <select value={quoteForm.lineOfBusiness} onChange={setQF('lineOfBusiness')} disabled={!quoteForm.carrierId} className="w-full border rounded px-2 py-1.5 disabled:opacity-50">
                  <option value="">— Select LOB —</option>
                  {availableLobs.map((l) => <option key={l} value={l}>{LOB_LABELS[l]}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Effective Date *</label>
                <input type="date" value={quoteForm.effectiveDate} onChange={setQF('effectiveDate')} className="w-full border rounded px-2 py-1.5" />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Expiration Date *</label>
                <input type="date" value={quoteForm.expirationDate} onChange={setQF('expirationDate')} className="w-full border rounded px-2 py-1.5" />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Premium</label>
                <input type="number" value={quoteForm.premiumAmount} onChange={setQF('premiumAmount')} placeholder="0.00" className="w-full border rounded px-2 py-1.5" />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Taxes & Fees</label>
                <input type="number" value={quoteForm.taxesAndFees} onChange={setQF('taxesAndFees')} placeholder="0.00" className="w-full border rounded px-2 py-1.5" />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Commission Rate</label>
                <input type="number" value={quoteForm.commissionRate} onChange={setQF('commissionRate')} placeholder="0.00" step="0.01" className="w-full border rounded px-2 py-1.5" />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Limit</label>
                <input type="number" value={quoteForm.limit} onChange={setQF('limit')} placeholder="Optional" className="w-full border rounded px-2 py-1.5" />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Deductible</label>
                <input type="number" value={quoteForm.deductible} onChange={setQF('deductible')} placeholder="Optional" className="w-full border rounded px-2 py-1.5" />
              </div>
            </div>
            <div className="flex gap-2">
              <button onClick={handleCreateQuote} disabled={createQuoteMutation.isPending} className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-600 text-white rounded text-sm hover:bg-blue-700 disabled:opacity-50">
                <Check className="h-3.5 w-3.5" /> Save Quote
              </button>
              <button onClick={() => setShowQuoteForm(false)} className="flex items-center gap-1.5 px-3 py-1.5 border rounded text-sm hover:bg-white">
                <X className="h-3.5 w-3.5" /> Cancel
              </button>
            </div>
          </div>
        )}

        {/* Quotes list */}
        {quotes.length === 0 && !showQuoteForm ? (
          <p className="text-sm text-slate-400 px-5 py-8 text-center">No quotes yet. Add one above.</p>
        ) : (
          <div className="divide-y">
            {quotes.map((q) => (
              <div key={q.id}>
                <div className="flex items-center justify-between px-5 py-3">
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium text-slate-900">
                        {q.policyNumber ?? q.quoteNumber}
                      </span>
                      <span className={`inline-flex px-2 py-0.5 rounded-full text-xs font-medium ${QUOTE_STATUS_COLORS[q.status]}`}>
                        {QUOTE_STATUS_LABELS[q.status]}
                      </span>
                    </div>
                    <p className="text-xs text-slate-500 mt-0.5">
                      {q.carrierName} · {LOB_LABELS[q.lineOfBusiness]}
                      {' · '}Eff. {new Date(q.effectiveDate).toLocaleDateString()} – {new Date(q.expirationDate).toLocaleDateString()}
                    </p>
                  </div>
                  <div className="flex items-center gap-3 ml-4">
                    <span className="text-sm font-medium text-slate-700">{formatCurrency(q.totalPremium)}</span>
                    {q.status !== 'Bound' && q.status !== 'Cancelled' && q.status !== 'Expired' && (
                      <button
                        onClick={() => {
                          setBindingQuoteId(q.id)
                          setBindForm({ boundDate: '', effectiveDate: q.effectiveDate, expirationDate: q.expirationDate })
                        }}
                        className="flex items-center gap-1 text-xs text-green-700 border border-green-300 px-2 py-1 rounded hover:bg-green-50"
                      >
                        <CheckCircle className="h-3.5 w-3.5" /> Bind
                      </button>
                    )}
                    {q.status !== 'Bound' && (
                      <button
                        onClick={() => { if (confirm('Delete this quote?')) deleteQuoteMutation.mutate(q.id) }}
                        className="p-1 text-slate-400 hover:text-red-600 rounded"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    )}
                    {q.status === 'Bound' && (
                      <Link to={`/policies/${q.id}`} className="text-xs text-blue-600 hover:underline">
                        View Policy
                      </Link>
                    )}
                  </div>
                </div>

                {/* Inline bind form */}
                {bindingQuoteId === q.id && (
                  <div className="px-5 py-3 bg-green-50 border-t border-green-100 space-y-3">
                    <p className="text-xs font-medium text-green-800">Bind Quote — confirm policy dates</p>
                    <div className="grid grid-cols-3 gap-3 text-sm">
                      <div>
                        <label className="block text-xs font-medium text-slate-600 mb-1">Bound Date *</label>
                        <input type="date" value={bindForm.boundDate} onChange={(e) => setBindForm((b) => ({ ...b, boundDate: e.target.value }))} className="w-full border rounded px-2 py-1.5" />
                      </div>
                      <div>
                        <label className="block text-xs font-medium text-slate-600 mb-1">Effective Date *</label>
                        <input type="date" value={bindForm.effectiveDate} onChange={(e) => setBindForm((b) => ({ ...b, effectiveDate: e.target.value }))} className="w-full border rounded px-2 py-1.5" />
                      </div>
                      <div>
                        <label className="block text-xs font-medium text-slate-600 mb-1">Expiration Date *</label>
                        <input type="date" value={bindForm.expirationDate} onChange={(e) => setBindForm((b) => ({ ...b, expirationDate: e.target.value }))} className="w-full border rounded px-2 py-1.5" />
                      </div>
                    </div>
                    <div className="flex gap-2">
                      <button onClick={handleBind} disabled={bindMutation.isPending} className="flex items-center gap-1.5 px-3 py-1.5 bg-green-600 text-white rounded text-sm hover:bg-green-700 disabled:opacity-50">
                        <CheckCircle className="h-3.5 w-3.5" /> Confirm Bind
                      </button>
                      <button onClick={() => setBindingQuoteId(null)} className="flex items-center gap-1.5 px-3 py-1.5 border rounded text-sm hover:bg-white">
                        <X className="h-3.5 w-3.5" /> Cancel
                      </button>
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Documents */}
      <div className="bg-white border rounded-lg p-5">
        <DocumentsSection entityType="Submission" entityId={id!} canUpload={canUploadAttachments} canDelete={canDeleteAttachments} />
      </div>
    </div>
  )
}
