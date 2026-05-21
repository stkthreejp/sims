using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Infrastructure.Services;

public class AnthropicGuidelineLlmInterpreterService : IAiGuidelineLlmInterpreterService
{
    private const string DefaultModelId = "claude-sonnet-4-6";
    private const int MaxOutputTokens = 2_500;
    private const int MaxGuidelineTextChars = 35_000;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly HashSet<string> AllowedConditionFields = new(StringComparer.Ordinal)
    {
        "largestSingleItemValue",
        "totalInsuredValue",
        "premiumAmount",
        "totalPremium",
        "lossRatio",
        "driverCount",
        "vehicleCount",
        "isFilingState"
    };
    private static readonly HashSet<string> AllowedConditionOperators = new(StringComparer.Ordinal)
    {
        ">",
        ">=",
        "<",
        "<=",
        "==",
        "!="
    };

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly DbContext _db;
    private readonly ILogger<AnthropicGuidelineLlmInterpreterService> _logger;

    public AnthropicGuidelineLlmInterpreterService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        DbContext db,
        ILogger<AnthropicGuidelineLlmInterpreterService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("anthropic");
        _configuration = configuration;
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CreateUnderwritingGuidelineControlRequest>> InterpretAsync(string guidelineText, CancellationToken ct = default)
    {
        var apiKey = _configuration["ANTHROPIC_API_KEY"] ??
            Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ??
            _configuration["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Anthropic guideline interpretation skipped because ANTHROPIC_API_KEY is not configured.");
            return [];
        }

        var modelId = await ResolveModelIdAsync(ct);
        var promptText = PrepareGuidelineText(guidelineText);
        _logger.LogInformation(
            "Anthropic guideline interpretation starting with model {ModelId}; extracted chars {ExtractedChars}; prompt chars {PromptChars}; max output tokens {MaxOutputTokens}.",
            modelId,
            guidelineText.Length,
            promptText.Length,
            MaxOutputTokens);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = JsonContent.Create(new
        {
            model = modelId,
            max_tokens = MaxOutputTokens,
            temperature = 0,
            system = SystemPrompt,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = $"Interpret this underwriting guideline text into proposed SIMS controls.\n\n{promptText}"
                }
            }
        });

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient.SendAsync(request, ct);
        stopwatch.Stop();
        _logger.LogInformation(
            "Anthropic guideline interpretation completed HTTP call with status {StatusCode} in {ElapsedMs}ms.",
            response.StatusCode,
            stopwatch.ElapsedMilliseconds);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Anthropic guideline interpretation failed with status {StatusCode}: {Body}", response.StatusCode, TrimForLog(errorBody));
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var message = await JsonSerializer.DeserializeAsync<AnthropicMessageResponse>(stream, JsonOptions, ct);
        var text = message?.Content?.FirstOrDefault(c => string.Equals(c.Type, "text", StringComparison.OrdinalIgnoreCase))?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Anthropic guideline interpretation returned no text content.");
            return [];
        }

        var payloadJson = ExtractJson(text);
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            _logger.LogWarning("Anthropic guideline interpretation returned text without a JSON object.");
            return [];
        }

        var payload = ParsePayload(payloadJson);
        if (payload?.Controls is null || payload.Controls.Count == 0)
        {
            _logger.LogWarning("Anthropic guideline interpretation returned no controls.");
            return [];
        }

        var controls = new List<CreateUnderwritingGuidelineControlRequest>();
        var sortOrder = 10;
        foreach (var proposed in payload.Controls)
        {
            var mapped = MapControl(proposed, sortOrder);
            if (mapped is null)
                continue;

            controls.Add(mapped);
            sortOrder += 10;
        }

        return controls;
    }

    private static AiGuidelineControlPayload? ParsePayload(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        if (!document.RootElement.TryGetProperty("controls", out var controlsElement) || controlsElement.ValueKind != JsonValueKind.Array)
            return null;

        var controls = new List<AiGuidelineControlItem>();
        foreach (var element in controlsElement.EnumerateArray())
        {
            controls.Add(new AiGuidelineControlItem(
                ReadString(element, "itemType"),
                ReadString(element, "stage"),
                ReadString(element, "severity"),
                ReadString(element, "ruleKey"),
                ReadString(element, "label"),
                ReadString(element, "description"),
                ReadConditionJson(element),
                ReadBool(element, "isBlocking"),
                ReadBool(element, "overrideAllowed", defaultValue: true),
                ReadString(element, "overridePermission"),
                ReadString(element, "sourceCitation"),
                ReadDecimal(element, "aiConfidence"),
                ReadInt(element, "sortOrder")));
        }

        return new AiGuidelineControlPayload(controls);
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.GetRawText();
    }

    private static string? ReadConditionJson(JsonElement element)
    {
        if (!element.TryGetProperty("conditionJson", out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.GetRawText();
    }

    private static bool ReadBool(JsonElement element, string propertyName, bool defaultValue = false)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return defaultValue;

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var parsed) => parsed,
            _ => defaultValue
        };
    }

    private static decimal? ReadDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var value))
            return value;

        return property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), out var parsed) ? parsed : null;
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return 0;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
            return value;

        return property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out var parsed) ? parsed : 0;
    }

    private async Task<string> ResolveModelIdAsync(CancellationToken ct)
    {
        var configured = _configuration["Anthropic:Model"] ?? _configuration["ANTHROPIC_MODEL"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();

        var modelId = await _db.Set<AiUseCaseModelSetting>()
            .Include(s => s.AiModel)
            .Where(s => s.UseCase == AiModelSettingsService.ReferralJudgment && s.AiModel.Provider == "Anthropic" && s.AiModel.Active)
            .Select(s => s.AiModel.ModelId)
            .SingleOrDefaultAsync(ct);

        return NormalizeModelId(modelId);
    }

    private static string NormalizeModelId(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId) || modelId.Equals("claude-sonnet-default", StringComparison.OrdinalIgnoreCase))
            return DefaultModelId;

        var trimmed = modelId.Trim();
        return trimmed.Equals("claude-sonnet-4-20250514", StringComparison.OrdinalIgnoreCase)
            ? DefaultModelId
            : trimmed;
    }

    private static CreateUnderwritingGuidelineControlRequest? MapControl(AiGuidelineControlItem item, int fallbackSortOrder)
    {
        if (!Enum.TryParse<UnderwritingControlItemType>(item.ItemType, ignoreCase: true, out var itemType))
            return null;
        if (!Enum.TryParse<UnderwritingControlStage>(item.Stage, ignoreCase: true, out var stage))
            return null;
        if (!Enum.TryParse<UnderwritingControlSeverity>(item.Severity, ignoreCase: true, out var severity))
            return null;
        if (string.IsNullOrWhiteSpace(item.RuleKey) || string.IsNullOrWhiteSpace(item.Label))
            return null;

        var conditionJson = NormalizeConditionJson(item.ConditionJson, out var conditionNote);
        var description = TrimToNull(item.Description);
        if (conditionNote is not null)
            description = string.IsNullOrWhiteSpace(description) ? conditionNote : $"{description} {conditionNote}";

        return new CreateUnderwritingGuidelineControlRequest(
            itemType,
            stage,
            severity,
            item.RuleKey.Trim(),
            item.Label.Trim(),
            description,
            conditionJson,
            item.IsBlocking,
            item.OverrideAllowed,
            TrimToNull(item.OverridePermission) ?? "underwriting.clearance.override",
            TrimToNull(item.SourceCitation),
            item.AiConfidence is >= 0 and <= 1 ? item.AiConfidence : null,
            item.SortOrder > 0 ? item.SortOrder : fallbackSortOrder);
    }

    private static string? NormalizeConditionJson(string? conditionJson, out string? conditionNote)
    {
        conditionNote = null;
        if (string.IsNullOrWhiteSpace(conditionJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(conditionJson);
            var root = document.RootElement;
            var field = root.TryGetProperty("field", out var fieldElement) ? fieldElement.GetString() : null;
            var op = root.TryGetProperty("operator", out var operatorElement) ? operatorElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(field) || !AllowedConditionFields.Contains(field))
            {
                conditionNote = $"Unsupported condition field '{field ?? "unknown"}' requires human review before publishing.";
                return null;
            }

            if (string.IsNullOrWhiteSpace(op) || !AllowedConditionOperators.Contains(op))
            {
                conditionNote = $"Unsupported condition operator '{op ?? "unknown"}' requires human review before publishing.";
                return null;
            }

            if (!root.TryGetProperty("value", out _))
            {
                conditionNote = "Condition value is missing and requires human review before publishing.";
                return null;
            }

            return JsonSerializer.Serialize(root);
        }
        catch (JsonException)
        {
            conditionNote = "Condition JSON was invalid and requires human review before publishing.";
            return null;
        }
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ExtractJson(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline >= 0 && lastFence > firstNewline)
                trimmed = trimmed[(firstNewline + 1)..lastFence].Trim();
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        return start >= 0 && end > start ? trimmed[start..(end + 1)] : null;
    }

    private static string TrimForLog(string value) =>
        value.Length <= 500 ? value : value[..500];

    private static string PrepareGuidelineText(string guidelineText)
    {
        if (guidelineText.Length <= MaxGuidelineTextChars)
            return guidelineText;

        return guidelineText[..MaxGuidelineTextChars] +
            "\n\n[Guideline text truncated before AI interpretation because the extracted attachment was too large.]";
    }

    private const string SystemPrompt = """
        You interpret insurance underwriting guideline text into SIMS proposed underwriting controls.
        Return JSON only with this shape: {"controls":[...]}.
        Each control must include itemType, stage, severity, ruleKey, label, description, conditionJson, isBlocking, overrideAllowed, overridePermission, sourceCitation, aiConfidence, sortOrder.
        Allowed itemType values: AppetiteRule, ReferralTrigger, AuthorityLimit, DocumentChecklistItem, AppetiteNote.
        Allowed stage values: Submission, Quote, Bind, Issue, PostBind, Renewal.
        Allowed severity values: Informational, Warning, ReferralRequired, HardBlock.
        Use conditionJson null for unconditional blockers or checklist requirements.
        For conditional referrals/blockers, conditionJson must be a JSON string with field/operator/value only.
        Allowed condition fields: largestSingleItemValue, totalInsuredValue, premiumAmount, totalPremium, lossRatio, driverCount, vehicleCount, isFilingState.
        Allowed operators: >, >=, <, <=, ==, !=.
        If a guideline needs a field not listed above, set conditionJson to null and mention the missing field in description or sourceCitation.
        Do not approve, publish, or enforce controls. These are only proposed controls for human review.
        """;

    private sealed record AnthropicMessageResponse(IReadOnlyList<AnthropicContentBlock>? Content);
    private sealed record AnthropicContentBlock(string? Type, string? Text);
    private sealed record AiGuidelineControlPayload(IReadOnlyList<AiGuidelineControlItem>? Controls);

    private sealed record AiGuidelineControlItem(
        string? ItemType,
        string? Stage,
        string? Severity,
        string? RuleKey,
        string? Label,
        string? Description,
        string? ConditionJson,
        bool IsBlocking,
        bool OverrideAllowed,
        string? OverridePermission,
        string? SourceCitation,
        decimal? AiConfidence,
        int SortOrder);
}
