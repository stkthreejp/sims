using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class TaskAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskInstanceId { get; set; }
    public Guid? UserId { get; set; }
    public TaskAuditAction Action { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Notes { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public TaskInstance TaskInstance { get; set; } = null!;
}
