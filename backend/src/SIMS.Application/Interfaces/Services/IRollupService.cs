using SIMS.Application.DTOs.Accounting;

namespace SIMS.Application.Interfaces.Services;

public interface IRollupService
{
    /// <summary>
    /// Groups all unrolled LedgerTransactions in the given period into a new JournalEntryRollup,
    /// invokes the specified driver, and returns the created rollup.
    /// </summary>
    Task<RollupDto> RollupPeriodAsync(int year, int month, string driverType, Guid userId, CancellationToken ct = default);

    /// <summary>Re-exports an existing rollup without changing which transactions belong to it.</summary>
    Task<RollupDto> ResyncAsync(long rollupId, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<RollupSummaryDto>> GetRollupsAsync(CancellationToken ct = default);
    Task<RollupDto?> GetRollupAsync(long id, CancellationToken ct = default);
    Task<string> GetDownloadUrlAsync(long rollupId, CancellationToken ct = default);
}
