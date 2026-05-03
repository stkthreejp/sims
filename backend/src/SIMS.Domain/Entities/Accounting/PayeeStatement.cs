namespace SIMS.Domain.Entities.Accounting;

public class PayeeStatement
{
    public long Id { get; set; }
    public int TenantId { get; set; } = 1;
    public string PayeeName { get; set; } = string.Empty;
    public DateOnly StatementDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? BlobPath { get; set; }
    public int ApLedgerAccountId { get; set; }
    public decimal StatementTotal { get; set; }
    public string Status { get; set; } = "Imported";  // Imported | Reconciled | Voided
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public LedgerAccount ApLedgerAccount { get; set; } = null!;
    public ICollection<PayeeStatementLine> Lines { get; set; } = new List<PayeeStatementLine>();
}
