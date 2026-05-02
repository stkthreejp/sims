namespace SIMS.Domain.Entities.Accounting;

public class FeeStateTaxability
{
    public long Id { get; set; }
    public long FeeDefinitionId { get; set; }
    public string StateCode { get; set; } = string.Empty;
    public bool IsTaxable { get; set; } = false;  // override: this state does NOT tax this fee

    public FeeDefinition FeeDefinition { get; set; } = null!;
}
