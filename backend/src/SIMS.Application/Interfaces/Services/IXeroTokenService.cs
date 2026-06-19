namespace SIMS.Application.Interfaces.Services;

public interface IXeroTokenService
{
    /// <summary>
    /// Returns a valid Xero access token, requesting a fresh one via the client-credentials
    /// grant if the cached token is expired or within 5 min of expiry.
    /// </summary>
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);

    /// <summary>True when Xero credentials are configured and a token can be obtained.</summary>
    Task<bool> IsConnectedAsync(CancellationToken ct = default);

    /// <summary>The configured Xero organisation (tenant) id, for the xero-tenant-id header.</summary>
    string TenantId { get; }

    /// <summary>
    /// Warms the token cache at startup if credentials are configured. No-op when unconfigured.
    /// </summary>
    Task BootstrapFromConfigAsync(CancellationToken ct = default);
}
