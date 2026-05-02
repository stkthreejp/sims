namespace SIMS.Domain.Entities.Accounting;

public class DisbursementLine
{
    public long Id { get; set; }
    public long DisbursementId { get; set; }
    public long PayableId { get; set; }
    public decimal Amount { get; set; }

    public Disbursement Disbursement { get; set; } = null!;
    public Payable Payable { get; set; } = null!;
}
