using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Underwriting;

public record AuthorityApprovalEvaluationRequest(
    AuthorityApprovalTargetType TargetType,
    Guid TargetId,
    string ActionCode,
    string ActionLabel,
    string RequiredPermission,
    string ApprovalType,
    string Reason,
    string? InputSnapshotJson,
    Guid? AssignedToUserId);

public record AuthorityApprovalEvaluationDto(
    bool Allowed,
    bool RequiresApproval,
    Guid? ApprovalRequestId,
    string Message);

public record AuthorityApprovalRequestDto(
    Guid Id,
    AuthorityApprovalTargetType TargetType,
    Guid TargetId,
    string ActionCode,
    string ActionLabel,
    string RequiredPermission,
    string ApprovalType,
    string Reason,
    AuthorityApprovalStatus Status,
    Guid RequestedById,
    DateTime RequestedAt,
    Guid? AssignedToUserId,
    DateTime? DueAt,
    Guid? DecisionById,
    DateTime? DecisionAt,
    string? DecisionNotes);
