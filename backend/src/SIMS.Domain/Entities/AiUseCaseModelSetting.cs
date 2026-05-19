namespace SIMS.Domain.Entities;

public class AiUseCaseModelSetting : BaseEntity
{
    public string UseCase { get; set; } = string.Empty;
    public Guid AiModelRegistryId { get; set; }
    public AiModelRegistry AiModel { get; set; } = null!;
    public string PromptVersion { get; set; } = "smm-underwriter-v1";
    public Guid? UpdatedByUserId { get; set; }
}
