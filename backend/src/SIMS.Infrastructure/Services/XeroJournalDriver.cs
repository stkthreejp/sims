using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Accounting;
using SIMS.Infrastructure.Data;

namespace SIMS.Infrastructure.Services;

/// <summary>
/// Exports a finalized <see cref="JournalEntryRollup"/> to Xero as one Manual Journal per
/// source transaction. Targets Xero's Manual
/// Journals API, which references accounts by their account <c>Code</c> (mapped via
/// <see cref="GlAccountMap"/> rows whose ExternalSystem is "Xero") and uses a single signed
/// LineAmount per line — positive for a debit, negative for a credit.
/// </summary>
public class XeroJournalDriver : IJournalDriver
{
    private readonly IXeroApiClient _xero;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<XeroJournalDriver> _logger;

    public string DriverType => "Xero";

    public XeroJournalDriver(IXeroApiClient xero, ApplicationDbContext db, ILogger<XeroJournalDriver> logger)
    {
        _xero = xero;
        _db = db;
        _logger = logger;
    }

    public async Task ExportAsync(
        JournalEntryRollup rollup,
        IReadOnlyList<LedgerTransactionLine> lines,
        CancellationToken ct = default)
    {
        // Load Xero account-code mappings for all accounts referenced by this rollup.
        var accountIds = lines.Select(l => l.AccountCode).Distinct().ToList();
        var maps = await _db.Set<GlAccountMap>()
            .Where(m => m.TenantId == 1 && m.ExternalSystem == "Xero" && m.IsActive)
            .Include(m => m.LedgerAccount)
            .Where(m => accountIds.Contains(m.LedgerAccount.InternalCode))
            .ToDictionaryAsync(m => m.LedgerAccount.InternalCode, m => m.ExternalId, ct);

        var txnGroups = lines.GroupBy(l => l.TransactionId).ToList();
        var postedIds = new List<string>();

        try
        {
            foreach (var group in txnGroups)
            {
                var payload = BuildManualJournalPayload(group.ToList(), maps, rollup);
                var xeroId = await _xero.PostManualJournalAsync(payload, ct);
                postedIds.Add(xeroId);
                _logger.LogDebug("Posted Xero ManualJournal {XeroId} for transaction {TxnId}", xeroId, group.Key);
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
            _logger.LogError(ex, "Xero manual journal export failed for rollup {RollupId}", rollup.Id);
        }
    }

    private static object BuildManualJournalPayload(
        IReadOnlyList<LedgerTransactionLine> lines,
        Dictionary<string, string> accountMaps,
        JournalEntryRollup rollup)
    {
        var date = lines[0].EffectiveDate.ToString("yyyy-MM-dd");
        var narration = lines[0].Memo ?? $"SIMS {rollup.PeriodYear}-{rollup.PeriodMonth:D2} Journal Entry";

        var journalLines = lines.Select(line =>
        {
            var xeroAccountCode = accountMaps.TryGetValue(line.AccountCode, out var code)
                ? code
                : throw new InvalidOperationException(
                    $"No Xero account mapping found for GL account {line.AccountCode} ({line.AccountLabel}). " +
                    "Configure the mapping via Carriers > GL Account Map (External System = Xero).");

            // Xero convention: positive LineAmount = debit, negative = credit.
            var lineAmount = line.Debit > 0 ? line.Debit : -line.Credit;

            return new
            {
                LineAmount = lineAmount,
                AccountCode = xeroAccountCode,
                Description = line.Memo ?? narration,
            };
        }).ToArray();

        return new
        {
            ManualJournals = new[]
            {
                new
                {
                    Narration = narration,
                    Date = date,
                    Status = "POSTED",
                    JournalLines = journalLines,
                },
            },
        };
    }
}
