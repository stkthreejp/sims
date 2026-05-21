using SIMS.Application.DTOs.Underwriting;

namespace SIMS.Application.Interfaces.Services;

public interface IAiGuidelineLlmInterpreterService
{
    Task<IReadOnlyList<CreateUnderwritingGuidelineControlRequest>> InterpretAsync(string guidelineText, CancellationToken ct = default);
}
