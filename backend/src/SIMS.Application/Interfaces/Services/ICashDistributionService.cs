using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Interfaces.Services;

public interface ICashDistributionService
{
    /// <summary>
    /// Called internally by CashApplicationService after each application is saved.
    /// Creates Pending CashMovementInstruction rows for every payable invoice line.
    /// </summary>
    Task GenerateInstructionsForApplicationAsync(
        CashApplication application,
        Invoice invoiceWithLines,
        int trustGlAccountId,
        Guid userId,
        CancellationToken ct = default);

    Task<IReadOnlyList<NettedPayeeDto>> GetPendingAsync(CancellationToken ct = default);

    Task<Result<BatchDetailDto>> CreateBatchAsync(
        CreateBatchRequest req, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<BatchSummaryDto>> GetBatchesAsync(CancellationToken ct = default);

    Task<Result<BatchDetailDto>> GetBatchAsync(long id, CancellationToken ct = default);

    Task<Result<BatchDetailDto>> MarkExecutedAsync(
        long batchId, MarkExecutedRequest req, Guid userId, CancellationToken ct = default);

    Task<Result<string>> GetBatchPdfDownloadUrlAsync(long batchId, CancellationToken ct = default);
}
