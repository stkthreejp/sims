namespace SIMS.Domain.Entities.Accounting;

public class GlAccountMap
{
    public int Id { get; set; }
    public int TenantId { get; set; } = 1;
    public int LedgerAccountId { get; set; }
    public string ExternalSystem { get; set; } = "Xero";   // 'Xero'|'CSV'
    public string ExternalId { get; set; } = string.Empty; // Xero account code or CSV column label
    public bool IsActive { get; set; } = true;

    public LedgerAccount LedgerAccount { get; set; } = null!;
}
