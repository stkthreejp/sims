using SIMS.Application.Common;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Domain.Enums;

namespace SIMS.Application.Interfaces.Services;

public interface IUnderwritingControlEnforcementService
{
    Task<UnderwritingControlEvaluationSummaryDto> EvaluateQuoteAsync(Guid quoteId, UnderwritingControlStage stage, Guid evaluatedByUserId, CancellationToken ct = default);
    Task<UnderwritingControlEvaluationSummaryDto> EvaluatePolicyAsync(Guid policyId, UnderwritingControlStage stage, Guid evaluatedByUserId, CancellationToken ct = default);
    Task<IReadOnlyList<UnderwritingControlEnforcementResultDto>> GetForTargetAsync(UnderwritingControlTargetType targetType, Guid targetId, CancellationToken ct = default);
    Task<Result<UnderwritingControlEnforcementResultDto>> OverrideAsync(Guid resultId, Guid userId, string reason, CancellationToken ct = default);
}
