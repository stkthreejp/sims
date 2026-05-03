using SIMS.Application.Common;
using SIMS.Application.DTOs.Quotes;

namespace SIMS.Application.Interfaces.Services;

public interface IRatingEngineService
{
    Task<Result<RatingResultDto>> RateAsync(Guid quoteId, RateQuoteRequest request, Guid ratedById);
}
