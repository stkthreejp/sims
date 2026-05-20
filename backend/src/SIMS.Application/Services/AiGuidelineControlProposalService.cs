using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public partial class AiGuidelineControlProposalService : IAiGuidelineControlProposalService
{
    private readonly IUnderwritingGuidelineControlService _guidelines;
    private readonly DbContext? _db;
    private readonly IBlobStorageService? _blobStorage;
    private readonly IDocumentAiExtractionService? _documentAi;

    public AiGuidelineControlProposalService(IUnderwritingGuidelineControlService guidelines)
    {
        _guidelines = guidelines;
    }

    public AiGuidelineControlProposalService(
        IUnderwritingGuidelineControlService guidelines,
        DbContext db,
        IBlobStorageService blobStorage,
        IDocumentAiExtractionService documentAi)
    {
        _guidelines = guidelines;
        _db = db;
        _blobStorage = blobStorage;
        _documentAi = documentAi;
    }

    public async Task<Result<AiGuidelineControlProposalResult>> ProposeFromAttachmentAsync(
        AiGuidelineControlProposalFromAttachmentRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        if (_db == null || _blobStorage == null || _documentAi == null)
            return Result<AiGuidelineControlProposalResult>.Failure("ATTACHMENT_EXTRACTION_NOT_CONFIGURED", "Guideline attachment extraction is not configured.");

        var attachment = await _db.Set<Attachment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == request.AttachmentId, ct);

        if (attachment == null)
            return Result<AiGuidelineControlProposalResult>.Failure("ATTACHMENT_NOT_FOUND", "Guideline attachment was not found.");

        if (attachment.DocumentType != DocumentType.UnderwritingGuidelines)
            return Result<AiGuidelineControlProposalResult>.Failure("ATTACHMENT_NOT_UNDERWRITING_GUIDELINES", "Only underwriting guideline attachments can be used for AI guideline control proposals.");

        if (!IsSupportedTextSource(attachment))
            return Result<AiGuidelineControlProposalResult>.Failure("UNSUPPORTED_GUIDELINE_ATTACHMENT", "Only PDF and plain-text underwriting guideline attachments are supported for AI proposals.");

        var content = await _blobStorage.DownloadAsync(attachment.BlobPath);
        var extractedText = await ExtractAttachmentTextAsync(attachment, content, ct);
        if (string.IsNullOrWhiteSpace(extractedText))
            return Result<AiGuidelineControlProposalResult>.Failure("GUIDELINE_TEXT_REQUIRED", "No guideline text could be extracted from the attachment.");

        var document = request.Document with
        {
            SourceFileName = string.IsNullOrWhiteSpace(request.Document.SourceFileName) ? attachment.FileName : request.Document.SourceFileName,
            SourceBlobName = string.IsNullOrWhiteSpace(request.Document.SourceBlobName) ? attachment.BlobPath : request.Document.SourceBlobName
        };

        return await ProposeFromTextAsync(new AiGuidelineControlProposalRequest(document, extractedText), userId, ct);
    }

    public async Task<Result<AiGuidelineControlProposalResult>> ProposeFromTextAsync(
        AiGuidelineControlProposalRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.GuidelineText))
            return Result<AiGuidelineControlProposalResult>.Failure("GUIDELINE_TEXT_REQUIRED", "Guideline text is required.");

        var controls = ExtractControls(request.GuidelineText);
        if (controls.Count == 0)
            return Result<AiGuidelineControlProposalResult>.Failure("NO_CONTROLS_PROPOSED", "No proposed controls were found in the guideline text.");

        var document = await _guidelines.CreateDocumentAsync(request.Document, userId, ct);
        if (!document.IsSuccess || document.Value == null)
            return Result<AiGuidelineControlProposalResult>.Failure(document.ErrorCode ?? "DOCUMENT_CREATE_FAILED", document.ErrorMessage ?? "Guideline document could not be created.");

        var proposed = await _guidelines.AddProposedControlsAsync(
            document.Value.Id,
            new AddProposedUnderwritingControlsRequest(controls),
            userId,
            ct);

        if (!proposed.IsSuccess || proposed.Value == null)
            return Result<AiGuidelineControlProposalResult>.Failure(proposed.ErrorCode ?? "PROPOSED_CONTROLS_FAILED", proposed.ErrorMessage ?? "Proposed controls could not be created.");

        return Result<AiGuidelineControlProposalResult>.Success(new AiGuidelineControlProposalResult(
            document.Value,
            proposed.Value,
            ["AI proposed controls require human review in Admin > UW Controls before publishing."]));
    }

    private async Task<string> ExtractAttachmentTextAsync(Attachment attachment, byte[] content, CancellationToken ct)
    {
        if (IsPdf(attachment))
        {
            var extraction = await _documentAi!.ProcessAsync(
                content,
                string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/pdf" : attachment.ContentType,
                attachment.FileName,
                ct);
            return extraction.Text;
        }

        if (IsPlainText(attachment))
            return System.Text.Encoding.UTF8.GetString(content);

        return string.Empty;
    }

    private static List<CreateUnderwritingGuidelineControlRequest> ExtractControls(string text)
    {
        var controls = new List<CreateUnderwritingGuidelineControlRequest>();
        var sortOrder = 10;

        if (LossRunsRegex().IsMatch(text))
        {
            controls.Add(new CreateUnderwritingGuidelineControlRequest(
                UnderwritingControlItemType.DocumentChecklistItem,
                UnderwritingControlStage.Submission,
                UnderwritingControlSeverity.Warning,
                FiveYearLossRunsRegex().IsMatch(text) ? "five-year-loss-runs" : "loss-runs-required",
                FiveYearLossRunsRegex().IsMatch(text) ? "Five years currently valued loss runs" : "Currently valued loss runs",
                "Guideline requests currently valued loss runs before underwriting review.",
                null,
                false,
                true,
                AppPermissions.UnderwritingClearanceOverride,
                SourceCitation(text, "loss runs"),
                0.86m,
                sortOrder));
            sortOrder += 10;
        }

        if (SignedApplicationRegex().IsMatch(text))
        {
            var requiredBeforeBind = RequiredBeforeBindRegex().IsMatch(text);
            controls.Add(new CreateUnderwritingGuidelineControlRequest(
                UnderwritingControlItemType.DocumentChecklistItem,
                requiredBeforeBind ? UnderwritingControlStage.Bind : UnderwritingControlStage.Submission,
                requiredBeforeBind ? UnderwritingControlSeverity.HardBlock : UnderwritingControlSeverity.Warning,
                "signed-application",
                "Signed application",
                requiredBeforeBind
                    ? "Guideline states the signed application is required before bind."
                    : "Guideline requests a signed application for underwriting review.",
                null,
                requiredBeforeBind,
                true,
                AppPermissions.UnderwritingClearanceOverride,
                SourceCitation(text, "signed application"),
                requiredBeforeBind ? 0.9m : 0.82m,
                sortOrder));
            sortOrder += 10;
        }

        var pieceThreshold = PieceThresholdRegex().Match(text);
        if (pieceThreshold.Success)
        {
            var amount = ParseAmount(pieceThreshold.Groups["amount"].Value);
            controls.Add(new CreateUnderwritingGuidelineControlRequest(
                UnderwritingControlItemType.ReferralTrigger,
                UnderwritingControlStage.Quote,
                UnderwritingControlSeverity.ReferralRequired,
                $"single-piece-over-{amount / 1000}k",
                $"Single piece over {FormatAmount(amount)}",
                "Guideline requires referral review when a single piece exceeds the threshold.",
                JsonSerializer.Serialize(new { field = "singlePieceValue", op = ">", amount }),
                false,
                true,
                AppPermissions.UnderwritingClearanceOverride,
                SourceCitation(text, pieceThreshold.Value),
                0.84m,
                sortOrder));
        }

        return controls;
    }

    private static int ParseAmount(string raw) =>
        int.Parse(raw.Replace(",", string.Empty));

    private static string FormatAmount(int amount) =>
        amount >= 1_000_000 ? $"${amount / 1_000_000}M" : $"${amount / 1000}K";

    private static bool IsPdf(Attachment attachment) =>
        attachment.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
        attachment.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    private static bool IsPlainText(Attachment attachment) =>
        attachment.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
        attachment.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedTextSource(Attachment attachment) =>
        IsPdf(attachment) || IsPlainText(attachment);

    private static string SourceCitation(string text, string phrase)
    {
        var index = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return "Guideline text match";

        var start = Math.Max(0, index - 60);
        var length = Math.Min(text.Length - start, phrase.Length + 120);
        var snippet = WhitespaceRegex().Replace(text.Substring(start, length), " ").Trim();
        return $"Guideline text: {snippet}";
    }

    [GeneratedRegex(@"loss\s+runs?", RegexOptions.IgnoreCase)]
    private static partial Regex LossRunsRegex();

    [GeneratedRegex(@"five\s+years?.{0,60}loss\s+runs?", RegexOptions.IgnoreCase)]
    private static partial Regex FiveYearLossRunsRegex();

    [GeneratedRegex(@"signed\s+application", RegexOptions.IgnoreCase)]
    private static partial Regex SignedApplicationRegex();

    [GeneratedRegex(@"signed\s+application.{0,80}(required|must).{0,80}(before|prior\s+to)\s+bind", RegexOptions.IgnoreCase)]
    private static partial Regex RequiredBeforeBindRegex();

    [GeneratedRegex(@"single\s+piece.{0,80}(over|exceeds?|greater\s+than)\s+\$?(?<amount>\d{1,3}(?:,\d{3})+|\d{5,})", RegexOptions.IgnoreCase)]
    private static partial Regex PieceThresholdRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
