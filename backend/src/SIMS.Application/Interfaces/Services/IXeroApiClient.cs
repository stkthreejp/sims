namespace SIMS.Application.Interfaces.Services;

public interface IXeroApiClient
{
    /// <summary>
    /// Creates a single Xero Manual Journal (PUT /ManualJournals) and returns its ManualJournalID.
    /// </summary>
    Task<string> PostManualJournalAsync(object payload, CancellationToken ct = default);

    /// <summary>Returns the Xero chart of accounts (GET /Accounts) for GL mapping setup.</summary>
    Task<IReadOnlyList<XeroAccount>> GetChartOfAccountsAsync(CancellationToken ct = default);
}

/// <summary>
/// A Xero ledger account. Note Manual Journals reference accounts by <see cref="Code"/>
/// (the user-facing account code), not the internal AccountID.
/// </summary>
public record XeroAccount(string AccountId, string Code, string Name, string Type, string? TaxType, string Status);
