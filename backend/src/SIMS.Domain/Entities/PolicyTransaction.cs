using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class PolicyTransaction : BaseEntity
{
    public Guid PolicyId { get; set; }
    public TransactionType TransactionType { get; set; }
    public PolicyTransactionStatus Status { get; set; } = PolicyTransactionStatus.Issued;
    public string TransactionNumber { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }

    // Endorsement
    public string? EndorsementDescription { get; set; }

    // Renewal — points to the prior policy this transaction renews from
    public Guid? PriorPolicyId { get; set; }

    // Cancellation
    public string? CancellationReason { get; set; }
    public string? CancellationMethod { get; set; }
    public string? CancellationComplianceChecklistJson { get; set; }
    public string? CancellationLegalRequirementSnapshotJson { get; set; }

    // Financials
    public decimal PremiumChange { get; set; }
    public decimal NewTotalPremium { get; set; }

    public Guid ProcessedById { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    // Navigation
    public Policy Policy { get; set; } = null!;
    public User ProcessedBy { get; set; } = null!;
    public Policy? PriorPolicy { get; set; }
}
