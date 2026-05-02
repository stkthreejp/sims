namespace SIMS.Domain.Entities.Accounting;

public class Payee
{
    public long Id { get; set; }
    public int TenantId { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public string PayeeType { get; set; } = string.Empty;  // 'Carrier'|'TaxFilingService'|'PremiumFinance'|'Broker'|'Other'
    public string? ExternalReference { get; set; }          // e.g. carrier NAIC code
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
