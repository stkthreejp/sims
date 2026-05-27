using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Security;

namespace SIMS.Application.Interfaces.Services;

public interface IQuoteService
{
    Task<PagedResult<QuoteListItemDto>> GetAllAsync(QueryParameters query, UserAccessScope access);
    Task<IEnumerable<QuoteListItemDto>> GetBySubmissionAsync(Guid submissionId, UserAccessScope access);
    Task<IEnumerable<QuoteListItemDto>> GetBoundByInsuredAsync(Guid insuredId);
    Task<Result<QuoteDto>> GetByIdAsync(Guid id, UserAccessScope access);
    Task<Result<QuoteDto>> CreateAsync(QuoteCreateDto dto, Guid createdById, UserAccessScope? access = null);
    Task<Result<QuoteDto>> UpdateAsync(Guid id, QuoteUpdateDto dto, UserAccessScope access);
    Task<Result<InvoicePreviewDto>> GetInvoicePreviewAsync(Guid id, UserAccessScope access);
    Task<Result<QuoteDto>> BindAsync(Guid id, QuoteBindDto dto, UserAccessScope access);
    Task<Result<QuoteDto>> ApplyCommissionOverrideAsync(Guid id, CommissionOverrideRequest req, UserAccessScope access);
    Task<Result> DeleteAsync(Guid id, UserAccessScope access);
}
