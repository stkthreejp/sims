import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Clock, Inbox, Paperclip } from 'lucide-react'
import { format } from 'date-fns'
import { inboundEmailsApi } from '@/api/inboundEmails.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { EmptyState } from '@/components/common/EmptyState'

export function InboxPage() {
  const navigate = useNavigate()
  const { data: emails = [], isLoading } = useQuery({
    queryKey: ['inbound-emails', 'unprocessed'],
    queryFn: inboundEmailsApi.getUnprocessed,
    refetchInterval: 60_000,
  })

  return (
    <div className="subs-wrap">
      <div className="subs-page-head">
        <PageHeader title="Submission Inbox" />
        {emails.length > 0 && <span className="sd-pill new">{emails.length}</span>}
      </div>

      <div className="subs-table-card">
        {isLoading ? (
          <LoadingSpinner />
        ) : emails.length === 0 ? (
          <EmptyState icon={Inbox} title="No unprocessed emails" description="New submission emails will appear here." />
        ) : (
          emails.map((email, index) => (
            <button
              key={email.id}
              type="button"
              onClick={() => navigate(`/inbox/${email.id}`)}
              style={{
                display: 'flex',
                alignItems: 'flex-start',
                justifyContent: 'space-between',
                gap: 16,
                padding: '12px 20px',
                width: '100%',
                textAlign: 'left',
                background: 'none',
                border: 'none',
                borderBottom: index < emails.length - 1 ? '1px solid var(--line-2)' : undefined,
                cursor: 'pointer',
              }}
              onMouseEnter={(e) => (e.currentTarget.style.background = 'var(--hover)')}
              onMouseLeave={(e) => (e.currentTarget.style.background = 'none')}
            >
              <div style={{ minWidth: 0, flex: 1 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <span style={{ fontWeight: 600, fontSize: 13, color: 'var(--ink)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {email.fromName || email.fromAddress}
                  </span>
                  {email.fromName && (
                    <span style={{ fontSize: 12, color: 'var(--ink-4)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      &lt;{email.fromAddress}&gt;
                    </span>
                  )}
                </div>
                <p style={{ margin: '2px 0 0', fontSize: 13, color: 'var(--ink-2)', fontWeight: 500, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {email.subject}
                </p>
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 4, flexShrink: 0 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 12, color: 'var(--ink-4)' }}>
                  <Clock style={{ width: 11, height: 11 }} />
                  {format(new Date(email.receivedAt), 'MMM d, h:mm a')}
                </div>
                {email.attachmentCount > 0 && (
                  <div style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 12, color: 'var(--ink-4)' }}>
                    <Paperclip style={{ width: 11, height: 11 }} />
                    {email.attachmentCount}
                  </div>
                )}
              </div>
            </button>
          ))
        )}
      </div>
    </div>
  )
}
