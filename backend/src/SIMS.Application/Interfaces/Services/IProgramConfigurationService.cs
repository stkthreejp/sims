using SIMS.Application.Common;
using SIMS.Application.DTOs.Underwriting;

namespace SIMS.Application.Interfaces.Services;

public interface IProgramConfigurationService
{
    Task<IReadOnlyList<ProgramConfigurationDto>> GetAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<Result<ProgramConfigurationDto>> CreateAsync(CreateProgramConfigurationRequest request, CancellationToken ct = default);
    Task<Result<ProgramConfigurationDto>> UpdateAsync(Guid id, UpdateProgramConfigurationRequest request, CancellationToken ct = default);
}
