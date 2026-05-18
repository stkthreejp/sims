namespace SIMS.Domain.Entities;

public class PolicyTransactionComplianceChecklist : BaseEntity
{
    public Guid PolicyTransactionId { get; set; }
    public string Purpose { get; set; } = string.Empty;

    public PolicyTransaction PolicyTransaction { get; set; } = null!;
    public ICollection<PolicyTransactionComplianceChecklistItem> Items { get; set; } = new List<PolicyTransactionComplianceChecklistItem>();
}
