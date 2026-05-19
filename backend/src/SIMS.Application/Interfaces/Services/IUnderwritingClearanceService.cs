using SIMS.Application.DTOs.Underwriting;

namespace SIMS.Application.Interfaces.Services;

public interface IUnderwritingClearanceService
{
    Task<UnderwritingClearanceEvaluationDto?> GetLatestSubmissionAsync(
        Guid submissionId,
        CancellationToken ct = default);

    Task<UnderwritingClearanceEvaluationDto> EvaluateSubmissionAsync(
        Guid submissionId,
        Guid reviewerId,
        CancellationToken ct = default);

    Task<UnderwritingClearanceEvaluationDto> OverrideSubmissionAsync(
        Guid submissionId,
        Guid overriddenById,
        string reason,
        CancellationToken ct = default);
}
