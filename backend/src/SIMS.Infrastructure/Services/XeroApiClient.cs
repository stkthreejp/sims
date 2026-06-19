using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using SIMS.Application.Configuration;
using SIMS.Application.Interfaces.Services;

namespace SIMS.Infrastructure.Services;

public class XeroApiClient : IXeroApiClient
{
    private readonly IXeroTokenService _tokens;
    private readonly XeroSettings _settings;
    private readonly IHttpClientFactory _httpFactory;

    public XeroApiClient(IXeroTokenService tokens, IOptions<XeroSettings> settings, IHttpClientFactory httpFactory)
    {
        _tokens = tokens;
        _settings = settings.Value;
        _httpFactory = httpFactory;
    }

    public async Task<string> PostManualJournalAsync(object payload, CancellationToken ct = default)
    {
        // PUT creates a new manual journal in Xero (POST is create-or-update).
        var url = $"{_settings.BaseUrl}/ManualJournals";
        var response = await SendAsync(HttpMethod.Put, url, payload, ct);
        var json = JsonNode.Parse(response)!;
        return json["ManualJournals"]?.AsArray().FirstOrDefault()?["ManualJournalID"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Unexpected Xero ManualJournals response shape.");
    }

    public async Task<IReadOnlyList<XeroAccount>> GetChartOfAccountsAsync(CancellationToken ct = default)
    {
        var url = $"{_settings.BaseUrl}/Accounts";
        var response = await SendAsync(HttpMethod.Get, url, null, ct);
        var json = JsonNode.Parse(response)!;
        var accounts = json["Accounts"]?.AsArray() ?? new JsonArray();
        return accounts
            .Select(a => new XeroAccount(
                a!["AccountID"]?.GetValue<string>() ?? "",
                a["Code"]?.GetValue<string>() ?? "",
                a["Name"]?.GetValue<string>() ?? "",
                a["Type"]?.GetValue<string>() ?? "",
                a["TaxType"]?.GetValue<string>(),
                a["Status"]?.GetValue<string>() ?? ""))
            .ToList();
    }

    private async Task<string> SendAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        var token = await _tokens.GetAccessTokenAsync(ct);
        var client = _httpFactory.CreateClient("xero_api");

        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("xero-tenant-id", _tokens.TenantId);

        if (body != null)
        {
            var json = JsonSerializer.Serialize(body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Xero API error {(int)response.StatusCode} ({response.ReasonPhrase}). {Truncate(content, 500)}");

        return content;
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];
}
