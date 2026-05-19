using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class PolicyTransaction : BaseEntity
{
    public Guid PolicyId { get; set; }
    public TransactionType TransactionType { get; set; }
    public PolicyTransactionStatus Status { get; set; } = PolicyTransactionStatus.Submitted;
    public string TransactionNumber { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public Guid? SourceQuoteId { get; set; }
    public Guid? RenewalQuoteId { get; set; }
    public Guid? PriorPolicyVersionId { get; set; }
    public Guid? ResultingPolicyVersionId { get; set; }

    public Guid? RequestedById { get; set; }
    public DateTime? RequestedAt { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? IssuedById { get; set; }
    public DateTime? IssuedAt { get; set; }
    public Guid? CompletedById { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonText { get; set; }

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
    public decimal? PremiumBefore { get; set; }
    public decimal PremiumChange { get; set; }
    public decimal NewTotalPremium { get; set; }
    public decimal? PremiumAfter { get; set; }
    public decimal? TaxesAndFeesDelta { get; set; }
    public decimal? CommissionDelta { get; set; }
    public string? BillingModeSnapshot { get; set; }
    public string? ExternalReference { get; set; }
    public Guid? VoidsPolicyTransactionId { get; set; }
    public Guid? ReversesPolicyTransactionId { get; set; }

    public Guid ProcessedById { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    // Navigation
    public Policy Policy { get; set; } = null!;
    public User ProcessedBy { get; set; } = null!;
    public Policy? PriorPolicy { get; set; }
    public PolicyCancellationDetail? CancellationDetail { get; set; }
    public PolicyNonRenewalDetail? NonRenewalDetail { get; set; }
    public PolicyReinstatementDetail? ReinstatementDetail { get; set; }
    public ICollection<PolicyTransactionStatusHistory> StatusHistory { get; set; } = new List<PolicyTransactionStatusHistory>();
    public ICollection<PolicyTransactionComplianceChecklist> ComplianceChecklists { get; set; } = new List<PolicyTransactionComplianceChecklist>();
    public ICollection<PolicyTransactionApproval> Approvals { get; set; } = new List<PolicyTransactionApproval>();
}
