namespace SIMS.Application.Interfaces.Services;

public interface IQboTokenService
{
    /// <summary>Returns a valid access token, refreshing if expired or within 5 min of expiry.</summary>
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
    Task<bool> IsConnectedAsync(CancellationToken ct = default);
    Task BootstrapFromConfigAsync(CancellationToken ct = default);
}
