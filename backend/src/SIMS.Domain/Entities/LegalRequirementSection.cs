namespace SIMS.Domain.Entities;

public class LegalRequirementSection : BaseEntity
{
    public string State { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string RequirementText { get; set; } = string.Empty;
    public string[] Citations { get; set; } = [];
    public string SourceName { get; set; } = string.Empty;
    public string SourceDocument { get; set; } = string.Empty;
    public DateTime SourceCreatedAt { get; set; }
    public string ReviewStatus { get; set; } = "Seeded";
    public DateTime LastVerifiedAt { get; set; } = DateTime.UtcNow;
    public int SortOrder { get; set; }
}
