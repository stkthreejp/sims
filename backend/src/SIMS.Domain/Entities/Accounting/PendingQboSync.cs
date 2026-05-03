namespace SIMS.Domain.Entities.Accounting;

public class PendingQboSync
{
    public long Id { get; set; }
    public int TenantId { get; set; } = 1;
    public long RollupId { get; set; }
    public string Status { get; set; } = "Pending"; // Pending|Retrying|Done|Failed
    public int AttemptCount { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public JournalEntryRollup? Rollup { get; set; }
}
