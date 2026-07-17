using System.Net.Http.Json;
using System.Text.Json;
using SIMS.Application.DTOs.DocumentExtraction;
using SIMS.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SIMS.Infrastructure.Services;

/// <summary>
/// Claude-vision implementation of the submission intake analyzer. Sends the rendered
/// pages to the Anthropic Messages API and asks for a single JSON object describing the
/// document/LOB boundaries, the monoline quoting line, and per-LOB extracted fields.
///
/// Privacy posture (design §7): first-party Anthropic API with Zero Data Retention +
/// no-training (contractual) and <c>inference_geo</c> pinned to a region (request param).
/// The model is read from config and must stay ZDR-eligible (Opus 4.8 / Sonnet 5 / Haiku).
/// </summary>
public class ClaudeSubmissionIntakeAnalyzer : ISubmissionIntakeAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClaudeSubmissionIntakeAnalyzer> _logger;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly string _inferenceGeo;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ClaudeSubmissionIntakeAnalyzer(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<ClaudeSubmissionIntakeAnalyzer> logger)
    {
        _httpClient = httpClientFactory.CreateClient("anthropic");
        _logger = logger;
        // Validate the key lazily at the call site, never in the ctor — a service ctor that
        // throws on missing config breaks DI for every consumer (see the inbox-500 fix).
        // Key is read the same way as AnthropicGuidelineLlmInterpreterService: the deployed
        // value is the flat ANTHROPIC_API_KEY app setting, not Anthropic:ApiKey.
        _apiKey = config["ANTHROPIC_API_KEY"]
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? config["Anthropic:ApiKey"];
        _model = config["Anthropic:Model"] ?? config["ANTHROPIC_MODEL"] ?? "claude-opus-4-8";
        _inferenceGeo = config["Anthropic:InferenceGeo"] ?? config["ANTHROPIC_INFERENCE_GEO"] ?? "us";
    }

    public async Task<SubmissionAnalysis?> AnalyzeSubmissionAsync(
        IReadOnlyList<RenderedPage> pages, string? emailBodyContext, CancellationToken ct = default)
    {
        if (pages.Count == 0)
        {
            _logger.LogInformation("Intake analysis: no rendered pages — skipping.");
            return null;
        }

        var apiKey = _apiKey
            ?? throw new InvalidOperationException("Anthropic:ApiKey is not configured.");

        // Content = one image block per page, then the instruction text.
        var content = new List<object>(pages.Count + 1);
        foreach (var page in pages)
        {
            content.Add(new
            {
                type = "image",
                source = new
                {
                    type = "base64",
                    media_type = "image/png",
                    data = Convert.ToBase64String(page.PngBytes),
                },
            });
        }
        content.Add(new { type = "text", text = BuildPrompt(emailBodyContext) });

        var body = new
        {
            model = _model,
            max_tokens = 16000,
            inference_geo = _inferenceGeo,
            messages = new[] { new { role = "user", content } },
        };

        string raw;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
            {
                Content = JsonContent.Create(body),
            };
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");

            var resp = await _httpClient.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Claude intake analysis returned {Status}.", resp.StatusCode);
                return null;
            }
            raw = await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Claude intake analysis call failed.");
            return null;
        }

        return ParseResponse(raw);
    }

    private SubmissionAnalysis? ParseResponse(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.TryGetProperty("stop_reason", out var stop) && stop.GetString() == "refusal")
            {
                _logger.LogWarning("Claude intake analysis was refused by the model.");
                return null;
            }

            // Concatenate all text blocks from the response content array.
            if (!root.TryGetProperty("content", out var contentArr) || contentArr.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Claude intake analysis response had no content array.");
                return null;
            }

            var sb = new System.Text.StringBuilder();
            foreach (var block in contentArr.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var t) && t.GetString() == "text"
                    && block.TryGetProperty("text", out var txt))
                {
                    sb.Append(txt.GetString());
                }
            }

            var json = ExtractJsonObject(sb.ToString());
            if (json == null)
            {
                _logger.LogWarning("Claude intake analysis response contained no JSON object.");
                return null;
            }

            var analysis = JsonSerializer.Deserialize<SubmissionAnalysis>(json, JsonOpts);
            if (analysis == null)
            {
                _logger.LogWarning("Claude intake analysis JSON deserialized to null.");
                return null;
            }

            _logger.LogInformation(
                "Intake analysis complete — {Spans} form span(s), quoting line {Line}, {Lobs} LOB extraction(s).",
                analysis.Boundaries.Count, analysis.QuotingLineOfBusiness ?? "(none)", analysis.PerLob.Count);
            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Claude intake analysis response.");
            return null;
        }
    }

    /// <summary>Pulls the first JSON object out of the text (tolerates code fences / preamble).</summary>
    private static string? ExtractJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : null;
    }

    private static string BuildPrompt(string? emailBodyContext)
    {
        var brokerNote = string.IsNullOrWhiteSpace(emailBodyContext)
            ? "(no broker email text provided)"
            : emailBodyContext.Trim();

        return $$"""
        You are an insurance submission intake assistant for a monoline MGA. The images are the
        pages, in order (page 1 = first image), of a combined submission PDF containing ACORD
        forms, supplemental applications, and possibly loss runs. Analyze them and respond with
        ONLY a single JSON object (no prose, no code fences) with this exact shape:

        {
          "boundaries": [
            { "startPage": 1, "endPage": 2, "form": "Acord125", "lineOfBusiness": "GeneralLiability" }
          ],
          "quotingLineOfBusiness": "GeneralLiability",
          "perLob": [
            { "lineOfBusiness": "GeneralLiability", "data": {
                "descriptionOfOperations": null, "dba": null, "entityType": null, "yearsInBusiness": null,
                "drivers": [], "vehicles": [], "locations": [], "priorCarriers": [],
                "supplemental": null, "glCoverages": null, "glClassifications": [],
                "imCoverages": null, "equipment": []
            } }
          ],
          "confidence": "High",
          "rationale": "one short sentence"
        }

        Rules:
        - Page numbers are 1-indexed and inclusive; every span must map to a contiguous run of pages.
        - "form" MUST be EXACTLY one of these literal values, copied verbatim (case-sensitive): "Acord125", "Acord126", "Acord127", "Acord146", "LossRun", "ScheduleOfValues", "SignedApplication", "Other". Any ACORD form NOT in this list (e.g. ACORD 45, 140, 175, 855) and any other or unrecognized document MUST be labeled "Other". Never output a "form" value outside this exact set — do not invent labels like "Acord45".
        - "lineOfBusiness" MUST be EXACTLY one of these literal values: "GeneralLiability", "InlandMarine", "AutoLiability", "AutoPhysicalDamage" — or null if the span is not line-specific. Never output any other value.
        - "quotingLineOfBusiness" MUST be one of those same four line values (or null). Monoline: pick the single most likely quoting line from the forms + the broker note. Still list every other line present in "boundaries".
        - In "perLob.data", populate only the fields you can read; leave others null / empty arrays. Use the exact field names shown.
        - If a value is unreadable, omit it rather than guessing.

        Broker email context:
        {{brokerNote}}
        """;
    }
}
