using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
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
    private readonly IDocumentMergeService _merge;
    private readonly IHtmlToPdfService _htmlToPdf;

    public PolicyAssemblyService(ApplicationDbContext db, IBlobStorageService blob, IAttachmentService attachments, IDocumentMergeService merge, IHtmlToPdfService htmlToPdf)
    {
        _db = db;
        _blob = blob;
        _attachments = attachments;
        _merge = merge;
        _htmlToPdf = htmlToPdf;
    }

    public async Task<Result<GeneratedDocumentDto>> AssembleAndFileAsync(Guid policyId, Guid userId, bool isPreview = false, Guid? policyVersionId = null, Guid? policyTransactionId = null)
    {
        var policy = await LoadPolicyForAssemblyAsync(policyId);
        if (policy == null)
            return Result<GeneratedDocumentDto>.Failure("NOT_FOUND", "Policy not found.");

        var forms = await LoadIncludedFormsAsync(policy.BoundQuoteId);
        if (forms.Count == 0)
            return Result<GeneratedDocumentDto>.Failure("FORMS_REQUIRED", "Review and include at least one policy form before issuing.");

        var preparedPdfs = new List<byte[]>();
        var surplusLines = await ResolveSurplusLinesSetupAsync(policy);
        var data = BuildPolicyData(policy, forms, surplusLines);
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

        var packetName = isPreview ? "DraftPolicyPacket" : "IssuedPolicyPacket";
        var fileName = $"{SanitizeFileName(policy.PolicyNumber)}_{packetName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
        await using var stream = new MemoryStream(packetBytes);
        var attachmentResult = await _attachments.CreateGeneratedAsync(
            DocumentEntityType.Policy,
            policy.BoundQuoteId,
            stream,
            fileName,
            "application/pdf",
            packetBytes.LongLength,
            isPreview ? DocumentType.PolicyPacketPreview : DocumentType.IssuedPolicyPacket,
            isPreview
                ? $"Draft policy packet preview for policy {policy.PolicyNumber} generated on {DateTime.UtcNow:MM/dd/yyyy HH:mm} UTC."
                : $"Issued policy packet for policy {policy.PolicyNumber} on {DateTime.UtcNow:MM/dd/yyyy HH:mm} UTC.",
            userId,
            isPreview ? null : policyVersionId,
            isPreview ? null : policyTransactionId);

        if (!attachmentResult.IsSuccess || attachmentResult.Value == null)
            return Result<GeneratedDocumentDto>.Failure(attachmentResult.ErrorCode ?? "ATTACHMENT_SAVE_FAILED", attachmentResult.ErrorMessage ?? "Policy packet could not be stored.");

        var urlResult = await _attachments.GetDownloadUrlAsync(attachmentResult.Value.Id, userId);
        if (!urlResult.IsSuccess || string.IsNullOrWhiteSpace(urlResult.Value))
            return Result<GeneratedDocumentDto>.Failure(urlResult.ErrorCode ?? "DOWNLOAD_URL_FAILED", urlResult.ErrorMessage ?? "Policy packet was stored, but a download URL could not be created.");

        return Result<GeneratedDocumentDto>.Success(new GeneratedDocumentDto(urlResult.Value, attachmentResult.Value));
    }

    public async Task<Result<GeneratedDocumentDto>> TestMergeTemplateAsync(Guid templateId, Guid policyId, Guid userId)
    {
        var policy = await LoadPolicyForAssemblyAsync(policyId);
        if (policy == null)
            return Result<GeneratedDocumentDto>.Failure("NOT_FOUND", "Policy not found.");

        var template = await _db.PolicyFormTemplates
            .AsNoTracking()
            .Include(t => t.FieldMappings)
            .Include(t => t.DocumentTemplate)
            .FirstOrDefaultAsync(t => t.Id == templateId);

        if (template == null)
            return Result<GeneratedDocumentDto>.Failure("NOT_FOUND", "Policy form template not found.");

        var forms = await LoadIncludedFormsAsync(policy.BoundQuoteId);
        var surplusLines = await ResolveSurplusLinesSetupAsync(policy);
        var data = BuildPolicyData(policy, forms, surplusLines);
        var testSelection = new QuotePolicyFormSelection
        {
            PolicyFormTemplateId = template.Id,
            PolicyFormTemplate = template,
            SequenceOrder = 1,
            IsIncluded = true,
        };

        var prepared = await PrepareFormPdfAsync(testSelection, data);
        if (!prepared.IsSuccess || prepared.Value == null)
            return Result<GeneratedDocumentDto>.Failure(
                prepared.ErrorCode ?? "FORM_PREP_FAILED",
                prepared.ErrorMessage ?? "Form could not be prepared.");

        var fileName = $"{SanitizeFileName(policy.PolicyNumber)}_{SanitizeFileName(template.FormNumber)}_TestMerge_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
        await using var stream = new MemoryStream(prepared.Value);
        var attachmentResult = await _attachments.CreateGeneratedAsync(
            DocumentEntityType.Policy,
            policy.BoundQuoteId,
            stream,
            fileName,
            "application/pdf",
            prepared.Value.LongLength,
            DocumentType.PolicyForm,
            $"Test merge for {template.FormNumber} using policy {policy.PolicyNumber} generated on {DateTime.UtcNow:MM/dd/yyyy HH:mm} UTC.",
            userId);

        if (!attachmentResult.IsSuccess || attachmentResult.Value == null)
            return Result<GeneratedDocumentDto>.Failure(attachmentResult.ErrorCode ?? "ATTACHMENT_SAVE_FAILED", attachmentResult.ErrorMessage ?? "Test merge could not be stored.");

        var urlResult = await _attachments.GetDownloadUrlAsync(attachmentResult.Value.Id, userId);
        if (!urlResult.IsSuccess || string.IsNullOrWhiteSpace(urlResult.Value))
            return Result<GeneratedDocumentDto>.Failure(urlResult.ErrorCode ?? "DOWNLOAD_URL_FAILED", urlResult.ErrorMessage ?? "Test merge was stored, but a download URL could not be created.");

        return Result<GeneratedDocumentDto>.Success(new GeneratedDocumentDto(urlResult.Value, attachmentResult.Value));
    }

    private Task<Policy?> LoadPolicyForAssemblyAsync(Guid policyId)
        => _db.Policies
            .AsNoTracking()
            .Include(p => p.Carrier)
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Submission).ThenInclude(s => s.Locations)
            .Include(p => p.Submission).ThenInclude(s => s.Equipment)
            .Include(p => p.Submission).ThenInclude(s => s.AdditionalInterests)
            .Include(p => p.Submission).ThenInclude(s => s.Vehicles)
            .Include(p => p.BoundQuote)
            .FirstOrDefaultAsync(p => p.Id == policyId);

    private Task<List<QuotePolicyFormSelection>> LoadIncludedFormsAsync(Guid quoteId)
        => _db.QuotePolicyFormSelections
            .AsNoTracking()
            .Include(f => f.PolicyFormTemplate)
                .ThenInclude(t => t.FieldMappings)
            .Include(f => f.PolicyFormTemplate)
                .ThenInclude(t => t.DocumentTemplate)
            .Where(f => f.QuoteId == quoteId && f.IsIncluded)
            .OrderBy(f => f.SequenceOrder)
            .ToListAsync();

    private async Task<Result<byte[]>> PrepareFormPdfAsync(QuotePolicyFormSelection form, DocumentMergeData data)
    {
        var template = form.PolicyFormTemplate;

        // F16: an authored Document Library template renders from its HTML (merge fields +
        // repeat blocks) rather than an uploaded binary.
        if (template.DocumentTemplate is { } authored)
        {
            try
            {
                var merged = _merge.MergeHtml(authored.HtmlContent, data);
                return Result<byte[]>.Success(await _htmlToPdf.ConvertAsync(merged));
            }
            catch (Exception ex)
            {
                return Result<byte[]>.Failure("HTML_CONVERSION_FAILED", $"Authored form '{template.Name}' could not be converted to PDF: {ex.Message}");
            }
        }

        if (string.IsNullOrWhiteSpace(template.StoragePath) || string.IsNullOrWhiteSpace(template.FileName))
            return Result<byte[]>.Failure("FORM_FILE_REQUIRED", "No file has been uploaded for this form.");

        var bytes = await _blob.DownloadAsync(template.StoragePath);
        var extension = Path.GetExtension(template.FileName).ToLowerInvariant();

        return extension switch
        {
            ".pdf" => FillPdfFields(bytes, template, data.Values),
            ".docx" => ConvertWordToPdf(_merge.MergeDocx(bytes, data), FormatType.Docx),
            ".doc" => ConvertWordToPdf(bytes, FormatType.Doc),
            ".html" or ".htm" => await ConvertHtmlToPdf(bytes, data),
            _ => Result<byte[]>.Failure("UNSUPPORTED_FORM_FILE", "Only PDF, DOC, DOCX, and HTML forms can be assembled into policy packets."),
        };
    }

    private async Task<Result<byte[]>> ConvertHtmlToPdf(byte[] bytes, DocumentMergeData data)
    {
        try
        {
            var html = Encoding.UTF8.GetString(bytes);
            var merged = _merge.MergeHtml(html, data);
            return Result<byte[]>.Success(await _htmlToPdf.ConvertAsync(merged));
        }
        catch (Exception ex)
        {
            return Result<byte[]>.Failure("HTML_CONVERSION_FAILED", $"HTML form could not be converted to PDF: {ex.Message}");
        }
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

    private static DocumentMergeData BuildPolicyData(Policy policy, IReadOnlyList<QuotePolicyFormSelection> forms, SurplusLinesStateSetup? surplusLines)
    {
        var quote = policy.BoundQuote;
        var insured = policy.Submission.Insured;
        var carrier = policy.Carrier;

        var data = new DocumentMergeData();
        var values = data.Values;

        values["Policy.PolicyNumber"] = policy.PolicyNumber;
        values["Policy.EffectiveDate"] = policy.EffectiveDate;
        values["Policy.ExpirationDate"] = policy.ExpirationDate;
        values["Policy.BoundDate"] = policy.BoundDate;
        values["Policy.IssuedDate"] = policy.IssuedDate;
        values["Policy.PremiumAmount"] = policy.PremiumAmount;
        values["Policy.TaxesAndFees"] = policy.TaxesAndFees;
        values["Policy.TotalPremium"] = policy.TotalPremium;
        values["Policy.LineOfBusiness"] = policy.LineOfBusiness.ToString();

        values["Quote.QuoteNumber"] = quote.QuoteNumber;
        values["Quote.PolicyNumber"] = quote.PolicyNumber;
        values["Quote.EffectiveDate"] = quote.EffectiveDate;
        values["Quote.ExpirationDate"] = quote.ExpirationDate;
        values["Quote.PremiumAmount"] = quote.PremiumAmount;
        values["Quote.TaxesAndFees"] = quote.TaxesAndFees;
        values["Quote.TotalPremium"] = quote.TotalPremium;
        values["Quote.CoverageDescription"] = quote.CoverageDescription;
        values["Quote.Deductible"] = quote.Deductible;
        values["Quote.Limit"] = quote.Limit;
        values["Quote.UninsuredMotoristLimit"] = quote.UninsuredMotoristLimit;
        values["Quote.MedicalPaymentsLimit"] = quote.MedicalPaymentsLimit;
        values["Quote.LineOfBusiness"] = quote.LineOfBusiness.ToString();

        values["Submission.SubmissionNumber"] = policy.Submission.SubmissionNumber;

        values["Insured.DisplayName"] = insured.DisplayName;
        values["Insured.Name"] = insured.DisplayName;
        values["Insured.CompanyName"] = insured.CompanyName;
        values["Insured.Dba"] = insured.Dba;
        values["Insured.FirstName"] = insured.FirstName;
        values["Insured.LastName"] = insured.LastName;
        values["Insured.AddressLine1"] = insured.AddressLine1;
        values["Insured.AddressLine2"] = insured.AddressLine2;
        values["Insured.City"] = insured.City;
        values["Insured.State"] = insured.State;
        values["Insured.ZipCode"] = insured.ZipCode;
        values["Insured.FullAddress"] = FormatAddress(insured.AddressLine1, insured.AddressLine2, insured.City, insured.State, insured.ZipCode);
        values["Insured.Email"] = insured.Email;
        values["Insured.Phone"] = insured.Phone;

        values["Carrier.Name"] = carrier.Name;
        values["Carrier.Naic"] = carrier.Naic;

        // Surplus-lines state wording (stamping/notice) resolved for the policy's filing state.
        values["SurplusLines.StampingWording"] = surplusLines?.StampingWording;
        values["SurplusLines.RequiredNotice"] = surplusLines?.RequiredNoticeText;

        data.RepeatingValues["Equipment"] = policy.Submission.Equipment
            .OrderBy(e => e.ItemNumber)
            .Select(e => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ItemNumber"] = e.ItemNumber,
                ["Description"] = e.Description,
                ["Year"] = e.Year,
                ["Make"] = e.Make,
                ["Model"] = e.Model,
                ["SerialNumber"] = e.SerialNumber,
                ["Value"] = e.Value,
                ["Limit"] = e.Value,
                ["Deductible"] = e.Deductible,
                ["Location"] = string.Empty,
                ["Territory"] = e.TerritoryCode,
            } as IReadOnlyDictionary<string, object?>)
            .ToList();

        data.RepeatingValues["AdditionalInterests"] = policy.Submission.AdditionalInterests
            .OrderBy(i => i.Name)
            .Select(i => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = i.Name,
                ["Address"] = FormatAddress(i.AddressLine1, i.AddressLine2, i.City, i.State, i.ZipCode),
                ["Types"] = FormatAdditionalInterestTypes(i),
                ["LoanNumber"] = i.ScheduledItemNumbers,
            } as IReadOnlyDictionary<string, object?>)
            .ToList();

        data.RepeatingValues["PolicyForms"] = forms
            .Where(f => f.IsIncluded)
            .OrderBy(f => f.SequenceOrder)
            .Select(f => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["FormNumber"] = f.PolicyFormTemplate.FormNumber,
                ["FormName"] = f.PolicyFormTemplate.Name,
                ["EditionDate"] = f.PolicyFormTemplate.EditionDate,
                ["Status"] = "Included",
            } as IReadOnlyDictionary<string, object?>)
            .ToList();

        data.RepeatingValues["Vehicles"] = BuildVehicleRows(policy.Submission.Vehicles);

        return data;
    }

    private async Task<SurplusLinesStateSetup?> ResolveSurplusLinesSetupAsync(Policy policy)
    {
        var state = policy.Submission.Insured.State;
        if (string.IsNullOrWhiteSpace(state))
            return null;

        var candidates = await _db.Set<SurplusLinesStateSetup>()
            .Where(s => s.IsActive)
            .ToListAsync();

        return SurplusLinesSetupResolver.Resolve(
            candidates, state, policy.ProgramId, policy.CarrierId, policy.LineOfBusiness, policy.EffectiveDate);
    }

    private static List<IReadOnlyDictionary<string, object?>> BuildVehicleRows(IEnumerable<SubmissionVehicle> vehicles) =>
        vehicles
            .OrderBy(v => v.UnitNumber)
            .Select(v => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["UnitNumber"] = v.UnitNumber,
                ["Year"] = v.Year,
                ["Make"] = v.Make,
                ["Model"] = v.Model,
                ["Vin"] = v.Vin,
                ["StatedValue"] = v.ApdStatedValue,
                ["CompDeductible"] = v.ApdCompDeductible,
                ["CollDeductible"] = v.ApdCollDeductible,
            } as IReadOnlyDictionary<string, object?>)
            .ToList();

    private static string FormatAdditionalInterestTypes(SubmissionAdditionalInterest interest)
    {
        var types = new List<string>();
        if (interest.AdditionalInsured) types.Add("Additional Insured");
        if (interest.LossPayee) types.Add("Loss Payee");
        if (interest.WaiverOfSubrogation) types.Add("Waiver of Subrogation");
        if (interest.PrimaryNonContributory) types.Add("Primary Non-Contributory");
        return string.Join(", ", types);
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
