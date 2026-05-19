namespace SIMS.Domain.Entities;

public class AiModelSettingAuditLog : BaseEntity
{
    public string UseCase { get; set; } = string.Empty;
    public Guid? PreviousAiModelRegistryId { get; set; }
    public Guid NewAiModelRegistryId { get; set; }
    public string? PreviousPromptVersion { get; set; }
    public string NewPromptVersion { get; set; } = string.Empty;
    public Guid ChangedByUserId { get; set; }
    public string ChangeReason { get; set; } = string.Empty;
}
