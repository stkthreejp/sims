using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Transactions;

public class PolicyTransactionDto
{
    public Guid Id { get; set; }
    public Guid QuoteId { get; set; }
    public TransactionType TransactionType { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public string? EndorsementDescription { get; set; }
    public string? CancellationReason { get; set; }
    public string? CancellationMethod { get; set; }
    public decimal PremiumChange { get; set; }
    public decimal NewTotalPremium { get; set; }
    public string ProcessedByName { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
