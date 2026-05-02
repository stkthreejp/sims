import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { CheckSquare, AlertTriangle, Clock, ExternalLink } from 'lucide-react'
import { tasksApi } from '@/api/tasks.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { TaskDetailDrawer } from './TaskDetailDrawer'
import type { TaskInstanceListItem, TaskInstanceStatus, TaskPriority } from '@/types/task.types'

const STATUS_OPTIONS: TaskInstanceStatus[] = ['Open', 'InProgress', 'Blocked', 'Closed', 'Cancelled']
const PRIORITY_OPTIONS: TaskPriority[] = ['High', 'Medium', 'Low']

const PRIORITY_PILL: Record<TaskPriority, { bg: string; fg: string }> = {
  High:   { bg: 'var(--pill-declined-bg)', fg: 'var(--pill-declined-fg)' },
  Medium: { bg: 'var(--pill-inprog-bg)',   fg: 'var(--pill-inprog-fg)' },
  Low:    { bg: 'var(--pill-draft-bg)',     fg: 'var(--pill-draft-fg)' },
}

const STATUS_PILL: Record<TaskInstanceStatus, { bg: string; fg: string }> = {
  Open:       { bg: 'var(--pill-draft-bg)',    fg: 'var(--pill-draft-fg)' },
  InProgress: { bg: 'var(--pill-inprog-bg)',   fg: 'var(--pill-inprog-fg)' },
  Blocked:    { bg: 'var(--pill-declined-bg)', fg: 'var(--pill-declined-fg)' },
  Closed:     { bg: 'var(--pill-bound-bg)',    fg: 'var(--pill-bound-fg)' },
  Cancelled:  { bg: 'var(--pill-draft-bg)',    fg: 'var(--pill-draft-fg)' },
}

function entityUrl(task: TaskInstanceListItem) {
  const base = task.entityType === 'Submission' ? '/submissions'
             : task.entityType === 'Policy'     ? '/policies'
             : '/insureds'
  return `${base}/${task.entityId}`
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

  const filtered = tasks.filter((t) => {
    if (filterStatus && t.status !== filterStatus) return false
    if (filterPriority && t.priority !== filterPriority) return false
    if (search && !t.taskTypeName.toLowerCase().includes(search.toLowerCase())) return false
    return true
  })

  if (isLoading) return <LoadingSpinner />

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="My Task Queue"
        subtitle={`${tasks.filter((t) => t.status === 'Open' || t.status === 'InProgress').length} open`}
      />

      {/* Filters */}
      <div className="flex flex-wrap gap-3">
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search tasks…"
          className="border rounded-lg px-3 py-1.5 text-sm w-48"
        />
        <select
          value={filterStatus}
          onChange={(e) => setFilterStatus(e.target.value as TaskInstanceStatus | '')}
          className="border rounded-lg px-3 py-1.5 text-sm"
        >
          <option value="">All statuses</option>
          {STATUS_OPTIONS.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        <select
          value={filterPriority}
          onChange={(e) => setFilterPriority(e.target.value as TaskPriority | '')}
          className="border rounded-lg px-3 py-1.5 text-sm"
        >
          <option value="">All priorities</option>
          {PRIORITY_OPTIONS.map((p) => <option key={p} value={p}>{p}</option>)}
        </select>
        {(filterStatus || filterPriority || search) && (
          <button
            onClick={() => { setFilterStatus(''); setFilterPriority(''); setSearch('') }}
            className="text-sm text-blue-600 hover:underline"
          >
            Clear filters
          </button>
        )}
      </div>

      {/* Table */}
      <div className="bg-white border rounded-lg overflow-hidden">
        {filtered.length === 0 ? (
          <div className="p-10 text-center text-slate-500 text-sm">
            <CheckSquare className="mx-auto mb-2 h-8 w-8 text-slate-300" />
            {tasks.length === 0 ? 'No tasks assigned to you.' : 'No tasks match your filters.'}
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b bg-slate-50 text-left text-xs text-slate-500 uppercase tracking-wide">
                <th className="px-4 py-3">Task</th>
                <th className="px-4 py-3">Priority</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Due Date</th>
                <th className="px-4 py-3">Entity</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {filtered.map((task) => (
                <tr
                  key={task.id}
                  onClick={() => setSelectedId(task.id)}
                  className="hover:bg-slate-50 cursor-pointer"
                >
                  <td className="px-4 py-3 font-medium text-slate-800">
                    <div className="flex items-center gap-2">
                      {task.isOverdue && <AlertTriangle className="h-3.5 w-3.5 text-red-500 shrink-0" />}
                      {task.escalationLevel > 0 && (
                        <span className="text-xs font-semibold text-orange-600 bg-orange-50 px-1.5 py-0.5 rounded">
                          L{task.escalationLevel}
                        </span>
                      )}
                      {task.taskTypeName}
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className="px-2 py-0.5 rounded-full text-xs font-medium"
                      style={{ background: PRIORITY_PILL[task.priority].bg, color: PRIORITY_PILL[task.priority].fg }}
                    >
                      {task.priority}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className="px-2 py-0.5 rounded-full text-xs font-medium"
                      style={{ background: STATUS_PILL[task.status].bg, color: STATUS_PILL[task.status].fg }}
                    >
                      {task.status}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className={`flex items-center gap-1 ${task.isOverdue ? 'text-red-600 font-semibold' : 'text-slate-600'}`}>
                      <Clock className="h-3.5 w-3.5 shrink-0" />
                      {new Date(task.dueDate).toLocaleDateString()}
                    </div>
                  </td>
                  <td className="px-4 py-3" onClick={(e) => e.stopPropagation()}>
                    <Link
                      to={entityUrl(task)}
                      className="flex items-center gap-1 text-blue-600 hover:underline text-xs"
                    >
                      {task.entityType} <ExternalLink className="h-3 w-3" />
                    </Link>
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
