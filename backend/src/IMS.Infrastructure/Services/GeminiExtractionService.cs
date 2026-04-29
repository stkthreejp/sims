using System.Net.Http.Json;
using System.Text.Json;
using IMS.Application.DTOs.Gemini;
using IMS.Application.Interfaces.Services;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IMS.Infrastructure.Services;

public class GeminiExtractionService : IGeminiExtractionService
{
    private readonly IBlobStorageService _blobStorage;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiExtractionService> _logger;
    private readonly string _apiKey;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

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

    public async Task<GeminiExtractionResult?> ExtractFromAttachmentsAsync(
        IEnumerable<EmailAttachment> attachments, CancellationToken ct = default)
    {
        // Include any PDF — recognized ACORD types use targeted prompts,
        // Unknown/Other PDFs use the generic prompt. Images are skipped.
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

        var merged = new GeminiExtractionResult();

        foreach (var attachment in eligible)
        {
            try
            {
                _logger.LogInformation("Extracting from attachment {FileName} (type: {DocType})", attachment.FileName, attachment.DocumentType);
                var result = await ExtractSingleAsync(attachment, ct);
                if (result != null)
                {
                    _logger.LogInformation("Extraction succeeded for {FileName}", attachment.FileName);
                    MergeInto(merged, result);
                }
                else
                {
                    _logger.LogWarning("Extraction returned null for {FileName}", attachment.FileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini extraction failed for attachment {FileName}", attachment.FileName);
            }
        }

        return merged;
    }

    private async Task<GeminiExtractionResult?> ExtractSingleAsync(EmailAttachment attachment, CancellationToken ct)
    {
        byte[] bytes;
        try
        {
            bytes = await _blobStorage.DownloadAsync(attachment.BlobUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not download blob {BlobUrl}", attachment.BlobUrl);
            return null;
        }

        var base64 = Convert.ToBase64String(bytes);
        var mimeType = string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/pdf" : attachment.ContentType;

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { inline_data = new { mime_type = mimeType, data = base64 } },
                        new { text = GetPrompt(attachment.DocumentType) }
                    }
                }
            },
            generationConfig = new { responseMimeType = "application/json", temperature = 0.0 }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_apiKey}";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(url, requestBody, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Gemini API returned {Status} for {FileName}: {Body}", response.StatusCode, attachment.FileName, errorBody);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini API call failed for {FileName}", attachment.FileName);
            return null;
        }

        var raw = await response.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            return JsonSerializer.Deserialize<GeminiExtractionResult>(text, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not deserialize Gemini response for {FileName}: {Text}", attachment.FileName, text);
            return null;
        }
    }

    private static bool IsPdf(EmailAttachment a) =>
        a.ContentType?.Contains("pdf", StringComparison.OrdinalIgnoreCase) == true
        || a.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    private static string GetPrompt(EmailAttachmentDocumentType docType) => docType switch
    {
        EmailAttachmentDocumentType.Acord125 => CommercialAutoPrompt,
        EmailAttachmentDocumentType.Acord126 => GeneralLiabilityPrompt,
        EmailAttachmentDocumentType.ScheduleOfValues => InlandMarinePrompt,
        _ => GenericPrompt,
    };

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

    private static void MergeInto(GeminiExtractionResult target, GeminiExtractionResult source)
    {
        target.DescriptionOfOperations ??= source.DescriptionOfOperations;
        target.Dba ??= source.Dba;
        target.EntityType ??= source.EntityType;
        target.YearsInBusiness ??= source.YearsInBusiness;
        target.Drivers.AddRange(source.Drivers);
        target.Vehicles.AddRange(source.Vehicles);
        target.Locations.AddRange(source.Locations);
        target.PriorCarriers.AddRange(source.PriorCarriers);
        target.Supplemental ??= source.Supplemental;
        target.GLCoverages ??= source.GLCoverages;
        target.GLClassifications.AddRange(source.GLClassifications);
        target.IMCoverages ??= source.IMCoverages;
        target.Equipment.AddRange(source.Equipment);
    }
}
