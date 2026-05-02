using SIMS.Application.DTOs.Accounting;

namespace SIMS.Application.Interfaces.Services;

public interface IFeeCalculationService
{
    Task<FeeCalculationResult> CalculateAsync(PolicyContext ctx, CancellationToken ct = default);
}
