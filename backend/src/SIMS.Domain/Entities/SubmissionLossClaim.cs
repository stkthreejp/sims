using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class SubmissionLossClaim : BaseEntity
{
    public Guid SubmissionLossYearId { get; set; }
    public DateOnly? DateOfLoss { get; set; }
    public string? ClaimNumber { get; set; }
    public LossClaimStatus Status { get; set; } = LossClaimStatus.Closed;
    public string? Description { get; set; }
    public string? CoverageType { get; set; }
    public decimal Paid { get; set; }
    public decimal Reserved { get; set; }
    public decimal Expense { get; set; }

    public SubmissionLossYear LossYear { get; set; } = null!;
}
