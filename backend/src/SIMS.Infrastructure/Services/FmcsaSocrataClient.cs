using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SIMS.Application.Configuration;

namespace SIMS.Infrastructure.Services;

public class FmcsaSocrataClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly FmcsaSocrataSettings _settings;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public FmcsaSocrataClient(IHttpClientFactory httpFactory, IOptions<FmcsaSocrataSettings> settings)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
    }

    public Task<List<Dictionary<string, JsonElement>>> GetCensusByDotAsync(string dotNumber, CancellationToken ct) =>
        GetRowsByDotAsync(_settings.CensusDatasetId, dotNumber, ct);

    public Task<List<Dictionary<string, JsonElement>>> GetInspectionsByDotAsync(string dotNumber, CancellationToken ct) =>
        GetRowsByDotAsync(_settings.InspectionsDatasetId, dotNumber, ct);

    public Task<List<Dictionary<string, JsonElement>>> GetViolationsByDotAsync(string dotNumber, CancellationToken ct) =>
        GetRowsByDotAsync(_settings.ViolationsDatasetId, dotNumber, ct);

    public Task<List<Dictionary<string, JsonElement>>> GetCrashesByDotAsync(string dotNumber, CancellationToken ct) =>
        GetRowsByDotAsync(_settings.CrashesDatasetId, dotNumber, ct);

    private async Task<List<Dictionary<string, JsonElement>>> GetRowsByDotAsync(string datasetId, string dotNumber, CancellationToken ct)
    {
        var allRows = new List<Dictionary<string, JsonElement>>();
        var pageSize = Math.Clamp(_settings.PageSize, 1, 50000);
        var maxRows = Math.Max(pageSize, _settings.MaxRowsPerDataset);

        for (var offset = 0; offset < maxRows; offset += pageSize)
        {
            var rows = await TryGetRowsPageAsync(datasetId, dotNumber, pageSize, offset, ct);
            allRows.AddRange(rows);
            if (rows.Count < pageSize) break;
        }

        return allRows;
    }

    private async Task<List<Dictionary<string, JsonElement>>> TryGetRowsPageAsync(
        string datasetId, string dotNumber, int limit, int offset, CancellationToken ct)
    {
        var dotColumns = new[] { "dot_number", "usdot_number", "us_dot_number", "usdot" };
        Exception? lastError = null;

        foreach (var dotColumn in dotColumns)
        {
            foreach (var query in BuildDotQueries(dotColumn, dotNumber, limit, offset))
            {
                try
                {
                    return await SendAsync(datasetId, query, ct);
                }
                catch (HttpRequestException ex) when (IsLikelyBadColumn(ex))
                {
                    lastError = ex;
                }
            }
        }

        throw lastError ?? new InvalidOperationException($"Unable to query Socrata dataset {datasetId}.");
    }

    private static IEnumerable<string> BuildDotQueries(string dotColumn, string dotNumber, int limit, int offset)
    {
        var numericDot = dotNumber.All(char.IsDigit);
        if (numericDot)
            yield return $"$limit={limit}&$offset={offset}&$where={WebUtility.UrlEncode($"{dotColumn}={dotNumber}")}";

        yield return $"$limit={limit}&$offset={offset}&$where={WebUtility.UrlEncode($"{dotColumn}='{dotNumber}'")}";
    }

    private async Task<List<Dictionary<string, JsonElement>>> SendAsync(string datasetId, string query, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("fmcsa_socrata");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/resource/{datasetId}.json?{query}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(_settings.AppToken))
            request.Headers.Add("X-App-Token", _settings.AppToken);

        var response = await client.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Socrata error {(int)response.StatusCode}: {content}", null, response.StatusCode);

        return JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(content, JsonOpts) ?? new();
    }

    private static bool IsLikelyBadColumn(HttpRequestException ex) =>
        ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound;
}
