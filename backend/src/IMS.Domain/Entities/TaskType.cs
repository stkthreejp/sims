using IMS.Domain.Enums;

namespace IMS.Domain.Entities;

public class TaskType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority DefaultPriority { get; set; } = TaskPriority.Medium;
    public string? AssignedRoleTemplate { get; set; }
    public string? DueDateFormula { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? ParentTaskTypeId { get; set; }

    public TaskType? ParentTaskType { get; set; }
    public ICollection<TaskType> ChildTaskTypes { get; set; } = new List<TaskType>();
    public ICollection<TaskInstance> TaskInstances { get; set; } = new List<TaskInstance>();
}
