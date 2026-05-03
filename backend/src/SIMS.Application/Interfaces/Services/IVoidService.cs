using SIMS.Application.DTOs.Accounting;

namespace SIMS.Application.Interfaces.Services;

public interface IVoidService
{
    Task<VoidResultDto> VoidReceiptAsync(long receiptId, string? reason, Guid userId, bool isAdmin, CancellationToken ct = default);
    Task<VoidResultDto> VoidCashApplicationAsync(long cashApplicationId, string? reason, Guid userId, bool isAdmin, CancellationToken ct = default);
    Task<VoidResultDto> VoidInvoiceAsync(long invoiceId, string? reason, Guid userId, bool isAdmin, CancellationToken ct = default);
    Task<VoidResultDto> VoidDisbursementAsync(long disbursementId, string? reason, Guid userId, bool isAdmin, CancellationToken ct = default);
}
