using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Claims;

public class ClaimListItemDto
{
    public Guid Id { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public string? CarrierClaimNumber { get; set; }
    public Guid? PolicyId { get; set; }
    public string? PolicyNumber { get; set; }
    public Guid? InsuredId { get; set; }
    public string? InsuredName { get; set; }
    public string? SourcePolicyReference { get; set; }
    public string? Account { get; set; }
    public string? CarrierName { get; set; }
    public DateOnly DateOfLoss { get; set; }
    public DateOnly ReportDate { get; set; }
    public DateOnly? ClosedDate { get; set; }
    public ClaimStatus Status { get; set; }
    public string? CoverageType { get; set; }
    public string? ClaimTypeDesc { get; set; }
    public string? LossCause { get; set; }
    public string? TpaName { get; set; }
    public string? ClaimantName { get; set; }
    public string? AdjusterName { get; set; }
    public decimal Paid { get; set; }
    public decimal Reserved { get; set; }
    public decimal Expense { get; set; }
    public decimal Recovery { get; set; }
    public decimal Incurred { get; set; }
    public DateOnly LastValuationDate { get; set; }
    public bool IsManualEntry { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClaimDto : ClaimListItemDto
{
    public string? Description { get; set; }
    public string? RiskState { get; set; }
    public string? AccidentState { get; set; }
    public string? TpaClaimNumber { get; set; }
    public string? Notes { get; set; }
    public Guid? ImportBatchId { get; set; }
}

public class UpsertClaimRequest
{
    public Guid? PolicyId { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public string? CarrierClaimNumber { get; set; }
    public string? SourcePolicyReference { get; set; }
    public string? Account { get; set; }
    public string? CarrierName { get; set; }
    public DateOnly DateOfLoss { get; set; }
    public DateOnly ReportDate { get; set; }
    public DateOnly? ClosedDate { get; set; }
    public ClaimStatus Status { get; set; } = ClaimStatus.Open;
    public string? CoverageType { get; set; }
    public string? ClaimTypeDesc { get; set; }
    public string? LossCause { get; set; }
    public string? Description { get; set; }
    public string? RiskState { get; set; }
    public string? AccidentState { get; set; }
    public string? ClaimantName { get; set; }
    public string? AdjusterName { get; set; }
    public string? TpaName { get; set; }
    public string? TpaClaimNumber { get; set; }
    public decimal Paid { get; set; }
    public decimal Reserved { get; set; }
    public decimal Expense { get; set; }
    public decimal Recovery { get; set; }
    public DateOnly LastValuationDate { get; set; }
    public string? Notes { get; set; }
}

// Matches the Unified_Claims_Import column layout exactly
public class ImportClaimsRequest
{
    public string FileName { get; set; } = string.Empty;
    public string? CarrierName { get; set; }
    public string? TpaName { get; set; }
    public DateOnly ValuationDate { get; set; }
    public List<UnifiedClaimImportRow> Rows { get; set; } = new();
}

// Column names match the Unified_Claims_Import spreadsheet header
public class UnifiedClaimImportRow
{
    public string? ClaimNumber { get; set; }
    public string? Account { get; set; }
    public string? ClaimStatusDesc { get; set; }
    public string? AdjusterName { get; set; }
    public string? ClaimTypeDesc { get; set; }
    public string? ClaimantName { get; set; }
    public string? DateOfClaim { get; set; }
    public string? DateReported { get; set; }
    public string? CarrierName { get; set; }
    public string? CarrierPolicyNum { get; set; }
    public string? CarrierEffectiveDate { get; set; }
    public string? NamedInsured { get; set; }
    public string? AccidentCauseDesc { get; set; }
    public string? AccidentDescription { get; set; }
    public string? RiskState { get; set; }
    public string? AccidentState { get; set; }
    public decimal? TotalLossPaid { get; set; }
    public decimal? TotalExpPaid { get; set; }
    public decimal? TotalOsLoss { get; set; }
    public decimal? TotalOsExp { get; set; }
    public decimal? TotalRecovery { get; set; }
    public decimal? TotalIncurred { get; set; }
    public string? Lob { get; set; }
    public string? ValueDate { get; set; }
}

public class ClaimImportBatchDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? CarrierName { get; set; }
    public string? TpaName { get; set; }
    public DateOnly ValuationDate { get; set; }
    public int RecordCount { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorSummaryJson { get; set; }
    public string ImportedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class LossRunDto
{
    public DateOnly AsOfDate { get; set; }
    public Guid? InsuredId { get; set; }
    public string? InsuredName { get; set; }
    public Guid? PolicyId { get; set; }
    public string? PolicyNumber { get; set; }
    public string? Account { get; set; }
    public int ClaimCount { get; set; }
    public int OpenCount { get; set; }
    public int ClosedCount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalReserved { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal TotalIncurred { get; set; }
    public IReadOnlyList<ClaimListItemDto> Claims { get; set; } = [];
}
