namespace SIMS.Domain.Entities.Accounting;

public class InvoiceLine
{
    public long Id { get; set; }
    public long InvoiceId { get; set; }
    public long? FeeRuleVersionId { get; set; }
    public string FeeCode { get; set; } = string.Empty;
    public string FeeDisplayName { get; set; } = string.Empty;
    public string FeeCategory { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsTaxable { get; set; }
    public int LedgerAccountId { get; set; }
    public string? PayableRouting { get; set; }
    public long? PayablePayeeId { get; set; }

    public Invoice Invoice { get; set; } = null!;
    public LedgerAccount LedgerAccount { get; set; } = null!;
}
