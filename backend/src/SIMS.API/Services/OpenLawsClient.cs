using System.Net.Http.Headers;
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
        client.BaseAddress = new Uri(NormalizeBaseUrl(request.BaseUrl));
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

    private static string NormalizeBaseUrl(string baseUrl)
    {
        var trimmed = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.openlaws.us" : baseUrl.Trim();
        return trimmed.EndsWith('/') ? trimmed[..^1] : trimmed;
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

public sealed class OpenLawsException(string message) : Exception(message);
