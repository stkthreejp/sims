import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { ArrowLeft, CheckCircle2, Clock, FileCheck2, XCircle } from 'lucide-react'
import { toast } from 'sonner'
import { complianceDocumentsApi } from '@/api/complianceDocuments.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { PageHeader } from '@/components/common/PageHeader'
import { formatDate, formatDateTime } from '@/lib/utils'
import { useAuthStore } from '@/store/authStore'
import type { ComplianceAttestationCampaign, ComplianceAttestationRecipient } from '@/types/compliance.types'

type FilterKey = 'Pending' | 'Completed' | 'All'

export function ComplianceAttestationsPage() {
  const navigate = useNavigate()
  const currentUser = useAuthStore((state) => state.user)
  const [filter, setFilter] = useState<FilterKey>('Pending')

  const campaignsQuery = useQuery({
    queryKey: ['compliance-documents', 'my-attestations'],
    queryFn: () => complianceDocumentsApi.getAttestationCampaigns(),
  })

  const assigned = useMemo(() => {
    return (campaignsQuery.data ?? [])
      .map((campaign) => ({
        campaign,
        recipient: campaign.recipients.find((item) => item.userId === currentUser?.id) ?? null,
      }))
      .filter((item): item is { campaign: ComplianceAttestationCampaign; recipient: ComplianceAttestationRecipient } => !!item.recipient)
  }, [campaignsQuery.data, currentUser?.id])

  const visible = assigned.filter(({ recipient }) => {
    if (filter === 'All') return true
    if (filter === 'Pending') return recipient.status === 'Pending'
    return recipient.status !== 'Pending'
  })

  const pendingCount = assigned.filter((item) => item.recipient.status === 'Pending').length
  const completedCount = assigned.length - pendingCount

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="My Attestations"
        subtitle="Review and acknowledge assigned compliance document versions"
        action={
          <button
            type="button"
            onClick={() => navigate('/compliance-documentation')}
            className="inline-flex items-center gap-2 rounded border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            <ArrowLeft className="h-4 w-4" />
            Compliance Register
          </button>
        }
      />

      <section className="grid grid-cols-1 gap-3 md:grid-cols-3">
        <Metric icon={Clock} label="Pending" value={pendingCount} tone="pending" />
        <Metric icon={CheckCircle2} label="Completed" value={completedCount} tone="complete" />
        <Metric icon={FileCheck2} label="Total Assigned" value={assigned.length} />
      </section>

      <section className="rounded border bg-white">
        <div className="flex flex-wrap items-center gap-2 border-b px-4 py-3">
          {(['Pending', 'Completed', 'All'] as FilterKey[]).map((option) => (
            <button
              key={option}
              type="button"
              onClick={() => setFilter(option)}
              className={`rounded px-3 py-1.5 text-sm font-medium ${filter === option ? 'bg-blue-50 text-blue-700' : 'text-slate-600 hover:bg-slate-50'}`}
            >
              {option}
            </button>
          ))}
        </div>

        {campaignsQuery.isLoading ? (
          <div className="p-8"><LoadingSpinner /></div>
        ) : visible.length === 0 ? (
          <div className="p-6 text-sm text-slate-500">No attestations match this view.</div>
        ) : (
          <div className="divide-y">
            {visible.map(({ campaign, recipient }) => (
              <AttestationRow
                key={campaign.id}
                campaign={campaign}
                recipient={recipient}
                onOpen={() => navigate(`/compliance-documentation/${campaign.documentId}`)}
              />
            ))}
          </div>
        )}
      </section>
    </div>
  )
}

function AttestationRow({
  campaign,
  recipient,
  onOpen,
}: {
  campaign: ComplianceAttestationCampaign
  recipient: ComplianceAttestationRecipient
  onOpen: () => void
}) {
  const qc = useQueryClient()
  const [comment, setComment] = useState('')
  const [expanded, setExpanded] = useState(recipient.status === 'Pending')

  const submitMutation = useMutation({
    mutationFn: (status: 'Attested' | 'Declined') => complianceDocumentsApi.submitAttestation(campaign.id, {
      status,
      comment: comment || null,
    }),
    onSuccess: () => {
      toast.success('Attestation recorded')
      qc.invalidateQueries({ queryKey: ['compliance-documents', 'my-attestations'] })
      qc.invalidateQueries({ queryKey: ['compliance-documents', campaign.documentId, 'attestations'] })
      qc.invalidateQueries({ queryKey: ['compliance-documents'] })
    },
    onError: () => toast.error('Could not record attestation'),
  })

  const pending = recipient.status === 'Pending'

  return (
    <article className="p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="text-sm font-semibold text-slate-900">{campaign.documentTitle}</h2>
            <StatusPill status={recipient.status} />
          </div>
          <div className="mt-1 text-xs text-slate-500">
            {campaign.name} · Version {campaign.versionNumber} · Due {formatDate(campaign.dueDate)}
          </div>
          {recipient.attestedAt && (
            <div className="mt-1 text-xs text-slate-500">Completed {formatDateTime(recipient.attestedAt)}</div>
          )}
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={onOpen}
            className="rounded border border-slate-300 px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
          >
            Open Document
          </button>
          <button
            type="button"
            onClick={() => setExpanded((value) => !value)}
            className="rounded border border-slate-300 px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
          >
            {expanded ? 'Hide' : 'View'}
          </button>
        </div>
      </div>

      {expanded && (
        <div className="mt-4 rounded border bg-slate-50 p-4">
          <div className="text-sm leading-6 text-slate-700">{campaign.statement}</div>
          {pending ? (
            <div className="mt-4 space-y-3">
              <label className="block text-sm font-medium text-slate-700">
                Comment
                <textarea
                  value={comment}
                  onChange={(event) => setComment(event.target.value)}
                  rows={3}
                  className="mt-1 w-full rounded border border-slate-300 bg-white px-3 py-2 text-sm"
                  placeholder="Optional"
                />
              </label>
              <div className="flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => submitMutation.mutate('Declined')}
                  disabled={submitMutation.isPending}
                  className="inline-flex items-center gap-1.5 rounded border border-red-200 px-3 py-2 text-sm font-medium text-red-700 hover:bg-red-50 disabled:opacity-50"
                >
                  <XCircle className="h-4 w-4" />
                  Decline
                </button>
                <button
                  type="button"
                  onClick={() => submitMutation.mutate('Attested')}
                  disabled={submitMutation.isPending}
                  className="inline-flex items-center gap-1.5 rounded bg-green-600 px-3 py-2 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50"
                >
                  <CheckCircle2 className="h-4 w-4" />
                  Attest
                </button>
              </div>
            </div>
          ) : (
            <div className="mt-3 text-sm text-slate-600">{recipient.comment || 'No comment recorded.'}</div>
          )}
        </div>
      )}
    </article>
  )
}

function Metric({ icon: Icon, label, value, tone = 'default' }: { icon: React.ElementType; label: string; value: number; tone?: 'default' | 'pending' | 'complete' }) {
  const color = tone === 'pending' ? 'text-amber-500' : tone === 'complete' ? 'text-green-500' : 'text-slate-400'
  return (
    <div className="rounded border bg-white p-4">
      <div className="flex items-center justify-between">
        <div className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</div>
        <Icon className={`h-4 w-4 ${color}`} />
      </div>
      <div className="mt-2 text-xl font-semibold text-slate-800">{value.toLocaleString()}</div>
    </div>
  )
}

function StatusPill({ status }: { status: string }) {
  const styles = status === 'Attested'
    ? 'border-green-200 bg-green-50 text-green-700'
    : status === 'Declined'
      ? 'border-red-200 bg-red-50 text-red-700'
      : 'border-amber-200 bg-amber-50 text-amber-700'

  return <span className={`rounded-full border px-2 py-0.5 text-xs font-medium ${styles}`}>{status}</span>
}
