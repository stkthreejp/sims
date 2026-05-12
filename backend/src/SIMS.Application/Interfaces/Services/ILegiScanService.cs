using SIMS.Application.Common;
using SIMS.Application.DTOs.Legal;

namespace SIMS.Application.Interfaces.Services;

public interface ILegiScanService
{
    Task<LegiScanStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LegiScanTrackedBillDto>> GetTrackedBillsAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<LegiScanTrackedBillDto>>> AddToMonitorAsync(int[] billIds, string? stance, CancellationToken ct = default);
    Task<Result> RemoveFromMonitorAsync(int billId, CancellationToken ct = default);
    Task<Result<LegiScanSyncResultDto>> SyncMonitorAsync(Guid? startedById, string? startedByName, CancellationToken ct = default);
}
