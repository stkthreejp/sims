namespace SIMS.Application.Interfaces.Services;

public interface IQboApiClient
{
    Task<string> PostJournalEntryAsync(object payload, CancellationToken ct = default);
    Task<IReadOnlyList<QboAccount>> GetChartOfAccountsAsync(CancellationToken ct = default);
}

public record QboAccount(string Id, string Name, string AccountType, string AccountSubType, string? AcctNum);
