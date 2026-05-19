namespace SIMS.Domain.Entities;

public class AiModelRegistry : BaseEntity
{
    public string Provider { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public string[] AllowedUseCases { get; set; } = [];
    public string[] DefaultUseCases { get; set; } = [];
    public string? CostNotes { get; set; }
    public DateOnly? RetirementReviewDate { get; set; }
}
