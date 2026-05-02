namespace SIMS.Domain.Entities.Accounting;

public class AccountingPeriod
{
    public long Id { get; set; }
    public int TenantId { get; set; } = 1;
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public string Status { get; set; } = "Open";  // 'Open'|'Closing'|'Closed'|'Reopened'
    public Guid? ClosedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ReopenedBy { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public string? Notes { get; set; }
}
