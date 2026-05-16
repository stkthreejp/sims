namespace SIMS.Domain.Entities;

public class PolicyNumberSequenceUsage : BaseEntity
{
    public Guid PolicyNumberSequenceId { get; set; }
    public Guid? PolicyNumberAssignmentId { get; set; }
    public Guid QuoteId { get; set; }
    public Guid? PolicyId { get; set; }
    public string BasePolicyNumber { get; set; } = string.Empty;
    public string FullPolicyNumber { get; set; } = string.Empty;
    public long SequenceValue { get; set; }
    public int TermNumber { get; set; }
    public bool WasManualOverride { get; set; }
    public Guid AssignedById { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public PolicyNumberSequence PolicyNumberSequence { get; set; } = null!;
    public PolicyNumberAssignment? PolicyNumberAssignment { get; set; }
    public Quote Quote { get; set; } = null!;
    public Policy? Policy { get; set; }
    public User AssignedBy { get; set; } = null!;
}
