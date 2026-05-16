import { useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { AlertTriangle, CheckSquare, Clock, ExternalLink, Search } from 'lucide-react'
import { tasksApi } from '@/api/tasks.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { TaskDetailDrawer } from './TaskDetailDrawer'
import type { TaskInstanceListItem, TaskInstanceStatus, TaskPriority } from '@/types/task.types'

const STATUS_OPTIONS: TaskInstanceStatus[] = ['Open', 'InProgress', 'Blocked', 'Closed', 'Cancelled']
const PRIORITY_OPTIONS: TaskPriority[] = ['High', 'Medium', 'Low']

const PRIORITY_TONE: Record<TaskPriority, { bg: string; fg: string; border: string }> = {
  High:   { bg: 'var(--pill-declined-bg)', fg: 'var(--pill-declined-fg)', border: 'var(--pill-declined-fg)' },
  Medium: { bg: 'var(--pill-inprog-bg)',   fg: 'var(--pill-inprog-fg)',   border: 'var(--pill-inprog-fg)' },
  Low:    { bg: 'var(--pill-draft-bg)',     fg: 'var(--pill-draft-fg)',    border: 'var(--line)' },
}

const STATUS_TONE: Record<TaskInstanceStatus, { label: string; bg: string; fg: string; border: string }> = {
  Open:       { label: 'Open',        bg: 'var(--pill-draft-bg)',    fg: 'var(--pill-draft-fg)',    border: 'var(--line)' },
  InProgress: { label: 'In progress', bg: 'var(--pill-inprog-bg)',   fg: 'var(--pill-inprog-fg)',   border: 'rgba(27, 117, 186, 0.35)' },
  Blocked:    { label: 'Blocked',     bg: '#fff0d6',                 fg: '#8a5a00',                 border: '#e4b85d' },
  Closed:     { label: 'Closed',      bg: 'var(--pill-draft-bg)',    fg: 'var(--pill-draft-fg)',    border: 'var(--line)' },
  Cancelled:  { label: 'Cancelled',   bg: 'var(--pill-draft-bg)',    fg: 'var(--pill-draft-fg)',    border: 'var(--line)' },
}

const SAMPLE_TASKS: TaskInstanceListItem[] = [
  {
    id: 'sample-review-quote',
    taskTypeName: 'Review quote writeup',
    entityType: 'Submission',
    entityId: '00000000-0000-0000-0000-000000000001',
    assignedUserName: 'Sample User',
    status: 'Open',
    priority: 'High',
    dueDate: daysFromToday(-1),
    isOverdue: true,
    escalationLevel: 1,
    createdAt: daysFromToday(-5),
  },
  {
    id: 'sample-request-info',
    taskTypeName: 'Request missing loss runs',
    entityType: 'Submission',
    entityId: '00000000-0000-0000-0000-000000000002',
    assignedUserName: 'Sample User',
    status: 'InProgress',
    priority: 'Medium',
    dueDate: daysFromToday(2),
    isOverdue: false,
    escalationLevel: 0,
    createdAt: daysFromToday(-2),
  },
  {
    id: 'sample-compliance',
    taskTypeName: 'Resolve compliance hold',
    entityType: 'Policy',
    entityId: '00000000-0000-0000-0000-000000000003',
    assignedUserName: 'Sample User',
    status: 'Blocked',
    priority: 'Medium',
    dueDate: daysFromToday(4),
    isOverdue: false,
    escalationLevel: 0,
    createdAt: daysFromToday(-1),
  },
  {
    id: 'sample-issue-policy',
    taskTypeName: 'Issue bound policy',
    entityType: 'Policy',
    entityId: '00000000-0000-0000-0000-000000000004',
    assignedUserName: 'Sample User',
    status: 'Closed',
    priority: 'Low',
    dueDate: daysFromToday(0),
    isOverdue: false,
    escalationLevel: 0,
    createdAt: daysFromToday(-4),
  },
]

function daysFromToday(days: number) {
  const date = new Date()
  date.setDate(date.getDate() + days)
  return date.toISOString()
}

function entityUrl(task: TaskInstanceListItem) {
  const base = task.entityType === 'Submission' ? '/submissions'
             : task.entityType === 'Policy'     ? '/policies'
             : task.entityType === 'ComplianceDocument' ? '/compliance-documentation'
             : '/insureds'
  return `${base}/${task.entityId}`
}

function taskAccent(task: TaskInstanceListItem) {
  if (task.status === 'Blocked') return STATUS_TONE.Blocked.border
  if (task.isOverdue) return 'rgba(155, 45, 31, 0.45)'
  if (task.priority === 'High') return 'rgba(155, 45, 31, 0.28)'
  if (task.status === 'InProgress') return STATUS_TONE.InProgress.border
  return 'transparent'
}

function isSampleTask(task: TaskInstanceListItem) {
  return task.id.startsWith('sample-')
}

export function TaskQueuePage() {
  const qc = useQueryClient()
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [filterStatus, setFilterStatus] = useState<TaskInstanceStatus | ''>('')
  const [filterPriority, setFilterPriority] = useState<TaskPriority | ''>('')
  const [search, setSearch] = useState('')

  const { data: tasks = [], isLoading } = useQuery({
    queryKey: ['tasks', 'my-queue'],
    queryFn: tasksApi.getMyQueue,
  })

  const usingSampleTasks = tasks.length === 0
  const queueTasks = usingSampleTasks ? SAMPLE_TASKS : tasks

  const counts = useMemo(() => ({
    open: queueTasks.filter((t) => t.status === 'Open' || t.status === 'InProgress').length,
    overdue: queueTasks.filter((t) => t.isOverdue).length,
    blocked: queueTasks.filter((t) => t.status === 'Blocked').length,
    closed: queueTasks.filter((t) => t.status === 'Closed').length,
  }), [queueTasks])

  const filtered = queueTasks.filter((t) => {
    if (filterStatus && t.status !== filterStatus) return false
    if (filterPriority && t.priority !== filterPriority) return false
    if (search) {
      const query = search.toLowerCase()
      const target = `${t.taskTypeName} ${t.entityType} ${t.assignedUserName ?? ''}`.toLowerCase()
      if (!target.includes(query)) return false
    }
    return true
  })

  if (isLoading) return <LoadingSpinner />

  return (
    <div className="space-y-5 p-6">
      <PageHeader
        title="Tasks"
        subtitle={usingSampleTasks ? 'Showing sample task rows for UI review' : `${counts.open} open tasks in your queue`}
      />

      <div className="grid gap-3 md:grid-cols-4">
        <TaskMetric label="Open" value={counts.open} tone="open" />
        <TaskMetric label="Overdue" value={counts.overdue} tone={counts.overdue > 0 ? 'attention' : 'muted'} />
        <TaskMetric label="Blocked" value={counts.blocked} tone={counts.blocked > 0 ? 'warning' : 'muted'} />
        <TaskMetric label="Closed" value={counts.closed} tone="muted" />
      </div>

      <div className="subs-toolbar flex-wrap">
        <label className="subs-search">
          <Search className="h-3.5 w-3.5 shrink-0" />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search tasks..."
            className="w-full border-0 bg-transparent p-0 text-sm outline-none placeholder:text-slate-400"
          />
        </label>
        <select
          value={filterStatus}
          onChange={(e) => setFilterStatus(e.target.value as TaskInstanceStatus | '')}
          className="subs-filter"
        >
          <option value="">All statuses</option>
          {STATUS_OPTIONS.map((s) => <option key={s} value={s}>{STATUS_TONE[s].label}</option>)}
        </select>
        <select
          value={filterPriority}
          onChange={(e) => setFilterPriority(e.target.value as TaskPriority | '')}
          className="subs-filter"
        >
          <option value="">All priorities</option>
          {PRIORITY_OPTIONS.map((p) => <option key={p} value={p}>{p}</option>)}
        </select>
        {(filterStatus || filterPriority || search) && (
          <button
            onClick={() => { setFilterStatus(''); setFilterPriority(''); setSearch('') }}
            className="sd-btn ghost sm"
          >
            Clear filters
          </button>
        )}
      </div>

      <div className="subs-table-card">
        {filtered.length === 0 ? (
          <div className="p-10 text-center text-sm" style={{ color: 'var(--ink-3)' }}>
            <CheckSquare className="mx-auto mb-2 h-8 w-8" style={{ color: 'var(--ink-4)' }} />
            No tasks match your filters.
          </div>
        ) : (
          <table className="sd-table">
            <thead>
              <tr>
                <th>Task</th>
                <th>Priority</th>
                <th>Status</th>
                <th>Due Date</th>
                <th>Entity</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((task) => (
                <tr
                  key={task.id}
                  onClick={() => { if (!isSampleTask(task)) setSelectedId(task.id) }}
                  style={{ boxShadow: `inset 3px 0 0 ${taskAccent(task)}` }}
                >
                  <td className="primary-cell">
                    <div className="flex items-center gap-2">
                      {task.isOverdue && <AlertTriangle className="h-3.5 w-3.5 shrink-0" style={{ color: 'rgba(155, 45, 31, 0.78)' }} />}
                      {task.escalationLevel > 0 && (
                        <span className="rounded px-1.5 py-0.5 text-xs font-semibold" style={{ background: 'var(--pill-inprog-bg)', color: 'var(--pill-inprog-fg)' }}>
                          L{task.escalationLevel}
                        </span>
                      )}
                      {task.taskTypeName}
                      {isSampleTask(task) && (
                        <span className="rounded px-1.5 py-0.5 text-xs font-semibold" style={{ background: 'var(--surface-2)', color: 'var(--ink-3)' }}>
                          Sample
                        </span>
                      )}
                    </div>
                  </td>
                  <td>
                    <TonePill label={task.priority} tone={PRIORITY_TONE[task.priority]} />
                  </td>
                  <td>
                    <TonePill label={STATUS_TONE[task.status].label} tone={STATUS_TONE[task.status]} />
                  </td>
                  <td>
                    <div
                      className="flex items-center gap-1"
                      style={{ color: task.isOverdue ? 'rgba(155, 45, 31, 0.78)' : 'var(--ink-3)', fontWeight: task.isOverdue ? 600 : 500 }}
                    >
                      <Clock className="h-3.5 w-3.5 shrink-0" />
                      {new Date(task.dueDate).toLocaleDateString()}
                    </div>
                  </td>
                  <td onClick={(e) => e.stopPropagation()}>
                    {isSampleTask(task) ? (
                      <span className="text-xs font-medium" style={{ color: 'var(--ink-3)' }}>{task.entityType}</span>
                    ) : (
                      <Link
                        to={entityUrl(task)}
                        className="flex items-center gap-1 text-xs font-medium"
                        style={{ color: 'var(--accent)' }}
                      >
                        {task.entityType} <ExternalLink className="h-3 w-3" />
                      </Link>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {selectedId && (
        <TaskDetailDrawer
          taskId={selectedId}
          onClose={() => setSelectedId(null)}
          onUpdated={() => qc.invalidateQueries({ queryKey: ['tasks', 'my-queue'] })}
        />
      )}
    </div>
  )
}

function TonePill({ label, tone }: { label: string; tone: { bg: string; fg: string } }) {
  return (
    <span
      className="sd-pill"
      style={{ background: tone.bg, color: tone.fg }}
    >
      {label}
    </span>
  )
}

function TaskMetric({ label, value, tone }: { label: string; value: number; tone: 'open' | 'attention' | 'warning' | 'muted' }) {
  const color = tone === 'attention' ? 'rgba(155, 45, 31, 0.82)'
    : tone === 'warning' ? '#8a5a00'
    : tone === 'open' ? 'var(--pill-inprog-fg)'
    : 'var(--ink-3)'

  return (
    <div className="subs-metric" style={{ borderColor: tone === 'muted' ? 'var(--line)' : 'var(--line)' }}>
      <p style={{ margin: 0, color: 'var(--ink-3)', fontSize: 'var(--fs-xs)', fontWeight: 600, letterSpacing: '.06em', textTransform: 'uppercase' }}>
        {label}
      </p>
      <p style={{ margin: '3px 0 0', color, fontSize: 22, fontWeight: 650, lineHeight: 1.1 }}>
        {value}
      </p>
    </div>
  )
}
