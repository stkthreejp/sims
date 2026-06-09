using SIMS.Application.Common;
using SIMS.Application.DTOs.Underwriting;

namespace SIMS.Application.Interfaces.Services;

public interface IProgramConfigurationService
{
    Task<IReadOnlyList<ProgramConfigurationDto>> GetAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<Result<ProgramConfigurationDto>> CreateAsync(CreateProgramConfigurationRequest request, CancellationToken ct = default);
    Task<Result<ProgramConfigurationDto>> UpdateAsync(Guid id, UpdateProgramConfigurationRequest request, CancellationToken ct = default);
    Task<Result<ProgramCarrierDto>> AddCarrierAsync(Guid programId, UpsertProgramCarrierRequest request, CancellationToken ct = default);
    Task<Result<ProgramCarrierDto>> UpdateCarrierAsync(Guid programId, Guid programCarrierId, UpsertProgramCarrierRequest request, CancellationToken ct = default);
    Task<Result<ProgramCarrierLineOfBusinessDto>> AddLineOfBusinessAsync(Guid programId, Guid programCarrierId, UpsertProgramCarrierLineOfBusinessRequest request, CancellationToken ct = default);
    Task<Result<ProgramCarrierLineOfBusinessDto>> UpdateLineOfBusinessAsync(Guid programId, Guid programCarrierId, Guid programCarrierLobId, UpsertProgramCarrierLineOfBusinessRequest request, CancellationToken ct = default);
    Task<Result<ProgramCarrierLobStateDto>> AddStateAsync(Guid programId, Guid programCarrierId, Guid programCarrierLobId, UpsertProgramCarrierLobStateRequest request, CancellationToken ct = default);
    Task<Result<ProgramCarrierLobStateDto>> UpdateStateAsync(Guid programId, Guid programCarrierId, Guid programCarrierLobId, Guid stateId, UpsertProgramCarrierLobStateRequest request, CancellationToken ct = default);
    Task<Result<ProgramCarrierLobStateDto>> CopyStateAsync(Guid programId, Guid programCarrierId, Guid programCarrierLobId, CopyProgramCarrierLobStateRequest request, CancellationToken ct = default);
    Task<ProgramOrphanAuditDto> GetOrphanAuditAsync(CancellationToken ct = default);
}
