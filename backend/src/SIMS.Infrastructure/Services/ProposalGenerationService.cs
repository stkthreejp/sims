using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Attachments;
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

    public ProposalGenerationService(ApplicationDbContext db, IAttachmentService attachments, IHtmlToPdfService htmlToPdf)
    {
        _db = db;
        _attachments = attachments;
        _htmlToPdf = htmlToPdf;
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
        var endorsements = BuildEndorsementData();
        var forms = BuildFormsData();

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
            DocumentEntityType.Submission,
            quote.SubmissionId,
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
            DocumentEntityType.Submission,
            quote.SubmissionId,
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

        var fromAddress = quote.Submission.Underwriter.Email;
        if (string.IsNullOrWhiteSpace(fromAddress))
            return Result<ProposalSendDraftDto>.Failure("MISSING_SENDER", "The underwriter does not have an email address.");

        var insuredName = quote.Submission.Insured.DisplayName;
        var communication = new OutboundCommunication
        {
            EntityType = OutboundCommunicationEntityType.Quote,
            EntityId = quoteId,
            ToAddress = recipientEmail.Trim(),
            ToName = quote.Submission.Agent?.Name ?? insuredName,
            FromAddress = fromAddress.Trim(),
            FromName = quote.Submission.Underwriter.FullName,
            SenderType = OutboundCommunicationSenderType.CurrentUser,
            Subject = $"Inland Marine Proposal - {insuredName}",
            BodyHtml = BuildProposalEmailBody(quote),
            Status = OutboundCommunicationStatus.Draft,
            CreatedById = userId,
        };
        communication.Attachments.Add(new OutboundCommunicationAttachment
        {
            AttachmentId = generatedResult.Value.Attachment.Id,
        });

        _db.OutboundCommunications.Add(communication);
        await _db.SaveChangesAsync();

        return Result<ProposalSendDraftDto>.Success(new ProposalSendDraftDto(generatedResult.Value, communication.Id));
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

        var premium = quote.PremiumAmount != 0m
            ? quote.PremiumAmount
            : snapshot?.ManualPremium ?? 0m;
        var total = quote.TotalPremium != 0m
            ? quote.TotalPremium
            : premium + quote.TaxesAndFees;

        var fees = new List<object[]>
        {
            new object[] { "Inland Marine Premium", FormatMoney(premium) },
        };
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

    private static IReadOnlyList<object> BuildEndorsementData() =>
    [
        new { name = "Debris Removal", limits = new[] { new { label = "Any one loss", value = "$2,500" }, new { label = "Aggregate", value = "$10,000" } }, included = true, premium = "$250.00", premiumNum = 250 },
        new { name = "Rental Reimbursement", limits = new[] { new { label = "Per day", value = "$2,500" }, new { label = "Aggregate", value = "$10,000" } }, included = true, premium = "$500.00", premiumNum = 500 },
        new { name = "Towing, Storage & Recovery", limits = new[] { new { label = "Any one loss", value = "$5,000" } }, included = true, premium = "$175.00", premiumNum = 175 },
        new { name = "Newly Acquired Equipment", note = "Coverage for newly purchased units, reported within 30 days.", limits = new[] { new { label = "Maximum limit", value = "$25,000" } }, included = false, premium = (string?)null, premiumNum = 0 },
    ];

    private static IReadOnlyList<object> BuildFormsData() =>
    [
        new { form = "LL IM SCHED", edition = "—", title = "LL Inland Marine Policy Schedule" },
        new { form = "LL IM EQ SCHED", edition = "—", title = "LL Inland Marine Equipment Schedule" },
        new { form = "SMM - SLSTAMP", edition = "—", title = "Surplus Lines — State Stamp Only" },
        new { form = "LL IM OPT END", edition = "—", title = "LL Inland Marine Optional Endorsements" },
        new { form = "FORMS - SCHED A", edition = "08 12", title = "Schedule of Taxes, Surcharges or Fees" },
        new { form = "LL IM CLAIMS", edition = "—", title = "LL Inland Marine Claims Page" },
        new { form = "FORMS - SCHED", edition = "08 12", title = "Schedule of Forms and Endorsements" },
        new { form = "LL IM FLOATER", edition = "—", title = "LL Inland Marine Floater" },
    ];

    private static async Task<string> BuildSelfContainedHtmlAsync(
        string templateDir,
        object proposal,
        IReadOnlyList<object> equipment,
        IReadOnlyList<object> lossPayees,
        IReadOnlyList<object> endorsements,
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
    private static string FormatMoney(decimal value) => value.ToString("C");
    private static string SanitizeFileName(string name) => Regex.Replace(name, @"[^\w\-]", "_").Trim('_');

    private static string BuildProposalEmailBody(Quote quote)
    {
        var insuredName = HtmlEncoder.Default.Encode(quote.Submission.Insured.DisplayName);
        var carrierName = HtmlEncoder.Default.Encode(quote.Carrier.Name);
        var underwriterName = HtmlEncoder.Default.Encode(quote.Submission.Underwriter.FullName);
        var premium = HtmlEncoder.Default.Encode(FormatMoney(quote.TotalPremium != 0m ? quote.TotalPremium : quote.PremiumAmount));

        return $"""
            <p>Please find attached our Inland Marine proposal for {insuredName}.</p>
            <p><strong>Carrier:</strong> {carrierName}<br/>
            <strong>Effective:</strong> {quote.EffectiveDate:MM/dd/yyyy}<br/>
            <strong>Expiration:</strong> {quote.ExpirationDate:MM/dd/yyyy}<br/>
            <strong>Total Premium:</strong> {premium}</p>
            <p>Please review and let us know if you would like to bind coverage.</p>
            <p>Thank you,<br/>{underwriterName}</p>
            """;
    }

    private static string? ExtractState(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var match = Regex.Match(address, @"\b[A-Z]{2}\b");
        return match.Success ? match.Value : null;
    }
}
