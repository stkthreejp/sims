import { useMemo, useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { format } from 'date-fns'
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  FileText,
  Paperclip,
  Search,
  Sparkles,
  User,
  UserPlus,
  X,
} from 'lucide-react'
import { toast } from 'sonner'
import { inboundEmailsApi } from '@/api/inboundEmails.api'
import type { CreateSubmissionResult } from '@/api/inboundEmails.api'
import { insuredsApi } from '@/api/insureds.api'
import { EmptyState } from '@/components/common/EmptyState'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { EmailAttachmentDocumentType } from '@/types/inboundEmail.types'
import type { InsuredListItem } from '@/types/insured.types'
import { ACTIVE_LOBS, LOB_LABELS, type PolicyLineOfBusiness } from '@/types/quote.types'

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

interface ConfirmCardProps {
  label: string
  children: ReactNode
}

function ConfirmCard({ label, children }: ConfirmCardProps) {
  return (
    <div className="sd-card">
      <div className="sd-card-body">
        <p className="sims-field-label">{label}</p>
        {children}
      </div>
    </div>
  )
}

export function InboxDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [step, setStep] = useState<Step>('idle')
  const [searchQuery, setSearchQuery] = useState('')
  const [selectedInsured, setSelectedInsured] = useState<InsuredListItem | null>(null)
  const [createNew, setCreateNew] = useState(false)
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

  const selectedAttachments = useMemo(
    () => (email?.attachments ?? []).filter((attachment) => !deselectedIds.has(attachment.id)),
    [email, deselectedIds]
  )

  const detectedLob = useMemo<PolicyLineOfBusiness | null>(() => {
    for (const attachment of selectedAttachments) {
      const lob = LOB_FROM_DOC_TYPE[attachment.documentType]
      if (lob) return lob
    }
    return null
  }, [selectedAttachments])

  const effectiveLob: PolicyLineOfBusiness | '' = selectedLob || detectedLob || ''

  const toggleAttachment = (attachmentId: string) => {
    setDeselectedIds((prev) => {
      const next = new Set(prev)
      if (next.has(attachmentId)) next.delete(attachmentId)
      else next.add(attachmentId)
      return next
    })
  }

  const createSubmission = useMutation({
    mutationFn: () =>
      inboundEmailsApi.createSubmission(
        id!,
        !createNew && selectedInsured ? selectedInsured.id : undefined,
        selectedAttachments.map((attachment) => attachment.id),
        effectiveLob || undefined
      ),
    onSuccess: (result: CreateSubmissionResult) => {
      queryClient.invalidateQueries({ queryKey: ['inbound-emails'] })
      if (result.extractionStatus === 'Completed') {
        toast.success('Submission created - data pre-filled from attachments')
      } else if (result.extractionStatus === 'DetectionFailed') {
        toast.warning('Submission created - LOB could not be detected. Review and set lines of business on the submission page.')
      } else if (result.extractionStatus === 'Failed') {
        toast.warning('Submission created - AI extraction failed. Fill in manually or re-run from the submission page.')
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
    return <LoadingSpinner />
  }

  if (isError || !email) {
    return (
      <EmptyState
        icon={AlertCircle}
        title="Email not found"
        description="The selected inbox message is no longer available."
        action={
          <button type="button" onClick={() => navigate('/inbox')} className="sd-btn outline sm">
            Back to inbox
          </button>
        }
      />
    )
  }

  return (
    <div className="flex h-full flex-col" style={{ background: 'var(--bg)' }}>
      <div className="flex items-start gap-3 px-7 py-5" style={{ borderBottom: '1px solid var(--line)', background: 'var(--surface)' }}>
        <button type="button" onClick={() => navigate('/inbox')} className="sims-icon-btn mt-0.5" aria-label="Back to inbox">
          <ArrowLeft className="h-4 w-4" />
        </button>

        <div className="min-w-0 flex-1">
          <h1 className="truncate" style={{ margin: 0, color: 'var(--ink)', fontSize: 'var(--fs-xl)', fontWeight: 600 }}>
            {email.subject}
          </h1>
          <p style={{ margin: '4px 0 0', color: 'var(--ink-3)', fontSize: 'var(--fs-body)' }}>
            From <span style={{ color: 'var(--ink-2)', fontWeight: 600 }}>{email.fromName ?? email.fromAddress}</span>
            {email.fromName && <span style={{ color: 'var(--ink-4)' }}> &lt;{email.fromAddress}&gt;</span>}
            <span style={{ color: 'var(--ink-4)' }}> - </span>
            {format(new Date(email.receivedAt), 'PPpp')}
          </p>
        </div>

        <div className="shrink-0">
          {email.isProcessed ? (
            <div className="flex items-center gap-2">
              <span className="sd-pill bound">
                <CheckCircle2 className="h-3.5 w-3.5" />
                Processed
              </span>
              {email.linkedSubmissionId && (
                <Link to={`/submissions/${email.linkedSubmissionId}`} className="sd-btn outline sm">
                  View Submission
                </Link>
              )}
            </div>
          ) : (
            <button
              type="button"
              onClick={openSearch}
              disabled={selectedAttachments.length === 0 && email.attachments.length > 0}
              className="sd-btn primary"
            >
              <FileText className="h-4 w-4" />
              Create Submission from Email
            </button>
          )}
        </div>
      </div>

      <div className="flex-1 overflow-auto p-7">
        <div className="grid gap-5">
          <div className="sd-card">
            <div className="sd-card-head">
              <h3>Message Body</h3>
            </div>
            <div className="sd-card-body">
              {email.bodyText ? (
                <div
                  className="max-h-96 overflow-auto whitespace-pre-wrap leading-relaxed"
                  style={{ color: 'var(--ink-2)', fontSize: 'var(--fs-body)' }}
                >
                  {email.bodyText.replace(/<[^>]+>/g, '')}
                </div>
              ) : (
                <EmptyState
                  icon={FileText}
                  title="No body content"
                  description="This email did not include readable body text."
                />
              )}
            </div>
          </div>

          {email.attachments.length > 0 && (
            <div className="sd-card">
              <div className="sd-card-head">
                <h3>
                  <Paperclip className="h-4 w-4" style={{ color: 'var(--ink-3)' }} />
                  Attachments <span className="cnt">{email.attachments.length}</span>
                </h3>
                {!email.isProcessed && (
                  <p style={{ margin: 0, color: 'var(--ink-4)', fontSize: 'var(--fs-sm)' }}>
                    Uncheck logos or irrelevant files before creating a submission
                  </p>
                )}
              </div>

              <ul className="m-0 list-none p-0">
                {email.attachments.map((attachment, index) => {
                  const checked = !deselectedIds.has(attachment.id)
                  return (
                    <li
                      key={attachment.id}
                      className={`flex items-center justify-between gap-4 px-4 py-3 ${!checked ? 'opacity-50' : ''}`}
                      style={index === email.attachments.length - 1 ? undefined : { borderBottom: '1px solid var(--line-2)' }}
                    >
                      <div className="flex min-w-0 items-center gap-3">
                        {!email.isProcessed ? (
                          <input
                            type="checkbox"
                            checked={checked}
                            onChange={() => toggleAttachment(attachment.id)}
                            className="h-4 w-4 shrink-0 cursor-pointer rounded"
                            style={{ accentColor: 'var(--accent)' }}
                          />
                        ) : (
                          <Paperclip className="h-4 w-4 shrink-0" style={{ color: 'var(--ink-4)' }} />
                        )}
                        <div className="min-w-0">
                          <p className="truncate" style={{ margin: 0, color: 'var(--ink)', fontSize: 'var(--fs-body)', fontWeight: 600 }}>
                            {attachment.fileName}
                          </p>
                          <p style={{ margin: '2px 0 0', color: 'var(--ink-4)', fontSize: 'var(--fs-sm)' }}>
                            {DOC_TYPE_LABELS[attachment.documentType]} - {formatBytes(attachment.fileSizeBytes)}
                          </p>
                        </div>
                      </div>
                      <a href={attachment.blobUrl} target="_blank" rel="noopener noreferrer" className="sd-btn outline sm shrink-0">
                        Download
                      </a>
                    </li>
                  )
                })}
              </ul>

              {!email.isProcessed && deselectedIds.size > 0 && (
                <p className="px-4 py-3" style={{ margin: 0, color: 'var(--ink-4)', fontSize: 'var(--fs-sm)' }}>
                  {selectedAttachments.length} of {email.attachments.length} attachments selected
                </p>
              )}
            </div>
          )}
        </div>
      </div>

      {step !== 'idle' && (
        <div className="sims-modal-backdrop">
          <div className="sims-modal max-w-lg">
            {step === 'search' && (
              <>
                <div className="sims-modal-head">
                  <div>
                    <h2 className="sims-modal-title">Find or create an insured</h2>
                    <p style={{ margin: '3px 0 0', color: 'var(--ink-3)', fontSize: 'var(--fs-body)' }}>
                      Search for an existing insured or create a new one from the sender info.
                    </p>
                  </div>
                  <button type="button" onClick={() => setStep('idle')} className="sims-icon-btn" aria-label="Close">
                    <X className="h-4 w-4" />
                  </button>
                </div>

                <div className="sims-modal-body space-y-4">
                  <div className="relative">
                    <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2" style={{ color: 'var(--ink-4)' }} />
                    <input
                      autoFocus
                      type="text"
                      placeholder="Search by name or company..."
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      className="sims-input w-full pl-9"
                    />
                  </div>

                  <div className="min-h-[160px] max-h-64 overflow-auto rounded-md" style={{ border: '1px solid var(--line)' }}>
                    {searchQuery.trim().length < 2 ? (
                      <div className="flex h-20 items-center justify-center" style={{ color: 'var(--ink-4)', fontSize: 'var(--fs-body)' }}>
                        Type at least 2 characters to search
                      </div>
                    ) : searchingInsureds ? (
                      <div className="flex h-20 items-center justify-center" style={{ color: 'var(--ink-4)', fontSize: 'var(--fs-body)' }}>
                        Searching...
                      </div>
                    ) : (insuredResults?.items ?? []).length === 0 ? (
                      <div className="flex h-20 items-center justify-center" style={{ color: 'var(--ink-4)', fontSize: 'var(--fs-body)' }}>
                        No insureds found
                      </div>
                    ) : (
                      (insuredResults?.items ?? []).map((insured) => (
                        <button
                          type="button"
                          key={insured.id}
                          onClick={() => {
                            setSelectedInsured(insured)
                            setCreateNew(false)
                            setStep('confirm')
                          }}
                          className="flex w-full items-center gap-3 px-4 py-3 text-left transition-colors hover:bg-[var(--hover)]"
                          style={{ borderBottom: '1px solid var(--line-2)', color: 'var(--ink-2)' }}
                        >
                          <User className="h-4 w-4 shrink-0" style={{ color: 'var(--ink-4)' }} />
                          <div>
                            <p style={{ margin: 0, color: 'var(--ink)', fontSize: 'var(--fs-body)', fontWeight: 600 }}>{insured.displayName}</p>
                            <p style={{ margin: '2px 0 0', color: 'var(--ink-4)', fontSize: 'var(--fs-sm)' }}>
                              {insured.email ?? 'No email'} - {insured.city}, {insured.state}
                            </p>
                          </div>
                        </button>
                      ))
                    )}
                  </div>
                </div>

                <div className="sims-modal-foot justify-between">
                  <button
                    type="button"
                    onClick={() => {
                      setCreateNew(true)
                      setStep('confirm')
                    }}
                    className="sd-btn outline sm"
                  >
                    <UserPlus className="h-4 w-4" />
                    Create new insured from sender
                  </button>
                  <button type="button" onClick={() => setStep('idle')} className="sd-btn ghost sm">
                    Cancel
                  </button>
                </div>
              </>
            )}

            {step === 'confirm' && (
              <>
                <div className="sims-modal-head">
                  <h2 className="sims-modal-title">Confirm submission</h2>
                  <button type="button" onClick={() => setStep('idle')} className="sims-icon-btn" aria-label="Close">
                    <X className="h-4 w-4" />
                  </button>
                </div>

                <div className="sims-modal-body space-y-3">
                  <ConfirmCard label="Insured">
                    {createNew ? (
                      <p style={{ margin: 0, color: 'var(--ink)', fontSize: 'var(--fs-body)', fontWeight: 600 }}>
                        New insured - <span style={{ color: 'var(--ink-3)', fontWeight: 400 }}>{email.fromName ?? email.fromAddress}</span>
                      </p>
                    ) : (
                      <p style={{ margin: 0, color: 'var(--ink)', fontSize: 'var(--fs-body)', fontWeight: 600 }}>{selectedInsured?.displayName}</p>
                    )}
                  </ConfirmCard>

                  <ConfirmCard label="Lines of Business">
                    {detectedLob ? (
                      <div className="space-y-2">
                        <div className="flex items-center gap-2">
                          <Sparkles className="h-4 w-4 shrink-0" style={{ color: 'var(--accent)' }} />
                          <span style={{ color: 'var(--accent-ink)', fontSize: 'var(--fs-body)', fontWeight: 600 }}>
                            {selectedLob ? LOB_LABELS[selectedLob] : LOB_LABELS[detectedLob]}
                          </span>
                          {selectedAttachments.some((attachment) => LOB_FROM_DOC_TYPE[attachment.documentType] === undefined) && (
                            <span style={{ color: 'var(--ink-4)', fontSize: 'var(--fs-sm)' }}>
                              - AI will detect additional LOBs from other PDFs
                            </span>
                          )}
                        </div>

                        {selectedLob ? (
                          <button type="button" onClick={() => setSelectedLob('')} className="sd-btn ghost sm">
                            Reset to detected
                          </button>
                        ) : (
                          <button type="button" onClick={() => setSelectedLob(detectedLob)} className="sd-btn ghost sm">
                            Override
                          </button>
                        )}

                        {selectedLob && (
                          <select
                            value={selectedLob}
                            onChange={(event) => setSelectedLob(event.target.value as PolicyLineOfBusiness)}
                            className="sims-select w-full"
                          >
                            {ACTIVE_LOBS.map((lob) => (
                              <option key={lob} value={lob}>
                                {LOB_LABELS[lob]}
                              </option>
                            ))}
                          </select>
                        )}
                      </div>
                    ) : (
                      <div className="space-y-2">
                        <div className="flex items-center gap-2" style={{ color: 'var(--ink-3)', fontSize: 'var(--fs-body)' }}>
                          <Sparkles className="h-4 w-4 shrink-0" />
                          <span>AI will detect lines of business from your PDFs</span>
                        </div>
                        <select
                          value={selectedLob}
                          onChange={(event) => setSelectedLob(event.target.value as PolicyLineOfBusiness)}
                          className="sims-select w-full"
                        >
                          <option value="">Optional hint (helps if AI cannot detect)</option>
                          {ACTIVE_LOBS.map((lob) => (
                            <option key={lob} value={lob}>
                              {LOB_LABELS[lob]}
                            </option>
                          ))}
                        </select>
                      </div>
                    )}
                  </ConfirmCard>

                  <ConfirmCard label="Email subject">
                    <p style={{ margin: 0, color: 'var(--ink)', fontSize: 'var(--fs-body)' }}>{email.subject}</p>
                  </ConfirmCard>

                  {selectedAttachments.length > 0 && (
                    <ConfirmCard label={`Attachments to copy (${selectedAttachments.length})`}>
                      <ul className="m-0 list-none space-y-1 p-0">
                        {selectedAttachments.map((attachment) => (
                          <li key={attachment.id} className="flex items-center gap-2" style={{ color: 'var(--ink-2)', fontSize: 'var(--fs-body)' }}>
                            <Paperclip className="h-3.5 w-3.5 shrink-0" style={{ color: 'var(--ink-4)' }} />
                            <span className="truncate">{attachment.fileName}</span>
                            <span className="shrink-0" style={{ color: 'var(--ink-4)' }}>
                              - {DOC_TYPE_LABELS[attachment.documentType]}
                            </span>
                          </li>
                        ))}
                      </ul>
                    </ConfirmCard>
                  )}
                </div>

                <div className="sims-modal-foot">
                  <button type="button" onClick={() => setStep('search')} className="sd-btn outline sm">
                    Back
                  </button>
                  <button
                    type="button"
                    onClick={() => createSubmission.mutate()}
                    disabled={createSubmission.isPending}
                    className="sd-btn primary sm"
                  >
                    {createSubmission.isPending ? 'Creating...' : 'Create Submission'}
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
