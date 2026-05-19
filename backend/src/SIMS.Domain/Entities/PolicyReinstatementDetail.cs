namespace SIMS.Domain.Entities;

public class PolicyReinstatementDetail : BaseEntity
{
    public Guid PolicyTransactionId { get; set; }
    public DateOnly ReinstatementEffectiveDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public PolicyTransaction PolicyTransaction { get; set; } = null!;
}
