using System.Text;
using System.Text.RegularExpressions;
using IMS.Application.Common;
using IMS.Application.Interfaces.Services;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;

namespace IMS.Infrastructure.Services;

public class DocumentGenerationService : IDocumentGenerationService
{
    private readonly IServiceProvider _sp;
    private readonly IBlobStorageService _blob;

    public DocumentGenerationService(IServiceProvider sp, IBlobStorageService blob)
    {
        _sp = sp;
        _blob = blob;
    }

    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public async Task<Result<string>> GenerateAsync(Guid templateId, TemplateEntityType entityType, Guid entityId)
    {
        // ── 1. Load template ──────────────────────────────────────────────────
        var template = await Db.Set<DocumentTemplate>().FindAsync(templateId);
        if (template == null)
            return Result<string>.Failure("NOT_FOUND", "Template not found.");

        // ── 2. Build tag data dictionary ──────────────────────────────────────
        Dictionary<string, string> data;
        try
        {
            data = await BuildDataDictionaryAsync(entityType, entityId);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure("DATA_ERROR", $"Could not load entity data: {ex.Message}");
        }

        // ── 3. Fill {{tags}} in HTML ──────────────────────────────────────────
        var filledHtml = FillTags(template.HtmlContent, data);

        // ── 4. Wrap in full HTML document with print-ready CSS ────────────────
        var fullHtml = BuildFullHtml(filledHtml, template.Name);

        // ── 5. Convert HTML → Word → PDF via Syncfusion ───────────────────────
        byte[] pdfBytes;
        try
        {
            pdfBytes = ConvertHtmlToPdf(fullHtml);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure("CONVERSION_ERROR", $"PDF conversion failed: {ex.Message}");
        }

        // ── 6. Store in Azure Blob ────────────────────────────────────────────
        var fileName = $"{SanitizeFileName(template.Name)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
        using var stream = new MemoryStream(pdfBytes);
        var blobPath = await _blob.UploadAsync(stream, fileName, "application/pdf");

        // ── 7. Return signed download URL ─────────────────────────────────────
        var url = await _blob.GetDownloadUrlAsync(blobPath, fileName, TimeSpan.FromHours(2));
        return Result<string>.Success(url);
    }

    // ── Tag replacement ───────────────────────────────────────────────────────

    private static string FillTags(string html, Dictionary<string, string> data)
    {
        // Replace TipTap tag node spans: <span data-tag="TagName" ...>{{TagName}}</span>
        html = Regex.Replace(
            html,
            @"<span[^>]*data-tag=""(\w+)""[^>]*>\{\{[^}]+\}\}</span>",
            m =>
            {
                var tag = m.Groups[1].Value;
                return data.TryGetValue(tag, out var val) ? val : $"[{tag}]";
            });

        // Also replace any plain {{TagName}} that might remain
        html = Regex.Replace(
            html,
            @"\{\{(\w+)\}\}",
            m =>
            {
                var tag = m.Groups[1].Value;
                return data.TryGetValue(tag, out var val) ? val : $"[{tag}]";
            });

        return html;
    }

    private static string BuildFullHtml(string bodyHtml, string title)
    {
        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8" />
              <title>{{title}}</title>
              <style>
                @page { margin: 1in; }
                body {
                  font-family: Arial, sans-serif;
                  font-size: 11pt;
                  line-height: 1.5;
                  color: #1a1a1a;
                }
                h1 { font-size: 18pt; font-weight: bold; margin: 0 0 12px; }
                h2 { font-size: 14pt; font-weight: bold; margin: 0 0 10px; }
                h3 { font-size: 12pt; font-weight: bold; margin: 0 0 8px; }
                p  { margin: 0 0 8px; }
                table {
                  border-collapse: collapse;
                  width: 100%;
                  margin-bottom: 12px;
                }
                td, th {
                  border: 1px solid #ccc;
                  padding: 6px 10px;
                  text-align: left;
                  vertical-align: top;
                }
                th {
                  background-color: #f0f0f0;
                  font-weight: bold;
                }
                ul, ol { margin: 0 0 8px; padding-left: 20px; }
                .page-break { page-break-after: always; }
              </style>
            </head>
            <body>
              {{bodyHtml}}
            </body>
            </html>
            """;
    }

    private static byte[] ConvertHtmlToPdf(string html)
    {
        using var htmlStream = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var wordDoc = new WordDocument(htmlStream, FormatType.Html);
        using var renderer = new DocIORenderer();
        using var pdfDoc = renderer.ConvertToPDF(wordDoc);
        using var pdfStream = new MemoryStream();
        pdfDoc.Save(pdfStream);
        return pdfStream.ToArray();
    }

    // ── Data dictionary builders ──────────────────────────────────────────────

    private async Task<Dictionary<string, string>> BuildDataDictionaryAsync(
        TemplateEntityType entityType, Guid entityId)
    {
        var today = DateTime.Today.ToString("MM/dd/yyyy");

        var data = new Dictionary<string, string>
        {
            ["TodayDate"] = today,
            ["CompanyName"] = "Specialty Market Managers",
        };

        switch (entityType)
        {
            case TemplateEntityType.Policy:
                await AddPolicyDataAsync(data, entityId);
                break;
            case TemplateEntityType.Submission:
                await AddSubmissionDataAsync(data, entityId);
                break;
            case TemplateEntityType.Carrier:
                await AddCarrierDataAsync(data, entityId);
                break;
            case TemplateEntityType.Agent:
                await AddAgentDataAsync(data, entityId);
                break;
        }

        return data;
    }

    private async Task AddPolicyDataAsync(Dictionary<string, string> d, Guid quoteId)
    {
        var quote = await Db.Set<Quote>()
            .Include(q => q.Carrier)
            .Include(q => q.Submission).ThenInclude(s => s.Insured)
            .Include(q => q.Submission).ThenInclude(s => s.Agent)
                .ThenInclude(a => a!.Locations)
            .Include(q => q.Submission).ThenInclude(s => s.Underwriter)
            .FirstOrDefaultAsync(q => q.Id == quoteId)
            ?? throw new Exception("Policy not found.");

        var insured = quote.Submission.Insured;
        var carrier = quote.Carrier;
        var agent = quote.Submission.Agent;
        var uw = quote.Submission.Underwriter;
        var primaryLocation = agent?.Locations.FirstOrDefault(l => l.IsPrimary)
                           ?? agent?.Locations.FirstOrDefault();

        d["PolicyNumber"] = quote.PolicyNumber ?? string.Empty;
        d["EffectiveDate"] = quote.EffectiveDate.ToString("MM/dd/yyyy");
        d["ExpirationDate"] = quote.ExpirationDate.ToString("MM/dd/yyyy");
        d["BoundDate"] = quote.BoundDate?.ToString("MM/dd/yyyy") ?? string.Empty;
        d["LineOfBusiness"] = quote.LineOfBusiness.ToString();
        d["TotalPremium"] = quote.TotalPremium.ToString("C");
        d["NetPremium"] = quote.PremiumAmount.ToString("C");
        d["TaxesAndFees"] = quote.TaxesAndFees.ToString("C");
        d["CommissionRate"] = $"{quote.CommissionRate:0.##}%";
        d["CommissionAmount"] = quote.CommissionAmount.ToString("C");
        d["Deductible"] = quote.Deductible?.ToString("C") ?? string.Empty;
        d["CoverageLimit"] = quote.Limit?.ToString("C") ?? string.Empty;
        d["CoverageDescription"] = quote.CoverageDescription ?? string.Empty;

        d["InsuredName"] = insured.DisplayName;
        d["InsuredType"] = insured.InsuredType.ToString();
        d["InsuredEmail"] = insured.Email ?? string.Empty;
        d["InsuredPhone"] = insured.Phone ?? string.Empty;
        d["InsuredAddressLine1"] = insured.AddressLine1;
        d["InsuredAddressLine2"] = insured.AddressLine2 ?? string.Empty;
        d["InsuredCity"] = insured.City;
        d["InsuredState"] = insured.State;
        d["InsuredZip"] = insured.ZipCode;
        d["InsuredCounty"] = insured.County ?? string.Empty;

        d["CarrierName"] = carrier.Name;
        d["CarrierNAIC"] = carrier.Naic ?? string.Empty;
        d["CarrierAMBest"] = carrier.AmBestRating ?? string.Empty;
        d["CarrierAddress"] = FormatAddress(carrier.AddressLine1, carrier.City, carrier.State, carrier.ZipCode);

        d["AgentName"] = agent?.Name ?? string.Empty;
        d["AgentAgency"] = agent?.AgencyName ?? string.Empty;
        d["AgentEmail"] = agent?.Email ?? string.Empty;
        d["AgentPhone"] = primaryLocation?.Phone ?? agent?.Phone ?? string.Empty;
        d["AgentLicense"] = agent?.LicenseNumber ?? string.Empty;
        d["AgentCity"] = primaryLocation?.City ?? string.Empty;
        d["AgentState"] = primaryLocation?.State ?? string.Empty;

        d["UnderwriterName"] = uw.FullName;
        d["UnderwriterEmail"] = uw.Email ?? string.Empty;
    }

    private async Task AddSubmissionDataAsync(Dictionary<string, string> d, Guid submissionId)
    {
        var sub = await Db.Set<Submission>()
            .Include(s => s.Insured)
            .Include(s => s.Agent).ThenInclude(a => a!.Locations)
            .Include(s => s.Underwriter)
            .FirstOrDefaultAsync(s => s.Id == submissionId)
            ?? throw new Exception("Submission not found.");

        var insured = sub.Insured;
        var agent = sub.Agent;
        var primaryLocation = agent?.Locations.FirstOrDefault(l => l.IsPrimary)
                           ?? agent?.Locations.FirstOrDefault();

        d["SubmissionNumber"] = sub.SubmissionNumber;
        d["SubmissionDate"] = sub.CreatedAt.ToString("MM/dd/yyyy");
        d["RequestedEffDate"] = sub.EffectiveDate?.ToString("MM/dd/yyyy") ?? string.Empty;
        d["SubmissionStatus"] = sub.Status.ToString();

        d["InsuredName"] = insured.DisplayName;
        d["InsuredType"] = insured.InsuredType.ToString();
        d["InsuredEmail"] = insured.Email ?? string.Empty;
        d["InsuredPhone"] = insured.Phone ?? string.Empty;
        d["InsuredAddressLine1"] = insured.AddressLine1;
        d["InsuredCity"] = insured.City;
        d["InsuredState"] = insured.State;
        d["InsuredZip"] = insured.ZipCode;

        d["AgentName"] = agent?.Name ?? string.Empty;
        d["AgentAgency"] = agent?.AgencyName ?? string.Empty;
        d["AgentEmail"] = agent?.Email ?? string.Empty;
        d["AgentPhone"] = primaryLocation?.Phone ?? agent?.Phone ?? string.Empty;

        d["UnderwriterName"] = sub.Underwriter.FullName;
        d["UnderwriterEmail"] = sub.Underwriter.Email ?? string.Empty;
    }

    private async Task AddCarrierDataAsync(Dictionary<string, string> d, Guid carrierId)
    {
        var carrier = await Db.Set<Carrier>().FindAsync(carrierId)
            ?? throw new Exception("Carrier not found.");

        d["CarrierName"] = carrier.Name;
        d["CarrierNAIC"] = carrier.Naic ?? string.Empty;
        d["CarrierAMBest"] = carrier.AmBestRating ?? string.Empty;
        d["CarrierAddressLine1"] = carrier.AddressLine1 ?? string.Empty;
        d["CarrierCity"] = carrier.City ?? string.Empty;
        d["CarrierState"] = carrier.State ?? string.Empty;
        d["CarrierZip"] = carrier.ZipCode ?? string.Empty;
    }

    private async Task AddAgentDataAsync(Dictionary<string, string> d, Guid agentId)
    {
        var agent = await Db.Set<Agent>()
            .Include(a => a.Locations)
            .FirstOrDefaultAsync(a => a.Id == agentId)
            ?? throw new Exception("Agent not found.");

        var primary = agent.Locations.FirstOrDefault(l => l.IsPrimary)
                   ?? agent.Locations.FirstOrDefault();

        d["AgentName"] = agent.Name;
        d["AgentAgency"] = agent.AgencyName ?? string.Empty;
        d["AgentLicense"] = agent.LicenseNumber ?? string.Empty;
        d["AgentEmail"] = agent.Email ?? string.Empty;
        d["AgentPhone"] = primary?.Phone ?? agent.Phone ?? string.Empty;
        d["AgentAddressLine1"] = primary?.AddressLine1 ?? string.Empty;
        d["AgentCity"] = primary?.City ?? string.Empty;
        d["AgentState"] = primary?.State ?? string.Empty;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatAddress(string? line1, string? city, string? state, string? zip)
    {
        var parts = new[] { line1, city != null && state != null ? $"{city}, {state} {zip}".Trim() : null }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(", ", parts);
    }

    private static string SanitizeFileName(string name) =>
        Regex.Replace(name, @"[^\w\-]", "_").Trim('_');
}
