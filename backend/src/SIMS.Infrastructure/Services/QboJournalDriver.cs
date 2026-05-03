using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Accounting;
using SIMS.Infrastructure.Data;

namespace SIMS.Infrastructure.Services;

public class QboJournalDriver : IJournalDriver
{
    private readonly IQboApiClient _qbo;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<QboJournalDriver> _logger;

    public string DriverType => "QBO";

    public QboJournalDriver(IQboApiClient qbo, ApplicationDbContext db, ILogger<QboJournalDriver> logger)
    {
        _qbo = qbo;
        _db = db;
        _logger = logger;
    }

    public async Task ExportAsync(
        JournalEntryRollup rollup,
        IReadOnlyList<LedgerTransactionLine> lines,
        CancellationToken ct = default)
    {
        // Load QBO account mappings for all accounts referenced by this rollup
        var accountIds = lines.Select(l => l.AccountCode).Distinct().ToList();
        var maps = await _db.Set<GlAccountMap>()
            .Where(m => m.TenantId == 1 && m.ExternalSystem == "QBO" && m.IsActive)
            .Include(m => m.LedgerAccount)
            .Where(m => accountIds.Contains(m.LedgerAccount.InternalCode))
            .ToDictionaryAsync(m => m.LedgerAccount.InternalCode, m => m.ExternalId, ct);

        // Group lines by TransactionId — each becomes one QBO JournalEntry
        var txnGroups = lines.GroupBy(l => l.TransactionId).ToList();
        var postedIds = new List<string>();

        try
        {
            foreach (var group in txnGroups)
            {
                var payload = BuildJournalEntryPayload(group.ToList(), maps, rollup);
                var qboId = await _qbo.PostJournalEntryAsync(payload, ct);
                postedIds.Add(qboId);
                _logger.LogDebug("Posted QBO JE {QboId} for transaction {TxnId}", qboId, group.Key);
            }

            rollup.Status = "Exported";
            rollup.ExternalId = string.Join(",", postedIds);
            rollup.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            rollup.Status = "Failed";
            rollup.ErrorMessage = ex.Message;
            rollup.CompletedAt = DateTime.UtcNow;
            _logger.LogError(ex, "QBO journal entry export failed for rollup {RollupId}", rollup.Id);
        }
    }

    private static object BuildJournalEntryPayload(
        IReadOnlyList<LedgerTransactionLine> lines,
        Dictionary<string, string> accountMaps,
        JournalEntryRollup rollup)
    {
        var date = lines[0].EffectiveDate.ToString("yyyy-MM-dd");
        var memo = lines[0].Memo ?? $"IMS {rollup.PeriodYear}-{rollup.PeriodMonth:D2} Journal Entry";

        var jeLines = lines.Select((line, idx) =>
        {
            var qboAccountId = accountMaps.TryGetValue(line.AccountCode, out var id)
                ? id
                : throw new InvalidOperationException(
                    $"No QBO account mapping found for GL account {line.AccountCode} ({line.AccountLabel}). " +
                    "Configure the mapping via Carriers > GL Account Map.");

            return new
            {
                Id = (idx + 1).ToString(),
                DetailType = "JournalEntryLineDetail",
                Amount = line.Debit > 0 ? line.Debit : line.Credit,
                Description = line.Memo ?? memo,
                JournalEntryLineDetail = new
                {
                    PostingType = line.Debit > 0 ? "Debit" : "Credit",
                    AccountRef = new { value = qboAccountId },
                },
            };
        }).ToArray();

        return new
        {
            TxnDate = date,
            PrivateNote = memo,
            Line = jeLines,
        };
    }
}
