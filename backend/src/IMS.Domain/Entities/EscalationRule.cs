namespace IMS.Domain.Entities;

public class EscalationRule : BaseEntity
{
    public Guid? TaskTypeId { get; set; }
    public int HoursOverdue { get; set; }
    public string NotifyRoleName { get; set; } = string.Empty;
    public bool IncreasePriority { get; set; }
    public bool IsActive { get; set; } = true;

    public TaskType? TaskType { get; set; }
}
