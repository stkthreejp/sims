using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;

namespace SIMS.Application.Interfaces.Services;

public interface IDisbursementService
{
    Task<PayableAgingDto> GetAgingAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpenPayableDto>> GetOpenPayablesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<DisbursementSummaryDto>> GetDisbursementsAsync(CancellationToken ct = default);
    Task<Result<DisbursementDetailDto>> GetDisbursementAsync(long id, CancellationToken ct = default);

    Task<Result<DisbursementDetailDto>> CreateDisbursementAsync(
        CreateDisbursementRequest req, Guid userId, CancellationToken ct = default);

    Task<Result<DisbursementDetailDto>> PostDisbursementAsync(
        long id, Guid userId, CancellationToken ct = default);

    Task<Result<DisbursementDetailDto>> VoidDisbursementAsync(
        long id, string? reason, Guid userId, CancellationToken ct = default);
}
