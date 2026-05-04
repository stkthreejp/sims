using SIMS.Application.Common;
using SIMS.Application.DTOs.Rating;
using SIMS.Application.DTOs.Quotes;

namespace SIMS.Application.Interfaces.Services;

public interface IShadowRatingService
{
    Task<Result<ShadowRatingResultDto>> ShadowRateAsync(Guid quoteId, RateQuoteRequest request, Guid ratedById);
    Task<IReadOnlyList<ShadowRatingResultDto>> GetResultsAsync(int days, CancellationToken ct = default);
}
