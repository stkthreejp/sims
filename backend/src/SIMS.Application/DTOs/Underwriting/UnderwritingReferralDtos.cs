using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Underwriting;

public class UnderwritingReferralSummaryDto
{
    public Guid SubmissionId { get; set; }
    public bool HasOpenRequiredReferrals { get; set; }
    public IReadOnlyList<UnderwritingAppetiteResultDto> AppetiteResults { get; set; } = [];
    public IReadOnlyList<UnderwritingReferralDto> Referrals { get; set; } = [];
}

public class UnderwritingAppetiteResultDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid? QuoteId { get; set; }
    public string? QuoteNumber { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public bool Triggered { get; set; }
    public bool ReferralRequired { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public Guid EvaluatedById { get; set; }
    public string EvaluatedByName { get; set; } = string.Empty;
    public DateTime EvaluatedAt { get; set; }
}

public class UnderwritingReferralDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid? QuoteId { get; set; }
    public string? QuoteNumber { get; set; }
    public string ReferralType { get; set; } = string.Empty;
    public UnderwritingReferralStatus Status { get; set; }
    public bool Required { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public Guid? DecisionById { get; set; }
    public string? DecisionByName { get; set; }
    public DateTime? DecisionAt { get; set; }
    public string? DecisionNotes { get; set; }
}
