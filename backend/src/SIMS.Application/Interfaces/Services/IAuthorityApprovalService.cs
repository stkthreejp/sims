using SIMS.Application.DTOs.Underwriting;
using SIMS.Domain.Enums;

namespace SIMS.Application.Interfaces.Services;

public interface IAuthorityApprovalService
{
    Task<AuthorityApprovalEvaluationDto> EvaluateAsync(
        AuthorityApprovalEvaluationRequest request,
        IReadOnlyCollection<string> currentUserPermissions,
        Guid currentUserId,
        CancellationToken ct = default);

    Task<AuthorityApprovalRequestDto> DecideAsync(
        Guid approvalRequestId,
        AuthorityApprovalStatus decision,
        Guid decisionById,
        string? notes,
        CancellationToken ct = default);
}
