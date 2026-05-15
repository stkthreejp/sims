namespace SIMS.Domain.Entities;

public class ComplianceDocumentReview : BaseEntity
{
    public Guid DocumentId { get; set; }
    public ComplianceDocument Document { get; set; } = null!;
    public Guid? VersionId { get; set; }
    public ComplianceDocumentVersion? Version { get; set; }
    public string Status { get; set; } = "Completed";
    public string? Notes { get; set; }
    public Guid ReviewedById { get; set; }
    public User ReviewedBy { get; set; } = null!;
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
    public DateOnly? NextReviewDate { get; set; }
}
