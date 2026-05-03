using SIMS.Application.Common;
using SIMS.Application.DTOs;

namespace SIMS.Application.Interfaces.Services;

public interface ICarrierCommissionService
{
    Task<IReadOnlyList<CarrierCommissionDto>> GetAllAsync(Guid carrierId, CancellationToken ct = default);
    Task<Result<CarrierCommissionDto>> CreateAsync(Guid carrierId, CreateCarrierCommissionRequest req, Guid userId, CancellationToken ct = default);
    Task<Result<CarrierCommissionDto>> DisableAsync(long id, DateOnly? disabledDate, CancellationToken ct = default);

    // Returns both the total carrier commission rate and the SMM retention rate
    Task<CarrierCommissionRates?> GetActiveRatesAsync(Guid carrierId, string? lineOfBusiness, DateOnly asOfDate, CancellationToken ct = default);
}
