namespace SIMS.Domain.Entities.Accounting;

public class GlAccountMap
{
    public int Id { get; set; }
    public int TenantId { get; set; } = 1;
    public int LedgerAccountId { get; set; }
    public string ExternalSystem { get; set; } = "QBO";   // 'QBO'|'CSV'
    public string ExternalId { get; set; } = string.Empty; // QBO account ID or CSV column label
    public bool IsActive { get; set; } = true;

    public LedgerAccount LedgerAccount { get; set; } = null!;
}
