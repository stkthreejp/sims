using SIMS.Application.Common;
using SIMS.Application.DTOs.OutboundCommunications;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace SIMS.Application.Services;

public class OutboundCommunicationService : IOutboundCommunicationService
{
    private readonly DbContext _db;
    private readonly IDocumentMergeService _merge;
    private readonly IOutboundEmailSenderService _emailSender;

    public OutboundCommunicationService(DbContext db, IDocumentMergeService merge, IOutboundEmailSenderService emailSender)
    {
        _db = db;
        _merge = merge;
        _emailSender = emailSender;
    }

    public async Task<IEnumerable<OutboundCommunicationListItemDto>> GetForEntityAsync(
        OutboundCommunicationEntityType entityType,
        Guid entityId,
        Guid? policyTransactionId = null)
    {
        var query = _db.Set<OutboundCommunication>()
            .Include(c => c.CreatedBy)
            .Include(c => c.Attachments.Where(a => !a.IsDeleted))
            .Where(c => c.EntityType == entityType && c.EntityId == entityId && !c.IsDeleted);

        if (policyTransactionId.HasValue)
            query = query.Where(c => c.PolicyTransactionId == policyTransactionId.Value);

        var communications = await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return communications.Select(MapToListItemDto);
    }

    public async Task<Result<OutboundCommunicationDto>> GetByIdAsync(Guid id)
    {
        var communication = await LoadByIdAsync(id);
        return communication == null
            ? Result<OutboundCommunicationDto>.Failure("NOT_FOUND", "Outbound communication not found.")
            : Result<OutboundCommunicationDto>.Success(MapToDto(communication));
    }

    public async Task<Result<OutboundCommunicationDto>> CreateDraftAsync(OutboundCommunicationCreateDto dto, Guid createdById)
    {
        var validation = await ValidateAsync(dto.TemplateId, dto.AttachmentIds);
        if (!validation.IsSuccess)
            return Result<OutboundCommunicationDto>.Failure(validation.ErrorCode!, validation.ErrorMessage!);

        (string Subject, string BodyHtml) merged;
        try
        {
            merged = await BuildMergedContentAsync(dto.EntityType, dto.EntityId, dto.TemplateId, dto.Subject, dto.BodyHtml);
        }
        catch (InvalidOperationException ex)
        {
            return Result<OutboundCommunicationDto>.Failure("DATA_ERROR", ex.Message);
        }

        var communication = new OutboundCommunication
        {
            EntityType = dto.EntityType,
            EntityId = dto.EntityId,
            PolicyTransactionId = dto.PolicyTransactionId,
            Purpose = dto.Purpose,
            TemplateId = dto.TemplateId,
            ToAddress = dto.ToAddress.Trim(),
            ToName = dto.ToName?.Trim(),
            CcAddresses = dto.CcAddresses?.Trim(),
            BccAddresses = dto.BccAddresses?.Trim(),
            FromAddress = dto.FromAddress.Trim(),
            FromName = dto.FromName?.Trim(),
            SenderType = dto.SenderType,
            Subject = merged.Subject.Trim(),
            BodyHtml = merged.BodyHtml,
            Status = OutboundCommunicationStatus.Draft,
            CreatedById = createdById,
        };

        foreach (var attachmentId in dto.AttachmentIds.Distinct())
            communication.Attachments.Add(new OutboundCommunicationAttachment { AttachmentId = attachmentId });

        _db.Set<OutboundCommunication>().Add(communication);
        await _db.SaveChangesAsync();

        return Result<OutboundCommunicationDto>.Success(MapToDto((await LoadByIdAsync(communication.Id))!));
    }

    public async Task<Result<OutboundCommunicationDto>> UpdateDraftAsync(Guid id, OutboundCommunicationUpdateDto dto)
    {
        var communication = await _db.Set<OutboundCommunication>()
            .Include(c => c.Attachments)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        if (communication == null)
            return Result<OutboundCommunicationDto>.Failure("NOT_FOUND", "Outbound communication not found.");
        if (communication.Status != OutboundCommunicationStatus.Draft)
            return Result<OutboundCommunicationDto>.Failure("NOT_DRAFT", "Only draft communications can be edited.");

        var validation = await ValidateAsync(communication.TemplateId, dto.AttachmentIds);
        if (!validation.IsSuccess)
            return Result<OutboundCommunicationDto>.Failure(validation.ErrorCode!, validation.ErrorMessage!);

        communication.ToAddress = dto.ToAddress.Trim();
        communication.PolicyTransactionId = dto.PolicyTransactionId;
        communication.Purpose = dto.Purpose;
        communication.ToName = dto.ToName?.Trim();
        communication.CcAddresses = dto.CcAddresses?.Trim();
        communication.BccAddresses = dto.BccAddresses?.Trim();
        communication.FromAddress = dto.FromAddress.Trim();
        communication.FromName = dto.FromName?.Trim();
        communication.SenderType = dto.SenderType;
        (string Subject, string BodyHtml) merged;
        try
        {
            merged = await BuildMergedContentAsync(communication.EntityType, communication.EntityId, communication.TemplateId, dto.Subject, dto.BodyHtml);
        }
        catch (InvalidOperationException ex)
        {
            return Result<OutboundCommunicationDto>.Failure("DATA_ERROR", ex.Message);
        }
        communication.Subject = merged.Subject.Trim();
        communication.BodyHtml = merged.BodyHtml;

        var requested = dto.AttachmentIds.Distinct().ToHashSet();
        foreach (var existing in communication.Attachments)
        {
            existing.IsDeleted = !requested.Contains(existing.AttachmentId);
            existing.DeletedAt = existing.IsDeleted ? DateTime.UtcNow : null;
        }

        var existingIds = communication.Attachments.Select(a => a.AttachmentId).ToHashSet();
        foreach (var attachmentId in requested.Where(id => !existingIds.Contains(id)))
            communication.Attachments.Add(new OutboundCommunicationAttachment { AttachmentId = attachmentId });

        await _db.SaveChangesAsync();
        return Result<OutboundCommunicationDto>.Success(MapToDto((await LoadByIdAsync(id))!));
    }

    public async Task<Result<OutboundCommunicationDto>> UpdateStatusAsync(
        Guid id,
        OutboundCommunicationStatusUpdateDto dto,
        Guid userId)
    {
        var communication = await _db.Set<OutboundCommunication>().FindAsync(id);
        if (communication == null || communication.IsDeleted)
            return Result<OutboundCommunicationDto>.Failure("NOT_FOUND", "Outbound communication not found.");

        communication.Status = dto.Status;
        communication.FailureReason = dto.FailureReason?.Trim();
        communication.GraphMessageId = dto.GraphMessageId?.Trim();

        if (dto.Status == OutboundCommunicationStatus.Sent)
        {
            communication.SentAt = DateTime.UtcNow;
            communication.SentById = userId;
        }

        await _db.SaveChangesAsync();
        return Result<OutboundCommunicationDto>.Success(MapToDto((await LoadByIdAsync(id))!));
    }

    public async Task<Result<OutboundCommunicationDto>> SendAsync(Guid id, Guid userId)
    {
        var communication = await LoadByIdAsync(id);
        if (communication == null)
            return Result<OutboundCommunicationDto>.Failure("NOT_FOUND", "Outbound communication not found.");

        if (communication.Status != OutboundCommunicationStatus.Draft && communication.Status != OutboundCommunicationStatus.Failed)
            return Result<OutboundCommunicationDto>.Failure("NOT_SENDABLE", "Only draft or failed communications can be sent.");

        var sendResult = await _emailSender.SendAsync(communication);
        if (!sendResult.IsSuccess || sendResult.Value == null)
        {
            communication.Status = OutboundCommunicationStatus.Failed;
            communication.FailureReason = sendResult.ErrorMessage;
            await _db.SaveChangesAsync();
            return Result<OutboundCommunicationDto>.Failure(sendResult.ErrorCode ?? "SEND_FAILED", sendResult.ErrorMessage ?? "Email could not be sent.");
        }

        communication.Status = OutboundCommunicationStatus.Sent;
        communication.FailureReason = null;
        communication.GraphMessageId = sendResult.Value.MessageId;
        communication.GraphMessageWebLink = sendResult.Value.WebLink;
        communication.SentAt = DateTime.UtcNow;
        communication.SentById = userId;

        await _db.SaveChangesAsync();
        return Result<OutboundCommunicationDto>.Success(MapToDto((await LoadByIdAsync(id))!));
    }

    private async Task<Result> ValidateAsync(Guid? templateId, IReadOnlyCollection<Guid> attachmentIds)
    {
        if (templateId.HasValue)
        {
            var templateExists = await _db.Set<DocumentTemplate>().AnyAsync(t => t.Id == templateId.Value && !t.IsDeleted);
            if (!templateExists)
                return Result.Failure("TEMPLATE_NOT_FOUND", "Selected template not found.");
        }

        if (attachmentIds.Count > 0)
        {
            var requested = attachmentIds.Distinct().ToList();
            var found = await _db.Set<Attachment>()
                .CountAsync(a => requested.Contains(a.Id) && !a.IsDeleted);
            if (found != requested.Count)
                return Result.Failure("ATTACHMENT_NOT_FOUND", "One or more selected attachments were not found.");
        }

        return Result.Success();
    }

    private async Task<(string Subject, string BodyHtml)> BuildMergedContentAsync(
        OutboundCommunicationEntityType entityType,
        Guid entityId,
        Guid? templateId,
        string subject,
        string bodyHtml)
    {
        var subjectTemplate = subject;
        var bodyTemplate = bodyHtml;

        if (templateId.HasValue)
        {
            var template = await _db.Set<DocumentTemplate>()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == templateId.Value && !t.IsDeleted);

            if (template != null)
            {
                if (string.IsNullOrWhiteSpace(subjectTemplate) && !string.IsNullOrWhiteSpace(template.SubjectTemplate))
                    subjectTemplate = template.SubjectTemplate;
                if (string.IsNullOrWhiteSpace(bodyTemplate))
                    bodyTemplate = !string.IsNullOrWhiteSpace(template.EmailBodyHtml) ? template.EmailBodyHtml : template.HtmlContent;
            }
        }

        var data = await BuildMergeDataAsync(entityType, entityId);
        return (_merge.MergeHtml(subjectTemplate, data), _merge.MergeHtml(bodyTemplate, data));
    }

    private async Task<DocumentMergeData> BuildMergeDataAsync(OutboundCommunicationEntityType entityType, Guid entityId)
    {
        var data = new DocumentMergeData();
        data.Values["TodayDate"] = DateTime.Today;
        data.Values["CompanyName"] = "Specialty Market Managers";

        switch (entityType)
        {
            case OutboundCommunicationEntityType.Quote:
                await AddQuoteDataAsync(data, entityId);
                break;
            case OutboundCommunicationEntityType.Policy:
                await AddPolicyDataAsync(data, entityId);
                break;
            case OutboundCommunicationEntityType.Submission:
                await AddSubmissionDataAsync(data, entityId);
                break;
            case OutboundCommunicationEntityType.Carrier:
                await AddCarrierDataAsync(data.Values, entityId);
                break;
            case OutboundCommunicationEntityType.Agent:
                await AddAgentDataAsync(data.Values, entityId);
                break;
            case OutboundCommunicationEntityType.Insured:
                await AddInsuredDataAsync(data.Values, entityId);
                break;
        }

        return data;
    }

    private async Task AddQuoteDataAsync(DocumentMergeData data, Guid quoteId)
    {
        var quote = await _db.Set<Quote>()
            .Include(q => q.Carrier)
            .Include(q => q.Submission).ThenInclude(s => s.Insured)
            .Include(q => q.Submission).ThenInclude(s => s.Agent).ThenInclude(a => a!.Locations)
            .Include(q => q.Submission).ThenInclude(s => s.Underwriter)
            .Include(q => q.Submission).ThenInclude(s => s.Equipment).ThenInclude(e => e.EquipmentType)
            .Include(q => q.Submission).ThenInclude(s => s.AdditionalInterests)
            .Include(q => q.Submission).ThenInclude(s => s.Vehicles)
            .FirstOrDefaultAsync(q => q.Id == quoteId)
            ?? throw new InvalidOperationException("Quote not found.");

        var d = data.Values;
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
        d["Quote.LineOfBusiness"] = quote.LineOfBusiness.ToString();
        d["QuoteNumber"] = quote.QuoteNumber;
        d["PolicyNumber"] = quote.PolicyNumber ?? string.Empty;
        d["EffectiveDate"] = quote.EffectiveDate;
        d["ExpirationDate"] = quote.ExpirationDate;
        d["TotalPremium"] = quote.TotalPremium;
        d["NetPremium"] = quote.PremiumAmount;
        d["TaxesAndFees"] = quote.TaxesAndFees;
        d["LineOfBusiness"] = quote.LineOfBusiness.ToString();

        AddCarrierValues(d, quote.Carrier);
        AddSubmissionValues(data, quote.Submission, quote.LineOfBusiness);
        await AddPolicyFormRowsAsync(data, quote.Id);
    }

    private async Task AddPolicyDataAsync(DocumentMergeData data, Guid policyId)
    {
        var policy = await _db.Set<Policy>()
            .Include(p => p.Carrier)
            .Include(p => p.BoundQuote)
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Submission).ThenInclude(s => s.Agent).ThenInclude(a => a!.Locations)
            .Include(p => p.Submission).ThenInclude(s => s.Underwriter)
            .Include(p => p.Submission).ThenInclude(s => s.Equipment).ThenInclude(e => e.EquipmentType)
            .Include(p => p.Submission).ThenInclude(s => s.AdditionalInterests)
            .Include(p => p.Submission).ThenInclude(s => s.Vehicles)
            .FirstOrDefaultAsync(p => p.Id == policyId)
            ?? throw new InvalidOperationException("Policy not found.");

        var d = data.Values;
        d["Policy.PolicyNumber"] = policy.PolicyNumber;
        d["Policy.EffectiveDate"] = policy.EffectiveDate;
        d["Policy.ExpirationDate"] = policy.ExpirationDate;
        d["Policy.BoundDate"] = policy.BoundDate;
        d["Policy.IssuedDate"] = policy.IssuedDate;
        d["Policy.PremiumAmount"] = policy.PremiumAmount;
        d["Policy.TaxesAndFees"] = policy.TaxesAndFees;
        d["Policy.TotalPremium"] = policy.TotalPremium;
        d["Policy.LineOfBusiness"] = policy.LineOfBusiness.ToString();
        d["PolicyNumber"] = policy.PolicyNumber;
        d["EffectiveDate"] = policy.EffectiveDate;
        d["ExpirationDate"] = policy.ExpirationDate;
        d["TotalPremium"] = policy.TotalPremium;
        d["NetPremium"] = policy.PremiumAmount;
        d["TaxesAndFees"] = policy.TaxesAndFees;
        d["LineOfBusiness"] = policy.LineOfBusiness.ToString();

        if (policy.BoundQuote != null)
        {
            d["Quote.QuoteNumber"] = policy.BoundQuote.QuoteNumber;
            d["Quote.PolicyNumber"] = policy.BoundQuote.PolicyNumber;
            d["Quote.TotalPremium"] = policy.BoundQuote.TotalPremium;
        }

        AddCarrierValues(d, policy.Carrier);
        AddSubmissionValues(data, policy.Submission, policy.LineOfBusiness);
        await AddPolicyFormRowsAsync(data, policy.BoundQuoteId);
    }

    private async Task AddSubmissionDataAsync(DocumentMergeData data, Guid submissionId)
    {
        var submission = await _db.Set<Submission>()
            .Include(s => s.Insured)
            .Include(s => s.Agent).ThenInclude(a => a!.Locations)
            .Include(s => s.Underwriter)
            .Include(s => s.Equipment).ThenInclude(e => e.EquipmentType)
            .Include(s => s.AdditionalInterests)
            .Include(s => s.Vehicles)
            .FirstOrDefaultAsync(s => s.Id == submissionId)
            ?? throw new InvalidOperationException("Submission not found.");

        AddSubmissionValues(data, submission, null);
    }

    private async Task AddCarrierDataAsync(Dictionary<string, object?> values, Guid carrierId)
    {
        var carrier = await _db.Set<Carrier>().FirstOrDefaultAsync(c => c.Id == carrierId)
            ?? throw new InvalidOperationException("Carrier not found.");
        AddCarrierValues(values, carrier);
    }

    private async Task AddAgentDataAsync(Dictionary<string, object?> values, Guid agentId)
    {
        var agent = await _db.Set<Agent>()
            .Include(a => a.Locations)
            .FirstOrDefaultAsync(a => a.Id == agentId)
            ?? throw new InvalidOperationException("Agent not found.");
        AddAgentValues(values, agent);
    }

    private async Task AddInsuredDataAsync(Dictionary<string, object?> values, Guid insuredId)
    {
        var insured = await _db.Set<Insured>().FirstOrDefaultAsync(i => i.Id == insuredId)
            ?? throw new InvalidOperationException("Insured not found.");
        AddInsuredValues(values, insured);
    }

    private static void AddSubmissionValues(DocumentMergeData data, Submission submission, PolicyLineOfBusiness? lineOfBusiness)
    {
        var d = data.Values;
        d["Submission.SubmissionNumber"] = submission.SubmissionNumber;
        d["SubmissionNumber"] = submission.SubmissionNumber;
        d["SubmissionDate"] = submission.CreatedAt;
        d["RequestedEffDate"] = submission.EffectiveDate;
        d["SubmissionStatus"] = submission.Status.ToString();

        AddInsuredValues(d, submission.Insured);
        if (submission.Agent != null)
            AddAgentValues(d, submission.Agent);

        d["UnderwriterName"] = submission.Underwriter.FullName;
        d["UnderwriterEmail"] = submission.Underwriter.Email ?? string.Empty;

        data.RepeatingValues["Equipment"] = submission.Equipment
            .Where(e => !e.IsDeleted)
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

        data.RepeatingValues["AdditionalInterests"] = submission.AdditionalInterests
            .Where(i => !i.IsDeleted && (!lineOfBusiness.HasValue || i.LineOfBusiness == lineOfBusiness.Value))
            .OrderBy(i => i.Name)
            .Select(i => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = i.Name,
                ["Address"] = FormatAddress(i.AddressLine1, i.AddressLine2, i.City, i.State, i.ZipCode),
                ["Types"] = FormatAdditionalInterestTypes(i),
                ["LoanNumber"] = i.ScheduledItemNumbers,
            } as IReadOnlyDictionary<string, object?>)
            .ToList();

        data.RepeatingValues["Vehicles"] = submission.Vehicles
            .Where(v => !v.IsDeleted)
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
    }

    private async Task AddPolicyFormRowsAsync(DocumentMergeData data, Guid quoteId)
    {
        var forms = await _db.Set<QuotePolicyFormSelection>()
            .AsNoTracking()
            .Include(f => f.PolicyFormTemplate)
            .Where(f => f.QuoteId == quoteId && f.IsIncluded)
            .OrderBy(f => f.SequenceOrder)
            .ToListAsync();

        data.RepeatingValues["PolicyForms"] = forms
            .Select(f => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["FormNumber"] = f.PolicyFormTemplate.FormNumber,
                ["FormName"] = f.PolicyFormTemplate.Name,
                ["EditionDate"] = f.PolicyFormTemplate.EditionDate,
                ["Status"] = f.IsIncluded ? "Included" : "Excluded",
            } as IReadOnlyDictionary<string, object?>)
            .ToList();
    }

    private static void AddInsuredValues(Dictionary<string, object?> d, Insured insured)
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
        d["InsuredName"] = insured.DisplayName;
        d["InsuredEmail"] = insured.Email ?? string.Empty;
        d["InsuredPhone"] = insured.Phone ?? string.Empty;
        d["InsuredFullAddress"] = FormatAddress(insured.AddressLine1, insured.City, insured.State, insured.ZipCode);
    }

    private static void AddCarrierValues(Dictionary<string, object?> d, Carrier carrier)
    {
        d["Carrier.Name"] = carrier.Name;
        d["Carrier.Naic"] = carrier.Naic;
        d["CarrierName"] = carrier.Name;
        d["CarrierNAIC"] = carrier.Naic ?? string.Empty;
        d["CarrierAMBest"] = carrier.AmBestRating ?? string.Empty;
    }

    private static void AddAgentValues(Dictionary<string, object?> d, Agent agent)
    {
        var primary = agent.Locations.FirstOrDefault(l => l.IsPrimary)
                   ?? agent.Locations.FirstOrDefault();

        d["AgentName"] = agent.Name;
        d["AgentAgency"] = agent.AgencyName ?? string.Empty;
        d["AgentEmail"] = agent.Email ?? string.Empty;
        d["AgentPhone"] = primary?.Phone ?? agent.Phone ?? string.Empty;
        d["AgentLicense"] = agent.LicenseNumber ?? string.Empty;
        d["AgentCity"] = primary?.City ?? string.Empty;
        d["AgentState"] = primary?.State ?? string.Empty;
    }

    private static string FormatAddress(params string?[] parts)
        => string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    private static string FormatAdditionalInterestTypes(SubmissionAdditionalInterest interest)
    {
        var types = new List<string>();
        if (interest.AdditionalInsured) types.Add("Additional Insured");
        if (interest.LossPayee) types.Add("Loss Payee");
        if (interest.WaiverOfSubrogation) types.Add("Waiver of Subrogation");
        if (interest.PrimaryNonContributory) types.Add("Primary Non-Contributory");
        return string.Join(", ", types);
    }

    private Task<OutboundCommunication?> LoadByIdAsync(Guid id) =>
        _db.Set<OutboundCommunication>()
            .Include(c => c.CreatedBy)
            .Include(c => c.SentBy)
            .Include(c => c.Attachments.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.Attachment)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

    private static OutboundCommunicationListItemDto MapToListItemDto(OutboundCommunication c) => new()
    {
        Id = c.Id,
        EntityType = c.EntityType,
        EntityId = c.EntityId,
        PolicyTransactionId = c.PolicyTransactionId,
        Purpose = c.Purpose,
        ToAddress = c.ToAddress,
        ToName = c.ToName,
        FromAddress = c.FromAddress,
        Subject = c.Subject,
        Status = c.Status,
        GraphMessageId = c.GraphMessageId,
        GraphMessageWebLink = c.GraphMessageWebLink,
        SentAt = c.SentAt,
        CreatedByName = c.CreatedBy?.FullName ?? string.Empty,
        AttachmentCount = c.Attachments?.Count ?? 0,
        CreatedAt = c.CreatedAt,
    };

    private static OutboundCommunicationDto MapToDto(OutboundCommunication c) => new()
    {
        Id = c.Id,
        EntityType = c.EntityType,
        EntityId = c.EntityId,
        PolicyTransactionId = c.PolicyTransactionId,
        Purpose = c.Purpose,
        TemplateId = c.TemplateId,
        ToAddress = c.ToAddress,
        ToName = c.ToName,
        CcAddresses = c.CcAddresses,
        BccAddresses = c.BccAddresses,
        FromAddress = c.FromAddress,
        FromName = c.FromName,
        SenderType = c.SenderType,
        Subject = c.Subject,
        BodyHtml = c.BodyHtml,
        Status = c.Status,
        FailureReason = c.FailureReason,
        GraphMessageId = c.GraphMessageId,
        GraphMessageWebLink = c.GraphMessageWebLink,
        CreatedByName = c.CreatedBy?.FullName ?? string.Empty,
        SentByName = c.SentBy?.FullName,
        SentAt = c.SentAt,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        Attachments = c.Attachments?.Select(a => new OutboundCommunicationAttachmentDto
        {
            AttachmentId = a.AttachmentId,
            FileName = a.Attachment?.FileName ?? string.Empty,
        }).ToList() ?? [],
    };
}
