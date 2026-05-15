namespace SIMS.Domain.Entities;

public class ComplianceEvidence : BaseEntity
{
    public Guid DocumentId { get; set; }
    public ComplianceDocument Document { get; set; } = null!;
    public Guid? ReviewId { get; set; }
    public ComplianceDocumentReview? Review { get; set; }
    public string Title { get; set; } = string.Empty;
    public string EvidenceType { get; set; } = "Note";
    public string? Description { get; set; }
    public string? Url { get; set; }
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
}
