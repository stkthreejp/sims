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
    private const string DefaultModelId = "claude-sonnet-4-20250514";
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
            return [];

        var modelId = await ResolveModelIdAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = JsonContent.Create(new
        {
            model = modelId,
            max_tokens = 6000,
            temperature = 0,
            system = SystemPrompt,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = $"Interpret this underwriting guideline text into proposed SIMS controls.\n\n{guidelineText}"
                }
            }
        });

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Anthropic guideline interpretation failed with status {StatusCode}", response.StatusCode);
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var message = await JsonSerializer.DeserializeAsync<AnthropicMessageResponse>(stream, JsonOptions, ct);
        var text = message?.Content?.FirstOrDefault(c => string.Equals(c.Type, "text", StringComparison.OrdinalIgnoreCase))?.Text;
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var payloadJson = ExtractJson(text);
        if (string.IsNullOrWhiteSpace(payloadJson))
            return [];

        var payload = JsonSerializer.Deserialize<AiGuidelineControlPayload>(payloadJson, JsonOptions);
        if (payload?.Controls is null || payload.Controls.Count == 0)
            return [];

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

        return modelId.Trim();
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
