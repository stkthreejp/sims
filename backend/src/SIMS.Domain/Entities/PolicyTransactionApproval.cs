namespace SIMS.Domain.Entities;

public class PolicyTransactionApproval : BaseEntity
{
    public Guid PolicyTransactionId { get; set; }
    public string ApprovalType { get; set; } = string.Empty;
    public Guid RequestedById { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public Guid? DecisionById { get; set; }
    public DateTime? DecisionAt { get; set; }
    public string? Decision { get; set; }
    public string? Notes { get; set; }

    public PolicyTransaction PolicyTransaction { get; set; } = null!;
    public User RequestedBy { get; set; } = null!;
    public User? DecisionBy { get; set; }
}
