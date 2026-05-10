namespace SIMS.Domain.Entities;

public class LegalSourceScanResult : BaseEntity
{
    public Guid ScanRunId { get; set; }
    public Guid? RequirementSectionId { get; set; }
    public string State { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string MatchStatus { get; set; } = "NeedsReview";
    public string SourceUrl { get; set; } = string.Empty;
    public string SourceCitation { get; set; } = string.Empty;
    public string SourceText { get; set; } = string.Empty;
    public string? SuggestedRequirementText { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public string ReviewStatus { get; set; } = "Pending";
    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public LegalSourceScanRun ScanRun { get; set; } = null!;
    public LegalRequirementSection? RequirementSection { get; set; }
    public User? ReviewedBy { get; set; }
}
