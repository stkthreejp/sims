namespace SIMS.Domain.Entities.Accounting;

/// <summary>
/// A failed journal-entry export queued for retry. Driver-agnostic: the retry worker
/// re-runs the rollup via its stored <see cref="JournalEntryRollup.DriverType"/>.
/// </summary>
public class PendingJournalSync
{
    public long Id { get; set; }
    public int TenantId { get; set; } = 1;
    public long RollupId { get; set; }
    public string Status { get; set; } = "Pending"; // Pending|Processing|Retrying|Done|Failed
    public int AttemptCount { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public JournalEntryRollup? Rollup { get; set; }
}
