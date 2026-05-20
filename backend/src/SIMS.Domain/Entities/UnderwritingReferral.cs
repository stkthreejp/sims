using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class UnderwritingReferral : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public Guid? QuoteId { get; set; }
    public string ReferralType { get; set; } = string.Empty;
    public UnderwritingReferralStatus Status { get; set; } = UnderwritingReferralStatus.Open;
    public bool Required { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid RequestedById { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public Guid? DecisionById { get; set; }
    public DateTime? DecisionAt { get; set; }
    public string? DecisionNotes { get; set; }

    public Submission Submission { get; set; } = null!;
    public Quote? Quote { get; set; }
    public User RequestedBy { get; set; } = null!;
    public User? DecisionBy { get; set; }
}
