namespace SIMS.Application.Configuration;

public class QboSettings
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string RealmId { get; set; } = "";
    public string Environment { get; set; } = "sandbox";
    public string WebhookVerifierToken { get; set; } = "";

    public string BaseUrl => Environment == "sandbox"
        ? "https://sandbox-quickbooks.api.intuit.com"
        : "https://quickbooks.api.intuit.com";
}
