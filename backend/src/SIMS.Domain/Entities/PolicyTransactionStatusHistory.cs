using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class PolicyTransactionStatusHistory : BaseEntity
{
    public Guid PolicyTransactionId { get; set; }
    public PolicyTransactionStatus? FromStatus { get; set; }
    public PolicyTransactionStatus ToStatus { get; set; }
    public string EventName { get; set; } = string.Empty;
    public Guid ChangedById { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public PolicyTransaction PolicyTransaction { get; set; } = null!;
    public User ChangedBy { get; set; } = null!;
}
