using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using SIMS.Application.Configuration;
using SIMS.Application.Interfaces.Services;

namespace SIMS.Infrastructure.Services;

public class QboApiClient : IQboApiClient
{
    private readonly IQboTokenService _tokens;
    private readonly QboSettings _settings;
    private readonly IHttpClientFactory _httpFactory;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public QboApiClient(IQboTokenService tokens, IOptions<QboSettings> settings, IHttpClientFactory httpFactory)
    {
        _tokens = tokens;
        _settings = settings.Value;
        _httpFactory = httpFactory;
    }

    public async Task<string> PostJournalEntryAsync(object payload, CancellationToken ct = default)
    {
        var url = $"{_settings.BaseUrl}/v3/company/{_settings.RealmId}/journalentry?minorversion=65";
        var response = await SendAsync(HttpMethod.Post, url, payload, ct);
        var json = JsonNode.Parse(response)!;
        return json["JournalEntry"]?["Id"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Unexpected QBO JournalEntry response shape.");
    }

    public async Task<IReadOnlyList<QboAccount>> GetChartOfAccountsAsync(CancellationToken ct = default)
    {
        var url = $"{_settings.BaseUrl}/v3/company/{_settings.RealmId}/query?query=SELECT%20*%20FROM%20Account%20MAXRESULTS%20500&minorversion=65";
        var response = await SendAsync(HttpMethod.Get, url, null, ct);
        var json = JsonNode.Parse(response)!;
        var accounts = json["QueryResponse"]?["Account"]?.AsArray() ?? new JsonArray();
        return accounts
            .Select(a => new QboAccount(
                a!["Id"]!.GetValue<string>(),
                a["Name"]!.GetValue<string>(),
                a["AccountType"]?.GetValue<string>() ?? "",
                a["AccountSubType"]?.GetValue<string>() ?? "",
                a["AcctNum"]?.GetValue<string>()))
            .ToList();
    }

    private async Task<string> SendAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        var token = await _tokens.GetAccessTokenAsync(ct);
        var client = _httpFactory.CreateClient("qbo_api");

        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (body != null)
        {
            var json = JsonSerializer.Serialize(body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"QBO API error {(int)response.StatusCode} ({response.ReasonPhrase}).");

        return content;
    }
}
