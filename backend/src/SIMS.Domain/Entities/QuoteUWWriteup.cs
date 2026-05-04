using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class QuoteUWWriteup : BaseEntity
{
    public Guid QuoteId { get; set; }
    public UWWriteupStatus Status { get; set; } = UWWriteupStatus.Draft;
    public UWWriteupDecision? Decision { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public int SchemaVersion { get; set; } = 1;
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }

    public Quote Quote { get; set; } = null!;
    public User? SubmittedBy { get; set; }
    public User? ApprovedBy { get; set; }
    public ICollection<QuoteUWWriteupCondition> Conditions { get; set; } = new List<QuoteUWWriteupCondition>();
}
