using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class Policy : BaseEntity
{
    public string PolicyNumber { get; set; } = string.Empty;
    public Guid SubmissionId { get; set; }
    public Guid BoundQuoteId { get; set; }
    public Guid CarrierId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public decimal PremiumAmount { get; set; }
    public decimal TaxesAndFees { get; set; }
    public decimal TotalPremium { get; set; }
    public PolicyStatus Status { get; set; } = PolicyStatus.Active;
    public DateOnly BoundDate { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public DateOnly? CancelledDate { get; set; }
    public DateOnly? NonRenewedDate { get; set; }

    // Navigation
    public Submission Submission { get; set; } = null!;
    public Quote BoundQuote { get; set; } = null!;
    public Carrier Carrier { get; set; } = null!;
    public ICollection<PolicyTransaction> Transactions { get; set; } = new List<PolicyTransaction>();
}
