using SIMS.Application.Common;
using SIMS.Application.DTOs.Quotes;

namespace SIMS.Application.Interfaces.Services;

public interface IQuotePolicyFormSelectionService
{
    Task<Result<IReadOnlyList<QuotePolicyFormSelectionDto>>> GetOrSeedAsync(Guid quoteId);
    Task<Result<IReadOnlyList<QuotePolicyFormSelectionDto>>> SaveAsync(Guid quoteId, IReadOnlyList<QuotePolicyFormSelectionUpsertDto> forms);
    Task<Result<IReadOnlyList<QuotePolicyFormSelectionDto>>> ResetFromPackageAsync(Guid quoteId);
}
