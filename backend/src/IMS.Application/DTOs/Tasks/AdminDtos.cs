using IMS.Domain.Enums;

namespace IMS.Application.DTOs.Tasks;

// ── WorkflowTemplate ─────────────────────────────────────────────────────────

public class WorkflowTemplateListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public Guid TriggerEventId { get; set; }
    public string TriggerEventName { get; set; } = string.Empty;
    public TaskEntityType EntityType { get; set; }
    public int StepCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WorkflowTemplateDto : WorkflowTemplateListItemDto
{
    public List<WorkflowStepDto> Steps { get; set; } = [];
}

public class WorkflowTemplateCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid TriggerEventId { get; set; }
    public TaskEntityType EntityType { get; set; }
}

public class WorkflowTemplateUpdateDto : WorkflowTemplateCreateDto { }

public class WorkflowStepDto
{
    public Guid Id { get; set; }
    public int StepOrder { get; set; }
    public Guid TaskTypeId { get; set; }
    public string TaskTypeName { get; set; } = string.Empty;
    public Guid? DependsOnStepId { get; set; }
    public string? TriggerCondition { get; set; }
}

public class WorkflowStepUpsertDto
{
    public Guid? Id { get; set; }
    public int StepOrder { get; set; }
    public Guid TaskTypeId { get; set; }
    public Guid? DependsOnStepId { get; set; }
    public string? TriggerCondition { get; set; }
}

// ── SystemEvent ──────────────────────────────────────────────────────────────

public class SystemEventDto
{
    public Guid Id { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

// ── HolidayCalendar ──────────────────────────────────────────────────────────

public class HolidayCalendarDto
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class HolidayCalendarCreateDto
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
}

// ── EscalationRule ───────────────────────────────────────────────────────────

public class EscalationRuleDto
{
    public Guid Id { get; set; }
    public Guid? TaskTypeId { get; set; }
    public string? TaskTypeName { get; set; }
    public int HoursOverdue { get; set; }
    public string NotifyRoleName { get; set; } = string.Empty;
    public bool IncreasePriority { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EscalationRuleCreateDto
{
    public Guid? TaskTypeId { get; set; }
    public int HoursOverdue { get; set; }
    public string NotifyRoleName { get; set; } = string.Empty;
    public bool IncreasePriority { get; set; }
    public bool IsActive { get; set; } = true;
}

public class EscalationRuleUpdateDto : EscalationRuleCreateDto { }
