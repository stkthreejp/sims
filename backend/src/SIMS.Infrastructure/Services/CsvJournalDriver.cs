using System.Globalization;
using System.Text;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Services;

public class CsvJournalDriver : IJournalDriver
{
    private readonly IBlobStorageService _blob;

    public CsvJournalDriver(IBlobStorageService blob) => _blob = blob;

    public string DriverType => "CSV";

    public async Task ExportAsync(
        JournalEntryRollup rollup,
        IReadOnlyList<LedgerTransactionLine> lines,
        CancellationToken ct = default)
    {
        try
        {
            var csv = BuildCsv(rollup, lines);
            var bytes = Encoding.UTF8.GetBytes(csv);
            var fileName = $"JE-{rollup.PeriodYear}-{rollup.PeriodMonth:D2}-{rollup.Id}.csv";

            using var stream = new MemoryStream(bytes);
            var blobPath = await _blob.UploadAsync(stream, fileName, "text/csv");

            rollup.Status = "Exported";
            rollup.BlobUri = blobPath;
            rollup.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            rollup.Status = "Failed";
            rollup.ErrorMessage = ex.Message;
            rollup.CompletedAt = DateTime.UtcNow;
        }
    }

    private static string BuildCsv(JournalEntryRollup rollup, IReadOnlyList<LedgerTransactionLine> lines)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date,JE Reference,Account Code,Account,Debit,Credit,Memo,Source Type,Source ID");

        // Group by TransactionId to assign per-JE reference numbers
        var txnGroups = lines
            .GroupBy(l => l.TransactionId)
            .Select((g, idx) => (
                Ref: $"JE-{rollup.PeriodYear}-{rollup.PeriodMonth:D2}-{rollup.Id}-{idx + 1:D4}",
                Lines: g.ToList()
            ));

        foreach (var (jeRef, jeLines) in txnGroups)
        {
            foreach (var line in jeLines)
            {
                sb.Append(line.EffectiveDate.ToString("yyyy-MM-dd")).Append(',');
                sb.Append(Escape(jeRef)).Append(',');
                sb.Append(Escape(line.AccountCode)).Append(',');
                sb.Append(Escape(line.AccountLabel)).Append(',');
                sb.Append(line.Debit > 0 ? line.Debit.ToString("F2", CultureInfo.InvariantCulture) : "").Append(',');
                sb.Append(line.Credit > 0 ? line.Credit.ToString("F2", CultureInfo.InvariantCulture) : "").Append(',');
                sb.Append(Escape(line.Memo ?? "")).Append(',');
                sb.Append(Escape(line.SourceType)).Append(',');
                sb.AppendLine(line.SourceId.ToString());
            }
        }

        return sb.ToString();
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
