using SIMS.Application.Common;
using SIMS.Application.DTOs.Fmcsa;

namespace SIMS.Application.Interfaces.Services;

public interface IFmcsaSafetyAnalyticsService
{
    Task<Result<FmcsaAnalyticsRefreshDto>> RefreshImportedCarrierAnalyticsAsync(string? snapshotMonth = null, CancellationToken ct = default);
    Task<Result<FmcsaAnalyticsRefreshDto>> RefreshOfficialSmsPeerAnalyticsAsync(string? snapshotMonth = null, int? maxRowsPerDataset = null, CancellationToken ct = default);
}
