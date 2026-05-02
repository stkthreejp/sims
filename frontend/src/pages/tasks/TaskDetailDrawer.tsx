import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { X, User, Clock, CheckCircle, AlertTriangle } from 'lucide-react'
import { toast } from 'sonner'
import { tasksApi } from '@/api/tasks.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import type { TaskInstanceStatus, TaskAuditAction } from '@/types/task.types'

const STATUS_OPTIONS: { value: TaskInstanceStatus; label: string }[] = [
  { value: 'Open',       label: 'Open' },
  { value: 'InProgress', label: 'In Progress' },
  { value: 'Blocked',    label: 'Blocked' },
  { value: 'Closed',     label: 'Closed' },
  { value: 'Cancelled',  label: 'Cancelled' },
]

const ACTION_LABELS: Partial<Record<TaskAuditAction, string>> = {
  Created:         '✦ Created',
  Assigned:        '📧 Assignment email sent',
  Reassigned:      '↪ Reassigned',
  StatusChanged:   '↕ Status changed',
  Completed:       '✓ Completed',
  Cancelled:       '✗ Cancelled',
  Escalated:       '⚠ Escalated',
  ReminderSent:    '🔔 Reminder sent',
  OverdueNotified: '⏰ Overdue alert sent',
  DigestSent:      '📋 Digest sent',
  Note:            '📝 Note',
}

interface Props {
  taskId: string
  onClose: () => void
  onUpdated: () => void
}

export function TaskDetailDrawer({ taskId, onClose, onUpdated }: Props) {
  const qc = useQueryClient()
  const [notes, setNotes] = useState('')

  const { data: task, isLoading } = useQuery({
    queryKey: ['task', taskId],
    queryFn: () => tasksApi.getById(taskId),
  })

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

  return (
    <>
      {/* Backdrop */}
      <div className="fixed inset-0 bg-black/20 z-40" onClick={onClose} />

      {/* Drawer */}
      <div
        className="fixed right-0 top-0 h-full z-50 flex flex-col bg-white shadow-xl"
        style={{ width: 460, borderLeft: '1px solid var(--line)' }}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b">
          <h2 className="font-semibold text-slate-800 text-base">
            {isLoading ? 'Loading…' : task?.taskTypeName}
          </h2>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-600">
            <X className="h-5 w-5" />
          </button>
        </div>

        {isLoading ? (
          <div className="flex-1 flex items-center justify-center"><LoadingSpinner /></div>
        ) : !task ? (
          <div className="flex-1 flex items-center justify-center text-slate-500 text-sm">Task not found.</div>
        ) : (
          <div className="flex-1 overflow-y-auto px-5 py-4 space-y-5">
            {/* Meta */}
            <div className="grid grid-cols-2 gap-3 text-sm">
              <MetaRow icon={<User className="h-3.5 w-3.5" />} label="Assigned to" value={task.assignedUserName ?? '(unassigned)'} />
              <MetaRow icon={<Clock className="h-3.5 w-3.5" />} label="Due" value={new Date(task.dueDate).toLocaleDateString()} accent={task.isOverdue} />
              <MetaRow icon={<CheckCircle className="h-3.5 w-3.5" />} label="Status" value={task.status} />
              <MetaRow icon={<AlertTriangle className="h-3.5 w-3.5" />} label="Priority" value={task.priority} />
              {task.escalationLevel > 0 && (
                <MetaRow icon={<AlertTriangle className="h-3.5 w-3.5 text-orange-500" />} label="Escalation" value={`Level ${task.escalationLevel}`} accent />
              )}
              {task.entityType && (
                <MetaRow icon={<></>} label="Entity" value={`${task.entityType}`} />
              )}
            </div>

            {/* Status update */}
            {task.status !== 'Closed' && task.status !== 'Cancelled' && (
              <div className="space-y-2">
                <p className="text-xs font-semibold text-slate-500 uppercase tracking-wide">Update Status</p>
                <div className="flex flex-wrap gap-2">
                  {STATUS_OPTIONS.filter((o) => o.value !== task.status && o.value !== 'Cancelled').map((opt) => (
                    <button
                      key={opt.value}
                      disabled={updatingStatus}
                      onClick={() => updateStatus({ status: opt.value })}
                      className="px-3 py-1 rounded-lg border text-xs font-medium hover:bg-slate-50 disabled:opacity-50"
                    >
                      {opt.label}
                    </button>
                  ))}
                </div>
                <textarea
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  placeholder="Optional notes…"
                  rows={2}
                  className="w-full border rounded-lg px-3 py-2 text-sm resize-none"
                />
              </div>
            )}

            {/* Audit log */}
            <div className="space-y-2">
              <p className="text-xs font-semibold text-slate-500 uppercase tracking-wide">Activity</p>
              {task.auditEntries.length === 0 ? (
                <p className="text-sm text-slate-400">No activity yet.</p>
              ) : (
                <ol className="relative border-l border-slate-200 ml-3 space-y-4">
                  {task.auditEntries.map((entry) => (
                    <li key={entry.id} className="ml-4">
                      <span className="absolute -left-1.5 mt-1.5 h-2.5 w-2.5 rounded-full border border-white bg-slate-300" />
                      <p className="text-xs text-slate-500">{new Date(entry.timestamp).toLocaleString()}</p>
                      <p className="text-sm font-medium text-slate-700">
                        {ACTION_LABELS[entry.action] ?? entry.action}
                        {entry.userName && <span className="font-normal text-slate-500"> — {entry.userName}</span>}
                      </p>
                      {entry.oldValue && entry.newValue && (
                        <p className="text-xs text-slate-400">{entry.oldValue} → {entry.newValue}</p>
                      )}
                      {entry.notes && <p className="text-xs text-slate-500 italic">{entry.notes}</p>}
                    </li>
                  ))}
                </ol>
              )}
            </div>
          </div>
        )}
      </div>
    </>
  )
}

function MetaRow({ icon, label, value, accent = false }: { icon: React.ReactNode; label: string; value: string; accent?: boolean }) {
  return (
    <div className="flex items-start gap-1.5">
      <span className={`mt-0.5 shrink-0 ${accent ? 'text-red-500' : 'text-slate-400'}`}>{icon}</span>
      <div>
        <p className="text-xs text-slate-400">{label}</p>
        <p className={`text-sm font-medium ${accent ? 'text-red-600' : 'text-slate-700'}`}>{value}</p>
      </div>
    </div>
  )
}
