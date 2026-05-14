using Microsoft.EntityFrameworkCore;
using System.Globalization;
using SIMS.Application.Common;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Parsing;

namespace SIMS.Infrastructure.Services;

public class PolicyAssemblyService : IPolicyAssemblyService
{
    private readonly ApplicationDbContext _db;
    private readonly IBlobStorageService _blob;
    private readonly IAttachmentService _attachments;

    public PolicyAssemblyService(ApplicationDbContext db, IBlobStorageService blob, IAttachmentService attachments)
    {
        _db = db;
        _blob = blob;
        _attachments = attachments;
    }

    public async Task<Result<GeneratedDocumentDto>> AssembleAndFileAsync(Guid policyId, Guid userId)
    {
        var policy = await _db.Policies
            .AsNoTracking()
            .Include(p => p.Carrier)
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Submission).ThenInclude(s => s.Locations)
            .Include(p => p.BoundQuote)
            .FirstOrDefaultAsync(p => p.Id == policyId);

        if (policy == null)
            return Result<GeneratedDocumentDto>.Failure("NOT_FOUND", "Policy not found.");

        var forms = await _db.QuotePolicyFormSelections
            .AsNoTracking()
            .Include(f => f.PolicyFormTemplate)
                .ThenInclude(t => t.FieldMappings)
            .Where(f => f.QuoteId == policy.BoundQuoteId && f.IsIncluded)
            .OrderBy(f => f.SequenceOrder)
            .ToListAsync();

        if (forms.Count == 0)
            return Result<GeneratedDocumentDto>.Failure("FORMS_REQUIRED", "Review and include at least one policy form before issuing.");

        var preparedPdfs = new List<byte[]>();
        var data = BuildPolicyData(policy);
        foreach (var form in forms)
        {
            var prepared = await PrepareFormPdfAsync(form, data);
            if (!prepared.IsSuccess || prepared.Value == null)
                return Result<GeneratedDocumentDto>.Failure(
                    prepared.ErrorCode ?? "FORM_PREP_FAILED",
                    $"{form.PolicyFormTemplate.FormNumber}: {prepared.ErrorMessage ?? "Form could not be prepared."}");

            preparedPdfs.Add(prepared.Value);
        }

        byte[] packetBytes;
        try
        {
            packetBytes = MergePdfs(preparedPdfs);
        }
        catch (Exception ex)
        {
            return Result<GeneratedDocumentDto>.Failure("PDF_MERGE_FAILED", $"Policy packet could not be assembled: {ex.Message}");
        }

        var fileName = $"{SanitizeFileName(policy.PolicyNumber)}_PolicyPacket_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
        await using var stream = new MemoryStream(packetBytes);
        var attachmentResult = await _attachments.CreateGeneratedAsync(
            DocumentEntityType.Policy,
            policy.BoundQuoteId,
            stream,
            fileName,
            "application/pdf",
            packetBytes.LongLength,
            DocumentType.PolicyForm,
            $"Issued policy packet for policy {policy.PolicyNumber} on {DateTime.UtcNow:MM/dd/yyyy HH:mm} UTC.",
            userId);

        if (!attachmentResult.IsSuccess || attachmentResult.Value == null)
            return Result<GeneratedDocumentDto>.Failure(attachmentResult.ErrorCode ?? "ATTACHMENT_SAVE_FAILED", attachmentResult.ErrorMessage ?? "Policy packet could not be stored.");

        var urlResult = await _attachments.GetDownloadUrlAsync(attachmentResult.Value.Id, userId);
        if (!urlResult.IsSuccess || string.IsNullOrWhiteSpace(urlResult.Value))
            return Result<GeneratedDocumentDto>.Failure(urlResult.ErrorCode ?? "DOWNLOAD_URL_FAILED", urlResult.ErrorMessage ?? "Policy packet was stored, but a download URL could not be created.");

        return Result<GeneratedDocumentDto>.Success(new GeneratedDocumentDto(urlResult.Value, attachmentResult.Value));
    }

    private async Task<Result<byte[]>> PrepareFormPdfAsync(QuotePolicyFormSelection form, IReadOnlyDictionary<string, object?> data)
    {
        var template = form.PolicyFormTemplate;
        if (string.IsNullOrWhiteSpace(template.StoragePath) || string.IsNullOrWhiteSpace(template.FileName))
            return Result<byte[]>.Failure("FORM_FILE_REQUIRED", "No file has been uploaded for this form.");

        var bytes = await _blob.DownloadAsync(template.StoragePath);
        var extension = Path.GetExtension(template.FileName).ToLowerInvariant();

        return extension switch
        {
            ".pdf" => FillPdfFields(bytes, template, data),
            ".docx" => ConvertWordToPdf(bytes, FormatType.Docx),
            ".doc" => ConvertWordToPdf(bytes, FormatType.Doc),
            _ => Result<byte[]>.Failure("UNSUPPORTED_FORM_FILE", "Only PDF, DOC, and DOCX forms can be assembled into policy packets."),
        };
    }

    private static Result<byte[]> FillPdfFields(byte[] bytes, PolicyFormTemplate template, IReadOnlyDictionary<string, object?> data)
    {
        var mappings = template.FieldMappings
            .Where(m => !string.IsNullOrWhiteSpace(m.PdfFieldName) && !string.IsNullOrWhiteSpace(m.DataPath))
            .ToList();

        if (!template.IsFillable || mappings.Count == 0)
            return Result<byte[]>.Success(bytes);

        try
        {
            using var loaded = new PdfLoadedDocument(bytes);
            if (loaded.Form == null || loaded.Form.Fields.Count == 0)
                return Result<byte[]>.Success(bytes);

            var fields = loaded.Form.Fields
                .OfType<PdfLoadedField>()
                .ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var mapping in mappings)
            {
                if (!fields.TryGetValue(mapping.PdfFieldName, out var field))
                    continue;
                if (!data.TryGetValue(mapping.DataPath, out var rawValue) || rawValue == null)
                    continue;

                var text = FormatMappedValue(rawValue, mapping.Format);
                SetLoadedFieldValue(field, text, rawValue);
                field.Flatten = true;
            }

            using var output = new MemoryStream();
            loaded.Save(output);
            return Result<byte[]>.Success(output.ToArray());
        }
        catch (Exception ex)
        {
            return Result<byte[]>.Failure("PDF_FIELD_FILL_FAILED", $"Fillable PDF could not be populated: {ex.Message}");
        }
    }

    private static void SetLoadedFieldValue(PdfLoadedField field, string text, object rawValue)
    {
        switch (field)
        {
            case PdfLoadedTextBoxField textBox:
                textBox.Text = text;
                break;
            case PdfLoadedComboBoxField comboBox:
                comboBox.SelectedValue = text;
                break;
            case PdfLoadedListBoxField listBox:
                listBox.SelectedValue = [text];
                break;
            case PdfLoadedCheckBoxField checkBox:
                checkBox.Checked = ToBoolean(rawValue);
                break;
            case PdfLoadedRadioButtonListField radio:
                radio.SelectedValue = text;
                break;
        }
    }

    private static Result<byte[]> ConvertWordToPdf(byte[] bytes, FormatType formatType)
    {
        try
        {
            using var input = new MemoryStream(bytes);
            using var word = new WordDocument(input, formatType);
            using var renderer = new DocIORenderer();
            using var pdf = renderer.ConvertToPDF(word);
            using var output = new MemoryStream();
            pdf.Save(output);
            return Result<byte[]>.Success(output.ToArray());
        }
        catch (Exception ex)
        {
            return Result<byte[]>.Failure("WORD_CONVERSION_FAILED", $"Word form could not be converted to PDF: {ex.Message}");
        }
    }

    private static byte[] MergePdfs(IReadOnlyList<byte[]> pdfs)
    {
        using var document = new PdfDocument();
        foreach (var pdf in pdfs)
        {
            using var loaded = new PdfLoadedDocument(pdf);
            document.ImportPageRange(loaded, 0, loaded.Pages.Count - 1);
        }

        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }

    private static IReadOnlyDictionary<string, object?> BuildPolicyData(Policy policy)
    {
        var quote = policy.BoundQuote;
        var insured = policy.Submission.Insured;
        var carrier = policy.Carrier;

        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Policy.PolicyNumber"] = policy.PolicyNumber,
            ["Policy.EffectiveDate"] = policy.EffectiveDate,
            ["Policy.ExpirationDate"] = policy.ExpirationDate,
            ["Policy.BoundDate"] = policy.BoundDate,
            ["Policy.IssuedDate"] = policy.IssuedDate,
            ["Policy.PremiumAmount"] = policy.PremiumAmount,
            ["Policy.TaxesAndFees"] = policy.TaxesAndFees,
            ["Policy.TotalPremium"] = policy.TotalPremium,
            ["Policy.LineOfBusiness"] = policy.LineOfBusiness.ToString(),

            ["Quote.QuoteNumber"] = quote.QuoteNumber,
            ["Quote.PolicyNumber"] = quote.PolicyNumber,
            ["Quote.EffectiveDate"] = quote.EffectiveDate,
            ["Quote.ExpirationDate"] = quote.ExpirationDate,
            ["Quote.PremiumAmount"] = quote.PremiumAmount,
            ["Quote.TaxesAndFees"] = quote.TaxesAndFees,
            ["Quote.TotalPremium"] = quote.TotalPremium,
            ["Quote.CoverageDescription"] = quote.CoverageDescription,
            ["Quote.Deductible"] = quote.Deductible,
            ["Quote.Limit"] = quote.Limit,
            ["Quote.UninsuredMotoristLimit"] = quote.UninsuredMotoristLimit,
            ["Quote.MedicalPaymentsLimit"] = quote.MedicalPaymentsLimit,
            ["Quote.LineOfBusiness"] = quote.LineOfBusiness.ToString(),

            ["Submission.SubmissionNumber"] = policy.Submission.SubmissionNumber,

            ["Insured.DisplayName"] = insured.DisplayName,
            ["Insured.Name"] = insured.DisplayName,
            ["Insured.CompanyName"] = insured.CompanyName,
            ["Insured.Dba"] = insured.Dba,
            ["Insured.FirstName"] = insured.FirstName,
            ["Insured.LastName"] = insured.LastName,
            ["Insured.AddressLine1"] = insured.AddressLine1,
            ["Insured.AddressLine2"] = insured.AddressLine2,
            ["Insured.City"] = insured.City,
            ["Insured.State"] = insured.State,
            ["Insured.ZipCode"] = insured.ZipCode,
            ["Insured.FullAddress"] = FormatAddress(insured.AddressLine1, insured.AddressLine2, insured.City, insured.State, insured.ZipCode),
            ["Insured.Email"] = insured.Email,
            ["Insured.Phone"] = insured.Phone,

            ["Carrier.Name"] = carrier.Name,
            ["Carrier.Naic"] = carrier.Naic,
        };

        return data;
    }

    private static string FormatMappedValue(object value, string? format)
    {
        if (value is DateOnly date)
            return date.ToString(string.IsNullOrWhiteSpace(format) ? "MM/dd/yyyy" : format, CultureInfo.InvariantCulture);
        if (value is DateTime dateTime)
            return dateTime.ToString(string.IsNullOrWhiteSpace(format) ? "MM/dd/yyyy" : format, CultureInfo.InvariantCulture);
        if (value is decimal decimalValue)
            return FormatDecimal(decimalValue, format);
        if (value is bool boolValue)
            return boolValue ? "Yes" : "No";

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string FormatDecimal(decimal value, string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return value.ToString("0.##", CultureInfo.InvariantCulture);

        return format.Trim().ToLowerInvariant() switch
        {
            "currency" => value.ToString("C2", CultureInfo.GetCultureInfo("en-US")),
            "number" => value.ToString("N2", CultureInfo.GetCultureInfo("en-US")),
            "percent" => value.ToString("P2", CultureInfo.GetCultureInfo("en-US")),
            _ => value.ToString(format, CultureInfo.InvariantCulture),
        };
    }

    private static bool ToBoolean(object value)
    {
        if (value is bool b)
            return b;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "y", StringComparison.OrdinalIgnoreCase)
            || text == "1";
    }

    private static string FormatAddress(params string?[] parts)
        => string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var safe = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "Policy" : safe;
    }
}
