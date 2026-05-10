using SIMS.Application.Common;
using SIMS.Application.DTOs.Fmcsa;

namespace SIMS.Application.Interfaces.Services;

public interface IFmcsaInspectionEnrichmentService
{
    Task<Result<FmcsaInspectionEnrichmentDto>> EnrichRecentInspectionsAsync(int maxInspections = 50, CancellationToken ct = default);
}
