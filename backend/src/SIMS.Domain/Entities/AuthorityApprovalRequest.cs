using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class AuthorityApprovalRequest : BaseEntity
{
    public AuthorityApprovalTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public string RequiredPermission { get; set; } = string.Empty;
    public string ApprovalType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? InputSnapshotJson { get; set; }
    public AuthorityApprovalStatus Status { get; set; } = AuthorityApprovalStatus.Pending;
    public Guid RequestedById { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public Guid? AssignedToUserId { get; set; }
    public DateTime? DueAt { get; set; }
    public Guid? DecisionById { get; set; }
    public DateTime? DecisionAt { get; set; }
    public string? DecisionNotes { get; set; }

    public User RequestedBy { get; set; } = null!;
    public User? AssignedToUser { get; set; }
    public User? DecisionBy { get; set; }
}
