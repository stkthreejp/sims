import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  X, User, Clock, CheckCircle, AlertTriangle,
  Plus, CornerDownRight, ArrowUpDown, CheckCheck, XCircle,
  AlertOctagon, Bell, Clock3, FileText, Sparkles, ExternalLink,
} from 'lucide-react'
import { toast } from 'sonner'
import { tasksApi } from '@/api/tasks.api'
import { usersApi } from '@/api/users.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { TaskInstanceStatus, TaskAuditAction, TaskEntityType } from '@/types/task.types'

const STATUS_OPTIONS: { value: TaskInstanceStatus; label: string }[] = [
  { value: 'Open',       label: 'Open' },
  { value: 'InProgress', label: 'In Progress' },
  { value: 'Blocked',    label: 'Blocked' },
  { value: 'Closed',     label: 'Closed' },
  { value: 'Cancelled',  label: 'Cancelled' },
]

const ACTION_CONFIG: Partial<Record<TaskAuditAction, { icon: React.ElementType; label: string }>> = {
  Created:         { icon: Plus,           label: 'Created' },
  Assigned:        { icon: Bell,           label: 'Assignment email sent' },
  Reassigned:      { icon: CornerDownRight, label: 'Reassigned' },
  StatusChanged:   { icon: ArrowUpDown,    label: 'Status changed' },
  Completed:       { icon: CheckCheck,     label: 'Completed' },
  Cancelled:       { icon: XCircle,        label: 'Cancelled' },
  Escalated:       { icon: AlertOctagon,   label: 'Escalated' },
  ReminderSent:    { icon: Bell,           label: 'Reminder sent' },
  OverdueNotified: { icon: Clock3,         label: 'Overdue alert sent' },
  DigestSent:      { icon: FileText,       label: 'Digest sent' },
  Note:            { icon: Sparkles,       label: 'Note' },
}

// PolicyTransaction has no standalone page (shown as plain text below), so it
// is intentionally absent — callers must null-check.
function entityUrl(entityType: TaskEntityType, entityId: string): string | null {
  switch (entityType) {
    case 'Submission':         return `/submissions/${entityId}`
    case 'Policy':             return `/policies/${entityId}`
    case 'ComplianceDocument': return `/compliance-documentation/${entityId}`
    case 'Account':            return `/insureds/${entityId}`
    default:                   return null
  }
}

interface Props {
  taskId: string
  onClose: () => void
  onUpdated: () => void
}

export function TaskDetailDrawer({ taskId, onClose, onUpdated }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const qc = useQueryClient()
  const [notes, setNotes] = useState('')
  const [reassignOpen, setReassignOpen] = useState(false)
  const [reassignUserId, setReassignUserId] = useState('')

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return
    dialog.showModal()
    return () => { if (dialog.open) dialog.close() }
  }, [])

  const { data: task, isLoading } = useQuery({
    queryKey: ['task', taskId],
    queryFn: () => tasksApi.getById(taskId),
  })

  const { data: usersData } = useQuery({
    queryKey: ['users', { pageSize: 100 }],
    queryFn: () => usersApi.getAll({ pageSize: 100 }),
    enabled: reassignOpen,
  })
  const users = usersData?.items ?? []

  const { mutate: updateStatus, isPending: updatingStatus } = useMutation({
    mutationFn: ({ status }: { status: TaskInstanceStatus }) =>
      tasksApi.updateStatus(taskId, status, notes || undefined),
    onSuccess: () => {
      toast.success('Task updated')
      qc.invalidateQueries({ queryKey: ['task', taskId] })
      onUpdated()
      setNotes('')
    },
    onError: () => toast.error('Failed to update task'),
  })

  const { mutate: reassign, isPending: reassigning } = useMutation({
    mutationFn: (newUserId: string) => tasksApi.reassign(taskId, newUserId),
    onSuccess: () => {
      toast.success('Task reassigned')
      qc.invalidateQueries({ queryKey: ['task', taskId] })
      onUpdated()
      setReassignOpen(false)
      setReassignUserId('')
    },
    onError: () => toast.error('Failed to reassign task'),
  })

  return (
    <dialog
      ref={dialogRef}
      onCancel={(e) => { e.preventDefault(); onClose() }}
      onClick={(e) => { if (e.target === e.currentTarget) onClose() }}
      className="sims-drawer flex flex-col p-0"
      style={{
        width: 460,
      }}
    >
      {/* Header */}
      <div className="sims-modal-head shrink-0">
        <h2 className="sims-modal-title">
          {isLoading ? 'Loading...' : task?.taskTypeName}
        </h2>
        <button onClick={onClose} className="sims-icon-btn" aria-label="Close">
          <X size={16} strokeWidth={1.7} />
        </button>
      </div>

      {isLoading ? (
        <div className="flex-1 flex items-center justify-center"><LoadingSpinner /></div>
      ) : !task ? (
        <div className="flex-1 flex items-center justify-center" style={{ color: 'var(--ink-3)', fontSize: 'var(--fs-body)' }}>Task not found.</div>
      ) : (
        <div className="flex-1 overflow-y-auto px-5 py-4 space-y-5">
          {/* Meta */}
          <div className="grid grid-cols-2 gap-3 text-sm">
            <MetaRow icon={<User className="h-3.5 w-3.5" />} label="Assigned to" value={task.assignedUserName ?? '(unassigned)'} />
            <MetaRow icon={<Clock className="h-3.5 w-3.5" />} label="Due" value={new Date(task.dueDate).toLocaleDateString()} accent={task.isOverdue} />
            <MetaRow icon={<CheckCircle className="h-3.5 w-3.5" />} label="Status" value={task.status} />
            <MetaRow icon={<AlertTriangle className="h-3.5 w-3.5" />} label="Priority" value={task.priority} />
            {task.escalationLevel > 0 && (
              <MetaRow icon={<AlertTriangle size={14} />} label="Escalation" value={`Level ${task.escalationLevel}`} accent />
            )}
            {task.entityType && (
              <MetaRow
                icon={<></>}
                label="Entity"
                value={task.entityType}
                href={entityUrl(task.entityType, task.entityId) ?? undefined}
              />
            )}
            {task.policyTransactionNumber && (
              <MetaRow icon={<FileText className="h-3.5 w-3.5" />} label="Transaction" value={`${task.policyTransactionNumber} ${task.policyTransactionType ?? ''}`.trim()} />
            )}
            {task.policyTransactionStatus && (
              <MetaRow icon={<CheckCircle className="h-3.5 w-3.5" />} label="Txn status" value={task.policyTransactionStatus} />
            )}
          </div>

          {/* Reassign */}
          {task.status !== 'Closed' && task.status !== 'Cancelled' && (
            <div className="space-y-2">
              {!reassignOpen ? (
                <button
                  onClick={() => { setReassignOpen(true); setReassignUserId(task.assignedUserId ?? '') }}
                  className="sd-btn ghost sm inline-flex items-center gap-1.5"
                >
                  <CornerDownRight size={14} /> Reassign
                </button>
              ) : (
                <>
                  <p className="sims-field-label">Reassign to</p>
                  <select
                    value={reassignUserId}
                    onChange={(e) => setReassignUserId(e.target.value)}
                    className="sims-select"
                  >
                    <option value="">— Select user —</option>
                    {users.map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
                  </select>
                  <div className="flex gap-2">
                    <button
                      disabled={reassigning || !reassignUserId || reassignUserId === task.assignedUserId}
                      onClick={() => reassign(reassignUserId)}
                      className="sd-btn primary sm"
                    >
                      Confirm reassign
                    </button>
                    <button
                      disabled={reassigning}
                      onClick={() => { setReassignOpen(false); setReassignUserId('') }}
                      className="sd-btn ghost sm"
                    >
                      Cancel
                    </button>
                  </div>
                </>
              )}
            </div>
          )}

          {/* Status update */}
          {task.status !== 'Closed' && task.status !== 'Cancelled' && (
            <div className="space-y-2">
              <p className="sims-field-label">Update Status</p>
              <div className="flex flex-wrap gap-2">
                {STATUS_OPTIONS.filter((o) => o.value !== task.status && o.value !== 'Cancelled').map((opt) => (
                  <button
                    key={opt.value}
                    disabled={updatingStatus}
                    onClick={() => updateStatus({ status: opt.value })}
                    className="sd-btn outline sm"
                  >
                    {opt.label}
                  </button>
                ))}
              </div>
              <textarea
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder="Optional notes..."
                rows={2}
                className="sims-textarea"
              />
              <button
                disabled={updatingStatus || !notes.trim()}
                onClick={() => updateStatus({ status: task.status })}
                className="sd-btn outline sm inline-flex items-center gap-1.5"
              >
                <Sparkles size={14} /> Add note
              </button>
            </div>
          )}

          {/* Audit log */}
          <div className="space-y-2">
            <p className="sims-field-label">Activity</p>
            {task.auditEntries.length === 0 ? (
              <p style={{ margin: 0, color: 'var(--ink-3)', fontSize: 'var(--fs-body)' }}>No activity yet.</p>
            ) : (
              <ol className="relative ml-3 space-y-4" style={{ borderLeft: '1px solid var(--line)' }}>
                {task.auditEntries.map((entry) => {
                  const config = ACTION_CONFIG[entry.action]
                  const ActionIcon = config?.icon
                  return (
                    <li key={entry.id} className="ml-4">
                      <span className="absolute -left-1.5 mt-1.5 h-2.5 w-2.5 rounded-full" style={{ border: '1px solid var(--surface)', background: 'var(--ink-4)' }} />
                      <p style={{ margin: 0, color: 'var(--ink-3)', fontSize: 'var(--fs-sm)' }}>{new Date(entry.timestamp).toLocaleString()}</p>
                      <p className="flex items-center gap-1.5" style={{ margin: 0, color: 'var(--ink-2)', fontSize: 'var(--fs-body)', fontWeight: 500 }}>
                        {ActionIcon && <ActionIcon size={14} className="shrink-0" style={{ color: 'var(--ink-3)' }} />}
                        {config?.label ?? entry.action}
                        {entry.userName && <span style={{ color: 'var(--ink-3)', fontWeight: 400 }}> - {entry.userName}</span>}
                      </p>
                      {entry.oldValue && entry.newValue && (
                        <p style={{ margin: 0, color: 'var(--ink-4)', fontSize: 'var(--fs-sm)' }}>{entry.oldValue} to {entry.newValue}</p>
                      )}
                      {entry.notes && <p style={{ margin: 0, color: 'var(--ink-3)', fontSize: 'var(--fs-sm)', fontStyle: 'italic' }}>{entry.notes}</p>}
                    </li>
                  )
                })}
              </ol>
            )}
          </div>
        </div>
      )}
    </dialog>
  )
}

function MetaRow({ icon, label, value, accent = false, href }: { icon: React.ReactNode; label: string; value: string; accent?: boolean; href?: string }) {
  return (
    <div className="flex items-start gap-1.5">
      <span className="mt-0.5 shrink-0" style={{ color: accent ? 'var(--bad-fg)' : 'var(--ink-3)' }}>{icon}</span>
      <div>
        <p style={{ margin: 0, color: 'var(--ink-4)', fontSize: 'var(--fs-xs)' }}>{label}</p>
        {href ? (
          <Link to={href} className="flex items-center gap-1" style={{ margin: 0, color: 'var(--accent)', fontSize: 'var(--fs-body)', fontWeight: 600 }}>
            {value} <ExternalLink style={{ width: 11, height: 11 }} />
          </Link>
        ) : (
          <p style={{ margin: 0, color: accent ? 'var(--bad-fg)' : 'var(--ink-2)', fontSize: 'var(--fs-body)', fontWeight: 600 }}>{value}</p>
        )}
      </div>
    </div>
  )
}
