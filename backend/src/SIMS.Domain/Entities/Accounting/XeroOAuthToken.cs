namespace SIMS.Domain.Entities.Accounting;

/// <summary>
/// Cached Xero access token. Xero Custom connections use the client-credentials grant,
/// which issues a short-lived access token (~30 min) with NO refresh token — so there is
/// nothing long-lived to persist. We cache the access token only to avoid re-requesting it
/// on every export; it is re-minted from the configured client id/secret whenever it expires.
/// </summary>
public class XeroOAuthToken
{
    public int Id { get; set; }
    public int TenantId { get; set; } = 1;

    /// <summary>The Xero organisation (tenant) id this token is scoped to.</summary>
    public string XeroTenantId { get; set; } = "";

    public string AccessToken { get; set; } = "";
    public DateTime AccessTokenExpiresAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
