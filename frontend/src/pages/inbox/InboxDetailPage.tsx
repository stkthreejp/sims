import { useState, useMemo } from 'react'
import { useNavigate, useParams, Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, FileText, Paperclip, CheckCircle2, AlertCircle, Search, UserPlus, User, Sparkles } from 'lucide-react'
import { toast } from 'sonner'
import { inboundEmailsApi } from '@/api/inboundEmails.api'
import type { CreateSubmissionResult } from '@/api/inboundEmails.api'
import { insuredsApi } from '@/api/insureds.api'
import { format } from 'date-fns'
import type { EmailAttachmentDocumentType } from '@/types/inboundEmail.types'
import type { InsuredListItem } from '@/types/insured.types'
import { LOB_LABELS, ACTIVE_LOBS, type PolicyLineOfBusiness } from '@/types/quote.types'

const DOC_TYPE_LABELS: Record<EmailAttachmentDocumentType, string> = {
  Unknown: 'Unknown',
  Acord125: 'ACORD 125',
  Acord126: 'ACORD 126',
  LossRun: 'Loss Run',
  DecPage: 'Dec Page',
  ScheduleOfValues: 'Schedule of Values',
  SignedApplication: 'Signed Application',
  Other: 'Other',
}

const LOB_FROM_DOC_TYPE: Partial<Record<EmailAttachmentDocumentType, PolicyLineOfBusiness>> = {
  Acord125: 'CommercialAuto',
  Acord126: 'GeneralLiability',
  ScheduleOfValues: 'InlandMarine',
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

type Step = 'idle' | 'search' | 'confirm'

export function InboxDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [step, setStep] = useState<Step>('idle')
  const [searchQuery, setSearchQuery] = useState('')
  const [selectedInsured, setSelectedInsured] = useState<InsuredListItem | null>(null)
  const [createNew, setCreateNew] = useState(false)

  // Track deselected attachment IDs (all start selected)
  const [deselectedIds, setDeselectedIds] = useState<Set<string>>(new Set())
  const [selectedLob, setSelectedLob] = useState<PolicyLineOfBusiness | ''>('')

  const { data: email, isLoading, isError } = useQuery({
    queryKey: ['inbound-emails', id],
    queryFn: () => inboundEmailsApi.getById(id!),
    enabled: !!id,
  })

  const { data: insuredResults, isFetching: searchingInsureds } = useQuery({
    queryKey: ['insureds', 'search', searchQuery],
    queryFn: () => insuredsApi.getAll({ search: searchQuery, pageSize: 10 }),
    enabled: step === 'search' && searchQuery.trim().length >= 2,
    placeholderData: (prev) => prev,
  })

  // Derive selected attachments and detected LOB from selections
  const selectedAttachments = useMemo(
    () => (email?.attachments ?? []).filter(a => !deselectedIds.has(a.id)),
    [email, deselectedIds]
  )

  const detectedLob = useMemo<PolicyLineOfBusiness | null>(() => {
    for (const att of selectedAttachments) {
      const lob = LOB_FROM_DOC_TYPE[att.documentType]
      if (lob) return lob
    }
    return null
  }, [selectedAttachments])

  // Effective LOB: user's explicit selection wins, then detected from doc types
  const effectiveLob: PolicyLineOfBusiness | '' = selectedLob || detectedLob || ''

  const toggleAttachment = (attId: string) => {
    setDeselectedIds(prev => {
      const next = new Set(prev)
      if (next.has(attId)) next.delete(attId)
      else next.add(attId)
      return next
    })
  }

  const createSubmission = useMutation({
    mutationFn: () =>
      inboundEmailsApi.createSubmission(
        id!,
        (!createNew && selectedInsured) ? selectedInsured.id : undefined,
        selectedAttachments.map(a => a.id),
        effectiveLob || undefined,
      ),
    onSuccess: (result: CreateSubmissionResult) => {
      queryClient.invalidateQueries({ queryKey: ['inbound-emails'] })
      if (result.extractionStatus === 'Completed') {
        toast.success('Submission created — data pre-filled from attachments')
      } else if (result.extractionStatus === 'DetectionFailed') {
        toast.warning('Submission created — LOB could not be detected. Review and set lines of business on the submission page.')
      } else if (result.extractionStatus === 'Failed') {
        toast.warning('Submission created — AI extraction failed. Fill in manually or re-run from the submission page.')
      } else {
        toast.success('Submission created successfully')
      }
      navigate(`/submissions/${result.submission.id}`, {
        state: {
          extractionStatus: result.extractionStatus,
          emailId: result.emailId,
        },
      })
    },
    onError: () => {
      toast.error('Failed to create submission')
    },
  })

  const openSearch = () => {
    setSearchQuery(email?.fromName ?? email?.fromAddress ?? '')
    setSelectedInsured(null)
    setCreateNew(false)
    setSelectedLob('')
    setStep('search')
  }

  if (isLoading) {
    return <div className="flex items-center justify-center h-48 text-slate-500 text-sm">Loading…</div>
  }

  if (isError || !email) {
    return (
      <div className="flex flex-col items-center justify-center h-48 gap-2 text-slate-500">
        <AlertCircle className="h-8 w-8 text-red-400" />
        <p className="text-sm">Email not found.</p>
      </div>
    )
  }

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="px-6 py-4 border-b border-slate-200 bg-white flex items-start gap-3">
        <button onClick={() => navigate('/inbox')} className="mt-0.5 text-slate-400 hover:text-slate-700 transition-colors">
          <ArrowLeft className="h-5 w-5" />
        </button>
        <div className="flex-1 min-w-0">
          <h1 className="text-lg font-semibold text-slate-900 truncate">{email.subject}</h1>
          <p className="text-sm text-slate-500">
            From{' '}
            <span className="font-medium text-slate-700">{email.fromName ?? email.fromAddress}</span>
            {email.fromName && <span className="ml-1 text-slate-400">&lt;{email.fromAddress}&gt;</span>}
            {' · '}
            {format(new Date(email.receivedAt), 'PPpp')}
          </p>
        </div>
        <div className="shrink-0 pt-0.5">
          {email.isProcessed ? (
            <div className="flex items-center gap-1.5 text-sm text-emerald-600">
              <CheckCircle2 className="h-4 w-4" />
              <span>Processed</span>
              {email.linkedSubmissionId && (
                <Link to={`/submissions/${email.linkedSubmissionId}`} className="ml-2 text-blue-600 hover:underline text-xs">
                  View Submission →
                </Link>
              )}
            </div>
          ) : (
            <button
              onClick={openSearch}
              disabled={selectedAttachments.length === 0 && email.attachments.length > 0}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              <FileText className="h-4 w-4" />
              Create Submission from Email
            </button>
          )}
        </div>
      </div>

      {/* Body + Attachments */}
      <div className="flex-1 overflow-auto p-6 space-y-6">
        <div className="bg-white border border-slate-200 rounded-lg p-6">
          <h2 className="text-sm font-semibold text-slate-500 uppercase tracking-wide mb-3">Message Body</h2>
          {email.bodyText ? (
            <div className="text-sm text-slate-700 leading-relaxed whitespace-pre-wrap max-h-96 overflow-auto">
              {email.bodyText.replace(/<[^>]+>/g, '')}
            </div>
          ) : (
            <p className="text-sm text-slate-400 italic">No body content</p>
          )}
        </div>

        {email.attachments.length > 0 && (
          <div className="bg-white border border-slate-200 rounded-lg p-6">
            <div className="flex items-center justify-between mb-3">
              <h2 className="text-sm font-semibold text-slate-500 uppercase tracking-wide">
                Attachments ({email.attachments.length})
              </h2>
              {!email.isProcessed && (
                <p className="text-xs text-slate-400">Uncheck logos or irrelevant files before creating a submission</p>
              )}
            </div>
            <ul className="divide-y divide-slate-100">
              {email.attachments.map((att) => {
                const checked = !deselectedIds.has(att.id)
                return (
                  <li key={att.id} className={`flex items-center justify-between py-3 ${!checked ? 'opacity-50' : ''}`}>
                    <div className="flex items-center gap-3 min-w-0">
                      {!email.isProcessed ? (
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={() => toggleAttachment(att.id)}
                          className="h-4 w-4 rounded border-slate-300 text-blue-600 focus:ring-blue-500 shrink-0 cursor-pointer"
                        />
                      ) : (
                        <Paperclip className="h-4 w-4 text-slate-400 shrink-0" />
                      )}
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-slate-800 truncate">{att.fileName}</p>
                        <p className="text-xs text-slate-400">
                          {DOC_TYPE_LABELS[att.documentType]} · {formatBytes(att.fileSizeBytes)}
                        </p>
                      </div>
                    </div>
                    <a href={att.blobUrl} target="_blank" rel="noopener noreferrer" className="ml-4 shrink-0 text-xs text-blue-600 hover:underline">
                      Download
                    </a>
                  </li>
                )
              })}
            </ul>
            {!email.isProcessed && deselectedIds.size > 0 && (
              <p className="mt-3 text-xs text-slate-400">
                {selectedAttachments.length} of {email.attachments.length} attachments selected
              </p>
            )}
          </div>
        )}
      </div>

      {/* Create Submission Modal */}
      {step !== 'idle' && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-xl shadow-xl w-full max-w-lg mx-4 overflow-hidden">

            {step === 'search' && (
              <>
                <div className="px-6 py-4 border-b border-slate-200">
                  <h2 className="text-base font-semibold text-slate-900">Find or create an insured</h2>
                  <p className="text-sm text-slate-500 mt-0.5">Search for an existing insured or create a new one from the sender info.</p>
                </div>

                <div className="px-6 py-4 space-y-4">
                  <div className="relative">
                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                    <input
                      autoFocus
                      type="text"
                      placeholder="Search by name or company…"
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      className="w-full pl-9 pr-4 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                    />
                  </div>

                  <div className="min-h-[160px] max-h-64 overflow-auto rounded-md border border-slate-200 divide-y divide-slate-100">
                    {searchQuery.trim().length < 2 ? (
                      <div className="flex items-center justify-center h-20 text-sm text-slate-400">
                        Type at least 2 characters to search
                      </div>
                    ) : searchingInsureds ? (
                      <div className="flex items-center justify-center h-20 text-sm text-slate-400">Searching…</div>
                    ) : (insuredResults?.items ?? []).length === 0 ? (
                      <div className="flex items-center justify-center h-20 text-sm text-slate-400">No insureds found</div>
                    ) : (
                      (insuredResults?.items ?? []).map((insured) => (
                        <button
                          key={insured.id}
                          onClick={() => { setSelectedInsured(insured); setCreateNew(false); setStep('confirm') }}
                          className="w-full text-left px-4 py-3 hover:bg-blue-50 transition-colors flex items-center gap-3"
                        >
                          <User className="h-4 w-4 text-slate-400 shrink-0" />
                          <div>
                            <p className="text-sm font-medium text-slate-800">{insured.displayName}</p>
                            <p className="text-xs text-slate-400">
                              {insured.email ?? 'No email'} · {insured.city}, {insured.state}
                            </p>
                          </div>
                        </button>
                      ))
                    )}
                  </div>
                </div>

                <div className="px-6 py-4 border-t border-slate-200 flex items-center justify-between">
                  <button
                    onClick={() => { setCreateNew(true); setStep('confirm') }}
                    className="flex items-center gap-2 text-sm font-medium text-slate-600 hover:text-slate-900 transition-colors"
                  >
                    <UserPlus className="h-4 w-4" />
                    Create new insured from sender
                  </button>
                  <button onClick={() => setStep('idle')} className="text-sm text-slate-400 hover:text-slate-700">
                    Cancel
                  </button>
                </div>
              </>
            )}

            {step === 'confirm' && (
              <>
                <div className="px-6 py-4 border-b border-slate-200">
                  <h2 className="text-base font-semibold text-slate-900">Confirm submission</h2>
                </div>
                <div className="px-6 py-5 space-y-3">
                  {/* Insured */}
                  <div className="rounded-lg bg-slate-50 border border-slate-200 px-4 py-3 text-sm">
                    <p className="text-slate-500 text-xs uppercase font-medium mb-1">Insured</p>
                    {createNew ? (
                      <p className="text-slate-800 font-medium">
                        New insured — <span className="text-slate-500 font-normal">{email.fromName ?? email.fromAddress}</span>
                        <span className="ml-2 text-xs text-amber-600">(placeholder, update after creation)</span>
                      </p>
                    ) : (
                      <p className="text-slate-800 font-medium">{selectedInsured?.displayName}</p>
                    )}
                  </div>

                  {/* LOB info / selector */}
                  <div className="rounded-lg bg-slate-50 border border-slate-200 px-4 py-3 text-sm space-y-2">
                    <p className="text-slate-500 text-xs uppercase font-medium">Lines of Business</p>

                    {detectedLob ? (
                      /* At least one ACORD type detected — show confirmed LOBs + optional override */
                      <>
                        <div className="flex items-center gap-2">
                          <Sparkles className="h-4 w-4 text-blue-500 shrink-0" />
                          <span className="text-blue-800 font-medium">
                            {selectedLob ? LOB_LABELS[selectedLob] : LOB_LABELS[detectedLob]}
                          </span>
                          {selectedAttachments.some(a => LOB_FROM_DOC_TYPE[a.documentType] === undefined) && (
                            <span className="text-slate-400 text-xs ml-1">
                              · AI will detect additional LOBs from other PDFs
                            </span>
                          )}
                        </div>
                        {selectedLob ? (
                          <button
                            type="button"
                            onClick={() => setSelectedLob('')}
                            className="text-xs text-slate-400 hover:text-slate-600 underline"
                          >
                            Reset to detected
                          </button>
                        ) : (
                          <button
                            type="button"
                            onClick={() => setSelectedLob(detectedLob)}
                            className="text-xs text-slate-400 hover:text-slate-600 underline"
                          >
                            Override
                          </button>
                        )}
                        {selectedLob && (
                          <select
                            value={selectedLob}
                            onChange={(e) => setSelectedLob(e.target.value as PolicyLineOfBusiness)}
                            className="w-full border border-slate-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white"
                          >
                            {ACTIVE_LOBS.map(lob => (
                              <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>
                            ))}
                          </select>
                        )}
                      </>
                    ) : (
                      /* No ACORD types — Gemini will auto-detect; user can optionally hint */
                      <>
                        <div className="flex items-center gap-2 text-slate-500">
                          <Sparkles className="h-4 w-4 shrink-0" />
                          <span>AI will detect lines of business from your PDFs</span>
                        </div>
                        <select
                          value={selectedLob}
                          onChange={(e) => setSelectedLob(e.target.value as PolicyLineOfBusiness)}
                          className="w-full border border-slate-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white"
                        >
                          <option value="">Optional hint (helps if AI can't detect)</option>
                          {ACTIVE_LOBS.map(lob => (
                            <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>
                          ))}
                        </select>
                      </>
                    )}
                  </div>

                  {/* Email subject */}
                  <div className="rounded-lg bg-slate-50 border border-slate-200 px-4 py-3 text-sm">
                    <p className="text-slate-500 text-xs uppercase font-medium mb-1">Email subject</p>
                    <p className="text-slate-800">{email.subject}</p>
                  </div>

                  {/* Selected attachments */}
                  {selectedAttachments.length > 0 && (
                    <div className="rounded-lg bg-slate-50 border border-slate-200 px-4 py-3 text-sm">
                      <p className="text-slate-500 text-xs uppercase font-medium mb-2">
                        Attachments to copy ({selectedAttachments.length})
                      </p>
                      <ul className="space-y-1">
                        {selectedAttachments.map(att => (
                          <li key={att.id} className="flex items-center gap-2 text-slate-700">
                            <Paperclip className="h-3.5 w-3.5 text-slate-400 shrink-0" />
                            <span className="truncate">{att.fileName}</span>
                            <span className="text-slate-400 shrink-0">· {DOC_TYPE_LABELS[att.documentType]}</span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                </div>
                <div className="px-6 py-4 border-t border-slate-200 flex items-center justify-end gap-3">
                  <button onClick={() => setStep('search')} className="text-sm text-slate-500 hover:text-slate-800">
                    Back
                  </button>
                  <button
                    onClick={() => createSubmission.mutate()}
                    disabled={createSubmission.isPending}
                    className="px-4 py-2 text-sm font-medium bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-60 disabled:cursor-not-allowed transition-colors"
                  >
                    {createSubmission.isPending ? 'Creating…' : 'Create Submission'}
                  </button>
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
