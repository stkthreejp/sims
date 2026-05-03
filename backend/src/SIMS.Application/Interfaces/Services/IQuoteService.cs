using SIMS.Application.Common;
using SIMS.Application.DTOs.Quotes;

namespace SIMS.Application.Interfaces.Services;

public interface IQuoteService
{
    Task<PagedResult<QuoteListItemDto>> GetAllAsync(QueryParameters query);
    Task<IEnumerable<QuoteListItemDto>> GetBySubmissionAsync(Guid submissionId);
    Task<IEnumerable<QuoteListItemDto>> GetBoundByInsuredAsync(Guid insuredId);
    Task<Result<QuoteDto>> GetByIdAsync(Guid id);
    Task<Result<QuoteDto>> CreateAsync(QuoteCreateDto dto, Guid createdById);
    Task<Result<QuoteDto>> UpdateAsync(Guid id, QuoteUpdateDto dto);
    Task<Result<QuoteDto>> BindAsync(Guid id, QuoteBindDto dto, Guid userId);
    Task<Result<QuoteDto>> ApplyCommissionOverrideAsync(Guid id, CommissionOverrideRequest req, Guid userId);
    Task<Result> DeleteAsync(Guid id);
}
