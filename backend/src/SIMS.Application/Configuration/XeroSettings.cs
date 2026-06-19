namespace SIMS.Application.Configuration;

/// <summary>
/// Configuration for the Xero accounting integration.
///
/// SIMS connects to a single Xero organisation using a Xero "Custom connection"
/// (the client-credentials OAuth2 grant). Unlike the QBO integration there is no
/// interactive authorization-code flow and no rotating refresh token — the backend
/// requests a short-lived access token (~30 min) directly with the client id/secret.
/// </summary>
public class XeroSettings
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// The Xero organisation (tenant) id. Sent as the <c>xero-tenant-id</c> header on
    /// every API call. For a Custom connection this is the single connected org.
    /// </summary>
    public string TenantId { get; set; } = "";

    /// <summary>Space-delimited OAuth scopes requested for the client-credentials token.</summary>
    public string Scopes { get; set; } = "accounting.transactions accounting.settings";

    /// <summary>Signing key used to validate inbound Xero webhook payloads (x-xero-signature).</summary>
    public string WebhookKey { get; set; } = "";

    /// <summary>Xero identity (token) endpoint.</summary>
    public string IdentityUrl { get; set; } = "https://identity.xero.com/connect/token";

    /// <summary>Base URL for the Xero Accounting API.</summary>
    public string BaseUrl { get; set; } = "https://api.xero.com/api.xro/2.0";
}
