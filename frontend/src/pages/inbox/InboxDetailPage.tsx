import { useState } from 'react'
import { useNavigate, useParams, Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, FileText, Paperclip, CheckCircle2, AlertCircle } from 'lucide-react'
import { toast } from 'sonner'
import { inboundEmailsApi } from '@/api/inboundEmails.api'
import { format } from 'date-fns'
import type { EmailAttachmentDocumentType } from '@/types/inboundEmail.types'

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

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export function InboxDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [showConfirm, setShowConfirm] = useState(false)

  const { data: email, isLoading, isError } = useQuery({
    queryKey: ['inbound-emails', id],
    queryFn: () => inboundEmailsApi.getById(id!),
    enabled: !!id,
  })

  const createSubmission = useMutation({
    mutationFn: () => inboundEmailsApi.createSubmission(id!),
    onSuccess: (submission) => {
      queryClient.invalidateQueries({ queryKey: ['inbound-emails'] })
      toast.success('Submission created successfully')
      navigate(`/submissions/${submission.id}`)
    },
    onError: () => {
      toast.error('Failed to create submission')
    },
  })

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-48 text-slate-500 text-sm">
        Loading…
      </div>
    )
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
      <div className="px-6 py-4 border-b border-slate-200 bg-white flex items-center gap-3">
        <button
          onClick={() => navigate('/inbox')}
          className="text-slate-400 hover:text-slate-700 transition-colors"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <div className="flex-1 min-w-0">
          <h1 className="text-lg font-semibold text-slate-900 truncate">{email.subject}</h1>
          <p className="text-sm text-slate-500">
            From{' '}
            <span className="font-medium text-slate-700">
              {email.fromName ?? email.fromAddress}
            </span>
            {email.fromName && (
              <span className="ml-1 text-slate-400">&lt;{email.fromAddress}&gt;</span>
            )}
            {' · '}
            {format(new Date(email.receivedAt), 'PPpp')}
          </p>
        </div>
        <div className="shrink-0">
          {email.isProcessed ? (
            <div className="flex items-center gap-1.5 text-sm text-emerald-600">
              <CheckCircle2 className="h-4 w-4" />
              <span>Processed</span>
              {email.linkedSubmissionId && (
                <Link
                  to={`/submissions/${email.linkedSubmissionId}`}
                  className="ml-2 text-blue-600 hover:underline text-xs"
                >
                  View Submission →
                </Link>
              )}
            </div>
          ) : showConfirm ? (
            <div className="flex items-center gap-2">
              <span className="text-sm text-slate-600">Create a new submission?</span>
              <button
                onClick={() => createSubmission.mutate()}
                disabled={createSubmission.isPending}
                className="px-3 py-1.5 text-sm font-medium bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-60 transition-colors"
              >
                {createSubmission.isPending ? 'Creating…' : 'Confirm'}
              </button>
              <button
                onClick={() => setShowConfirm(false)}
                className="px-3 py-1.5 text-sm font-medium text-slate-600 hover:text-slate-900 transition-colors"
              >
                Cancel
              </button>
            </div>
          ) : (
            <button
              onClick={() => setShowConfirm(true)}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium bg-blue-600 text-white rounded-md hover:bg-blue-700 transition-colors"
            >
              <FileText className="h-4 w-4" />
              Create Submission from Email
            </button>
          )}
        </div>
      </div>

      <div className="flex-1 overflow-auto p-6 space-y-6">
        {/* Body */}
        <div className="bg-white border border-slate-200 rounded-lg p-6">
          <h2 className="text-sm font-semibold text-slate-500 uppercase tracking-wide mb-3">
            Message Body
          </h2>
          {email.bodyText ? (
            <div
              className="text-sm text-slate-700 leading-relaxed whitespace-pre-wrap max-h-96 overflow-auto"
              dangerouslySetInnerHTML={
                email.bodyText.includes('<') ? { __html: email.bodyText } : undefined
              }
            >
              {!email.bodyText.includes('<') ? email.bodyText : undefined}
            </div>
          ) : (
            <p className="text-sm text-slate-400 italic">No body content</p>
          )}
        </div>

        {/* Attachments */}
        {email.attachments.length > 0 && (
          <div className="bg-white border border-slate-200 rounded-lg p-6">
            <h2 className="text-sm font-semibold text-slate-500 uppercase tracking-wide mb-3">
              Attachments ({email.attachments.length})
            </h2>
            <ul className="divide-y divide-slate-100">
              {email.attachments.map((att) => (
                <li key={att.id} className="flex items-center justify-between py-3">
                  <div className="flex items-center gap-3 min-w-0">
                    <Paperclip className="h-4 w-4 text-slate-400 shrink-0" />
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-slate-800 truncate">{att.fileName}</p>
                      <p className="text-xs text-slate-400">
                        {DOC_TYPE_LABELS[att.documentType]} · {formatBytes(att.fileSizeBytes)}
                      </p>
                    </div>
                  </div>
                  <a
                    href={att.blobUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="ml-4 shrink-0 text-xs text-blue-600 hover:underline"
                  >
                    Download
                  </a>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  )
}
