import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, Download, Printer } from 'lucide-react'
import { complianceDocumentsApi } from '@/api/complianceDocuments.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { formatDate, formatDateTime } from '@/lib/utils'
import type { ComplianceAttestationCampaign, ComplianceAuditLog, ComplianceDocumentDetail } from '@/types/compliance.types'

export function ComplianceEvidenceReportPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()

  const documentQuery = useQuery({
    queryKey: ['compliance-documents', id],
    queryFn: () => complianceDocumentsApi.getById(id!),
    enabled: !!id,
  })

  const attestationQuery = useQuery({
    queryKey: ['compliance-documents', id, 'attestations'],
    queryFn: () => complianceDocumentsApi.getAttestationCampaigns(id),
    enabled: !!id,
  })

  const auditLogQuery = useQuery({
    queryKey: ['compliance-documents', id, 'audit-log'],
    queryFn: () => complianceDocumentsApi.getAuditLog(id!),
    enabled: !!id,
  })

  const document = documentQuery.data
  const campaigns = attestationQuery.data ?? []
  const auditLogs = auditLogQuery.data ?? []

  const reportStats = useMemo(() => {
    const recipients = campaigns.flatMap((campaign) => campaign.recipients)
    return {
      campaignCount: campaigns.length,
      recipientCount: recipients.length,
      attestedCount: recipients.filter((recipient) => recipient.status === 'Attested').length,
      pendingCount: recipients.filter((recipient) => recipient.status === 'Pending').length,
      declinedCount: recipients.filter((recipient) => recipient.status === 'Declined').length,
    }
  }, [campaigns])

  if (documentQuery.isLoading || attestationQuery.isLoading || auditLogQuery.isLoading) return <LoadingSpinner />
  if (!document) return <div className="p-6 text-sm text-slate-500">Compliance document not found.</div>

  return (
    <div className="min-h-full bg-slate-100">
      <div className="sticky top-0 z-10 flex items-center justify-between gap-3 border-b bg-white px-6 py-3 print:hidden">
        <button
          type="button"
          onClick={() => navigate(`/compliance-documentation/${document.id}`)}
          className="inline-flex items-center gap-2 rounded border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          <ArrowLeft className="h-4 w-4" />
          Document
        </button>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => downloadAttestationCsv(document, campaigns)}
            className="inline-flex items-center gap-2 rounded border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            <Download className="h-4 w-4" />
            Attestation CSV
          </button>
          <button
            type="button"
            onClick={() => downloadAuditCsv(document, auditLogs)}
            className="inline-flex items-center gap-2 rounded border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            <Download className="h-4 w-4" />
            Audit CSV
          </button>
          <button
            type="button"
            onClick={() => window.print()}
            className="inline-flex items-center gap-2 rounded bg-slate-900 px-3 py-2 text-sm font-medium text-white hover:bg-slate-800"
          >
            <Printer className="h-4 w-4" />
            Print Report
          </button>
        </div>
      </div>

      <main className="mx-auto max-w-5xl bg-white p-8 shadow-sm print:max-w-none print:p-0 print:shadow-none">
        <header className="border-b pb-6">
          <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">Compliance Evidence Report</div>
          <h1 className="mt-2 text-2xl font-semibold text-slate-950">{document.title}</h1>
          <div className="mt-2 text-sm text-slate-500">Generated {formatDateTime(new Date().toISOString())}</div>
        </header>

        <section className="grid grid-cols-1 gap-3 py-6 md:grid-cols-4">
          <Metric label="Document Status" value={document.status} />
          <Metric label="Published Version" value={document.currentPublishedVersion ? `v${document.currentPublishedVersion.versionNumber}` : 'None'} />
          <Metric label="Attested" value={reportStats.attestedCount.toLocaleString()} />
          <Metric label="Pending" value={reportStats.pendingCount.toLocaleString()} />
        </section>

        <ReportSection title="Document Metadata">
          <dl className="grid grid-cols-1 gap-3 text-sm md:grid-cols-2">
            <Meta label="Category" value={document.category} />
            <Meta label="Type" value={document.documentType} />
            <Meta label="Owner" value={document.ownerName ?? '-'} />
            <Meta label="Approver" value={document.approverName ?? '-'} />
            <Meta label="Effective Date" value={formatDate(document.effectiveDate)} />
            <Meta label="Last Reviewed" value={formatDate(document.lastReviewedDate)} />
            <Meta label="Next Review" value={formatDate(document.nextReviewDate)} />
            <Meta label="Review Cadence" value={document.reviewCadence} />
          </dl>
        </ReportSection>

        <ReportSection title="Current Published Version">
          {document.currentPublishedVersion ? (
            <div className="space-y-2 text-sm text-slate-700">
              <div>Version {document.currentPublishedVersion.versionNumber}</div>
              <div>Approved by {document.currentPublishedVersion.approvedByName ?? '-'} on {formatDateTime(document.currentPublishedVersion.approvedAt)}</div>
              {document.currentPublishedVersion.changeSummary && <div>Change summary: {document.currentPublishedVersion.changeSummary}</div>}
            </div>
          ) : (
            <EmptyText>No published version yet.</EmptyText>
          )}
        </ReportSection>

        <ReportSection title="Review History">
          {document.reviews.length === 0 ? <EmptyText>No reviews recorded.</EmptyText> : (
            <SimpleTable
              headers={['Reviewed', 'Status', 'Reviewer', 'Next Review', 'Notes']}
              rows={document.reviews.map((review) => [
                formatDateTime(review.reviewedAt),
                review.status,
                review.reviewedByName,
                formatDate(review.nextReviewDate),
                review.notes ?? '-',
              ])}
            />
          )}
        </ReportSection>

        <ReportSection title="Evidence Items">
          {document.evidenceItems.length === 0 ? <EmptyText>No evidence items recorded.</EmptyText> : (
            <SimpleTable
              headers={['Created', 'Type', 'Title', 'Description', 'URL', 'Attachments']}
              rows={document.evidenceItems.map((evidence) => [
                formatDateTime(evidence.createdAt),
                evidence.evidenceType,
                evidence.title,
                evidence.description ?? '-',
                evidence.url ?? '-',
                evidence.attachments.length === 0 ? '-' : evidence.attachments.map((attachment) => attachment.fileName).join('; '),
              ])}
            />
          )}
        </ReportSection>

        <ReportSection title="Attestation Summary">
          {campaigns.length === 0 ? <EmptyText>No attestation campaigns recorded.</EmptyText> : (
            <div className="space-y-5">
              <div className="grid grid-cols-2 gap-3 text-sm md:grid-cols-5">
                <Metric label="Campaigns" value={reportStats.campaignCount.toLocaleString()} />
                <Metric label="Recipients" value={reportStats.recipientCount.toLocaleString()} />
                <Metric label="Attested" value={reportStats.attestedCount.toLocaleString()} />
                <Metric label="Pending" value={reportStats.pendingCount.toLocaleString()} />
                <Metric label="Declined" value={reportStats.declinedCount.toLocaleString()} />
              </div>
              {campaigns.map((campaign) => (
                <div key={campaign.id}>
                  <h3 className="text-sm font-semibold text-slate-900">{campaign.name}</h3>
                  <div className="mb-2 mt-1 text-xs text-slate-500">Version {campaign.versionNumber} · Due {formatDate(campaign.dueDate)} · {campaign.status}</div>
                  <SimpleTable
                    headers={['Recipient', 'Email', 'Status', 'Completed', 'Comment']}
                    rows={campaign.recipients.map((recipient) => [
                      recipient.userName,
                      recipient.email,
                      recipient.status,
                      formatDateTime(recipient.attestedAt),
                      recipient.comment ?? '-',
                    ])}
                  />
                </div>
              ))}
            </div>
          )}
        </ReportSection>

        <ReportSection title="Audit Trail Summary">
          {auditLogs.length === 0 ? <EmptyText>No audit entries recorded.</EmptyText> : (
            <SimpleTable
              headers={['Timestamp', 'Action', 'User', 'Field', 'Old', 'New', 'Comment']}
              rows={auditLogs.slice(0, 100).map((log) => [
                formatDateTime(log.createdAt),
                cleanAction(log.action),
                log.userName,
                log.fieldName ?? '-',
                log.oldValue ?? '-',
                log.newValue ?? '-',
                log.comment ?? '-',
              ])}
            />
          )}
        </ReportSection>
      </main>
    </div>
  )
}

function ReportSection({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="border-t py-6">
      <h2 className="mb-3 text-base font-semibold text-slate-950">{title}</h2>
      {children}
    </section>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded border bg-slate-50 p-3">
      <div className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-1 text-base font-semibold text-slate-900">{value}</div>
    </div>
  )
}

function Meta({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</dt>
      <dd className="mt-1 text-slate-800">{value}</dd>
    </div>
  )
}

function SimpleTable({ headers, rows }: { headers: string[]; rows: string[][] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse text-left text-xs">
        <thead>
          <tr className="border-y bg-slate-50 text-slate-500">
            {headers.map((header) => <th key={header} className="px-2 py-2 font-semibold">{header}</th>)}
          </tr>
        </thead>
        <tbody className="divide-y">
          {rows.map((row, rowIndex) => (
            <tr key={`${row[0]}-${rowIndex}`} className="align-top">
              {row.map((cell, cellIndex) => <td key={`${cellIndex}-${cell}`} className="max-w-xs px-2 py-2 text-slate-700">{cell}</td>)}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function EmptyText({ children }: { children: React.ReactNode }) {
  return <div className="text-sm text-slate-500">{children}</div>
}

function cleanAction(value: string) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function downloadAttestationCsv(document: ComplianceDocumentDetail, campaigns: ComplianceAttestationCampaign[]) {
  const rows = campaigns.flatMap((campaign) =>
    campaign.recipients.map((recipient) => [
      document.title,
      `v${campaign.versionNumber}`,
      campaign.name,
      campaign.dueDate,
      recipient.userName,
      recipient.email,
      recipient.status,
      recipient.attestedAt ?? '',
      recipient.comment ?? '',
    ])
  )
  downloadCsv(`${safeFileName(document.title)}-attestations.csv`, ['Document', 'Version', 'Campaign', 'Due Date', 'Recipient', 'Email', 'Status', 'Completed At', 'Comment'], rows)
}

function downloadAuditCsv(document: ComplianceDocumentDetail, logs: ComplianceAuditLog[]) {
  const rows = logs.map((log) => [
    document.title,
    log.createdAt,
    cleanAction(log.action),
    log.userName,
    log.fieldName ?? '',
    log.oldValue ?? '',
    log.newValue ?? '',
    log.comment ?? '',
  ])
  downloadCsv(`${safeFileName(document.title)}-audit.csv`, ['Document', 'Timestamp', 'Action', 'User', 'Field', 'Old', 'New', 'Comment'], rows)
}

function downloadCsv(fileName: string, headers: string[], rows: string[][]) {
  const csv = [headers, ...rows].map((row) => row.map(escapeCsv).join(',')).join('\n')
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}

function escapeCsv(value: string) {
  return `"${value.replace(/"/g, '""')}"`
}

function safeFileName(value: string) {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '') || 'compliance-report'
}
