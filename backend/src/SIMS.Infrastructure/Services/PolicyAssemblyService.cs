using Microsoft.EntityFrameworkCore;
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
            .Include(p => p.BoundQuote)
            .FirstOrDefaultAsync(p => p.Id == policyId);

        if (policy == null)
            return Result<GeneratedDocumentDto>.Failure("NOT_FOUND", "Policy not found.");

        var forms = await _db.QuotePolicyFormSelections
            .AsNoTracking()
            .Include(f => f.PolicyFormTemplate)
            .Where(f => f.QuoteId == policy.BoundQuoteId && f.IsIncluded)
            .OrderBy(f => f.SequenceOrder)
            .ToListAsync();

        if (forms.Count == 0)
            return Result<GeneratedDocumentDto>.Failure("FORMS_REQUIRED", "Review and include at least one policy form before issuing.");

        var preparedPdfs = new List<byte[]>();
        foreach (var form in forms)
        {
            var prepared = await PrepareFormPdfAsync(form);
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

    private async Task<Result<byte[]>> PrepareFormPdfAsync(QuotePolicyFormSelection form)
    {
        var template = form.PolicyFormTemplate;
        if (string.IsNullOrWhiteSpace(template.StoragePath) || string.IsNullOrWhiteSpace(template.FileName))
            return Result<byte[]>.Failure("FORM_FILE_REQUIRED", "No file has been uploaded for this form.");

        var bytes = await _blob.DownloadAsync(template.StoragePath);
        var extension = Path.GetExtension(template.FileName).ToLowerInvariant();

        return extension switch
        {
            ".pdf" => Result<byte[]>.Success(bytes),
            ".docx" => ConvertWordToPdf(bytes, FormatType.Docx),
            ".doc" => ConvertWordToPdf(bytes, FormatType.Doc),
            _ => Result<byte[]>.Failure("UNSUPPORTED_FORM_FILE", "Only PDF, DOC, and DOCX forms can be assembled into policy packets."),
        };
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

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var safe = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "Policy" : safe;
    }
}
