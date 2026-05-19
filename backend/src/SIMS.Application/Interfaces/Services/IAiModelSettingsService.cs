using SIMS.Application.Common;
using SIMS.Application.DTOs.Ai;

namespace SIMS.Application.Interfaces.Services;

public interface IAiModelSettingsService
{
    Task EnsureDefaultsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AiModelRegistryDto>> GetModelsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AiUseCaseModelSettingDto>> GetSettingsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AiModelSettingAuditLogDto>> GetAuditLogAsync(CancellationToken ct = default);
    Task<Result<AiUseCaseModelSettingDto>> UpdateUseCaseModelAsync(
        string useCase,
        Guid aiModelRegistryId,
        Guid userId,
        string changeReason,
        string? promptVersion = null,
        CancellationToken ct = default);
}
