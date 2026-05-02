using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class TaskInstance : BaseEntity
{
    public Guid TaskTypeId { get; set; }
    public Guid? WorkflowStepId { get; set; }
    public TaskEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public Guid? AssignedUserId { get; set; }
    public string? AssignedRoleExpression { get; set; }
    public TaskInstanceStatus Status { get; set; } = TaskInstanceStatus.Open;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public string? ReferenceUrl { get; set; }
    public int EscalationLevel { get; set; } = 0;
    public DateTime? EscalatedAt { get; set; }

    public TaskType TaskType { get; set; } = null!;
    public WorkflowStep? WorkflowStep { get; set; }
    public ICollection<TaskAuditEntry> AuditEntries { get; set; } = new List<TaskAuditEntry>();
}
