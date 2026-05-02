namespace SIMS.Domain.Entities.Accounting;

public class CashDistributionBatch
{
    public long Id { get; set; }
    public int TenantId { get; set; } = 1;
    public string BatchNumber { get; set; } = string.Empty;      // BATCH-{YYYY}-{NNNNN}
    public string Status { get; set; } = "Open";                 // Open|PdfGenerated|Executed|Voided
    public int TotalInstructions { get; set; }
    public int TotalWires { get; set; }                          // distinct payees (netted)
    public decimal TotalAmount { get; set; }
    public string? PdfBlobPath { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public Guid? ExecutedBy { get; set; }
    public string? BankReference { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CashMovementInstruction> Instructions { get; set; } = new List<CashMovementInstruction>();
}
