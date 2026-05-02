using IMS.Domain.Enums;

namespace IMS.Application.DTOs.Tasks;

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
