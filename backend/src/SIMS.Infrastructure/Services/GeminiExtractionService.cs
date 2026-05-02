using System.Net.Http.Json;
using System.Text.Json;
using SIMS.Application.DTOs.Gemini;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SIMS.Infrastructure.Services;

public class GeminiExtractionService : IGeminiExtractionService
{
    private readonly IBlobStorageService _blobStorage;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiExtractionService> _logger;
    private readonly string _apiKey;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // LOBs Gemini is allowed to return from the detection prompt
    private static readonly HashSet<string> KnownLobs = new(StringComparer.OrdinalIgnoreCase)
    {
        "CommercialAuto", "GeneralLiability", "InlandMarine", "Property",
        "WorkersCompensation", "BusinessOwners", "ProfessionalLiability", "Umbrella",
    };

    public GeminiExtractionService(
        IBlobStorageService blobStorage,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<GeminiExtractionService> logger)
    {
        _blobStorage = blobStorage;
        _httpClient = httpClientFactory.CreateClient("gemini");
        _logger = logger;
        _apiKey = config["GeminiApi:ApiKey"]
            ?? throw new InvalidOperationException("GeminiApi:ApiKey is not configured.");
    }

    public async Task<List<GeminiLobExtraction>?> ExtractFromAttachmentsAsync(
        IEnumerable<EmailAttachment> attachments, string? lineOfBusinessHint = null, CancellationToken ct = default)
    {
        var eligible = attachments
            .Where(a => a.DocumentType is
                EmailAttachmentDocumentType.Acord125 or
                EmailAttachmentDocumentType.Acord126 or
                EmailAttachmentDocumentType.LossRun or
                EmailAttachmentDocumentType.ScheduleOfValues or
                EmailAttachmentDocumentType.SignedApplication
                || IsPdf(a))
            .ToList();

        _logger.LogInformation(
            "Gemini extraction: {Total} attachments total, {Eligible} eligible PDFs",
            attachments.Count(), eligible.Count);

        if (eligible.Count == 0)
        {
            _logger.LogInformation("No eligible PDF attachments found — skipping extraction");
            return null;
        }

        // Accumulate results keyed by LOB; same LOB across multiple attachments is merged
        var resultsByLob = new Dictionary<string, GeminiExtractionResult>(StringComparer.OrdinalIgnoreCase);

        foreach (var attachment in eligible)
        {
            try
            {
                var downloaded = await DownloadAttachmentAsync(attachment, ct);
                if (downloaded == null) continue;
                var (bytes, mimeType) = downloaded.Value;

                // Determine the LOB(s) to extract for this attachment
                var knownLob = attachment.DocumentType switch
                {
                    EmailAttachmentDocumentType.Acord125 => "CommercialAuto",
                    EmailAttachmentDocumentType.Acord126 => "GeneralLiability",
                    EmailAttachmentDocumentType.ScheduleOfValues => "InlandMarine",
                    _ => null,
                };

                if (knownLob != null)
                {
                    _logger.LogInformation("Extracting {Lob} from {FileName} (known ACORD type)", knownLob, attachment.FileName);
                    var data = await ExtractWithPromptAsync(bytes, mimeType, GetPromptForLob(knownLob), attachment.FileName, ct);
                    if (data != null) Accumulate(resultsByLob, knownLob, data);
                }
                else
                {
                    // Unknown/Other PDF — ask Gemini which LOBs it contains
                    _logger.LogInformation("Running LOB detection on {FileName}", attachment.FileName);
                    var detectedLobs = await DetectLinesOfBusinessAsync(bytes, mimeType, attachment.FileName, ct);

                    if (detectedLobs.Count == 0)
                    {
                        if (!string.IsNullOrWhiteSpace(lineOfBusinessHint))
                        {
                            // User told us the LOB — use the targeted prompt
                            _logger.LogInformation("No LOBs detected in {FileName} — using hint {Hint}", attachment.FileName, lineOfBusinessHint);
                            var data = await ExtractWithPromptAsync(bytes, mimeType, GetPromptForLob(lineOfBusinessHint), attachment.FileName, ct);
                            if (data != null) Accumulate(resultsByLob, lineOfBusinessHint, data);
                        }
                        else
                        {
                            // No hint and no detection — run generic extraction but signal the caller
                            // by using an empty string as the LOB key so InboundEmailService can set
                            // extractionStatus = "DetectionFailed" and prompt the user.
                            _logger.LogInformation("No LOBs detected in {FileName} and no hint — using generic prompt (detection failed)", attachment.FileName);
                            var data = await ExtractWithPromptAsync(bytes, mimeType, GenericPrompt, attachment.FileName, ct);
                            if (data != null) Accumulate(resultsByLob, "", data);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Detected {Count} LOB(s) in {FileName}: {Lobs}",
                            detectedLobs.Count, attachment.FileName, string.Join(", ", detectedLobs));

                        foreach (var lob in detectedLobs)
                        {
                            var data = await ExtractWithPromptAsync(bytes, mimeType, GetPromptForLob(lob), attachment.FileName, ct);
                            if (data != null) Accumulate(resultsByLob, lob, data);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini extraction failed for attachment {FileName}", attachment.FileName);
            }
        }

        if (resultsByLob.Count == 0)
        {
            _logger.LogWarning("Extraction ran but produced no results");
            return [];
        }

        var results = resultsByLob
            .Select(kvp => new GeminiLobExtraction(kvp.Key, kvp.Value))
            .ToList();

        _logger.LogInformation("Extraction complete — {Count} LOB(s): {Lobs}",
            results.Count, string.Join(", ", results.Select(r => r.LineOfBusiness)));

        return results;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static void Accumulate(Dictionary<string, GeminiExtractionResult> dict, string lob, GeminiExtractionResult data)
    {
        if (dict.TryGetValue(lob, out var existing))
            GeminiExtractionResult.MergeInto(existing, data);
        else
            dict[lob] = data;
    }

    private async Task<(byte[] bytes, string mimeType)?> DownloadAttachmentAsync(EmailAttachment attachment, CancellationToken ct)
    {
        try
        {
            var bytes = await _blobStorage.DownloadAsync(attachment.BlobUrl);
            var mimeType = string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/pdf" : attachment.ContentType;
            return (bytes, mimeType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not download blob {BlobUrl}", attachment.BlobUrl);
            return null;
        }
    }

    private async Task<List<string>> DetectLinesOfBusinessAsync(byte[] bytes, string mimeType, string fileName, CancellationToken ct)
    {
        var requestBody = BuildRequest(bytes, mimeType, DetectionPrompt);
        var url = GeminiUrl;

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(url, requestBody, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Gemini detection returned {Status} for {FileName}: {Body}", response.StatusCode, fileName, err);
                return [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini detection call failed for {FileName}", fileName);
            return [];
        }

        var raw = await response.Content.ReadAsStringAsync(ct);
        try
        {
            var text = ExtractTextFromResponse(raw);
            if (string.IsNullOrWhiteSpace(text)) return [];

            using var inner = JsonDocument.Parse(text);
            var lobs = new List<string>();
            foreach (var item in inner.RootElement.GetProperty("linesOfBusiness").EnumerateArray())
            {
                var v = item.GetString();
                if (!string.IsNullOrWhiteSpace(v) && KnownLobs.Contains(v))
                    lobs.Add(v);
            }
            return lobs;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse detection response for {FileName}", fileName);
            return [];
        }
    }

    private async Task<GeminiExtractionResult?> ExtractWithPromptAsync(
        byte[] bytes, string mimeType, string prompt, string fileName, CancellationToken ct)
    {
        _logger.LogInformation("Sending {Bytes} bytes to Gemini for {FileName}", bytes.Length, fileName);

        var requestBody = BuildRequest(bytes, mimeType, prompt);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(GeminiUrl, requestBody, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Gemini API returned {Status} for {FileName}: {Body}", response.StatusCode, fileName, err);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini API call failed for {FileName}", fileName);
            return null;
        }

        var raw = await response.Content.ReadAsStringAsync(ct);
        var text = ExtractTextFromResponse(raw);
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            return JsonSerializer.Deserialize<GeminiExtractionResult>(text, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not deserialize Gemini response for {FileName}: {Text}", fileName, text);
            return null;
        }
    }

    private string GeminiUrl =>
        $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_apiKey}";

    private static object BuildRequest(byte[] bytes, string mimeType, string prompt) => new
    {
        contents = new[]
        {
            new
            {
                parts = new object[]
                {
                    new { inline_data = new { mime_type = mimeType, data = Convert.ToBase64String(bytes) } },
                    new { text = prompt }
                }
            }
        },
        generationConfig = new { responseMimeType = "application/json", temperature = 0.0 }
    };

    private static string? ExtractTextFromResponse(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
    }

    private static bool IsPdf(EmailAttachment a) =>
        a.ContentType?.Contains("pdf", StringComparison.OrdinalIgnoreCase) == true
        || a.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    private static string GetPromptForLob(string lob) => lob switch
    {
        "CommercialAuto" => CommercialAutoPrompt,
        "GeneralLiability" => GeneralLiabilityPrompt,
        "InlandMarine" or "Property" => InlandMarinePrompt,
        _ => GenericPrompt,
    };

    // -------------------------------------------------------------------------
    // Prompts
    // -------------------------------------------------------------------------

    private const string DetectionPrompt = """
        Identify all lines of insurance business that this document contains applications or schedules for.
        Return ONLY valid JSON with this exact schema: {"linesOfBusiness": ["..."]}
        Use ONLY these exact string values — include all that apply:
        "CommercialAuto"         — commercial auto application (ACORD 125 or similar)
        "GeneralLiability"       — general liability application (ACORD 126 or similar)
        "InlandMarine"           — inland marine or equipment schedule / statement of values
        "Property"               — commercial property application
        "WorkersCompensation"    — workers compensation application
        "BusinessOwners"         — business owners policy (BOP) application
        "ProfessionalLiability"  — professional liability or E&O application
        "Umbrella"               — umbrella or excess liability application
        Return an empty array [] if the document is NOT an insurance application (e.g. loss run, dec page, certificate).
        """;

    private const string CommercialAutoPrompt = """
        Extract all data from this commercial auto insurance application (ACORD 125 or similar).
        Return ONLY valid JSON. Use null for fields you cannot find. Use empty arrays for lists you cannot find.
        Schema:
        {
          "descriptionOfOperations": "string or null",
          "dba": "string or null",
          "entityType": "Individual|SoleProprietor|Partnership|LLC|Corporation|Trust|Other or null",
          "yearsInBusiness": number or null,
          "drivers": [{"driverNumber":number,"name":"string","dateOfBirth":"YYYY-MM-DD or null","licenseNumber":"string or null","licenseState":"2-letter or null","dateHired":"YYYY-MM-DD or null"}],
          "vehicles": [{"unitNumber":number,"year":number or null,"make":"string or null","model":"string or null","vin":"string or null","gvw":number or null,"vehicleClass":"Truck|Tractor|Trailer or null","garagingZip":"string or null","radius":"Local|Intermediate or null"}],
          "locations": [],
          "priorCarriers": [{"lineOfBusiness":"CommercialAuto","carrierName":"string","policyNumber":"string or null","expirationDate":"YYYY-MM-DD or null","premium":number or null}],
          "supplemental": {"commoditiesHauled":["string"],"terminalLocations":["string"],"filingsRequired":["string"],"safetyProgramInPlace":true,"ownerOperator":false},
          "glCoverages": null,
          "glClassifications": [],
          "imCoverages": null,
          "equipment": []
        }
        """;

    private const string GeneralLiabilityPrompt = """
        Extract all data from this general liability application (ACORD 126 or similar).
        Return ONLY valid JSON. Use null for fields you cannot find. Use empty arrays for lists you cannot find.
        Schema:
        {
          "descriptionOfOperations": "string or null",
          "dba": "string or null",
          "entityType": "Individual|SoleProprietor|Partnership|LLC|Corporation|Trust|Other or null",
          "yearsInBusiness": number or null,
          "drivers": [],
          "vehicles": [],
          "locations": [{"locationNumber":number,"address":"string","zipCode":"string or null"}],
          "priorCarriers": [{"lineOfBusiness":"GeneralLiability","carrierName":"string","policyNumber":"string or null","expirationDate":"YYYY-MM-DD or null","premium":number or null}],
          "supplemental": null,
          "glCoverages": {"generalAggregate":number or null,"productsCompletedOps":number or null,"eachOccurrence":number or null,"personalAndAdvInjury":number or null,"damageToRentedPremises":number or null,"medicalExpense":number or null,"totalSubcontractorCost":number or null},
          "glClassifications": [{"locationNumber":number or null,"classCode":"string","description":"string","premiumBasis":"string or null","exposure":number or null}],
          "imCoverages": null,
          "equipment": []
        }
        """;

    private const string InlandMarinePrompt = """
        Extract all data from this inland marine / equipment schedule.
        Return ONLY valid JSON. Use null for fields you cannot find. Use empty arrays for lists you cannot find.
        Schema:
        {
          "descriptionOfOperations": null,
          "dba": null,
          "entityType": null,
          "yearsInBusiness": null,
          "drivers": [],
          "vehicles": [],
          "locations": [],
          "priorCarriers": [],
          "supplemental": null,
          "glCoverages": null,
          "glClassifications": [],
          "imCoverages": {"scheduledEquipmentTotalLimit":number or null,"unscheduledEquipmentLimit":number or null,"maximumValueAnyOneItem":number or null,"deductible":number or null,"coinsurancePercentage":number or null},
          "equipment": [{"itemNumber":number or null,"year":number or null,"make":"string or null","model":"string or null","description":"string","serialNumber":"string or null","value":number or null}]
        }
        """;

    private const string GenericPrompt = """
        Extract all insurance application data from this document.
        Return ONLY valid JSON. Use null for fields you cannot find. Use empty arrays for lists you cannot find.
        Schema:
        {
          "descriptionOfOperations": "string or null",
          "dba": "string or null",
          "entityType": "Individual|SoleProprietor|Partnership|LLC|Corporation|Trust|Other or null",
          "yearsInBusiness": number or null,
          "drivers": [],
          "vehicles": [],
          "locations": [],
          "priorCarriers": [],
          "supplemental": null,
          "glCoverages": null,
          "glClassifications": [],
          "imCoverages": null,
          "equipment": []
        }
        """;
}
