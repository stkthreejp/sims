using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<AiGuidelineControlProposalService>? _logger;
    private readonly IAiGuidelineLlmInterpreterService? _llmInterpreter;

    public AiGuidelineControlProposalService(IUnderwritingGuidelineControlService guidelines, IAiGuidelineLlmInterpreterService? llmInterpreter = null)
    {
        _guidelines = guidelines;
        _llmInterpreter = llmInterpreter;
    }

    public AiGuidelineControlProposalService(
        IUnderwritingGuidelineControlService guidelines,
        DbContext db,
        IBlobStorageService blobStorage,
        IDocumentAiExtractionService documentAi,
        ILogger<AiGuidelineControlProposalService>? logger = null,
        IAiGuidelineLlmInterpreterService? llmInterpreter = null)
    {
        _guidelines = guidelines;
        _db = db;
        _blobStorage = blobStorage;
        _documentAi = documentAi;
        _logger = logger;
        _llmInterpreter = llmInterpreter;
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

        string extractedText;
        try
        {
            var content = await _blobStorage.DownloadAsync(attachment.BlobPath);
            extractedText = await ExtractAttachmentTextAsync(attachment, content, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is HttpRequestException)
        {
            _logger?.LogWarning(ex,
                "AI guideline attachment extraction failed for attachment {AttachmentId}, file {FileName}, content type {ContentType}",
                attachment.Id,
                attachment.FileName,
                attachment.ContentType);

            return Result<AiGuidelineControlProposalResult>.Failure(
                "GUIDELINE_ATTACHMENT_EXTRACTION_FAILED",
                "Guideline attachment could not be extracted. Verify Document AI configuration and that the uploaded file is readable.");
        }

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

        var (controls, usedLlm, fallbackReason, failureCode) = await ExtractControlsAsync(request.GuidelineText, ct);
        if (!string.IsNullOrWhiteSpace(failureCode))
            return Result<AiGuidelineControlProposalResult>.Failure(failureCode, fallbackReason ?? "AI guideline proposal failed.");

        if (controls.Count == 0)
        {
            var reason = string.IsNullOrWhiteSpace(fallbackReason) ? null : $" {fallbackReason}";
            return Result<AiGuidelineControlProposalResult>.Failure("NO_CONTROLS_PROPOSED", $"No proposed controls were found in the guideline text.{reason}");
        }

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
            BuildWarnings(usedLlm, fallbackReason)));
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
            AddControl(controls, new CreateUnderwritingGuidelineControlRequest(
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
            AddControl(controls, new CreateUnderwritingGuidelineControlRequest(
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
            AddControl(controls, new CreateUnderwritingGuidelineControlRequest(
                UnderwritingControlItemType.ReferralTrigger,
                UnderwritingControlStage.Quote,
                UnderwritingControlSeverity.ReferralRequired,
                $"single-piece-over-{amount / 1000}k",
                $"Single piece over {FormatAmount(amount)}",
                "Guideline requires referral review when a single piece exceeds the threshold.",
                JsonSerializer.Serialize(new { field = "largestSingleItemValue", @operator = ">", value = amount }),
                false,
                true,
                AppPermissions.UnderwritingClearanceOverride,
                SourceCitation(text, pieceThreshold.Value),
                0.84m,
                sortOrder));
            sortOrder += 10;
        }

        var totalInsuredValueThreshold = TotalInsuredValueThresholdRegex().Match(text);
        if (totalInsuredValueThreshold.Success)
        {
            var amount = ParseAmount(totalInsuredValueThreshold.Groups["amount"].Value);
            AddControl(controls, new CreateUnderwritingGuidelineControlRequest(
                UnderwritingControlItemType.ReferralTrigger,
                UnderwritingControlStage.Quote,
                UnderwritingControlSeverity.ReferralRequired,
                $"total-insured-value-over-{AmountKey(amount)}",
                $"Total insured value over {FormatAmount(amount)}",
                "Guideline requires referral review when total insured value exceeds the threshold.",
                JsonSerializer.Serialize(new { field = "totalInsuredValue", @operator = ">", value = amount }),
                false,
                true,
                AppPermissions.UnderwritingClearanceOverride,
                SourceCitation(text, totalInsuredValueThreshold.Value),
                0.84m,
                sortOrder));
            sortOrder += 10;
        }

        foreach (var line in RequirementLines(text))
        {
            if (KnownSpecialRequirementRegex().IsMatch(line))
                continue;

            var label = CleanRequirementLabel(line);
            if (string.IsNullOrWhiteSpace(label))
                continue;

            var requiredBeforeBind = RequiredBeforeBindRegex().IsMatch(line);
            AddControl(controls, new CreateUnderwritingGuidelineControlRequest(
                UnderwritingControlItemType.DocumentChecklistItem,
                requiredBeforeBind ? UnderwritingControlStage.Bind : UnderwritingControlStage.Submission,
                requiredBeforeBind ? UnderwritingControlSeverity.HardBlock : UnderwritingControlSeverity.Warning,
                Slug(label),
                label,
                requiredBeforeBind
                    ? $"Guideline states {label.ToLowerInvariant()} is required before bind."
                    : $"Guideline requires {label.ToLowerInvariant()} for underwriting review.",
                null,
                requiredBeforeBind,
                true,
                AppPermissions.UnderwritingClearanceOverride,
                SourceCitation(text, line),
                requiredBeforeBind ? 0.82m : 0.76m,
                sortOrder));
            sortOrder += 10;
        }

        return controls;
    }

    private async Task<(List<CreateUnderwritingGuidelineControlRequest> Controls, bool UsedLlm, string? FallbackReason, string? FailureCode)> ExtractControlsAsync(string text, CancellationToken ct)
    {
        if (_llmInterpreter is not null)
        {
            try
            {
                var llmControls = await _llmInterpreter.InterpretAsync(text, ct);
                if (llmControls.Count > 0)
                    return (llmControls.ToList(), true, null, null);

                _logger?.LogWarning("AI guideline LLM interpretation returned no controls; falling back to pattern parser.");
                return (ExtractControls(text), false, "LLM returned no controls, so SIMS used the fallback pattern parser.", null);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is HttpRequestException || ex is JsonException || ex is TaskCanceledException)
            {
                if (ex is TaskCanceledException)
                {
                    _logger?.LogWarning(ex, "AI guideline LLM interpretation timed out.");
                    return ([], false, "Claude timed out while reading the guideline. Try again, or use a smaller guideline attachment.", "GUIDELINE_LLM_TIMEOUT");
                }

                _logger?.LogWarning(ex, "AI guideline LLM interpretation failed; falling back to pattern parser.");
                return (ExtractControls(text), false, "LLM interpretation failed, so SIMS used the fallback pattern parser.", null);
            }
        }

        return (ExtractControls(text), false, "LLM interpretation is not configured, so SIMS used the fallback pattern parser.", null);
    }

    private static IReadOnlyList<string> BuildWarnings(bool usedLlm, string? fallbackReason)
    {
        var warnings = new List<string>
        {
            usedLlm
                ? "LLM interpreted guideline controls require human review in Admin > UW Controls before publishing."
                : "AI proposed controls require human review in Admin > UW Controls before publishing."
        };

        if (!string.IsNullOrWhiteSpace(fallbackReason))
            warnings.Add(fallbackReason);

        return warnings;
    }

    private static void AddControl(List<CreateUnderwritingGuidelineControlRequest> controls, CreateUnderwritingGuidelineControlRequest control)
    {
        if (controls.Any(existing => existing.RuleKey.Equals(control.RuleKey, StringComparison.OrdinalIgnoreCase)))
            return;

        controls.Add(control);
    }

    private static int ParseAmount(string raw) =>
        int.Parse(raw.Replace(",", string.Empty));

    private static string FormatAmount(int amount) =>
        amount >= 1_000_000 ? $"${amount / 1_000_000}M" : $"${amount / 1000}K";

    private static string AmountKey(int amount) =>
        amount >= 1_000_000 ? $"{amount / 1_000_000}m" : $"{amount / 1000}k";

    private static IEnumerable<string> RequirementLines(string text)
    {
        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var match = RequirementLineRegex().Match(line);
            if (match.Success)
                yield return match.Groups["label"].Value.Trim();
        }
    }

    private static string CleanRequirementLabel(string line)
    {
        var label = BulletPrefixRegex().Replace(line, string.Empty).Trim();
        label = TrailingRequirementPhraseRegex().Replace(label, string.Empty).Trim();
        return label.Trim(' ', '.', ':', ';');
    }

    private static string Slug(string value)
    {
        var slug = SlugInvalidRegex().Replace(value.ToLowerInvariant(), "-").Trim('-');
        return SlugCollapseRegex().Replace(slug, "-");
    }

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

    [GeneratedRegex(@"total\s+(insured\s+value|tiv).{0,80}(over|exceeds?|greater\s+than)\s+\$?(?<amount>\d{1,3}(?:,\d{3})+|\d{5,})", RegexOptions.IgnoreCase)]
    private static partial Regex TotalInsuredValueThresholdRegex();

    [GeneratedRegex(@"^\s*(?:[-*]|\d+[.)])?\s*(?<label>.+?)\s+(?:is\s+|are\s+)?(?:required|must\s+be\s+provided|must\s+be\s+submitted|must\s+accompany|shall\s+be\s+provided)(?:\b|\.|;)", RegexOptions.IgnoreCase)]
    private static partial Regex RequirementLineRegex();

    [GeneratedRegex(@"(loss\s+runs?|signed\s+application|referral)", RegexOptions.IgnoreCase)]
    private static partial Regex KnownSpecialRequirementRegex();

    [GeneratedRegex(@"^\s*(?:[-*]|\d+[.)])\s*")]
    private static partial Regex BulletPrefixRegex();

    [GeneratedRegex(@"\s+(?:is\s+|are\s+)?(?:required|must\s+be\s+provided|must\s+be\s+submitted|must\s+accompany|shall\s+be\s+provided).*$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingRequirementPhraseRegex();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugInvalidRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex SlugCollapseRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
