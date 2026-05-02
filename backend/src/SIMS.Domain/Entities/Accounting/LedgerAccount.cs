namespace SIMS.Domain.Entities.Accounting;

public class LedgerAccount
{
    public int Id { get; set; }
    public int TenantId { get; set; } = 1;
    public string InternalCode { get; set; } = string.Empty;   // e.g. "1100"
    public string ExternalLabel { get; set; } = string.Empty;  // e.g. "Cash — Trust Account"
    public string AccountType { get; set; } = string.Empty;    // 'Asset'|'Liability'|'Revenue'|'Expense'
    public int? ParentId { get; set; }
    public bool IsActive { get; set; } = true;

    public LedgerAccount? Parent { get; set; }
    public ICollection<LedgerAccount> Children { get; set; } = new List<LedgerAccount>();
    public ICollection<LedgerTransaction> Transactions { get; set; } = new List<LedgerTransaction>();
}
