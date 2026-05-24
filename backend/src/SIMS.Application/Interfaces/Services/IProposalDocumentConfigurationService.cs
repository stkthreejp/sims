using SIMS.Application.Common;
using SIMS.Application.DTOs.ProposalDocuments;

namespace SIMS.Application.Interfaces.Services;

public interface IProposalDocumentConfigurationService
{
    Task<IReadOnlyList<ProposalDocumentConfigurationDto>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<Result<ProposalDocumentConfigurationDto>> CreateAsync(UpsertProposalDocumentConfigurationRequest request, CancellationToken ct = default);
    Task<Result<ProposalDocumentConfigurationDto>> UpdateAsync(Guid id, UpsertProposalDocumentConfigurationRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result<ProposalDocumentSelectionDto>> ResolveForQuoteAsync(Guid quoteId, CancellationToken ct = default);
}
