using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Tasks;

public class TaskTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority DefaultPriority { get; set; }
    public string? AssignedRoleTemplate { get; set; }
    public string? DueDateFormula { get; set; }
    public bool IsActive { get; set; }
    public Guid? ParentTaskTypeId { get; set; }
    public string? ParentTaskTypeName { get; set; }
}

public class TaskTypeListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TaskPriority DefaultPriority { get; set; }
    public bool IsActive { get; set; }
    public int ChildCount { get; set; }
}

public class TaskTypeCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority DefaultPriority { get; set; } = TaskPriority.Medium;
    public string? AssignedRoleTemplate { get; set; }
    public string? DueDateFormula { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? ParentTaskTypeId { get; set; }
}

public class TaskTypeUpdateDto : TaskTypeCreateDto { }

// ── TaskInstance DTOs ─────────────────────────────────────────────────────

public class TaskInstanceListItemDto
{
    public Guid Id { get; set; }
    public string TaskTypeName { get; set; } = string.Empty;
    public TaskEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public Guid? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
    public TaskInstanceStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsOverdue { get; set; }
    public int EscalationLevel { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TaskInstanceDto : TaskInstanceListItemDto
{
    public Guid TaskTypeId { get; set; }
    public Guid? WorkflowStepId { get; set; }
    public string? AssignedRoleExpression { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public string? CompletedByUserName { get; set; }
    public string? ReferenceUrl { get; set; }
    public List<TaskAuditEntryDto> AuditEntries { get; set; } = [];
}

public class TaskAuditEntryDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public TaskAuditAction Action { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Notes { get; set; }
    public DateTime Timestamp { get; set; }
}

public class UpdateTaskStatusDto
{
    public TaskInstanceStatus NewStatus { get; set; }
    public string? Notes { get; set; }
}

public class ReassignTaskDto
{
    public Guid NewUserId { get; set; }
}
