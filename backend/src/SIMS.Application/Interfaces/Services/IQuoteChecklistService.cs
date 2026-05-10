using SIMS.Application.Common;
using SIMS.Application.DTOs.Quotes;
using SIMS.Domain.Enums;

namespace SIMS.Application.Interfaces.Services;

public interface IQuoteChecklistService
{
    Task<Result<List<QuoteChecklistItemDto>>> GetForQuoteAsync(Guid quoteId);
    Task<Result<QuoteChecklistItemDto>> ToggleAsync(Guid itemId, bool completed, Guid userId, string userName);
    Task SeedDefaultsAsync(Guid quoteId, PolicyLineOfBusiness lob);
}
