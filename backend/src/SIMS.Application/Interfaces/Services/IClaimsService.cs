using SIMS.Application.Common;
using SIMS.Application.DTOs.Claims;
using SIMS.Domain.Enums;

namespace SIMS.Application.Interfaces.Services;

public interface IClaimsService
{
    Task<IReadOnlyList<ClaimListItemDto>> GetClaimsAsync(
        Guid? policyId = null,
        Guid? insuredId = null,
        ClaimStatus? status = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default);

    Task<Result<ClaimDto>> GetClaimAsync(Guid id, CancellationToken ct = default);

    Task<Result<ClaimDto>> CreateClaimAsync(UpsertClaimRequest request, Guid createdById, CancellationToken ct = default);

    Task<Result<ClaimDto>> UpdateClaimAsync(Guid id, UpsertClaimRequest request, CancellationToken ct = default);

    Task<Result<ClaimImportBatchDto>> ImportClaimsAsync(ImportClaimsRequest request, Guid importedById, CancellationToken ct = default);

    Task<IReadOnlyList<ClaimImportBatchDto>> GetImportBatchesAsync(CancellationToken ct = default);

    Task<Result<LossRunDto>> GetLossRunAsync(Guid? insuredId, Guid? policyId, DateOnly asOfDate, CancellationToken ct = default);
}
