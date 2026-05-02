namespace SIMS.Domain.Entities.Accounting;

public class FeeDefinition
{
    public long Id { get; set; }
    public int TenantId { get; set; } = 1;
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FeeCategory { get; set; } = string.Empty;  // 'Tax'|'StampingFee'|'PolicyFee'|'BrokerFee'|'Inspection'|'Other'
    public bool IsTaxable { get; set; } = true;
    public int CalculationOrder { get; set; } = 100;
    public int LedgerAccountId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public LedgerAccount LedgerAccount { get; set; } = null!;
    public ICollection<FeeRuleVersion> RuleVersions { get; set; } = new List<FeeRuleVersion>();
    public ICollection<FeeStateTaxability> StateTaxabilities { get; set; } = new List<FeeStateTaxability>();
}
