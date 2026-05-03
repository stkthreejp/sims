using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class Quote : BaseEntity
{
    public string QuoteNumber { get; set; } = string.Empty;
    public Guid SubmissionId { get; set; }
    public Guid CarrierId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;

    // Set when bound
    public string? PolicyNumber { get; set; }
    public DateOnly? BoundDate { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public DateOnly? CancelledDate { get; set; }

    // Coverage dates
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }

    // Financials
    public decimal PremiumAmount { get; set; }
    public decimal TaxesAndFees { get; set; }
    public decimal TotalPremium { get; set; }

    // Commission rates — auto-populated from schedules at quote creation
    public decimal CarrierCommissionRate { get; set; }  // total commission from carrier
    public decimal SMMRetentionRate { get; set; }        // portion SMM keeps
    public decimal AgentCommissionRate { get; set; }     // agent's rate

    // Commission give-back override — set pre-bind by UW/Admin, locked at bind
    // When set, these rates replace the above for all endorsements this policy term
    public decimal? CommissionOverrideCarrierRate { get; set; }
    public decimal? CommissionOverrideSMMRate { get; set; }
    public decimal? CommissionOverrideAgentRate { get; set; }
    public Guid? CommissionOverrideBy { get; set; }
    public DateTime? CommissionOverrideAt { get; set; }

    // Coverage details
    public string? CoverageDescription { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? Limit { get; set; }

    // Commercial auto coverage limits (populated for CommercialAuto LOB)
    public decimal? UninsuredMotoristLimit { get; set; }
    public decimal? MedicalPaymentsLimit { get; set; }

    public Guid CreatedById { get; set; }

    // Navigation
    public Submission Submission { get; set; } = null!;
    public Carrier Carrier { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<PolicyTransaction> Transactions { get; set; } = new List<PolicyTransaction>();
    public ICollection<Note> Notes { get; set; } = new List<Note>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();

    // Effective rates for this term — override takes precedence when set
    public decimal EffectiveCarrierRate => CommissionOverrideCarrierRate ?? CarrierCommissionRate;
    public decimal EffectiveSMMRate => CommissionOverrideSMMRate ?? SMMRetentionRate;
    public decimal EffectiveAgentRate => CommissionOverrideAgentRate ?? AgentCommissionRate;
    public bool HasCommissionOverride => CommissionOverrideCarrierRate.HasValue;
}
