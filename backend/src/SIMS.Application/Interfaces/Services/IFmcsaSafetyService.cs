using SIMS.Application.Common;
using SIMS.Application.DTOs.Quotes;

namespace SIMS.Application.Interfaces.Services;

public interface IFmcsaSafetyService
{
    Task<Result<AutoSafetySummaryDto>> GetQuoteAutoSafetyAsync(Guid quoteId, CancellationToken ct = default);
    Task<Result<AutoSafetyRefreshDto>> RefreshQuoteAutoSafetyAsync(Guid quoteId, CancellationToken ct = default);
}
