using IMS.Domain.Enums;

namespace IMS.Domain.Entities;

public class PolicyTransaction : BaseEntity
{
    public Guid QuoteId { get; set; }
    public TransactionType TransactionType { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }

    // Endorsement
    public string? EndorsementDescription { get; set; }

    // Renewal
    public Guid? RenewalQuoteId { get; set; }

    // Cancellation
    public string? CancellationReason { get; set; }
    public string? CancellationMethod { get; set; }

    // Financials
    public decimal PremiumChange { get; set; }
    public decimal NewTotalPremium { get; set; }

    public Guid ProcessedById { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    // Navigation
    public Quote Quote { get; set; } = null!;
    public User ProcessedBy { get; set; } = null!;
    public Quote? RenewalQuote { get; set; }
}
