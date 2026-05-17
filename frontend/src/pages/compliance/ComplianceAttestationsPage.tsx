import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { ArrowLeft, CheckCircle2, Clock, FileCheck2, XCircle } from 'lucide-react'
import { toast } from 'sonner'
import { complianceDocumentsApi } from '@/api/complianceDocuments.api'
import { ATTESTATION_STATUS } from '@/constants/compliance'
import { EmptyState } from '@/components/common/EmptyState'
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
    if (filter === 'Pending') return recipient.status === ATTESTATION_STATUS.PENDING
    return recipient.status !== 'Pending'
  })

  const pendingCount = assigned.filter((item) => item.recipient.status === ATTESTATION_STATUS.PENDING).length
  const completedCount = assigned.length - pendingCount

  return (
    <div className="space-y-5 p-6" style={{ background: 'var(--surface-2)' }}>
      <PageHeader
        title="My Attestations"
        subtitle="Review and acknowledge assigned compliance document versions"
        action={
          <button
            type="button"
            onClick={() => navigate('/compliance-documentation')}
            className="sd-btn outline"
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

      <section className="sd-card">
        <div className="sd-card-head flex-wrap gap-2">
          {(['Pending', 'Completed', 'All'] as FilterKey[]).map((option) => (
            <button
              key={option}
              type="button"
              onClick={() => setFilter(option)}
              className={`sd-btn sm ${filter === option ? 'primary' : 'ghost'}`}
            >
              {option}
            </button>
          ))}
        </div>

        {campaignsQuery.isLoading ? (
          <div className="p-8"><LoadingSpinner /></div>
        ) : visible.length === 0 ? (
          <div className="p-6">
            <EmptyState icon={FileCheck2} title="No attestations" description="No attestations match this view." />
          </div>
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
  const [expanded, setExpanded] = useState(recipient.status === ATTESTATION_STATUS.PENDING)

  const submitMutation = useMutation({
    mutationFn: (status: typeof ATTESTATION_STATUS.ATTESTED | typeof ATTESTATION_STATUS.DECLINED) => complianceDocumentsApi.submitAttestation(campaign.id, {
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

  const pending = recipient.status === ATTESTATION_STATUS.PENDING

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
            className="sd-btn outline sm"
          >
            Open Document
          </button>
          <button
            type="button"
            onClick={() => setExpanded((value) => !value)}
            className="sd-btn outline sm"
          >
            {expanded ? 'Hide' : 'View'}
          </button>
        </div>
      </div>

      {expanded && (
        <div className="mt-4 rounded-lg p-4" style={{ border: '1px solid var(--line)', background: 'var(--surface-2)' }}>
          <div className="text-sm leading-6" style={{ color: 'var(--ink-2)' }}>{campaign.statement}</div>
          {pending ? (
            <div className="mt-4 space-y-3">
              <label className="block text-sm font-medium text-slate-700">
                Comment
                <textarea
                  value={comment}
                  onChange={(event) => setComment(event.target.value)}
                  rows={3}
                  className="sims-textarea mt-1"
                  placeholder="Optional"
                />
              </label>
              <div className="flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => submitMutation.mutate(ATTESTATION_STATUS.DECLINED)}
                  disabled={submitMutation.isPending}
                  className="sd-btn danger"
                >
                  <XCircle className="h-4 w-4" />
                  Decline
                </button>
                <button
                  type="button"
                  onClick={() => submitMutation.mutate(ATTESTATION_STATUS.ATTESTED)}
                  disabled={submitMutation.isPending}
                  className="sd-btn success"
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
    <div className="sd-card p-4">
      <div className="flex items-center justify-between">
        <div className="text-xs font-medium uppercase tracking-wide" style={{ color: 'var(--ink-4)' }}>{label}</div>
        <Icon className={`h-4 w-4 ${color}`} />
      </div>
      <div className="mt-2 text-xl font-semibold" style={{ color: 'var(--ink)' }}>{value.toLocaleString()}</div>
    </div>
  )
}

function StatusPill({ status }: { status: string }) {
  const styles = status === ATTESTATION_STATUS.ATTESTED
    ? 'border-green-200 bg-green-50 text-green-700'
    : status === ATTESTATION_STATUS.DECLINED
      ? 'border-red-200 bg-red-50 text-red-700'
      : 'border-amber-200 bg-amber-50 text-amber-700'

  return <span className={`rounded-full border px-2 py-0.5 text-xs font-medium ${styles}`}>{status}</span>
}
