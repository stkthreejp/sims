using SIMS.Application.Common;
using SIMS.Application.DTOs.Rating;
using SIMS.Application.DTOs.Quotes;
using SIMS.Domain.Enums;

namespace SIMS.Application.Interfaces.Services;

public interface IShadowRatingService
{
    Task<Result<ShadowRatingResultDto>> ShadowRateAsync(Guid quoteId, RateQuoteRequest request, Guid ratedById);
    Task<IReadOnlyList<ShadowRatingResultDto>> GetResultsAsync(int days, CancellationToken ct = default);
    Task<bool> IsShadowModeEnabledForLobAsync(PolicyLineOfBusiness lob, CancellationToken ct = default);
    Task<ShadowSettingsDto> GetShadowSettingsAsync(CancellationToken ct = default);
    Task SetShadowModeForLobAsync(PolicyLineOfBusiness lob, bool enabled, CancellationToken ct = default);
}
