namespace SIMS.Domain.Entities.Accounting;

public class Disbursement
{
    public long Id { get; set; }
    public int TenantId { get; set; } = 1;
    public string DisbursementNumber { get; set; } = string.Empty; // DISB-{YYYY}-{NNNNN}
    public string PayeeName { get; set; } = string.Empty;
    public Guid? CarrierId { get; set; }        // FK → Carrier.Id (when paying a carrier)
    public decimal TotalAmount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string PaymentMethod { get; set; } = "Check"; // Check|Wire|ACH
    public string? Reference { get; set; }      // check # / wire ref
    public string Status { get; set; } = "Draft"; // Draft|Posted|Voided
    public Guid? LedgerTransactionId { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DisbursementLine> Lines { get; set; } = new List<DisbursementLine>();
}
