namespace SIMS.Domain.Entities.Accounting;

public class FeeAuditLog
{
    public long Id { get; set; }
    public long FeeRuleVersionId { get; set; }
    public Guid EditedBy { get; set; }
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;
    public string ChangeType { get; set; } = string.Empty;  // 'Created'|'Edited'|'Disabled'|'NewVersion'
    public string? FieldChanges { get; set; }  // JSON: { "PercentRate": ["0.04500", "0.04850"] }
    public string? Notes { get; set; }

    public FeeRuleVersion FeeRuleVersion { get; set; } = null!;
}
