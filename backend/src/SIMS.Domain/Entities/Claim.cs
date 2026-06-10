using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class Claim : BaseEntity
{
    // Policy link — nullable until the claim is matched to a SIMS policy
    public Guid? PolicyId { get; set; }
    public string? PolicyNumber { get; set; }
    public Guid? InsuredId { get; set; }
    public string? InsuredName { get; set; }

    // Source identifiers from carrier / TPA feed
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
    // Incurred = Paid + Reserved + Expense — stored for loss-run filtering
    public decimal Incurred { get; set; }

    public DateOnly LastValuationDate { get; set; }

    public Guid? ImportBatchId { get; set; }
    public bool IsManualEntry { get; set; } = true;
    public string? Notes { get; set; }

    // Navigation
    public Policy? Policy { get; set; }
    public ClaimImportBatch? ImportBatch { get; set; }
}
