using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SIMS.Application.Common;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;

namespace SIMS.Infrastructure.Services;

public class DocumentGenerationService : IDocumentGenerationService
{
    private readonly IServiceProvider _sp;
    private readonly IAttachmentService _attachments;
    private readonly IDocumentMergeService _merge;

    public DocumentGenerationService(IServiceProvider sp, IAttachmentService attachments, IDocumentMergeService merge)
    {
        _sp = sp;
        _attachments = attachments;
        _merge = merge;
    }

    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public async Task<Result<GeneratedDocumentDto>> GenerateAsync(
        Guid templateId,
        TemplateEntityType entityType,
        Guid entityId,
        DocumentType? documentType,
        Guid userId)
    {
        // ── 1. Load template ──────────────────────────────────────────────────
        var template = await Db.Set<DocumentTemplate>().FindAsync(templateId);
        if (template == null)
            return Result<GeneratedDocumentDto>.Failure("NOT_FOUND", "Template not found.");
        if (template.Kind == DocumentTemplateKind.Email)
            return Result<GeneratedDocumentDto>.Failure("INVALID_TEMPLATE_KIND", "Email-only templates cannot be generated as documents.");

        // ── 2. Build tag data dictionary ──────────────────────────────────────
        DocumentMergeData data;
        try
        {
            data = await BuildDataDictionaryAsync(entityType, entityId);
        }
        catch (Exception ex)
        {
            return Result<GeneratedDocumentDto>.Failure("DATA_ERROR", $"Could not load entity data: {ex.Message}");
        }

        // ── 3. Fill {{tags}} in HTML ──────────────────────────────────────────
        var filledHtml = _merge.MergeHtml(template.HtmlContent, data);

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
            return Result<GeneratedDocumentDto>.Failure("CONVERSION_ERROR", $"PDF conversion failed: {ex.Message}");
        }

        // ── 6. Store in Azure Blob ────────────────────────────────────────────
        var fileName = $"{SanitizeFileName(template.Name)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
        var attachmentTarget = await ResolveAttachmentTargetAsync(entityType, entityId);
        if (attachmentTarget == null)
            return Result<GeneratedDocumentDto>.Failure("UNSUPPORTED_ENTITY", "Generated documents cannot be stored for this entity type.");

        using var stream = new MemoryStream(pdfBytes);
        var attachmentResult = await _attachments.CreateGeneratedAsync(
            attachmentTarget.Value.EntityType,
            attachmentTarget.Value.EntityId,
            stream,
            fileName,
            "application/pdf",
            pdfBytes.LongLength,
            documentType ?? DefaultDocumentType(entityType, template.Name),
            $"Generated from template \"{template.Name}\" on {DateTime.UtcNow:MM/dd/yyyy HH:mm} UTC.",
            userId);

        // ── 7. Return signed download URL ─────────────────────────────────────
        if (!attachmentResult.IsSuccess || attachmentResult.Value == null)
            return Result<GeneratedDocumentDto>.Failure(attachmentResult.ErrorCode ?? "ATTACHMENT_SAVE_FAILED", attachmentResult.ErrorMessage ?? "Generated document could not be stored.");

        var urlResult = await _attachments.GetDownloadUrlAsync(attachmentResult.Value.Id, userId);
        if (!urlResult.IsSuccess || string.IsNullOrWhiteSpace(urlResult.Value))
            return Result<GeneratedDocumentDto>.Failure(urlResult.ErrorCode ?? "DOWNLOAD_URL_FAILED", urlResult.ErrorMessage ?? "Generated document was stored, but a download URL could not be created.");

        return Result<GeneratedDocumentDto>.Success(new GeneratedDocumentDto(urlResult.Value, attachmentResult.Value));
    }

    // ── Tag replacement ───────────────────────────────────────────────────────

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

    private async Task<(DocumentEntityType EntityType, Guid EntityId)?> ResolveAttachmentTargetAsync(
        TemplateEntityType entityType,
        Guid entityId)
    {
        return entityType switch
        {
            TemplateEntityType.Quote => (DocumentEntityType.Policy, entityId),
            TemplateEntityType.Policy => await ResolvePolicyAttachmentTargetAsync(entityId),
            TemplateEntityType.Submission => (DocumentEntityType.Submission, entityId),
            TemplateEntityType.Carrier => (DocumentEntityType.Carrier, entityId),
            TemplateEntityType.Agent => (DocumentEntityType.Agent, entityId),
            _ => null,
        };
    }

    private async Task<(DocumentEntityType EntityType, Guid EntityId)?> ResolvePolicyAttachmentTargetAsync(Guid policyId)
    {
        var boundQuoteId = await Db.Set<Policy>()
            .Where(p => p.Id == policyId && !p.IsDeleted)
            .Select(p => (Guid?)p.BoundQuoteId)
            .FirstOrDefaultAsync();

        return boundQuoteId.HasValue ? (DocumentEntityType.Policy, boundQuoteId.Value) : null;
    }

    private static DocumentType DefaultDocumentType(TemplateEntityType entityType, string templateName)
    {
        var name = templateName.ToLowerInvariant();
        if (name.Contains("cancel") || name.Contains("nonrenew") || name.Contains("non-renew"))
            return DocumentType.CancellationNonRenewal;
        if (name.Contains("endorse"))
            return DocumentType.Endorsement;
        if (entityType == TemplateEntityType.Quote)
            return DocumentType.ProposalQuoteLetter;

        return entityType == TemplateEntityType.Policy ? DocumentType.PolicyForm : DocumentType.Correspondence;
    }

    // ── Data dictionary builders ──────────────────────────────────────────────

    private async Task<DocumentMergeData> BuildDataDictionaryAsync(
        TemplateEntityType entityType, Guid entityId)
    {
        var today = DateTime.Today.ToString("MM/dd/yyyy");

        var data = new DocumentMergeData();
        var d = data.Values;
        d["TodayDate"] = today;
        d["CompanyName"] = "Specialty Market Managers";

        switch (entityType)
        {
            case TemplateEntityType.Quote:
                await AddQuoteDataAsync(d, entityId);
                break;
            case TemplateEntityType.Policy:
                await AddPolicyDataAsync(d, entityId);
                break;
            case TemplateEntityType.Submission:
                await AddSubmissionDataAsync(d, entityId);
                break;
            case TemplateEntityType.Carrier:
                await AddCarrierDataAsync(d, entityId);
                break;
            case TemplateEntityType.Agent:
                await AddAgentDataAsync(d, entityId);
                break;
        }

        return data;
    }

    private async Task AddQuoteDataAsync(Dictionary<string, object?> d, Guid quoteId)
    {
        var quote = await Db.Set<Quote>()
            .Include(q => q.Carrier)
            .Include(q => q.Submission).ThenInclude(s => s.Insured)
            .Include(q => q.Submission).ThenInclude(s => s.Agent)
                .ThenInclude(a => a!.Locations)
            .Include(q => q.Submission).ThenInclude(s => s.Underwriter)
            .FirstOrDefaultAsync(q => q.Id == quoteId)
            ?? throw new Exception("Quote not found.");

        var insured = quote.Submission.Insured;
        var carrier = quote.Carrier;
        var agent = quote.Submission.Agent;
        var uw = quote.Submission.Underwriter;
        var primaryLocation = agent?.Locations.FirstOrDefault(l => l.IsPrimary)
                           ?? agent?.Locations.FirstOrDefault();

        d["QuoteNumber"] = quote.QuoteNumber;
        d["QuoteStatus"] = quote.Status.ToString();
        d["PolicyNumber"] = quote.PolicyNumber ?? string.Empty;
        d["EffectiveDate"] = quote.EffectiveDate.ToString("MM/dd/yyyy");
        d["ExpirationDate"] = quote.ExpirationDate.ToString("MM/dd/yyyy");
        d["BoundDate"] = quote.BoundDate?.ToString("MM/dd/yyyy") ?? string.Empty;
        d["IssuedDate"] = quote.IssuedDate?.ToString("MM/dd/yyyy") ?? string.Empty;
        d["LineOfBusiness"] = quote.LineOfBusiness.ToString();
        d["TotalPremium"] = quote.TotalPremium.ToString("C");
        d["NetPremium"] = quote.PremiumAmount.ToString("C");
        d["TaxesAndFees"] = quote.TaxesAndFees.ToString("C");
        d["CommissionRate"] = $"{quote.EffectiveAgentRate * 100:0.##}%";
        d["SMMRetentionRate"] = $"{quote.EffectiveSMMRate * 100:0.##}%";
        d["AgentCommissionRate"] = $"{quote.EffectiveAgentRate * 100:0.##}%";
        d["CommissionAmount"] = (quote.PremiumAmount * quote.EffectiveAgentRate).ToString("C");
        d["Deductible"] = quote.Deductible?.ToString("C") ?? string.Empty;
        d["CoverageLimit"] = quote.Limit?.ToString("C") ?? string.Empty;
        d["CoverageDescription"] = quote.CoverageDescription ?? string.Empty;

        d["InsuredName"] = insured.DisplayName;
        d["InsuredDBA"] = insured.Dba ?? string.Empty;
        d["InsuredType"] = insured.InsuredType.ToString();
        d["InsuredEmail"] = insured.Email ?? string.Empty;
        d["InsuredPhone"] = insured.Phone ?? string.Empty;
        d["InsuredAddressLine1"] = insured.AddressLine1;
        d["InsuredAddressLine2"] = insured.AddressLine2 ?? string.Empty;
        d["InsuredCity"] = insured.City;
        d["InsuredState"] = insured.State;
        d["InsuredZip"] = insured.ZipCode;
        d["InsuredCounty"] = insured.County ?? string.Empty;
        d["InsuredFullAddress"] = FormatAddress(insured.AddressLine1, insured.City, insured.State, insured.ZipCode);

        d["CarrierName"] = carrier.Name;
        d["CarrierNAIC"] = carrier.Naic ?? string.Empty;
        d["CarrierAMBest"] = carrier.AmBestRating ?? string.Empty;
        d["CarrierAddress"] = FormatAddress(carrier.AddressLine1, carrier.City, carrier.State, carrier.ZipCode);
        d["CarrierAddressLine1"] = carrier.AddressLine1 ?? string.Empty;
        d["CarrierCity"] = carrier.City ?? string.Empty;
        d["CarrierState"] = carrier.State ?? string.Empty;
        d["CarrierZip"] = carrier.ZipCode ?? string.Empty;

        d["AgentName"] = agent?.Name ?? string.Empty;
        d["AgentAgency"] = agent?.AgencyName ?? string.Empty;
        d["AgentEmail"] = agent?.Email ?? string.Empty;
        d["AgentPhone"] = primaryLocation?.Phone ?? agent?.Phone ?? string.Empty;
        d["AgentLicense"] = agent?.LicenseNumber ?? string.Empty;
        d["AgentCity"] = primaryLocation?.City ?? string.Empty;
        d["AgentState"] = primaryLocation?.State ?? string.Empty;

        d["UnderwriterName"] = uw.FullName;
        d["UnderwriterEmail"] = uw.Email ?? string.Empty;

        AddQuoteTagAliases(d, quote);
        AddInsuredTagAliases(d, insured);
        AddCarrierTagAliases(d, carrier);
    }

    private async Task AddPolicyDataAsync(Dictionary<string, object?> d, Guid policyId)
    {
        var policy = await Db.Set<Policy>()
            .Include(p => p.Carrier)
            .Include(p => p.BoundQuote)
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Submission).ThenInclude(s => s.Agent)
                .ThenInclude(a => a!.Locations)
            .Include(p => p.Submission).ThenInclude(s => s.Underwriter)
            .Include(p => p.Transactions).ThenInclude(t => t.ProcessedBy)
            .FirstOrDefaultAsync(p => p.Id == policyId)
            ?? throw new Exception("Policy not found.");

        var quote = policy.BoundQuote;
        var insured = policy.Submission.Insured;
        var carrier = policy.Carrier;
        var agent = policy.Submission.Agent;
        var uw = policy.Submission.Underwriter;
        var primaryLocation = agent?.Locations.FirstOrDefault(l => l.IsPrimary)
                           ?? agent?.Locations.FirstOrDefault();
        var cancellation = policy.Transactions
            .Where(t => t.TransactionType == TransactionType.Cancellation)
            .OrderByDescending(t => t.ProcessedAt)
            .FirstOrDefault();

        d["PolicyNumber"] = policy.PolicyNumber;
        d["PolicyStatus"] = policy.Status.ToString();
        d["EffectiveDate"] = policy.EffectiveDate.ToString("MM/dd/yyyy");
        d["ExpirationDate"] = policy.ExpirationDate.ToString("MM/dd/yyyy");
        d["BoundDate"] = policy.BoundDate.ToString("MM/dd/yyyy");
        d["IssuedDate"] = policy.IssuedDate?.ToString("MM/dd/yyyy") ?? string.Empty;
        d["LineOfBusiness"] = policy.LineOfBusiness.ToString();
        d["TotalPremium"] = policy.TotalPremium.ToString("C");
        d["NetPremium"] = policy.PremiumAmount.ToString("C");
        d["TaxesAndFees"] = policy.TaxesAndFees.ToString("C");
        d["CommissionRate"] = $"{quote.EffectiveAgentRate * 100:0.##}%";
        d["SMMRetentionRate"] = $"{quote.EffectiveSMMRate * 100:0.##}%";
        d["AgentCommissionRate"] = $"{quote.EffectiveAgentRate * 100:0.##}%";
        d["CommissionAmount"] = (policy.PremiumAmount * quote.EffectiveAgentRate).ToString("C");
        d["Deductible"] = quote.Deductible?.ToString("C") ?? string.Empty;
        d["CoverageLimit"] = quote.Limit?.ToString("C") ?? string.Empty;
        d["CoverageDescription"] = quote.CoverageDescription ?? string.Empty;

        d["CancellationDate"] = policy.CancelledDate?.ToString("MM/dd/yyyy") ?? cancellation?.EffectiveDate.ToString("MM/dd/yyyy") ?? string.Empty;
        d["CancellationReason"] = cancellation?.CancellationReason ?? string.Empty;
        d["CancellationMethod"] = cancellation?.CancellationMethod ?? string.Empty;
        d["CancellationPremiumChange"] = cancellation?.PremiumChange.ToString("C") ?? string.Empty;
        d["CancellationNewTotalPremium"] = cancellation?.NewTotalPremium.ToString("C") ?? string.Empty;
        d["CancellationProcessedBy"] = cancellation?.ProcessedBy != null
            ? $"{cancellation.ProcessedBy.FirstName} {cancellation.ProcessedBy.LastName}".Trim()
            : string.Empty;
        d["CancellationProcessedAt"] = cancellation?.ProcessedAt.ToString("MM/dd/yyyy") ?? string.Empty;
        d["CancellationNotes"] = cancellation?.Notes ?? string.Empty;
        d["CancellationComplianceChecklist"] = FormatChecklist(cancellation?.CancellationComplianceChecklistJson);

        d["InsuredName"] = insured.DisplayName;
        d["InsuredDBA"] = insured.Dba ?? string.Empty;
        d["InsuredType"] = insured.InsuredType.ToString();
        d["InsuredEmail"] = insured.Email ?? string.Empty;
        d["InsuredPhone"] = insured.Phone ?? string.Empty;
        d["InsuredAddressLine1"] = insured.AddressLine1;
        d["InsuredAddressLine2"] = insured.AddressLine2 ?? string.Empty;
        d["InsuredCity"] = insured.City;
        d["InsuredState"] = insured.State;
        d["InsuredZip"] = insured.ZipCode;
        d["InsuredCounty"] = insured.County ?? string.Empty;
        d["InsuredFullAddress"] = FormatAddress(insured.AddressLine1, insured.City, insured.State, insured.ZipCode);

        d["CarrierName"] = carrier.Name;
        d["CarrierNAIC"] = carrier.Naic ?? string.Empty;
        d["CarrierAMBest"] = carrier.AmBestRating ?? string.Empty;
        d["CarrierAddress"] = FormatAddress(carrier.AddressLine1, carrier.City, carrier.State, carrier.ZipCode);
        d["CarrierAddressLine1"] = carrier.AddressLine1 ?? string.Empty;
        d["CarrierCity"] = carrier.City ?? string.Empty;
        d["CarrierState"] = carrier.State ?? string.Empty;
        d["CarrierZip"] = carrier.ZipCode ?? string.Empty;

        d["AgentName"] = agent?.Name ?? string.Empty;
        d["AgentAgency"] = agent?.AgencyName ?? string.Empty;
        d["AgentEmail"] = agent?.Email ?? string.Empty;
        d["AgentPhone"] = primaryLocation?.Phone ?? agent?.Phone ?? string.Empty;
        d["AgentLicense"] = agent?.LicenseNumber ?? string.Empty;
        d["AgentCity"] = primaryLocation?.City ?? string.Empty;
        d["AgentState"] = primaryLocation?.State ?? string.Empty;

        d["UnderwriterName"] = uw.FullName;
        d["UnderwriterEmail"] = uw.Email ?? string.Empty;

        AddPolicyTagAliases(d, policy);
        AddQuoteTagAliases(d, quote);
        AddInsuredTagAliases(d, insured);
        AddCarrierTagAliases(d, carrier);

        await AddLegalCancellationDataAsync(d, insured.State, cancellation?.CancellationLegalRequirementSnapshotJson);
    }

    private async Task AddLegalCancellationDataAsync(Dictionary<string, object?> d, string state, string? snapshotJson)
    {
        var legalState = NormalizeState(state);
        d["LegalCancellationState"] = legalState;

        var snapshotRows = DeserializeLegalSnapshot(snapshotJson);
        if (snapshotRows.Count > 0)
        {
            d["LegalNoticeRequirements"] = FormatLegalRows(snapshotRows.Where(r => r.Category == "NOTICE REQUIREMENTS"));
            d["LegalReasonRequirements"] = FormatLegalRows(snapshotRows.Where(r => r.Category == "REASONS"));
            d["LegalProofOfNoticeRequirements"] = FormatLegalRows(snapshotRows.Where(r => r.Topic.Contains("Proof", StringComparison.OrdinalIgnoreCase)));
            d["LegalLienholderRequirements"] = FormatLegalRows(snapshotRows.Where(r =>
                r.Topic.Contains("Lienholder", StringComparison.OrdinalIgnoreCase) ||
                r.Topic.Contains("Mortgagee", StringComparison.OrdinalIgnoreCase)));
            d["LegalStateAuthorityRequirements"] = FormatLegalRows(snapshotRows.Where(r =>
                r.Topic.Contains("State Authority", StringComparison.OrdinalIgnoreCase) ||
                r.RequirementText.Contains("Department", StringComparison.OrdinalIgnoreCase) ||
                r.RequirementText.Contains("DMV", StringComparison.OrdinalIgnoreCase)));
            d["LegalReturnPremiumRequirements"] = FormatLegalRows(snapshotRows.Where(r =>
                r.Topic.Contains("Return of Unearned Premium", StringComparison.OrdinalIgnoreCase) ||
                r.RequirementText.Contains("unearned premium", StringComparison.OrdinalIgnoreCase)));
            d["LegalCancellationRequirements"] = FormatLegalRows(snapshotRows);
            return;
        }

        var rows = string.IsNullOrWhiteSpace(legalState)
            ? new List<LegalRequirementSection>()
            : await Db.Set<LegalRequirementSection>()
                .Where(r => r.State == legalState && r.Action == "Cancellation")
                .OrderBy(r => r.Category == "NOTICE REQUIREMENTS" ? 0 :
                              r.Category == "REASONS" ? 1 :
                              r.Category == "INSURER REQUIREMENTS" ? 2 : 3)
                .ThenBy(r => r.SortOrder)
                .ToListAsync();

        var rowDtos = rows.Select(r => new LegalRequirementSnapshotRow(
            r.Id,
            r.State,
            r.Category,
            r.Topic,
            r.RequirementText,
            r.Citations,
            r.LastVerifiedAt)).ToList();

        d["LegalNoticeRequirements"] = FormatLegalRows(rowDtos.Where(r => r.Category == "NOTICE REQUIREMENTS"));
        d["LegalReasonRequirements"] = FormatLegalRows(rowDtos.Where(r => r.Category == "REASONS"));
        d["LegalProofOfNoticeRequirements"] = FormatLegalRows(rowDtos.Where(r => r.Topic.Contains("Proof", StringComparison.OrdinalIgnoreCase)));
        d["LegalLienholderRequirements"] = FormatLegalRows(rowDtos.Where(r =>
            r.Topic.Contains("Lienholder", StringComparison.OrdinalIgnoreCase) ||
            r.Topic.Contains("Mortgagee", StringComparison.OrdinalIgnoreCase)));
        d["LegalStateAuthorityRequirements"] = FormatLegalRows(rowDtos.Where(r =>
            r.Topic.Contains("State Authority", StringComparison.OrdinalIgnoreCase) ||
            r.RequirementText.Contains("Department", StringComparison.OrdinalIgnoreCase) ||
            r.RequirementText.Contains("DMV", StringComparison.OrdinalIgnoreCase)));
        d["LegalReturnPremiumRequirements"] = FormatLegalRows(rowDtos.Where(r =>
            r.Topic.Contains("Return of Unearned Premium", StringComparison.OrdinalIgnoreCase) ||
            r.RequirementText.Contains("unearned premium", StringComparison.OrdinalIgnoreCase)));
        d["LegalCancellationRequirements"] = FormatLegalRows(rowDtos);
    }

    private async Task AddSubmissionDataAsync(Dictionary<string, object?> d, Guid submissionId)
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

        d["Submission.SubmissionNumber"] = sub.SubmissionNumber;
        AddInsuredTagAliases(d, insured);
    }

    private async Task AddCarrierDataAsync(Dictionary<string, object?> d, Guid carrierId)
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

        AddCarrierTagAliases(d, carrier);
    }

    private async Task AddAgentDataAsync(Dictionary<string, object?> d, Guid agentId)
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

    private static void AddQuoteTagAliases(Dictionary<string, object?> d, Quote quote)
    {
        d["Quote.QuoteNumber"] = quote.QuoteNumber;
        d["Quote.PolicyNumber"] = quote.PolicyNumber;
        d["Quote.EffectiveDate"] = quote.EffectiveDate;
        d["Quote.ExpirationDate"] = quote.ExpirationDate;
        d["Quote.PremiumAmount"] = quote.PremiumAmount;
        d["Quote.TaxesAndFees"] = quote.TaxesAndFees;
        d["Quote.TotalPremium"] = quote.TotalPremium;
        d["Quote.CoverageDescription"] = quote.CoverageDescription;
        d["Quote.Deductible"] = quote.Deductible;
        d["Quote.Limit"] = quote.Limit;
        d["Quote.UninsuredMotoristLimit"] = quote.UninsuredMotoristLimit;
        d["Quote.MedicalPaymentsLimit"] = quote.MedicalPaymentsLimit;
        d["Quote.LineOfBusiness"] = quote.LineOfBusiness.ToString();
    }

    private static void AddPolicyTagAliases(Dictionary<string, object?> d, Policy policy)
    {
        d["Policy.PolicyNumber"] = policy.PolicyNumber;
        d["Policy.EffectiveDate"] = policy.EffectiveDate;
        d["Policy.ExpirationDate"] = policy.ExpirationDate;
        d["Policy.BoundDate"] = policy.BoundDate;
        d["Policy.IssuedDate"] = policy.IssuedDate;
        d["Policy.PremiumAmount"] = policy.PremiumAmount;
        d["Policy.TaxesAndFees"] = policy.TaxesAndFees;
        d["Policy.TotalPremium"] = policy.TotalPremium;
        d["Policy.LineOfBusiness"] = policy.LineOfBusiness.ToString();
    }

    private static void AddInsuredTagAliases(Dictionary<string, object?> d, Insured insured)
    {
        d["Insured.DisplayName"] = insured.DisplayName;
        d["Insured.Name"] = insured.DisplayName;
        d["Insured.CompanyName"] = insured.CompanyName;
        d["Insured.Dba"] = insured.Dba;
        d["Insured.FirstName"] = insured.FirstName;
        d["Insured.LastName"] = insured.LastName;
        d["Insured.AddressLine1"] = insured.AddressLine1;
        d["Insured.AddressLine2"] = insured.AddressLine2;
        d["Insured.City"] = insured.City;
        d["Insured.State"] = insured.State;
        d["Insured.ZipCode"] = insured.ZipCode;
        d["Insured.FullAddress"] = FormatAddress(insured.AddressLine1, insured.City, insured.State, insured.ZipCode);
        d["Insured.Email"] = insured.Email;
        d["Insured.Phone"] = insured.Phone;
    }

    private static void AddCarrierTagAliases(Dictionary<string, object?> d, Carrier carrier)
    {
        d["Carrier.Name"] = carrier.Name;
        d["Carrier.Naic"] = carrier.Naic;
    }

    private static string FormatLegalRows(IEnumerable<LegalRequirementSnapshotRow> rows)
    {
        return string.Join("<br/><br/>", rows.Select(r =>
        {
            var citations = r.Citations.Length > 0 ? $" [{string.Join("; ", r.Citations)}]" : string.Empty;
            return $"<strong>{r.Topic}</strong>: {r.RequirementText}{citations}";
        }));
    }

    private static string FormatChecklist(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;

        try
        {
            var items = JsonSerializer.Deserialize<List<CancellationChecklistSnapshotRow>>(json) ?? [];
            return string.Join("<br/>", items.Select(i => $"{(i.IsCompleted ? "[x]" : "[ ]")} {i.Label}"));
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static IReadOnlyList<LegalRequirementSnapshotRow> DeserializeLegalSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<LegalRequirementSnapshotRow>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string NormalizeState(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        if (trimmed.Length != 2) return trimmed;

        return trimmed.ToUpperInvariant() switch
        {
            "AL" => "Alabama",
            "AR" => "Arkansas",
            "FL" => "Florida",
            "GA" => "Georgia",
            "LA" => "Louisiana",
            "MD" => "Maryland",
            "MS" => "Mississippi",
            "NC" => "North Carolina",
            "OK" => "Oklahoma",
            "PA" => "Pennsylvania",
            "SC" => "South Carolina",
            "TN" => "Tennessee",
            "TX" => "Texas",
            "VA" => "Virginia",
            _ => trimmed
        };
    }

    private static string SanitizeFileName(string name) =>
        Regex.Replace(name, @"[^\w\-]", "_").Trim('_');

    private sealed record LegalRequirementSnapshotRow(
        Guid Id,
        string State,
        string Category,
        string Topic,
        string RequirementText,
        string[] Citations,
        DateTime LastVerifiedAt);

    private sealed record CancellationChecklistSnapshotRow(
        string Key,
        string Label,
        bool IsCompleted,
        Guid[] RequirementSectionIds);
}
