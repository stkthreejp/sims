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

    public async Task<List<Dictionary<string, JsonElement>>> GetViolationsByInspectionIdsAsync(IEnumerable<string> inspectionIds, CancellationToken ct)
    {
        var ids = inspectionIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (ids.Count == 0)
            return [];

        var allRows = new List<Dictionary<string, JsonElement>>();
        foreach (var batch in ids.Chunk(50))
        {
            var quoted = string.Join(",", batch.Select(id => $"'{id.Replace("'", "''")}'"));
            var query = $"$limit={Math.Clamp(_settings.PageSize, 1, 50000)}&$where={WebUtility.UrlEncode($"unique_id in ({quoted})")}";
            allRows.AddRange(await SendAsync(_settings.ViolationsDatasetId, query, ct));
        }

        return allRows;
    }

    public Task<List<Dictionary<string, JsonElement>>> GetCrashesByDotAsync(string dotNumber, CancellationToken ct) =>
        GetRowsByDotAsync(_settings.CrashesDatasetId, dotNumber, ct);

    public async Task<(string Source, List<Dictionary<string, JsonElement>> Rows)> GetSmsScoresByDotAsync(string dotNumber, CancellationToken ct)
    {
        var datasets = new[]
        {
            ("SMS AB Pass", _settings.SmsAbPassDatasetId),
            ("SMS C Pass", _settings.SmsCPassDatasetId),
            ("SMS AB PassProperty", _settings.SmsAbPassPropertyDatasetId),
            ("SMS C PassProperty", _settings.SmsCPassPropertyDatasetId),
        };

        foreach (var (source, datasetId) in datasets)
        {
            if (string.IsNullOrWhiteSpace(datasetId))
                continue;

            var rows = await GetRowsByDotAsync(datasetId, dotNumber, ct);
            if (rows.Count > 0)
                return (source, rows);
        }

        return ("Official SMS", []);
    }

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
        var dotColumns = new[]
        {
            "dot_number",
            "usdot_number",
            "us_dot_number",
            "usdot",
            "usdot_num",
            "dot_num",
            "dot"
        };
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

        try
        {
            var query = $"$limit={limit}&$offset={offset}&$q={WebUtility.UrlEncode(dotNumber)}";
            var rows = await SendAsync(datasetId, query, ct);
            return rows.Where(r => RowMatchesDotNumber(r, dotNumber)).ToList();
        }
        catch (HttpRequestException ex) when (IsLikelyBadColumn(ex))
        {
            lastError = ex;
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

    private static bool RowMatchesDotNumber(Dictionary<string, JsonElement> row, string dotNumber)
    {
        foreach (var (key, value) in row)
        {
            if (!key.Contains("dot", StringComparison.OrdinalIgnoreCase))
                continue;

            var raw = value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : value.ValueKind == JsonValueKind.Number
                    ? value.ToString()
                    : null;

            if (string.Equals(NormalizeDigits(raw), dotNumber, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string? NormalizeDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private static bool IsLikelyBadColumn(HttpRequestException ex) =>
        ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound;
}
