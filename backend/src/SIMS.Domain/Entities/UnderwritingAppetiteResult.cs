namespace SIMS.Domain.Entities;

public class UnderwritingAppetiteResult : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public Guid? QuoteId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public bool Triggered { get; set; }
    public bool ReferralRequired { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public Guid EvaluatedById { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    public Submission Submission { get; set; } = null!;
    public Quote? Quote { get; set; }
    public User EvaluatedBy { get; set; } = null!;
}
