using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class UnderwritingControlEnforcementResult : BaseEntity
{
    public Guid GuidelineControlId { get; set; }
    public UnderwritingControlTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public UnderwritingControlStage Stage { get; set; }
    public UnderwritingControlEvaluationStatus Status { get; set; }
    public bool IsBlocking { get; set; }
    public bool OverrideAllowed { get; set; }
    public string? OverridePermission { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ConditionJson { get; set; }
    public string? InputSnapshotJson { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public Guid? OverriddenByUserId { get; set; }
    public DateTime? OverriddenAt { get; set; }
    public string? OverrideReason { get; set; }

    public UnderwritingGuidelineControl GuidelineControl { get; set; } = null!;
    public User? OverriddenByUser { get; set; }
}
