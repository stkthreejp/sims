namespace SIMS.Domain.Entities;

public class UnderwritingGuidelineAuditLog : BaseEntity
{
    public Guid? GuidelineDocumentId { get; set; }
    public Guid? GuidelineControlId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public string? Notes { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }

    public UnderwritingGuidelineDocument? GuidelineDocument { get; set; }
    public UnderwritingGuidelineControl? GuidelineControl { get; set; }
    public User ActorUser { get; set; } = null!;
}

