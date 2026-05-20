using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Underwriting;

public record UnderwritingControlEnforcementResultDto(
    Guid Id,
    Guid GuidelineControlId,
    UnderwritingControlTargetType TargetType,
    Guid TargetId,
    UnderwritingControlStage Stage,
    UnderwritingControlEvaluationStatus Status,
    bool IsBlocking,
    bool OverrideAllowed,
    string? OverridePermission,
    string Message,
    string RuleKey,
    string Label,
    string? SourceCitation,
    string? ConditionJson,
    string? InputSnapshotJson,
    DateTime EvaluatedAt,
    Guid? OverriddenByUserId,
    DateTime? OverriddenAt,
    string? OverrideReason);

public record UnderwritingControlEvaluationSummaryDto(
    IReadOnlyList<UnderwritingControlEnforcementResultDto> Results)
{
    public bool HasBlockingResults => Results.Any(r => r.Status == UnderwritingControlEvaluationStatus.Blocked && r.IsBlocking);
    public IReadOnlyList<UnderwritingControlEnforcementResultDto> BlockingResults =>
        Results.Where(r => r.Status == UnderwritingControlEvaluationStatus.Blocked && r.IsBlocking).ToList();
}

public record UnderwritingControlOverrideRequest(string Reason);
