export type TaskEntityType = 'Account' | 'Submission' | 'Policy'
export type TaskInstanceStatus = 'Open' | 'InProgress' | 'Blocked' | 'Closed' | 'Cancelled'
export type TaskPriority = 'Low' | 'Medium' | 'High'
export type TaskAuditAction =
  | 'Created' | 'Assigned' | 'Reassigned' | 'StatusChanged' | 'PriorityChanged'
  | 'DueDateChanged' | 'Completed' | 'Cancelled' | 'Escalated' | 'ReminderSent'
  | 'OverdueNotified' | 'DigestSent' | 'Note'

export interface TaskAuditEntry {
  id: string
  userId?: string
  userName?: string
  action: TaskAuditAction
  oldValue?: string
  newValue?: string
  notes?: string
  timestamp: string
}

export interface TaskInstanceListItem {
  id: string
  taskTypeName: string
  entityType: TaskEntityType
  entityId: string
  assignedUserId?: string
  assignedUserName?: string
  status: TaskInstanceStatus
  priority: TaskPriority
  dueDate: string
  isOverdue: boolean
  escalationLevel: number
  createdAt: string
}

export interface TaskInstance extends TaskInstanceListItem {
  taskTypeId: string
  workflowStepId?: string
  assignedRoleExpression?: string
  completedAt?: string
  completedByUserId?: string
  completedByUserName?: string
  referenceUrl?: string
  auditEntries: TaskAuditEntry[]
}

// Admin types

export interface SystemEvent {
  id: string
  eventName: string
  description?: string
}

export interface WorkflowStep {
  id: string
  stepOrder: number
  taskTypeId: string
  taskTypeName: string
  dependsOnStepId?: string
  triggerCondition?: string
}

export interface WorkflowTemplateListItem {
  id: string
  name: string
  description?: string
  isActive: boolean
  triggerEventId: string
  triggerEventName: string
  entityType: TaskEntityType
  stepCount: number
  createdAt: string
}

export interface WorkflowTemplate extends WorkflowTemplateListItem {
  steps: WorkflowStep[]
}

export interface HolidayCalendar {
  id: string
  date: string
  name: string
}

export interface EscalationRule {
  id: string
  taskTypeId?: string
  taskTypeName?: string
  hoursOverdue: number
  notifyRoleName: string
  increasePriority: boolean
  isActive: boolean
  createdAt: string
}

export interface TaskType {
  id: string
  name: string
  description?: string
  defaultPriority: TaskPriority
  assignedRoleTemplate?: string
  dueDateFormula?: string
  isActive: boolean
  parentTaskTypeId?: string
  parentTaskTypeName?: string
}

export interface TaskTypeListItem {
  id: string
  name: string
  defaultPriority: TaskPriority
  isActive: boolean
  childCount: number
}
