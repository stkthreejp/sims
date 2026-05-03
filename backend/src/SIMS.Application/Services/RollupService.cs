using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Services;

public class RollupService : IRollupService
{
    private readonly IServiceProvider _sp;
    private readonly IEnumerable<IJournalDriver> _drivers;
    private readonly IBlobStorageService _blob;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public RollupService(IServiceProvider sp, IEnumerable<IJournalDriver> drivers, IBlobStorageService blob)
    {
        _sp = sp;
        _drivers = drivers;
        _blob = blob;
    }

    public async Task<RollupDto> RollupPeriodAsync(
        int year, int month, string driverType, Guid userId, CancellationToken ct = default)
    {
        var db = Db;
        var driver = GetDriver(driverType);

        var fromDate = new DateOnly(year, month, 1);
        var toDate = fromDate.AddMonths(1).AddDays(-1);

        // Load all unrolled posted transactions in this period
        var txns = await db.Set<LedgerTransaction>()
            .Include(t => t.Account)
            .Where(t => t.TenantId == 1
                && t.RolledUpIn == null
                && t.PostingStatus == "Posted"
                && t.EffectiveDate >= fromDate
                && t.EffectiveDate <= toDate)
            .OrderBy(t => t.EffectiveDate).ThenBy(t => t.TransactionId).ThenBy(t => t.Id)
            .ToListAsync(ct);

        if (txns.Count == 0)
            throw new InvalidOperationException($"No unrolled transactions found for {year}-{month:D2}");

        // Create the rollup record
        var rollup = new JournalEntryRollup
        {
            PeriodYear = year,
            PeriodMonth = month,
            DriverType = driverType,
            Status = "Pending",
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
        };
        db.Set<JournalEntryRollup>().Add(rollup);
        await db.SaveChangesAsync(ct);

        // Assign transactions to this rollup
        foreach (var t in txns) t.RolledUpIn = rollup.Id;
        await db.SaveChangesAsync(ct);

        // Build lines with external account labels
        var lines = await BuildLinesAsync(txns, driverType, ct);

        // Invoke the driver
        await driver.ExportAsync(rollup, lines, ct);
        await db.SaveChangesAsync(ct);

        return MapRollup(rollup, txns.Select(t => t.TransactionId).Distinct().Count(), txns.Count);
    }

    public async Task<RollupDto> ResyncAsync(long rollupId, Guid userId, CancellationToken ct = default)
    {
        var db = Db;
        var rollup = await db.Set<JournalEntryRollup>()
            .FirstOrDefaultAsync(r => r.Id == rollupId, ct)
            ?? throw new InvalidOperationException($"Rollup {rollupId} not found");

        var driver = GetDriver(rollup.DriverType);

        var txns = await db.Set<LedgerTransaction>()
            .Include(t => t.Account)
            .Where(t => t.RolledUpIn == rollupId)
            .OrderBy(t => t.EffectiveDate).ThenBy(t => t.TransactionId).ThenBy(t => t.Id)
            .ToListAsync(ct);

        rollup.Status = "Pending";
        rollup.ErrorMessage = null;
        rollup.CompletedAt = null;
        await db.SaveChangesAsync(ct);

        var lines = await BuildLinesAsync(txns, rollup.DriverType, ct);
        await driver.ExportAsync(rollup, lines, ct);
        await db.SaveChangesAsync(ct);

        return MapRollup(rollup, txns.Select(t => t.TransactionId).Distinct().Count(), txns.Count);
    }

    public async Task<IReadOnlyList<RollupSummaryDto>> GetRollupsAsync(CancellationToken ct = default)
    {
        var rollups = await Db.Set<JournalEntryRollup>()
            .OrderByDescending(r => r.PeriodYear).ThenByDescending(r => r.PeriodMonth).ThenByDescending(r => r.Id)
            .ToListAsync(ct);

        var counts = await Db.Set<LedgerTransaction>()
            .Where(t => t.RolledUpIn != null)
            .GroupBy(t => t.RolledUpIn!)
            .Select(g => new { RollupId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var countMap = counts.ToDictionary(c => c.RollupId!.Value, c => c.Count);

        return rollups.Select(r => new RollupSummaryDto(
            r.Id, r.PeriodYear, r.PeriodMonth, r.DriverType, r.Status,
            countMap.GetValueOrDefault(r.Id, 0),
            r.ExternalId, r.BlobUri, r.ErrorMessage, r.CreatedAt, r.CompletedAt
        )).ToList();
    }

    public async Task<RollupDto?> GetRollupAsync(long id, CancellationToken ct = default)
    {
        var rollup = await Db.Set<JournalEntryRollup>()
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rollup == null) return null;

        var txns = await Db.Set<LedgerTransaction>()
            .Where(t => t.RolledUpIn == id)
            .ToListAsync(ct);

        return MapRollup(rollup, txns.Select(t => t.TransactionId).Distinct().Count(), txns.Count);
    }

    public async Task<string> GetDownloadUrlAsync(long rollupId, CancellationToken ct = default)
    {
        var rollup = await Db.Set<JournalEntryRollup>()
            .FirstOrDefaultAsync(r => r.Id == rollupId, ct)
            ?? throw new InvalidOperationException($"Rollup {rollupId} not found");

        if (string.IsNullOrEmpty(rollup.BlobUri))
            throw new InvalidOperationException("Rollup has no exported file");

        var fileName = $"JE-{rollup.PeriodYear}-{rollup.PeriodMonth:D2}-{rollup.Id}.csv";
        return await _blob.GetDownloadUrlAsync(rollup.BlobUri, fileName);
    }

    // ---- Helpers ----

    private IJournalDriver GetDriver(string driverType) =>
        _drivers.FirstOrDefault(d => d.DriverType == driverType)
        ?? throw new InvalidOperationException($"No driver registered for type '{driverType}'");

    private async Task<IReadOnlyList<LedgerTransactionLine>> BuildLinesAsync(
        List<LedgerTransaction> txns, string driverType, CancellationToken ct)
    {
        var accountIds = txns.Select(t => t.AccountId).Distinct().ToList();

        var accountMaps = await Db.Set<GlAccountMap>()
            .Where(m => m.ExternalSystem == driverType && accountIds.Contains(m.LedgerAccountId) && m.IsActive)
            .ToDictionaryAsync(m => m.LedgerAccountId, m => m.ExternalId, ct);

        return txns.Select(t => new LedgerTransactionLine(
            t.Id,
            t.TransactionId,
            t.EffectiveDate,
            t.Account.InternalCode,
            accountMaps.TryGetValue(t.AccountId, out var label) ? label : t.Account.ExternalLabel,
            t.Debit,
            t.Credit,
            t.Memo,
            t.SourceType,
            t.SourceId
        )).ToList();
    }

    private static RollupDto MapRollup(JournalEntryRollup r, int txnCount, int lineCount) =>
        new(r.Id, r.PeriodYear, r.PeriodMonth, r.DriverType, r.Status,
            txnCount, lineCount, r.ExternalId, r.BlobUri, r.ErrorMessage, r.CreatedAt, r.CompletedAt);
}
