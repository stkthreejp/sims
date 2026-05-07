using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Submissions;

public class SubmissionLossHistorySummaryDto
{
    public int YearCount { get; set; }
    public int ClaimCount { get; set; }
    public decimal TotalPremium { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalReserved { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal TotalIncurred { get; set; }
    public decimal? LossRatio { get; set; }
    public decimal? AverageSeverity { get; set; }
    public decimal LargestLoss { get; set; }
    public decimal OpenReserve { get; set; }
    public List<SubmissionLossYearDto> Years { get; set; } = new();
}

public class SubmissionLossYearDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public int PolicyYear { get; set; }
    public string? LineOfBusiness { get; set; }
    public string? CarrierName { get; set; }
    public string? PolicyNumber { get; set; }
    public decimal PremiumAmount { get; set; }
    public LossPremiumBasis PremiumBasis { get; set; }
    public bool IsSmmWritten { get; set; }
    public string? Source { get; set; }
    public DateOnly? AsOfDate { get; set; }
    public decimal? PaidOverride { get; set; }
    public decimal? ReservedOverride { get; set; }
    public decimal? ExpenseOverride { get; set; }
    public string? Notes { get; set; }
    public decimal Paid { get; set; }
    public decimal Reserved { get; set; }
    public decimal Expense { get; set; }
    public decimal Incurred { get; set; }
    public decimal? LossRatio { get; set; }
    public int ClaimCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<SubmissionLossClaimDto> Claims { get; set; } = new();
}

public class SubmissionLossYearCreateDto
{
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
}

public class SubmissionLossYearUpdateDto : SubmissionLossYearCreateDto { }

public class SubmissionLossClaimDto
{
    public Guid Id { get; set; }
    public Guid SubmissionLossYearId { get; set; }
    public DateOnly? DateOfLoss { get; set; }
    public string? ClaimNumber { get; set; }
    public LossClaimStatus Status { get; set; }
    public string? Description { get; set; }
    public string? CoverageType { get; set; }
    public decimal Paid { get; set; }
    public decimal Reserved { get; set; }
    public decimal Expense { get; set; }
    public decimal Incurred { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SubmissionLossClaimCreateDto
{
    public DateOnly? DateOfLoss { get; set; }
    public string? ClaimNumber { get; set; }
    public LossClaimStatus Status { get; set; } = LossClaimStatus.Closed;
    public string? Description { get; set; }
    public string? CoverageType { get; set; }
    public decimal Paid { get; set; }
    public decimal Reserved { get; set; }
    public decimal Expense { get; set; }
}

public class SubmissionLossClaimUpdateDto : SubmissionLossClaimCreateDto { }
