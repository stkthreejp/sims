namespace SIMS.Domain.Entities.Accounting;

public class Payable
{
    public long Id { get; set; }
    public int TenantId { get; set; } = 1;
    public long InvoiceId { get; set; }

    // Payee — either a Carrier (by Guid) or a named free-text entry
    public Guid? CarrierId { get; set; }   // FK → Carrier.Id
    public long? PayeeId { get; set; }
    public string PayeeName { get; set; } = string.Empty;

    // GL account to debit on disbursement (e.g. 2100 Carrier AP)
    public int GlAccountId { get; set; }

    public decimal Amount { get; set; }      // original owed (GrossPremium)
    public decimal PaidAmount { get; set; } = 0;

    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }    // InvoiceDate + net terms

    public string Status { get; set; } = "Open"; // Open|PartiallyPaid|Paid|Voided

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Invoice Invoice { get; set; } = null!;
    public Payee? Payee { get; set; }
    public LedgerAccount GlAccount { get; set; } = null!;
    public ICollection<DisbursementLine> DisbursementLines { get; set; } = new List<DisbursementLine>();
}
