using IMS.Domain.Enums;

namespace IMS.Domain.Entities;

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
    public decimal CommissionRate { get; set; }
    public decimal CommissionAmount { get; set; }

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
}
