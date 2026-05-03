namespace SIMS.Domain.Entities.Accounting;

public class PeriodCloseChecklistItem
{
    public long Id { get; set; }
    public int TenantId { get; set; } = 1;
    public long PeriodId { get; set; }
    public string CheckKey { get; set; } = string.Empty;  // 'PendingSync'|'UnappliedCash'|'OpenRecs'
    public int IssueCount { get; set; }
    public bool IsBlocking { get; set; }
    public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;

    public AccountingPeriod Period { get; set; } = null!;
}
