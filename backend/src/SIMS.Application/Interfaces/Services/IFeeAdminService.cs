using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;

namespace SIMS.Application.Interfaces.Services;

public interface IFeeAdminService
{
    Task<IReadOnlyList<FeeDefinitionDto>> GetDefinitionsAsync(CancellationToken ct = default);
    Task<Result<FeeDefinitionDto>> GetDefinitionAsync(long id, CancellationToken ct = default);
    Task<Result<FeeDefinitionDto>> CreateDefinitionAsync(CreateFeeDefinitionRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<FeeRuleVersionDto>> GetVersionsAsync(long feeDefinitionId, CancellationToken ct = default);
    Task<Result<FeeRuleVersionDto>> GetVersionAsync(long id, CancellationToken ct = default);
    Task<Result<FeeRuleVersionDto>> CreateVersionAsync(Guid userId, CreateFeeRuleVersionRequest req, CancellationToken ct = default);
    Task<Result<FeeRuleVersionDto>> NewVersionFromExistingAsync(Guid userId, long existingVersionId, CreateFeeRuleVersionRequest req, CancellationToken ct = default);
    Task<Result> DisableVersionAsync(Guid userId, long id, DateOnly disabledDate, string? notes, CancellationToken ct = default);

    Task<Result> SetStateTaxabilityAsync(long feeDefinitionId, SetStateTaxabilityRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<FeeAuditLogDto>> GetAuditLogAsync(long feeRuleVersionId, CancellationToken ct = default);
}
