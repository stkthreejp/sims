using SIMS.Domain.Constants;

namespace SIMS.Domain.Entities;

public class ComplianceDocumentVersion : BaseEntity
{
    public Guid DocumentId { get; set; }
    public ComplianceDocument Document { get; set; } = null!;
    public int VersionNumber { get; set; }
    public string Status { get; set; } = ComplianceVersionStatus.Draft;
    public string HtmlContent { get; set; } = string.Empty;
    public string PlainText { get; set; } = string.Empty;
    public string? ChangeSummary { get; set; }
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public Guid? ApprovedById { get; set; }
    public User? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateOnly? EffectiveDate { get; set; }
}
