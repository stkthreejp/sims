using IMS.Domain.Enums;

namespace IMS.Domain.Entities;

public class WorkflowTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid TriggerEventId { get; set; }
    public TaskEntityType EntityType { get; set; }

    public SystemEvent TriggerEvent { get; set; } = null!;
    public ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
}
