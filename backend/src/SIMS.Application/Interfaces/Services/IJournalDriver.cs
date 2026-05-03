using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Interfaces.Services;

/// <summary>
/// Abstraction over how a finalized JournalEntryRollup is exported/posted externally.
/// Implementations: CsvJournalDriver (blob download), QboJournalDriver (QBO API).
/// </summary>
public interface IJournalDriver
{
    string DriverType { get; }   // matches JournalEntryRollup.DriverType

    /// <summary>
    /// Exports the rollup. Implementations update rollup.Status, rollup.BlobUri / rollup.ExternalId,
    /// and rollup.CompletedAt on success or set rollup.ErrorMessage on failure.
    /// </summary>
    Task ExportAsync(
        JournalEntryRollup rollup,
        IReadOnlyList<LedgerTransactionLine> lines,
        CancellationToken ct = default);
}

/// <summary>Flattened view of a ledger transaction row for driver consumption.</summary>
public record LedgerTransactionLine(
    long Id,
    Guid TransactionId,
    DateOnly EffectiveDate,
    string AccountCode,         // LedgerAccount.InternalCode
    string AccountLabel,        // GlAccountMap.ExternalId (CSV label) or LedgerAccount.ExternalLabel
    decimal Debit,
    decimal Credit,
    string? Memo,
    string SourceType,
    long SourceId
);
