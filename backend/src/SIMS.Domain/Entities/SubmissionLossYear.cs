using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class SubmissionLossYear : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public int PolicyYear { get; set; }
    public string? LineOfBusiness { get; set; }
    public string? CarrierName { get; set; }
    public string? PolicyNumber { get; set; }
    public decimal PremiumAmount { get; set; }
    public LossPremiumBasis PremiumBasis { get; set; } = LossPremiumBasis.Projected;
    public bool IsSmmWritten { get; set; }
    public string? Source { get; set; }
    public DateOnly? AsOfDate { get; set; }
    public decimal? PaidOverride { get; set; }
    public decimal? ReservedOverride { get; set; }
    public decimal? ExpenseOverride { get; set; }
    public string? Notes { get; set; }

    public Submission Submission { get; set; } = null!;
    public ICollection<SubmissionLossClaim> Claims { get; set; } = new List<SubmissionLossClaim>();
}
