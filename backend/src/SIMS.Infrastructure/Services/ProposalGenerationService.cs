using System.Globalization;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Attachments;
using SIMS.Application.DTOs.OutboundCommunications;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;

namespace SIMS.Infrastructure.Services;

public class ProposalGenerationService : IProposalGenerationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        PropertyNamingPolicy = null,
    };

    private readonly ApplicationDbContext _db;
    private readonly IAttachmentService _attachments;
    private readonly IHtmlToPdfService _htmlToPdf;
    private readonly IOutboundCommunicationService _outboundCommunications;
    private readonly string? _mailboxAddress;

    public ProposalGenerationService(
        ApplicationDbContext db,
        IAttachmentService attachments,
        IHtmlToPdfService htmlToPdf,
        IOutboundCommunicationService outboundCommunications,
        IConfiguration config)
    {
        _db = db;
        _attachments = attachments;
        _htmlToPdf = htmlToPdf;
        _outboundCommunications = outboundCommunications;
        _mailboxAddress = config["GraphApi:MailboxAddress"];
    }

    public async Task<Result<string>> GenerateInlandMarineHtmlAsync(Guid quoteId)
    {
        var quote = await _db.Quotes
            .Include(q => q.Carrier)
            .Include(q => q.Submission).ThenInclude(s => s.Insured)
            .Include(q => q.Submission).ThenInclude(s => s.Agent)
            .Include(q => q.Submission).ThenInclude(s => s.Underwriter)
            .Include(q => q.Submission).ThenInclude(s => s.Locations)
            .Include(q => q.Submission).ThenInclude(s => s.Equipment)
                .ThenInclude(e => e.EquipmentType)
            .Include(q => q.Submission).ThenInclude(s => s.AdditionalInterests)
            .Include(q => q.Submission).ThenInclude(s => s.IMCoverages)
            .FirstOrDefaultAsync(q => q.Id == quoteId);

        if (quote == null)
            return Result<string>.Failure("NOT_FOUND", "Quote not found.");
        if (quote.LineOfBusiness != PolicyLineOfBusiness.InlandMarine)
            return Result<string>.Failure("INVALID_LOB", "This proposal template is only available for Inland Marine quotes.");

        var latestSnapshot = await _db.QuoteRatingSnapshots
            .Include(s => s.Lines)
            .Where(s => s.QuoteId == quoteId)
            .OrderByDescending(s => s.RatedAt)
            .FirstOrDefaultAsync();

        var templateDir = Path.Combine(AppContext.BaseDirectory, "Templates", "Proposals", "LongleafInlandMarine");
        var indexPath = Path.Combine(templateDir, "index.html");
        if (!File.Exists(indexPath))
            return Result<string>.Failure("TEMPLATE_NOT_FOUND", "The Inland Marine proposal template is missing.");

        var proposal = BuildProposalData(quote, latestSnapshot);
        var equipment = BuildEquipmentData(quote, latestSnapshot);
        var lossPayees = BuildLossPayeeData(quote.Submission.AdditionalInterests);
        var endorsements = BuildEndorsementData(latestSnapshot);
        var forms = await BuildFormsDataAsync(quote, latestSnapshot, endorsements);

        var html = await BuildSelfContainedHtmlAsync(templateDir, proposal, equipment, lossPayees, endorsements, forms);
        return Result<string>.Success(html);
    }

    public async Task<Result<GeneratedDocumentDto>> SaveInlandMarineHtmlAsync(Guid quoteId, Guid userId)
    {
        var htmlResult = await GenerateInlandMarineHtmlAsync(quoteId);
        if (!htmlResult.IsSuccess || string.IsNullOrWhiteSpace(htmlResult.Value))
            return Result<GeneratedDocumentDto>.Failure(htmlResult.ErrorCode ?? "PROPOSAL_ERROR", htmlResult.ErrorMessage ?? "Proposal could not be generated.");

        var quote = await _db.Quotes
            .AsNoTracking()
            .Where(q => q.Id == quoteId)
            .Select(q => new { q.QuoteNumber, q.SubmissionId })
            .FirstOrDefaultAsync();
        if (quote == null)
            return Result<GeneratedDocumentDto>.Failure("NOT_FOUND", "Quote not found.");

        var bytes = Encoding.UTF8.GetBytes(htmlResult.Value);
        await using var stream = new MemoryStream(bytes);
        var fileName = $"{SanitizeFileName(quote.QuoteNumber)}_InlandMarineProposal_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html";

        var attachmentResult = await _attachments.CreateGeneratedAsync(
            DocumentEntityType.Policy,
            quoteId,
            stream,
            fileName,
            "text/html",
            bytes.LongLength,
            DocumentType.ProposalQuoteLetter,
            $"Generated Inland Marine proposal for quote {quote.QuoteNumber} on {DateTime.UtcNow:MM/dd/yyyy HH:mm} UTC.",
            userId);

        if (!attachmentResult.IsSuccess || attachmentResult.Value == null)
            return Result<GeneratedDocumentDto>.Failure(attachmentResult.ErrorCode ?? "ATTACHMENT_SAVE_FAILED", attachmentResult.ErrorMessage ?? "Generated proposal could not be stored.");

        var urlResult = await _attachments.GetDownloadUrlAsync(attachmentResult.Value.Id, userId);
        if (!urlResult.IsSuccess || string.IsNullOrWhiteSpace(urlResult.Value))
            return Result<GeneratedDocumentDto>.Failure(urlResult.ErrorCode ?? "DOWNLOAD_URL_FAILED", urlResult.ErrorMessage ?? "Generated proposal was stored, but a download URL could not be created.");

        return Result<GeneratedDocumentDto>.Success(new GeneratedDocumentDto(urlResult.Value, attachmentResult.Value));
    }

    public async Task<Result<GeneratedDocumentDto>> SaveInlandMarinePdfAsync(Guid quoteId, Guid userId)
    {
        var htmlResult = await GenerateInlandMarineHtmlAsync(quoteId);
        if (!htmlResult.IsSuccess || string.IsNullOrWhiteSpace(htmlResult.Value))
            return Result<GeneratedDocumentDto>.Failure(htmlResult.ErrorCode ?? "PROPOSAL_ERROR", htmlResult.ErrorMessage ?? "Proposal could not be generated.");

        var quote = await _db.Quotes
            .AsNoTracking()
            .Where(q => q.Id == quoteId)
            .Select(q => new { q.QuoteNumber, q.SubmissionId })
            .FirstOrDefaultAsync();
        if (quote == null)
            return Result<GeneratedDocumentDto>.Failure("NOT_FOUND", "Quote not found.");

        byte[] pdfBytes;
        try
        {
            pdfBytes = await _htmlToPdf.ConvertAsync(htmlResult.Value);
        }
        catch (Exception ex)
        {
            return Result<GeneratedDocumentDto>.Failure("PDF_RENDER_FAILED", $"Proposal PDF could not be rendered: {ex.Message}");
        }

        await using var stream = new MemoryStream(pdfBytes);
        var fileName = $"{SanitizeFileName(quote.QuoteNumber)}_InlandMarineProposal_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

        var attachmentResult = await _attachments.CreateGeneratedAsync(
            DocumentEntityType.Policy,
            quoteId,
            stream,
            fileName,
            "application/pdf",
            pdfBytes.LongLength,
            DocumentType.ProposalQuoteLetter,
            $"Generated Inland Marine proposal PDF for quote {quote.QuoteNumber} on {DateTime.UtcNow:MM/dd/yyyy HH:mm} UTC.",
            userId);

        if (!attachmentResult.IsSuccess || attachmentResult.Value == null)
            return Result<GeneratedDocumentDto>.Failure(attachmentResult.ErrorCode ?? "ATTACHMENT_SAVE_FAILED", attachmentResult.ErrorMessage ?? "Generated proposal could not be stored.");

        var urlResult = await _attachments.GetDownloadUrlAsync(attachmentResult.Value.Id, userId);
        if (!urlResult.IsSuccess || string.IsNullOrWhiteSpace(urlResult.Value))
            return Result<GeneratedDocumentDto>.Failure(urlResult.ErrorCode ?? "DOWNLOAD_URL_FAILED", urlResult.ErrorMessage ?? "Generated proposal was stored, but a download URL could not be created.");

        return Result<GeneratedDocumentDto>.Success(new GeneratedDocumentDto(urlResult.Value, attachmentResult.Value));
    }

    public async Task<Result<ProposalSendDraftDto>> CreateInlandMarineSendDraftAsync(Guid quoteId, Guid userId)
    {
        var generatedResult = await SaveInlandMarinePdfAsync(quoteId, userId);
        if (!generatedResult.IsSuccess || generatedResult.Value == null)
            return Result<ProposalSendDraftDto>.Failure(generatedResult.ErrorCode ?? "PROPOSAL_ERROR", generatedResult.ErrorMessage ?? "Proposal could not be generated.");

        var quote = await _db.Quotes
            .Include(q => q.Submission).ThenInclude(s => s.Insured)
            .Include(q => q.Submission).ThenInclude(s => s.Agent)
            .Include(q => q.Submission).ThenInclude(s => s.Underwriter)
            .Include(q => q.Carrier)
            .FirstOrDefaultAsync(q => q.Id == quoteId);
        if (quote == null)
            return Result<ProposalSendDraftDto>.Failure("NOT_FOUND", "Quote not found.");

        var recipientEmail = quote.Submission.Agent?.Email ?? quote.Submission.Insured.Email;
        if (string.IsNullOrWhiteSpace(recipientEmail))
            return Result<ProposalSendDraftDto>.Failure("MISSING_RECIPIENT", "No agent or insured email address is available for this proposal.");

        var fromAddress = IsLocalPlaceholderEmail(quote.Submission.Underwriter.Email)
            ? _mailboxAddress
            : quote.Submission.Underwriter.Email;
        if (string.IsNullOrWhiteSpace(fromAddress))
            return Result<ProposalSendDraftDto>.Failure("MISSING_SENDER", "The underwriter does not have an email address.");

        var draftResult = await _outboundCommunications.CreateDraftAsync(new OutboundCommunicationCreateDto
        {
            EntityType = OutboundCommunicationEntityType.Quote,
            EntityId = quoteId,
            ToAddress = recipientEmail.Trim(),
            ToName = quote.Submission.Agent?.Name ?? quote.Submission.Insured.DisplayName,
            FromAddress = fromAddress.Trim(),
            FromName = quote.Submission.Underwriter.FullName,
            SenderType = OutboundCommunicationSenderType.CurrentUser,
            Subject = "Inland Marine Proposal - {{Insured.DisplayName}}",
            BodyHtml = BuildProposalEmailBody(),
            AttachmentIds = [generatedResult.Value.Attachment.Id],
        }, userId);

        if (!draftResult.IsSuccess || draftResult.Value == null)
            return Result<ProposalSendDraftDto>.Failure(draftResult.ErrorCode ?? "EMAIL_DRAFT_FAILED", draftResult.ErrorMessage ?? "Proposal email draft could not be created.");

        return Result<ProposalSendDraftDto>.Success(new ProposalSendDraftDto(generatedResult.Value, draftResult.Value.Id));
    }

    private static object BuildProposalData(Quote quote, QuoteRatingSnapshot? snapshot)
    {
        var insured = quote.Submission.Insured;
        var equipment = quote.Submission.Equipment.Where(e => !e.IsDeleted).ToList();
        var tiv = equipment.Sum(e => e.Value ?? 0m);
        var maxItem = equipment.Count == 0 ? 0m : equipment.Max(e => e.Value ?? 0m);
        var states = quote.Submission.Locations
            .Select(l => ExtractState(l.Address))
            .Append(insured.State)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var premium = snapshot?.ManualPremium
            ?? (quote.PremiumAmount != 0m ? quote.PremiumAmount : 0m);
        var total = quote.TotalPremium != 0m
            ? quote.TotalPremium
            : (snapshot?.GrandTotalPremium ?? premium) + quote.TaxesAndFees;

        var fees = new List<object[]>
        {
            new object[] { "Inland Marine Premium", FormatMoney(premium) },
        };
        if (snapshot?.EndorsementPremium > 0)
            fees.Add(new object[] { "Optional Endorsements", FormatMoney(snapshot.EndorsementPremium) });
        if (quote.TaxesAndFees != 0m)
            fees.Add(new object[] { "Taxes & Fees", FormatMoney(quote.TaxesAndFees) });

        return new
        {
            insured = insured.DisplayName,
            dba = string.IsNullOrWhiteSpace(insured.Dba) ? string.Empty : $"dba {insured.Dba}",
            address1 = insured.AddressLine1,
            address2 = $"{insured.City}, {insured.State} {insured.ZipCode}".Trim(),
            locations = states.Count > 0
                ? $"{Math.Max(quote.Submission.Locations.Count, 1)} LOCATION{(quote.Submission.Locations.Count == 1 ? "" : "S")} · {string.Join(" · ", states)}"
                : string.Empty,
            company = quote.Carrier.Name,
            carrierMeta = BuildCarrierMeta(quote.Carrier),
            effFrom = FormatProposalDate(quote.EffectiveDate),
            effTo = FormatProposalDate(quote.ExpirationDate),
            quoteDate = DateTime.Today.ToString("MMMM d, yyyy"),
            proposalNo = quote.QuoteNumber,
            underwriter = quote.Submission.Underwriter.FullName,
            tiv = FormatMoney(tiv),
            perItem = quote.Limit.HasValue ? FormatMoney(quote.Limit.Value) : FormatMoney(maxItem),
            aggregate = quote.Limit.HasValue ? FormatMoney(quote.Limit.Value) : FormatMoney(tiv),
            deductible = "See Attached Schedule",
            debris = "Included · $25,000",
            rental = "$1,500 / day · 30 days max",
            towing = "$10,000 limit",
            fees,
            total = FormatMoney(total),
            conditions = new[]
            {
                $"Quote valid 30 days from {DateTime.Today:MMMM d, yyyy}.",
                "Subject to receipt and satisfactory review of any required Inland Marine supplemental application.",
                "Subject to receipt of currently-valued loss runs when requested by underwriting.",
                "Subject to executed Surplus Lines Disclosure prior to binding.",
            },
        };
    }

    private static IReadOnlyList<object> BuildEquipmentData(Quote quote, QuoteRatingSnapshot? snapshot)
    {
        var premiumByItem = snapshot?.Lines
            .Where(l => l.ExposureRef.StartsWith("EQ-", StringComparison.OrdinalIgnoreCase))
            .Select(l => new { Line = l, Match = Regex.Match(l.ExposureRef, @"EQ-(\d+)") })
            .Where(x => x.Match.Success)
            .ToDictionary(x => int.Parse(x.Match.Groups[1].Value), x => x.Line.LinePremium)
            ?? new Dictionary<int, decimal>();

        return quote.Submission.Equipment
            .Where(e => !e.IsDeleted)
            .OrderBy(e => e.ItemNumber)
            .Select(e => new
            {
                no = e.ItemNumber,
                year = e.Year?.ToString() ?? string.Empty,
                make = e.Make ?? string.Empty,
                type = e.EquipmentType?.Name ?? e.Description ?? string.Empty,
                model = e.Model ?? string.Empty,
                serial = e.SerialNumber ?? string.Empty,
                stated = FormatMoney(e.Value ?? 0m),
                basis = e.SettlementBasis ?? "ACV",
                coIns = $"{quote.Submission.IMCoverages?.CoinsurancePercentage ?? 90:0}%",
                ded = e.Deductible.HasValue ? FormatMoney(e.Deductible.Value) : "10% ACV",
                prem = premiumByItem.TryGetValue(e.ItemNumber, out var premium) ? FormatMoney(premium) : string.Empty,
            })
            .Cast<object>()
            .ToList();
    }

    private static IReadOnlyList<object> BuildLossPayeeData(IEnumerable<SubmissionAdditionalInterest> interests)
    {
        return interests
            .Where(i => !i.IsDeleted && i.LineOfBusiness == PolicyLineOfBusiness.InlandMarine && i.LossPayee)
            .OrderBy(i => i.Name)
            .Select(i => new
            {
                item = string.IsNullOrWhiteSpace(i.ScheduledItemNumbers) ? "All" : i.ScheduledItemNumbers,
                name = i.Name,
                addr = i.AddressLine1 ?? string.Empty,
                city = i.City ?? string.Empty,
                state = i.State ?? string.Empty,
                zip = i.ZipCode ?? string.Empty,
            })
            .Cast<object>()
            .ToList();
    }

    private static IReadOnlyList<EndorsementProposalRow> BuildEndorsementData(QuoteRatingSnapshot? snapshot)
    {
        var debris = snapshot?.DebrisRemoval ?? true;
        var rental = snapshot?.RentalReimbursement ?? true;
        var towing = snapshot?.TowingStorageRecovery ?? true;
        var newly = snapshot?.NewlyAcquiredEquipment ?? false;

        return
        [
            new("Debris Removal", [new("Any one loss", "$2,500"), new("Aggregate", "$10,000")], debris, debris ? FormatMoney(250m) : null, debris ? 250m : 0m),
            new("Rental Reimbursement", [new("Per day", "$2,500"), new("Aggregate", "$10,000")], rental, rental ? FormatMoney(500m) : null, rental ? 500m : 0m),
            new("Towing, Storage & Recovery", [new("Any one loss", "$5,000")], towing, towing ? FormatMoney(175m) : null, towing ? 175m : 0m),
            new("Newly Acquired Equipment", [new("Maximum limit", "$25,000")], newly, null, 0m, "Coverage for newly purchased units, reported within 30 days."),
        ];
    }

    private async Task<IReadOnlyList<object>> BuildFormsDataAsync(
        Quote quote,
        QuoteRatingSnapshot? snapshot,
        IReadOnlyList<EndorsementProposalRow> endorsements)
    {
        var reviewedForms = await _db.QuotePolicyFormSelections
            .AsNoTracking()
            .Include(f => f.PolicyFormTemplate)
            .Where(f => f.QuoteId == quote.Id && f.IsIncluded)
            .OrderBy(f => f.SequenceOrder)
            .Select(f => new
            {
                form = f.PolicyFormTemplate.FormNumber,
                edition = string.IsNullOrWhiteSpace(f.PolicyFormTemplate.EditionDate) ? "-" : f.PolicyFormTemplate.EditionDate,
                title = f.PolicyFormTemplate.Name,
            })
            .Cast<object>()
            .ToListAsync();

        if (reviewedForms.Count > 0)
            return reviewedForms;

        var state = ResolvePackageState(quote);
        var package = await _db.PolicyPackageConfigurations
            .AsNoTracking()
            .Include(p => p.Forms)
                .ThenInclude(f => f.PolicyFormTemplate)
            .Where(p => p.IsActive
                && p.CarrierId == quote.CarrierId
                && p.LineOfBusiness == quote.LineOfBusiness
                && p.State == state)
            .OrderByDescending(p => p.UpdatedAt)
            .FirstOrDefaultAsync();

        if (package == null)
            return BuildFallbackFormsData(endorsements);

        var configuredForms = package.Forms
            .Where(f => ShouldIncludePackageForm(f, quote, snapshot))
            .OrderBy(f => f.SequenceOrder)
            .Select(f => new
            {
                form = f.PolicyFormTemplate.FormNumber,
                edition = string.IsNullOrWhiteSpace(f.PolicyFormTemplate.EditionDate) ? "-" : f.PolicyFormTemplate.EditionDate,
                title = f.PolicyFormTemplate.Name,
            })
            .Cast<object>()
            .ToList();

        return configuredForms.Count > 0
            ? configuredForms
            : BuildFallbackFormsData(endorsements);
    }

    private static IReadOnlyList<object> BuildFallbackFormsData(IReadOnlyList<EndorsementProposalRow> endorsements)
    {
        var forms = new List<object>
        {
            new { form = "LL IM SCHED", edition = "-", title = "LL Inland Marine Policy Schedule" },
            new { form = "LL IM EQ SCHED", edition = "-", title = "LL Inland Marine Equipment Schedule" },
            new { form = "SMM - SLSTAMP", edition = "-", title = "Surplus Lines - State Stamp Only" },
            new { form = "LL IM OPT END", edition = "-", title = "LL Inland Marine Optional Endorsements" },
            new { form = "FORMS - SCHED A", edition = "08 12", title = "Schedule of Taxes, Surcharges or Fees" },
            new { form = "LL IM CLAIMS", edition = "-", title = "LL Inland Marine Claims Page" },
            new { form = "FORMS - SCHED", edition = "08 12", title = "Schedule of Forms and Endorsements" },
        };

        foreach (var endorsement in endorsements.Where(e => e.included))
        {
            forms.Add(new
            {
                form = $"LL IM END - {EndorsementCode(endorsement.name)}",
                edition = "-",
                title = endorsement.name,
            });
        }

        forms.Add(new { form = "LL IM FLOATER", edition = "-", title = "LL Inland Marine Floater" });
        return forms;
    }

    private static bool ShouldIncludePackageForm(PolicyPackageForm packageForm, Quote quote, QuoteRatingSnapshot? snapshot)
        => packageForm.FormType switch
        {
            PolicyFormType.Mandatory => true,
            PolicyFormType.AdHoc => false,
            PolicyFormType.Conditional => EvaluateTriggerCondition(packageForm.TriggerConditionJson, quote, snapshot),
            _ => false,
        };

    private static bool EvaluateTriggerCondition(string? triggerConditionJson, Quote quote, QuoteRatingSnapshot? snapshot)
    {
        if (string.IsNullOrWhiteSpace(triggerConditionJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(triggerConditionJson);
            return EvaluateTriggerNode(doc.RootElement, quote, snapshot);
        }
        catch
        {
            return false;
        }
    }

    private static bool EvaluateTriggerNode(JsonElement node, Quote quote, QuoteRatingSnapshot? snapshot)
    {
        if (node.TryGetProperty("all", out var all) && all.ValueKind == JsonValueKind.Array)
            return all.EnumerateArray().All(child => EvaluateTriggerNode(child, quote, snapshot));

        if (node.TryGetProperty("any", out var any) && any.ValueKind == JsonValueKind.Array)
            return any.EnumerateArray().Any(child => EvaluateTriggerNode(child, quote, snapshot));

        if (!node.TryGetProperty("path", out var pathElement) || pathElement.ValueKind != JsonValueKind.String)
            return false;

        var actual = GetTriggerValue(pathElement.GetString(), quote, snapshot);
        if (actual == null)
            return false;

        if (node.TryGetProperty("equals", out var equals))
            return TriggerValuesEqual(actual, equals);

        if (node.TryGetProperty("notEquals", out var notEquals))
            return !TriggerValuesEqual(actual, notEquals);

        if (node.TryGetProperty("greaterThan", out var greaterThan) && TryGetDecimal(actual, out var actualDecimal) && TryGetDecimal(greaterThan, out var greaterThanDecimal))
            return actualDecimal > greaterThanDecimal;

        if (node.TryGetProperty("lessThan", out var lessThan) && TryGetDecimal(actual, out actualDecimal) && TryGetDecimal(lessThan, out var lessThanDecimal))
            return actualDecimal < lessThanDecimal;

        return false;
    }

    private static object? GetTriggerValue(string? path, Quote quote, QuoteRatingSnapshot? snapshot)
        => path switch
        {
            "Rating.DebrisRemoval" => snapshot?.DebrisRemoval,
            "Rating.RentalReimbursement" => snapshot?.RentalReimbursement,
            "Rating.TowingStorageRecovery" => snapshot?.TowingStorageRecovery,
            "Rating.NewlyAcquiredEquipment" => snapshot?.NewlyAcquiredEquipment,
            "Rating.Tria" => snapshot?.Tria,
            "Rating.EndorsementPremium" => snapshot?.EndorsementPremium,
            "Rating.GrandTotalPremium" => snapshot?.GrandTotalPremium,
            "Quote.TotalPremium" => quote.TotalPremium,
            "Quote.PremiumAmount" => quote.PremiumAmount,
            "Quote.IsFilingState" => quote.IsFilingState,
            "Quote.LineOfBusiness" => quote.LineOfBusiness.ToString(),
            "Submission.State" => ResolvePackageState(quote),
            "Submission.LossPayeeCount" => quote.Submission.AdditionalInterests.Count(i => !i.IsDeleted && i.LineOfBusiness == quote.LineOfBusiness && i.LossPayee),
            _ => null,
        };

    private static bool TriggerValuesEqual(object actual, JsonElement expected)
        => expected.ValueKind switch
        {
            JsonValueKind.True => actual is bool b && b,
            JsonValueKind.False => actual is bool b && !b,
            JsonValueKind.Number => TryGetDecimal(actual, out var actualDecimal) && TryGetDecimal(expected, out var expectedDecimal) && actualDecimal == expectedDecimal,
            JsonValueKind.String => string.Equals(Convert.ToString(actual, CultureInfo.InvariantCulture), expected.GetString(), StringComparison.OrdinalIgnoreCase),
            _ => false,
        };

    private static bool TryGetDecimal(object value, out decimal result)
        => decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out result);

    private static bool TryGetDecimal(JsonElement value, out decimal result)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out result))
            return true;
        if (value.ValueKind == JsonValueKind.String)
            return decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);

        result = 0;
        return false;
    }

    private static string ResolvePackageState(Quote quote)
        => (quote.Submission.Insured.State ?? ExtractState(quote.Submission.Locations.FirstOrDefault()?.Address) ?? string.Empty)
            .Trim()
            .ToUpperInvariant();

    private static async Task<string> BuildSelfContainedHtmlAsync(
        string templateDir,
        object proposal,
        IReadOnlyList<object> equipment,
        IReadOnlyList<object> lossPayees,
        IReadOnlyList<EndorsementProposalRow> endorsements,
        IReadOnlyList<object> forms)
    {
        var html = await File.ReadAllTextAsync(Path.Combine(templateDir, "index.html"));
        var logoPath = Path.Combine(templateDir, "assets", "longleaf-logo.png");
        if (File.Exists(logoPath))
        {
            var logo = Convert.ToBase64String(await File.ReadAllBytesAsync(logoPath));
            html = html.Replace("assets/longleaf-logo.png", $"data:image/png;base64,{logo}");
        }

        foreach (var css in new[]
        {
            "proposal-a.css",
            "proposal-a-schedule.css",
            "proposal-a-endorsements.css",
            "proposal-a-forms.css",
            "proposal-a-claims.css",
        })
        {
            var content = await File.ReadAllTextAsync(Path.Combine(templateDir, "variants", css));
            html = html.Replace($"""<link rel="stylesheet" href="variants/{css}"/>""", $"<style>\n{content}\n</style>");
        }

        html = html.Replace("""<script src="variants/data.js"></script>""", $"<script>window.PROPOSAL = {JsonSerializer.Serialize(proposal, JsonOptions)};</script>");
        html = html.Replace("""<script src="variants/data-schedule.js"></script>""", $"<script>window.PROPOSAL_EQUIPMENT = {JsonSerializer.Serialize(equipment, JsonOptions)}; window.PROPOSAL_LOSS_PAYEES = {JsonSerializer.Serialize(lossPayees, JsonOptions)};</script>");
        html = html.Replace("""<script src="variants/data-endorsements.js"></script>""", $"<script>window.PROPOSAL_ENDORSEMENTS = {JsonSerializer.Serialize(endorsements, JsonOptions)};</script>");
        html = html.Replace("""<script src="variants/data-forms.js"></script>""", $"<script>window.PROPOSAL_FORMS = {JsonSerializer.Serialize(forms, JsonOptions)};</script>");

        foreach (var jsx in new[]
        {
            "proposal-a-traditional.jsx",
            "proposal-a-schedule.jsx",
            "proposal-a-endorsements.jsx",
            "proposal-a-forms.jsx",
            "proposal-a-claims.jsx",
        })
        {
            var content = await File.ReadAllTextAsync(Path.Combine(templateDir, "variants", jsx));
            html = html.Replace($"""<script type="text/babel" src="variants/{jsx}"></script>""", $"<script type=\"text/babel\">\n{content}\n</script>");
        }

        return html;
    }

    private static string BuildCarrierMeta(Carrier carrier)
    {
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(carrier.AmBestRating) ? null : $"A.M. Best {carrier.AmBestRating}",
            string.IsNullOrWhiteSpace(carrier.Naic) ? null : $"NAIC {carrier.Naic}",
        }.Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(" · ", parts);
    }

    private static string FormatProposalDate(DateOnly date) => date.ToString("MM / dd / yyyy");
    private static string FormatMoney(decimal value) => value.ToString("C", CultureInfo.GetCultureInfo("en-US"));
    private static string SanitizeFileName(string name) => Regex.Replace(name, @"[^\w\-]", "_").Trim('_');

    private static string EndorsementCode(string name) => name switch
    {
        "Debris Removal" => "DEBRIS",
        "Rental Reimbursement" => "RENTAL",
        "Towing, Storage & Recovery" => "TOWING",
        "Newly Acquired Equipment" => "NEWLY",
        _ => Regex.Replace(name.ToUpperInvariant(), @"[^A-Z0-9]+", "-").Trim('-'),
    };

    private sealed record EndorsementLimitRow(string label, string value);
    private sealed record EndorsementProposalRow(
        string name,
        IReadOnlyList<EndorsementLimitRow> limits,
        bool included,
        string? premium,
        decimal premiumNum,
        string? note = null);

    private static string BuildProposalEmailBody()
    {
        return """
            <p>Please find attached our Inland Marine proposal for {{Insured.DisplayName}}.</p>
            <p><strong>Carrier:</strong> {{Carrier.Name}}<br/>
            <strong>Effective:</strong> {{Quote.EffectiveDate | MM/dd/yyyy}}<br/>
            <strong>Expiration:</strong> {{Quote.ExpirationDate | MM/dd/yyyy}}<br/>
            <strong>Total Premium:</strong> {{Quote.TotalPremium | currency}}</p>
            <p>Please review and let us know if you would like to bind coverage.</p>
            <p>Thank you,<br/>{{UnderwriterName}}</p>
            """;
    }

    private static bool IsLocalPlaceholderEmail(string? email)
        => string.IsNullOrWhiteSpace(email) || email.EndsWith(".local", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractState(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var match = Regex.Match(address, @"\b[A-Z]{2}\b");
        return match.Success ? match.Value : null;
    }
}
