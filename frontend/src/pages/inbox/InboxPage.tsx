import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Inbox, Paperclip, Clock } from 'lucide-react'
import { inboundEmailsApi } from '@/api/inboundEmails.api'
import { format } from 'date-fns'

export function InboxPage() {
  const navigate = useNavigate()
  const { data: emails = [], isLoading } = useQuery({
    queryKey: ['inbound-emails', 'unprocessed'],
    queryFn: inboundEmailsApi.getUnprocessed,
    refetchInterval: 60_000,
  })

  return (
    <div className="flex flex-col h-full">
      <div className="px-6 py-4 border-b border-slate-200 bg-white">
        <div className="flex items-center gap-2">
          <Inbox className="h-5 w-5 text-slate-500" />
          <h1 className="text-xl font-semibold text-slate-900">Submission Inbox</h1>
          {emails.length > 0 && (
            <span className="ml-2 inline-flex items-center rounded-full bg-blue-100 px-2.5 py-0.5 text-xs font-medium text-blue-700">
              {emails.length}
            </span>
          )}
        </div>
        <p className="mt-1 text-sm text-slate-500">
          Unprocessed emails from the submissions mailbox
        </p>
      </div>

      <div className="flex-1 overflow-auto">
        {isLoading ? (
          <div className="flex items-center justify-center h-48 text-slate-500 text-sm">
            Loading emails…
          </div>
        ) : emails.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-48 gap-2 text-slate-500">
            <Inbox className="h-10 w-10 text-slate-300" />
            <p className="text-sm">No unprocessed emails</p>
          </div>
        ) : (
          <ul className="divide-y divide-slate-100">
            {emails.map((email) => (
              <li key={email.id}>
                <button
                  onClick={() => navigate(`/inbox/${email.id}`)}
                  className="w-full text-left px-6 py-4 hover:bg-slate-50 transition-colors"
                >
                  <div className="flex items-start justify-between gap-4">
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2">
                        <span className="font-medium text-sm text-slate-900 truncate">
                          {email.fromName || email.fromAddress}
                        </span>
                        {email.fromName && (
                          <span className="text-xs text-slate-400 truncate hidden sm:block">
                            &lt;{email.fromAddress}&gt;
                          </span>
                        )}
                      </div>
                      <p className="mt-0.5 text-sm text-slate-700 font-medium truncate">
                        {email.subject}
                      </p>
                    </div>
                    <div className="flex flex-col items-end gap-1 shrink-0">
                      <div className="flex items-center gap-1 text-xs text-slate-400">
                        <Clock className="h-3 w-3" />
                        {format(new Date(email.receivedAt), 'MMM d, h:mm a')}
                      </div>
                      {email.attachmentCount > 0 && (
                        <div className="flex items-center gap-1 text-xs text-slate-400">
                          <Paperclip className="h-3 w-3" />
                          {email.attachmentCount}
                        </div>
                      )}
                    </div>
                  </div>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
