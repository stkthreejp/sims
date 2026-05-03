namespace SIMS.Domain.Entities.Accounting;

public class PayeeStatementLine
{
    public long Id { get; set; }
    public long PayeeStatementId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string StateCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string MatchStatus { get; set; } = "Unmatched";  // Unmatched | AutoMatched | ManualMatched
    public long? MatchedInvoiceLineId { get; set; }
    public Guid? ReconciliationTransactionId { get; set; }

    public PayeeStatement Statement { get; set; } = null!;
    public InvoiceLine? MatchedInvoiceLine { get; set; }
}
