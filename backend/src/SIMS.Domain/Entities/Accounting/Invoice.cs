namespace SIMS.Domain.Entities.Accounting;

public class Invoice
{
    public long Id { get; set; }
    public int TenantId { get; set; } = 1;
    public string InvoiceNumber { get; set; } = string.Empty;
    public long? PolicyTransactionId { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public decimal GrossPremium { get; set; }
    public decimal TotalFees { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid LedgerTransactionId { get; set; }
    public decimal ClearedAmount { get; set; } = 0;
    public string Status { get; set; } = "Posted";  // 'Posted'|'PartiallyPaid'|'Paid'|'Voided'
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
}
