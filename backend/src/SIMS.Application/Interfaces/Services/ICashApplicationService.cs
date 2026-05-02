using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;

namespace SIMS.Application.Interfaces.Services;

public interface ICashApplicationService
{
    Task<IReadOnlyList<OpenInvoiceDto>> GetOpenInvoicesAsync(CancellationToken ct = default);
    Task<Result<ApplyCashResultDto>> ApplyAsync(ApplyCashRequest req, Guid userId, CancellationToken ct = default);
}
