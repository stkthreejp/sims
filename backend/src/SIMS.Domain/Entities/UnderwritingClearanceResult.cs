using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class UnderwritingClearanceResult : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public UnderwritingClearanceCheckType CheckType { get; set; }
    public UnderwritingClearanceStatus Status { get; set; }
    public Guid? MatchedRecordId { get; set; }
    public string? MatchedRecordLabel { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public Guid ReviewedById { get; set; }
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
    public string SnapshotJson { get; set; } = "{}";

    public Submission Submission { get; set; } = null!;
    public User ReviewedBy { get; set; } = null!;
}
