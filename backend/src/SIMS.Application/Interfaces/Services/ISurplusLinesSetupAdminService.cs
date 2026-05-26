using SIMS.Application.Common;
using SIMS.Application.DTOs.SurplusLines;

namespace SIMS.Application.Interfaces.Services;

public interface ISurplusLinesSetupAdminService
{
    Task<IReadOnlyList<SurplusLinesStateSetupDto>> GetAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<Result<SurplusLinesStateSetupDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<SurplusLinesStateSetupDto>> CreateAsync(UpsertSurplusLinesStateSetupRequest request, CancellationToken ct = default);
    Task<Result<SurplusLinesStateSetupDto>> UpdateAsync(Guid id, UpsertSurplusLinesStateSetupRequest request, CancellationToken ct = default);
    Task<Result<SurplusLinesStateSetupDto>> CopyAsync(Guid sourceSetupId, CopySurplusLinesStateSetupRequest request, CancellationToken ct = default);
}
