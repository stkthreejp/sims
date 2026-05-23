using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class AuthorityApprovalService : IAuthorityApprovalService
{
    private readonly DbContext _db;

    public AuthorityApprovalService(DbContext db) => _db = db;

    public async Task<AuthorityApprovalEvaluationDto> EvaluateAsync(
        AuthorityApprovalEvaluationRequest request,
        IReadOnlyCollection<string> currentUserPermissions,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        if (currentUserPermissions.Contains(request.RequiredPermission, StringComparer.OrdinalIgnoreCase))
            return new AuthorityApprovalEvaluationDto(true, false, null, $"{request.ActionLabel} is within authority.");

        var existingApproved = await MatchingRequests(request)
            .Where(r => r.Status == AuthorityApprovalStatus.Approved)
            .OrderByDescending(r => r.DecisionAt)
            .FirstOrDefaultAsync(ct);

        if (existingApproved is not null)
            return new AuthorityApprovalEvaluationDto(true, false, existingApproved.Id, $"{request.ActionLabel} has approved authority.");

        var pending = await MatchingRequests(request)
            .Where(r => r.Status == AuthorityApprovalStatus.Pending)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(ct);

        if (pending is null)
        {
            pending = new AuthorityApprovalRequest
            {
                TargetType = request.TargetType,
                TargetId = request.TargetId,
                ActionCode = request.ActionCode.Trim(),
                ActionLabel = request.ActionLabel.Trim(),
                RequiredPermission = request.RequiredPermission.Trim(),
                ApprovalType = request.ApprovalType.Trim(),
                Reason = request.Reason.Trim(),
                InputSnapshotJson = string.IsNullOrWhiteSpace(request.InputSnapshotJson) ? null : request.InputSnapshotJson,
                RequestedById = currentUserId,
                AssignedToUserId = request.AssignedToUserId
            };
            _db.Set<AuthorityApprovalRequest>().Add(pending);
            await _db.SaveChangesAsync(ct);
        }

        return new AuthorityApprovalEvaluationDto(
            false,
            true,
            pending.Id,
            $"Approval required for {request.ActionLabel}.");
    }

    public async Task<AuthorityApprovalRequestDto> DecideAsync(
        Guid approvalRequestId,
        AuthorityApprovalStatus decision,
        Guid decisionById,
        string? notes,
        CancellationToken ct = default)
    {
        if (decision is not AuthorityApprovalStatus.Approved and not AuthorityApprovalStatus.Declined and not AuthorityApprovalStatus.Cancelled)
            throw new InvalidOperationException("Authority approval decision must close the request.");

        var request = await _db.Set<AuthorityApprovalRequest>().FirstOrDefaultAsync(r => r.Id == approvalRequestId, ct)
            ?? throw new InvalidOperationException("Authority approval request was not found.");

        if (request.Status != AuthorityApprovalStatus.Pending)
            throw new InvalidOperationException("Only pending authority approval requests can be decided.");

        request.Status = decision;
        request.DecisionById = decisionById;
        request.DecisionAt = DateTime.UtcNow;
        request.DecisionNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        request.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Map(request);
    }

    private IQueryable<AuthorityApprovalRequest> MatchingRequests(AuthorityApprovalEvaluationRequest request) =>
        _db.Set<AuthorityApprovalRequest>().Where(r =>
            r.TargetType == request.TargetType
            && r.TargetId == request.TargetId
            && r.ActionCode == request.ActionCode
            && r.ApprovalType == request.ApprovalType);

    private static AuthorityApprovalRequestDto Map(AuthorityApprovalRequest request) => new(
        request.Id,
        request.TargetType,
        request.TargetId,
        request.ActionCode,
        request.ActionLabel,
        request.RequiredPermission,
        request.ApprovalType,
        request.Reason,
        request.Status,
        request.RequestedById,
        request.RequestedAt,
        request.AssignedToUserId,
        request.DueAt,
        request.DecisionById,
        request.DecisionAt,
        request.DecisionNotes);
}
