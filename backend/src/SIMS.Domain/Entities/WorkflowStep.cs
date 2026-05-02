namespace SIMS.Domain.Entities;

public class WorkflowStep : BaseEntity
{
    public Guid WorkflowTemplateId { get; set; }
    public Guid TaskTypeId { get; set; }
    public int StepOrder { get; set; }
    public Guid? DependsOnStepId { get; set; }
    public string? TriggerCondition { get; set; }

    public WorkflowTemplate WorkflowTemplate { get; set; } = null!;
    public TaskType TaskType { get; set; } = null!;
    public WorkflowStep? DependsOnStep { get; set; }
}
