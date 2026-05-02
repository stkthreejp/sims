namespace SIMS.Domain.Entities.Accounting;

public class CashApplication
{
    public long Id { get; set; }
    public int TenantId { get; set; } = 1;
    public long ReceiptId { get; set; }
    public long InvoiceId { get; set; }
    public decimal GrossApplied { get; set; }         // full invoice amount credited to AR
    public decimal CommissionAmount { get; set; }     // broker commission deducted (DR CommExp)
    public decimal NetApplied { get; set; }           // GrossApplied - CommissionAmount (DR Unapplied)
    public Guid LedgerTransactionId { get; set; }     // the balanced GL group for this application
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Receipt Receipt { get; set; } = null!;
    public Invoice Invoice { get; set; } = null!;
}
