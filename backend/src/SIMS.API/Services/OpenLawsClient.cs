using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SIMS.API.Services;

public interface IOpenLawsClient
{
    Task<IReadOnlyList<OpenLawsSearchResult>> SearchAsync(OpenLawsSearchRequest request, CancellationToken cancellationToken);
}

public sealed record OpenLawsSearchRequest(
    string BaseUrl,
    string ApiKey,
    string Jurisdiction,
    string Query,
    int Limit);

public sealed record OpenLawsSearchResult(
    string Jurisdiction,
    string LawKey,
    string Path,
    string DisplayName,
    string? Identifier,
    string? WebUrl,
    string Text);

public sealed class OpenLawsClient(IHttpClientFactory httpClientFactory) : IOpenLawsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<OpenLawsSearchResult>> SearchAsync(OpenLawsSearchRequest request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("openlaws");
        client.BaseAddress = new Uri(OpenLawsEndpointGuard.NormalizeBaseUrl(request.BaseUrl));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);

        var path = $"/api/v1/jurisdictions/{Uri.EscapeDataString(request.Jurisdiction)}/laws/search";
        var query = $"query={Uri.EscapeDataString(request.Query)}&type=phrase&with_federal=true&limit={request.Limit}";
        using var response = await client.GetAsync($"{path}?{query}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound &&
                body.Contains("No matching Divisions for that search.", StringComparison.OrdinalIgnoreCase))
                return [];

            throw new OpenLawsException($"OpenLaws returned {(int)response.StatusCode} {response.ReasonPhrase}: {TrimErrorBody(body)}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var divisions = await JsonSerializer.DeserializeAsync<List<OpenLawsDivision>>(stream, JsonOptions, cancellationToken) ?? [];

        return divisions
            .Where(d => !string.IsNullOrWhiteSpace(d.PlaintextContent))
            .Select(d => new OpenLawsSearchResult(
                d.JurisdictionKey ?? request.Jurisdiction,
                d.LawKey ?? string.Empty,
                d.Path ?? string.Empty,
                d.DisplayName ?? d.Name ?? d.Identifier ?? d.Path ?? "OpenLaws result",
                d.Identifier,
                d.OpenLawsWebUrl,
                d.PlaintextContent!.Trim()))
            .ToList();
    }

    private static string TrimErrorBody(string body)
    {
        var trimmed = body.Trim();
        return trimmed.Length <= 500 ? trimmed : trimmed[..500];
    }

    private sealed class OpenLawsDivision
    {
        [JsonPropertyName("jurisdiction_key")]
        public string? JurisdictionKey { get; set; }

        [JsonPropertyName("law_key")]
        public string? LawKey { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("identifier")]
        public string? Identifier { get; set; }

        [JsonPropertyName("openlaws_web_url")]
        public string? OpenLawsWebUrl { get; set; }

        [JsonPropertyName("plaintext_content")]
        public string? PlaintextContent { get; set; }
    }
}

public static class OpenLawsEndpointGuard
{
    public const string DefaultBaseUrl = "https://api.openlaws.us";
    private const string AllowedHost = "api.openlaws.us";

    public static bool IsOpenLawsSourceType(string? sourceType)
        => string.Equals(sourceType?.Trim(), "OpenLaw API", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(sourceType?.Trim(), "OpenLaws API", StringComparison.OrdinalIgnoreCase);

    public static string NormalizeBaseUrl(string? baseUrl)
    {
        var validationError = ValidateBaseUrl(baseUrl);
        if (validationError != null)
            throw new OpenLawsException(validationError);

        var trimmed = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim();
        return trimmed.EndsWith('/') ? trimmed[..^1] : trimmed;
    }

    public static string? ValidateBaseUrl(string? baseUrl)
    {
        var trimmed = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return "OpenLaws base URL must be a valid absolute URL.";

        var host = uri.IdnHost;
        if (IsPrivateOrLocalHost(host))
            return "OpenLaws base URL cannot point to localhost, private, or link-local addresses.";

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return "OpenLaws base URL must use HTTPS.";

        if (!string.IsNullOrWhiteSpace(uri.UserInfo) ||
            !string.IsNullOrWhiteSpace(uri.Query) ||
            !string.IsNullOrWhiteSpace(uri.Fragment))
            return "OpenLaws base URL cannot include credentials, query strings, or fragments.";

        if (!uri.IsDefaultPort)
            return "OpenLaws base URL cannot include a custom port.";

        if (!string.IsNullOrWhiteSpace(uri.AbsolutePath) && uri.AbsolutePath != "/")
            return "OpenLaws base URL cannot include a path.";

        if (!string.Equals(host, AllowedHost, StringComparison.OrdinalIgnoreCase))
            return $"OpenLaws base URL host must be {AllowedHost}.";

        return null;
    }

    private static bool IsPrivateOrLocalHost(string host)
    {
        var normalizedHost = host.Trim('[', ']');
        if (string.Equals(normalizedHost, "localhost", StringComparison.OrdinalIgnoreCase) ||
            normalizedHost.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!IPAddress.TryParse(normalizedHost, out var address))
            return false;

        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   (bytes[0] == 169 && bytes[1] == 254);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return address.IsIPv6LinkLocal ||
                   address.IsIPv6SiteLocal ||
                   (bytes[0] & 0xFE) == 0xFC;
        }

        return false;
    }
}

public sealed class OpenLawsException(string message) : Exception(message);
