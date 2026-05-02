using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;

namespace SIMS.Application.Interfaces.Services;

public interface IInvoicingService
{
    Task<Result<InvoiceDetailDto>> BindAsync(CreateInvoiceRequest req, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<InvoiceSummaryDto>> GetInvoicesAsync(CancellationToken ct = default);
    Task<Result<InvoiceDetailDto>> GetInvoiceAsync(long id, CancellationToken ct = default);
}
