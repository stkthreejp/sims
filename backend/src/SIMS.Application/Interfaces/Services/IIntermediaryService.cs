using SIMS.Application.Common;
using SIMS.Application.DTOs.Intermediaries;

namespace SIMS.Application.Interfaces.Services;

public interface IIntermediaryService
{
    Task<IReadOnlyList<IntermediaryListItemDto>> GetAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<Result<IntermediaryDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IntermediaryDto>> CreateAsync(CreateIntermediaryRequest request, CancellationToken ct = default);
    Task<Result<IntermediaryDto>> UpdateAsync(Guid id, UpdateIntermediaryRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result<IntermediaryBrokerageSetupDto>> CreateBrokerageSetupAsync(Guid intermediaryId, UpsertIntermediaryBrokerageSetupRequest request, CancellationToken ct = default);
    Task<Result<IntermediaryBrokerageSetupDto>> UpdateBrokerageSetupAsync(Guid intermediaryId, Guid setupId, UpsertIntermediaryBrokerageSetupRequest request, CancellationToken ct = default);
    Task<Result> DeleteBrokerageSetupAsync(Guid intermediaryId, Guid setupId, CancellationToken ct = default);
}
