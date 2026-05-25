using SIMS.Application.Common;
using SIMS.Application.DTOs.Bordereaux;
using SIMS.Domain.Enums;

namespace SIMS.Application.Interfaces.Services;

public interface IBordereauxService
{
    Task<IReadOnlyList<BordereauxProfileDto>> GetProfilesAsync(
        bool includeInactive = false,
        Guid? programId = null,
        Guid? carrierId = null,
        BordereauxReportType? reportType = null,
        BordereauxOutputFormat? outputFormat = null,
        CancellationToken ct = default);

    Task<Result<BordereauxProfileDto>> GetProfileAsync(Guid id, CancellationToken ct = default);
    Task<Result<BordereauxProfileDto>> CreateProfileAsync(UpsertBordereauxProfileRequest request, CancellationToken ct = default);
    Task<Result<BordereauxProfileDto>> UpdateProfileAsync(Guid id, UpsertBordereauxProfileRequest request, CancellationToken ct = default);
}
