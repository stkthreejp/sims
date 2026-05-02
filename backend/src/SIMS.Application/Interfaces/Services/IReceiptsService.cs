using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;

namespace SIMS.Application.Interfaces.Services;

public interface IReceiptsService
{
    Task<Result<ReceiptDetailDto>> CreateAsync(CreateReceiptRequest req, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ReceiptSummaryDto>> GetReceiptsAsync(CancellationToken ct = default);
    Task<Result<ReceiptDetailDto>> GetReceiptAsync(long id, CancellationToken ct = default);
}
