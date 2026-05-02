namespace SIMS.Domain.Entities.Accounting;

public class CashMovementInstruction
{
    public long Id { get; set; }
    public int TenantId { get; set; } = 1;

    // Source traceability — each instruction traces back to a specific receipt + application + fee line
    public long ReceiptId { get; set; }
    public long CashApplicationId { get; set; }
    public long InvoiceLineId { get; set; }

    // Destination
    public long PayeeId { get; set; }
    public decimal Amount { get; set; }

    // GL accounts for the sweep JE (populated at instruction creation)
    public int SourceGlAccountId { get; set; }       // Trust Account (1100) — CR on sweep
    public int DistributionGlAccountId { get; set; } // Payable account from invoice line — DR on sweep

    // Lifecycle
    public string Status { get; set; } = "Pending"; // Pending|Batched|Executed|Voided
    public long? BatchId { get; set; }
    public Guid? LedgerTransactionId { get; set; }   // set when executed

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Receipt Receipt { get; set; } = null!;
    public CashApplication CashApplication { get; set; } = null!;
    public InvoiceLine InvoiceLine { get; set; } = null!;
    public Payee Payee { get; set; } = null!;
    public CashDistributionBatch? Batch { get; set; }
}
