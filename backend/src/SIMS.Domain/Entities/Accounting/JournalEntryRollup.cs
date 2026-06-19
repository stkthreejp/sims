namespace SIMS.Domain.Entities.Accounting;

public class JournalEntryRollup
{
    public long Id { get; set; }
    public int TenantId { get; set; } = 1;
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public string DriverType { get; set; } = "CSV";   // 'CSV'|'Xero'
    public string Status { get; set; } = "Pending";   // 'Pending'|'Exported'|'Posted'|'Failed'
    public string? ExternalId { get; set; }            // Xero ManualJournalID(s) once posted
    public string? BlobUri { get; set; }               // Azure Blob URI for CSV export
    public string? ErrorMessage { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public ICollection<LedgerTransaction> Transactions { get; set; } = new List<LedgerTransaction>();
}
