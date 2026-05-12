using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SIMS.Application.Configuration;

namespace SIMS.Infrastructure.Services;

public class LegiScanClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly LegiScanSettings _settings;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public LegiScanClient(IHttpClientFactory httpFactory, IOptions<LegiScanSettings> settings)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
    }

    public async Task<IReadOnlyList<LegiScanMonitorBill>> GetMonitorListRawAsync(CancellationToken ct)
    {
        using var doc = await SendAsync("getMonitorListRaw", null, ct);
        if (!TryGetProperty(doc.RootElement, "monitorlist", out var monitorList))
            return [];

        return ExtractBills(monitorList).ToList();
    }

    public async Task<LegiScanBill> GetBillAsync(int billId, CancellationToken ct)
    {
        using var doc = await SendAsync("getBill", new Dictionary<string, string> { ["id"] = billId.ToString() }, ct);
        if (!TryGetProperty(doc.RootElement, "bill", out var bill))
            throw new InvalidOperationException("LegiScan getBill response did not contain a bill payload.");

        return new LegiScanBill(
            GetInt(bill, "bill_id") ?? billId,
            GetString(bill, "state") ?? string.Empty,
            GetString(bill, "bill_number") ?? string.Empty,
            GetString(bill, "title") ?? string.Empty,
            GetString(bill, "description"),
            GetString(bill, "change_hash"),
            GetInt(bill, "status"),
            GetDateOnly(bill, "status_date"),
            GetString(bill, "url"),
            bill.GetRawText());
    }

    public async Task<Dictionary<int, string>> SetMonitorAsync(IEnumerable<int> billIds, string action, string stance, CancellationToken ct)
    {
        var idList = string.Join(",", billIds.Distinct().Order());
        if (string.IsNullOrWhiteSpace(idList))
            return new Dictionary<int, string>();

        using var doc = await SendAsync("setMonitor", new Dictionary<string, string>
        {
            ["action"] = action,
            ["stance"] = stance,
            ["list"] = idList
        }, ct);

        if (!TryGetProperty(doc.RootElement, "return", out var result) || result.ValueKind != JsonValueKind.Object)
            return new Dictionary<int, string>();

        return result.EnumerateObject()
            .Where(p => int.TryParse(p.Name, out _))
            .ToDictionary(p => int.Parse(p.Name), p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? string.Empty : p.Value.ToString());
    }

    private async Task<JsonDocument> SendAsync(string operation, Dictionary<string, string>? parameters, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            throw new InvalidOperationException("LegiScan API key is not configured.");

        var query = new List<string>
        {
            $"key={WebUtility.UrlEncode(_settings.ApiKey)}",
            $"op={WebUtility.UrlEncode(operation)}"
        };

        if (parameters != null)
        {
            query.AddRange(parameters.Select(p =>
                $"{WebUtility.UrlEncode(p.Key)}={WebUtility.UrlEncode(p.Value)}"));
        }

        var client = _httpFactory.CreateClient("legiscan");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/?{string.Join("&", query)}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await client.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"LegiScan HTTP error {(int)response.StatusCode}.", null, response.StatusCode);

        var doc = JsonDocument.Parse(content);
        var status = TryGetProperty(doc.RootElement, "status", out var statusElement) ? statusElement.GetString() : null;
        if (!string.Equals(status, "OK", StringComparison.Ordinal))
        {
            var message = TryGetProperty(doc.RootElement, "alert", out var alert) &&
                          TryGetProperty(alert, "message", out var alertMessage)
                ? alertMessage.GetString()
                : "LegiScan returned an ERROR status.";
            doc.Dispose();
            throw new InvalidOperationException(message);
        }

        return doc;
    }

    private static IEnumerable<LegiScanMonitorBill> ExtractBills(JsonElement monitorList)
    {
        if (monitorList.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var property in monitorList.EnumerateObject())
        {
            if (!int.TryParse(property.Name, out var billId) || property.Value.ValueKind != JsonValueKind.Object)
                continue;

            var bill = property.Value;
            yield return new LegiScanMonitorBill(
                billId,
                GetString(bill, "state") ?? string.Empty,
                GetString(bill, "bill_number") ?? string.Empty,
                GetString(bill, "change_hash"),
                GetInt(bill, "status"),
                GetDateOnly(bill, "status_date"),
                GetString(bill, "url"));
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }

    private static int? GetInt(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)
            ? number
            : null;
    }

    private static DateOnly? GetDateOnly(JsonElement element, string name)
    {
        var raw = GetString(element, name);
        return DateOnly.TryParse(raw, out var date) ? date : null;
    }
}

public sealed record LegiScanMonitorBill(
    int BillId,
    string State,
    string BillNumber,
    string? ChangeHash,
    int? Status,
    DateOnly? StatusDate,
    string? Url);

public sealed record LegiScanBill(
    int BillId,
    string State,
    string BillNumber,
    string Title,
    string? Description,
    string? ChangeHash,
    int? Status,
    DateOnly? StatusDate,
    string? Url,
    string RawJson);
