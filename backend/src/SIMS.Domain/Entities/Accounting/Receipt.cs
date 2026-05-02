namespace SIMS.Domain.Entities.Accounting;

public class Receipt
{
    public long Id { get; set; }
    public int TenantId { get; set; } = 1;
    public string ReceiptNumber { get; set; } = string.Empty;   // RCT-{YYYY}-{NNNNN}
    public DateOnly ReceivedDate { get; set; }
    public decimal Amount { get; set; }
    public string PayerName { get; set; } = string.Empty;
    public string? Reference { get; set; }                       // wire ref / check #
    public string? RemittanceBlobPath { get; set; }              // Azure Blob path
    public Guid LedgerTransactionId { get; set; }                // DR Trust / CR Unapplied
    public string Status { get; set; } = "Open";                 // 'Open'|'Applied'|'PartiallyApplied'|'Voided'
    public decimal AppliedAmount { get; set; } = 0;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CashApplication> Applications { get; set; } = new List<CashApplication>();
}
