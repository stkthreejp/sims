using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Compliance;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;

namespace SIMS.Application.Services;

public class ComplianceDocumentService : IComplianceDocumentService
{
    private readonly IServiceProvider _sp;
    public ComplianceDocumentService(IServiceProvider sp) => _sp = sp;

    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public async Task<ComplianceDocumentSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dueSoon = today.AddDays(30);

        return new ComplianceDocumentSummaryDto
        {
            TotalDocuments = await Db.Set<ComplianceDocument>().CountAsync(ct),
            ActiveDocuments = await Db.Set<ComplianceDocument>().CountAsync(d => d.Status == "Active", ct),
            DraftDocuments = await Db.Set<ComplianceDocument>().CountAsync(d => d.Status == "Draft" || d.Status == "Under Review", ct),
            DueSoon = await Db.Set<ComplianceDocument>().CountAsync(d => d.NextReviewDate != null && d.NextReviewDate >= today && d.NextReviewDate <= dueSoon, ct),
            Overdue = await Db.Set<ComplianceDocument>().CountAsync(d => d.NextReviewDate != null && d.NextReviewDate < today, ct),
            ActiveAttestationCampaigns = await Db.Set<ComplianceAttestationCampaign>().CountAsync(c => c.Status == "Active", ct),
            PendingAttestations = await Db.Set<ComplianceAttestationRecipient>().CountAsync(r => r.Status == "Pending", ct),
        };
    }

    public async Task<IReadOnlyList<ComplianceDocumentListItemDto>> GetDocumentsAsync(
        string? status = null,
        string? category = null,
        string? search = null,
        CancellationToken ct = default)
    {
        var q = Db.Set<ComplianceDocument>()
            .Include(d => d.Owner)
            .Include(d => d.Approver)
            .Include(d => d.CurrentPublishedVersion)
            .Include(d => d.CurrentDraftVersion)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(d => d.Status == status);

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(d => d.Category == category);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = q.Where(d =>
                d.Title.ToLower().Contains(term) ||
                d.Category.ToLower().Contains(term) ||
                d.DocumentType.ToLower().Contains(term) ||
                d.Tags.Any(t => t.ToLower().Contains(term)) ||
                (d.CurrentPublishedVersion != null && d.CurrentPublishedVersion.PlainText.ToLower().Contains(term)) ||
                (d.CurrentDraftVersion != null && d.CurrentDraftVersion.PlainText.ToLower().Contains(term)));
        }

        var documents = await q.OrderBy(d => d.NextReviewDate == null).ThenBy(d => d.NextReviewDate).ThenBy(d => d.Title).ToListAsync(ct);
        return documents.Select(MapListItem).ToList();
    }

    public async Task<Result<ComplianceDocumentDetailDto>> GetDocumentAsync(Guid id, CancellationToken ct = default)
    {
        var document = await LoadDetailQuery().FirstOrDefaultAsync(d => d.Id == id, ct);
        return document == null
            ? Result<ComplianceDocumentDetailDto>.Failure("NOT_FOUND", "Compliance document not found.")
            : Result<ComplianceDocumentDetailDto>.Success(MapDetail(document));
    }

    public async Task<Result<ComplianceDocumentDetailDto>> CreateDocumentAsync(ComplianceDocumentCreateDto dto, Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<ComplianceDocumentDetailDto>.Failure("VALIDATION", "Document title is required.");

        var document = new ComplianceDocument
        {
            Title = dto.Title.Trim(),
            Category = Clean(dto.Category, "IT"),
            DocumentType = Clean(dto.DocumentType, "Policy"),
            OwnerId = dto.OwnerId,
            ApproverId = dto.ApproverId,
            EffectiveDate = dto.EffectiveDate,
            NextReviewDate = dto.NextReviewDate,
            ReviewCadence = Clean(dto.ReviewCadence, "Annual"),
            Tags = CleanTags(dto.Tags),
            Status = "Draft",
        };

        var version = new ComplianceDocumentVersion
        {
            Document = document,
            VersionNumber = 1,
            Status = "Draft",
            HtmlContent = string.IsNullOrWhiteSpace(dto.HtmlContent) ? "<p></p>" : dto.HtmlContent,
            PlainText = ToPlainText(dto.HtmlContent),
            CreatedById = userId,
            EffectiveDate = dto.EffectiveDate,
        };
        document.CurrentDraftVersion = version;

        Db.Set<ComplianceDocument>().Add(document);
        Db.Set<ComplianceAuditLog>().Add(new ComplianceAuditLog
        {
            Document = document,
            Version = version,
            Action = "Created",
            UserId = userId,
            NewValue = document.Title
        });

        await Db.SaveChangesAsync(ct);
        return await GetDocumentAsync(document.Id, ct);
    }

    public async Task<Result<ComplianceDocumentDetailDto>> UpdateDocumentAsync(Guid id, ComplianceDocumentUpdateDto dto, Guid userId, CancellationToken ct = default)
    {
        var document = await Db.Set<ComplianceDocument>().FindAsync([id], ct);
        if (document == null)
            return Result<ComplianceDocumentDetailDto>.Failure("NOT_FOUND", "Compliance document not found.");

        var oldStatus = document.Status;
        document.Title = Clean(dto.Title, document.Title);
        document.Category = Clean(dto.Category, document.Category);
        document.DocumentType = Clean(dto.DocumentType, document.DocumentType);
        document.Status = Clean(dto.Status, document.Status);
        document.OwnerId = dto.OwnerId;
        document.ApproverId = dto.ApproverId;
        document.EffectiveDate = dto.EffectiveDate;
        document.NextReviewDate = dto.NextReviewDate;
        document.ReviewCadence = Clean(dto.ReviewCadence, document.ReviewCadence);
        document.Tags = CleanTags(dto.Tags);

        if (oldStatus != document.Status)
            AddAudit(document.Id, null, "StatusChanged", "Status", oldStatus, document.Status, null, userId);

        await Db.SaveChangesAsync(ct);
        return await GetDocumentAsync(document.Id, ct);
    }

    public async Task<Result<ComplianceDocumentDetailDto>> SaveDraftAsync(Guid id, ComplianceDraftSaveDto dto, Guid userId, CancellationToken ct = default)
    {
        var document = await Db.Set<ComplianceDocument>()
            .Include(d => d.CurrentDraftVersion)
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (document == null)
            return Result<ComplianceDocumentDetailDto>.Failure("NOT_FOUND", "Compliance document not found.");

        var draft = document.CurrentDraftVersion;
        if (draft == null || draft.Status != "Draft")
        {
            var nextVersionNumber = document.Versions.Count == 0 ? 1 : document.Versions.Max(v => v.VersionNumber) + 1;
            draft = new ComplianceDocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = nextVersionNumber,
                Status = "Draft",
                CreatedById = userId,
            };
            Db.Set<ComplianceDocumentVersion>().Add(draft);
            document.CurrentDraftVersion = draft;
        }

        draft.HtmlContent = string.IsNullOrWhiteSpace(dto.HtmlContent) ? "<p></p>" : dto.HtmlContent;
        draft.PlainText = ToPlainText(dto.HtmlContent);
        draft.ChangeSummary = dto.ChangeSummary?.Trim();
        document.Status = "Draft";
        AddAudit(document.Id, draft.Id, "DraftSaved", null, null, draft.ChangeSummary, null, userId);

        await Db.SaveChangesAsync(ct);
        return await GetDocumentAsync(document.Id, ct);
    }

    public async Task<Result<ComplianceDocumentDetailDto>> SubmitForReviewAsync(Guid id, ComplianceWorkflowActionDto dto, Guid userId, CancellationToken ct = default)
    {
        var document = await Db.Set<ComplianceDocument>()
            .Include(d => d.CurrentDraftVersion)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (document == null)
            return Result<ComplianceDocumentDetailDto>.Failure("NOT_FOUND", "Compliance document not found.");

        if (document.CurrentDraftVersion == null)
            return Result<ComplianceDocumentDetailDto>.Failure("VALIDATION", "A draft is required before submitting for review.");

        var oldStatus = document.Status;
        document.Status = "Under Review";
        AddAudit(document.Id, document.CurrentDraftVersion.Id, "SubmittedForReview", "Status", oldStatus, document.Status, dto.Notes, userId);

        await Db.SaveChangesAsync(ct);
        return await GetDocumentAsync(document.Id, ct);
    }

    public async Task<Result<ComplianceDocumentDetailDto>> RequireChangesAsync(Guid id, ComplianceWorkflowActionDto dto, Guid userId, CancellationToken ct = default)
    {
        var document = await Db.Set<ComplianceDocument>()
            .Include(d => d.CurrentDraftVersion)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (document == null)
            return Result<ComplianceDocumentDetailDto>.Failure("NOT_FOUND", "Compliance document not found.");

        if (document.CurrentDraftVersion == null)
            return Result<ComplianceDocumentDetailDto>.Failure("VALIDATION", "A draft is required before requesting changes.");

        var oldStatus = document.Status;
        document.Status = "Needs Update";
        AddAudit(document.Id, document.CurrentDraftVersion.Id, "ChangesRequested", "Status", oldStatus, document.Status, dto.Notes, userId);

        await Db.SaveChangesAsync(ct);
        return await GetDocumentAsync(document.Id, ct);
    }

    public async Task<Result<ComplianceDocumentDetailDto>> PublishDraftAsync(Guid id, CompliancePublishDto dto, Guid userId, CancellationToken ct = default)
    {
        var document = await Db.Set<ComplianceDocument>()
            .Include(d => d.CurrentDraftVersion)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (document?.CurrentDraftVersion == null)
            return Result<ComplianceDocumentDetailDto>.Failure("NOT_FOUND", "Draft not found.");

        var draft = document.CurrentDraftVersion;
        draft.Status = "Published";
        draft.ApprovedById = userId;
        draft.ApprovedAt = DateTime.UtcNow;
        draft.EffectiveDate = dto.EffectiveDate ?? document.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        document.CurrentPublishedVersionId = draft.Id;
        document.CurrentDraftVersionId = null;
        document.Status = "Active";
        document.EffectiveDate = draft.EffectiveDate;
        document.LastReviewedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        document.NextReviewDate = CalculateNextReviewDate(document.LastReviewedDate.Value, document.ReviewCadence);

        Db.Set<ComplianceDocumentReview>().Add(new ComplianceDocumentReview
        {
            DocumentId = document.Id,
            VersionId = draft.Id,
            Status = "Approved",
            Notes = dto.Notes?.Trim(),
            ReviewedById = userId,
            NextReviewDate = document.NextReviewDate,
        });
        AddAudit(document.Id, draft.Id, "Published", "Status", "Draft", "Active", dto.Notes, userId);

        await Db.SaveChangesAsync(ct);
        return await GetDocumentAsync(document.Id, ct);
    }

    public async Task<Result<ComplianceDocumentReviewDto>> AddReviewAsync(Guid id, ComplianceReviewCreateDto dto, Guid userId, CancellationToken ct = default)
    {
        var document = await Db.Set<ComplianceDocument>().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (document == null)
            return Result<ComplianceDocumentReviewDto>.Failure("NOT_FOUND", "Compliance document not found.");

        var reviewedAt = DateOnly.FromDateTime(DateTime.UtcNow);
        var nextReviewDate = dto.NextReviewDate ?? CalculateNextReviewDate(reviewedAt, document.ReviewCadence);
        var review = new ComplianceDocumentReview
        {
            DocumentId = id,
            VersionId = document.CurrentPublishedVersionId,
            Status = Clean(dto.Status, "Completed"),
            Notes = dto.Notes?.Trim(),
            ReviewedById = userId,
            NextReviewDate = nextReviewDate,
        };

        document.LastReviewedDate = reviewedAt;
        document.NextReviewDate = nextReviewDate;
        if (document.Status == "Needs Update" && review.Status == "Completed")
            document.Status = "Active";

        Db.Set<ComplianceDocumentReview>().Add(review);
        AddAudit(document.Id, review.VersionId, "Reviewed", "ReviewStatus", null, review.Status, review.Notes, userId);
        await Db.SaveChangesAsync(ct);

        var loaded = await Db.Set<ComplianceDocumentReview>()
            .Include(r => r.ReviewedBy)
            .FirstAsync(r => r.Id == review.Id, ct);
        return Result<ComplianceDocumentReviewDto>.Success(MapReview(loaded));
    }

    public async Task<Result<ComplianceEvidenceDto>> AddEvidenceAsync(Guid id, ComplianceEvidenceCreateDto dto, Guid userId, CancellationToken ct = default)
    {
        if (!await Db.Set<ComplianceDocument>().AnyAsync(d => d.Id == id, ct))
            return Result<ComplianceEvidenceDto>.Failure("NOT_FOUND", "Compliance document not found.");

        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<ComplianceEvidenceDto>.Failure("VALIDATION", "Evidence title is required.");

        var evidence = new ComplianceEvidence
        {
            DocumentId = id,
            Title = dto.Title.Trim(),
            EvidenceType = Clean(dto.EvidenceType, "Note"),
            Description = dto.Description?.Trim(),
            Url = dto.Url?.Trim(),
            CreatedById = userId,
        };

        Db.Set<ComplianceEvidence>().Add(evidence);
        AddAudit(id, null, "EvidenceAdded", null, null, evidence.Title, null, userId);
        await Db.SaveChangesAsync(ct);

        var loaded = await Db.Set<ComplianceEvidence>().Include(e => e.CreatedBy).FirstAsync(e => e.Id == evidence.Id, ct);
        return Result<ComplianceEvidenceDto>.Success(MapEvidence(loaded));
    }

    public async Task<Result<ComplianceVersionCompareDto>> CompareVersionsAsync(Guid id, Guid? fromVersionId = null, Guid? toVersionId = null, CancellationToken ct = default)
    {
        var document = await Db.Set<ComplianceDocument>()
            .Include(d => d.CurrentPublishedVersion)
            .Include(d => d.CurrentDraftVersion)
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (document == null)
            return Result<ComplianceVersionCompareDto>.Failure("NOT_FOUND", "Compliance document not found.");

        var from = fromVersionId.HasValue
            ? document.Versions.FirstOrDefault(v => v.Id == fromVersionId.Value)
            : document.CurrentPublishedVersion;
        var to = toVersionId.HasValue
            ? document.Versions.FirstOrDefault(v => v.Id == toVersionId.Value)
            : document.CurrentDraftVersion ?? document.CurrentPublishedVersion;

        if (from == null || to == null)
            return Result<ComplianceVersionCompareDto>.Failure("NOT_FOUND", "Two versions are required for comparison.");

        return Result<ComplianceVersionCompareDto>.Success(new ComplianceVersionCompareDto
        {
            FromVersionId = from.Id,
            ToVersionId = to.Id,
            FromTitle = $"Version {from.VersionNumber} ({from.Status})",
            ToTitle = $"Version {to.VersionNumber} ({to.Status})",
            Parts = BuildDiff(from.PlainText, to.PlainText)
        });
    }

    public async Task<IReadOnlyList<ComplianceAuditLogDto>> GetAuditLogAsync(Guid documentId, CancellationToken ct = default)
    {
        var logs = await Db.Set<ComplianceAuditLog>()
            .Include(l => l.User)
            .Where(l => l.DocumentId == documentId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        return logs.Select(MapAuditLog).ToList();
    }

    public async Task<IReadOnlyList<ComplianceAttestationCampaignDto>> GetAttestationCampaignsAsync(Guid? documentId = null, CancellationToken ct = default)
    {
        var q = AttestationCampaignQuery();
        if (documentId.HasValue)
            q = q.Where(c => c.DocumentId == documentId.Value);

        var campaigns = await q
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return campaigns.Select(MapCampaign).ToList();
    }

    public async Task<Result<ComplianceAttestationCampaignDto>> CreateAttestationCampaignAsync(Guid documentId, ComplianceAttestationCampaignCreateDto dto, Guid userId, CancellationToken ct = default)
    {
        if (dto.UserIds.Length == 0)
            return Result<ComplianceAttestationCampaignDto>.Failure("VALIDATION", "At least one recipient is required.");

        var document = await Db.Set<ComplianceDocument>()
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (document == null)
            return Result<ComplianceAttestationCampaignDto>.Failure("NOT_FOUND", "Compliance document not found.");

        var version = document.Versions.FirstOrDefault(v => v.Id == dto.VersionId);
        if (version == null || version.Status != "Published")
            return Result<ComplianceAttestationCampaignDto>.Failure("VALIDATION", "Attestations can only be launched for a published version.");

        var recipientIds = dto.UserIds.Distinct().ToArray();
        var activeUserIds = await Db.Set<User>()
            .Where(u => recipientIds.Contains(u.Id) && !u.IsDeleted)
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (activeUserIds.Count == 0)
            return Result<ComplianceAttestationCampaignDto>.Failure("VALIDATION", "No active recipients were found.");

        var campaign = new ComplianceAttestationCampaign
        {
            DocumentId = document.Id,
            VersionId = version.Id,
            Name = Clean(dto.Name, $"{document.Title} v{version.VersionNumber} Attestation"),
            Statement = Clean(dto.Statement, "I acknowledge that I have reviewed and understand this document version."),
            DueDate = dto.DueDate,
            Status = "Active",
            CreatedById = userId,
            Recipients = activeUserIds.Select(id => new ComplianceAttestationRecipient
            {
                UserId = id,
                Status = "Pending",
            }).ToList()
        };

        Db.Set<ComplianceAttestationCampaign>().Add(campaign);
        AddAudit(document.Id, version.Id, "AttestationLaunched", null, null, campaign.Name, null, userId);
        await Db.SaveChangesAsync(ct);

        return await GetAttestationCampaignAsync(campaign.Id, ct);
    }

    public async Task<Result<ComplianceAttestationCampaignDto>> GetAttestationCampaignAsync(Guid campaignId, CancellationToken ct = default)
    {
        var campaign = await AttestationCampaignQuery().FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        return campaign == null
            ? Result<ComplianceAttestationCampaignDto>.Failure("NOT_FOUND", "Attestation campaign not found.")
            : Result<ComplianceAttestationCampaignDto>.Success(MapCampaign(campaign));
    }

    public async Task<Result<ComplianceAttestationRecipientDto>> SubmitAttestationAsync(Guid campaignId, ComplianceAttestationSubmitDto dto, Guid userId, CancellationToken ct = default)
    {
        var recipient = await Db.Set<ComplianceAttestationRecipient>()
            .Include(r => r.User)
            .Include(r => r.Campaign)
            .ThenInclude(c => c.Document)
            .FirstOrDefaultAsync(r => r.CampaignId == campaignId && r.UserId == userId, ct);

        if (recipient == null)
            return Result<ComplianceAttestationRecipientDto>.Failure("NOT_FOUND", "You are not assigned to this attestation campaign.");

        if (recipient.Campaign.Status != "Active")
            return Result<ComplianceAttestationRecipientDto>.Failure("VALIDATION", "This attestation campaign is not active.");

        var oldStatus = recipient.Status;
        recipient.Status = dto.Status == "Declined" ? "Declined" : "Attested";
        recipient.Comment = dto.Comment?.Trim();
        recipient.AttestedAt = DateTime.UtcNow;
        AddAudit(recipient.Campaign.DocumentId, recipient.Campaign.VersionId, "AttestationSubmitted", "Status", oldStatus, recipient.Status, recipient.Comment, userId);

        await Db.SaveChangesAsync(ct);
        return Result<ComplianceAttestationRecipientDto>.Success(MapRecipient(recipient));
    }

    private IQueryable<ComplianceDocument> LoadDetailQuery() =>
        Db.Set<ComplianceDocument>()
            .Include(d => d.Owner)
            .Include(d => d.Approver)
            .Include(d => d.CurrentPublishedVersion)!.ThenInclude(v => v!.CreatedBy)
            .Include(d => d.CurrentPublishedVersion)!.ThenInclude(v => v!.ApprovedBy)
            .Include(d => d.CurrentDraftVersion)!.ThenInclude(v => v!.CreatedBy)
            .Include(d => d.Versions).ThenInclude(v => v.CreatedBy)
            .Include(d => d.Versions).ThenInclude(v => v.ApprovedBy)
            .Include(d => d.Reviews).ThenInclude(r => r.ReviewedBy)
            .Include(d => d.EvidenceItems).ThenInclude(e => e.CreatedBy);

    private IQueryable<ComplianceAttestationCampaign> AttestationCampaignQuery() =>
        Db.Set<ComplianceAttestationCampaign>()
            .Include(c => c.Document)
            .Include(c => c.Version)
            .Include(c => c.CreatedBy)
            .Include(c => c.Recipients)
            .ThenInclude(r => r.User);

    private static ComplianceDocumentDetailDto MapDetail(ComplianceDocument d)
    {
        var item = MapListItem(d);
        return new ComplianceDocumentDetailDto
        {
            Id = item.Id,
            Title = item.Title,
            Category = item.Category,
            DocumentType = item.DocumentType,
            Status = item.Status,
            OwnerId = d.OwnerId,
            OwnerName = item.OwnerName,
            ApproverId = d.ApproverId,
            ApproverName = item.ApproverName,
            EffectiveDate = item.EffectiveDate,
            LastReviewedDate = item.LastReviewedDate,
            NextReviewDate = item.NextReviewDate,
            ReviewCadence = item.ReviewCadence,
            Tags = item.Tags,
            CurrentPublishedVersionNumber = item.CurrentPublishedVersionNumber,
            CurrentDraftVersionNumber = item.CurrentDraftVersionNumber,
            UpdatedAt = item.UpdatedAt,
            CurrentPublishedVersion = d.CurrentPublishedVersion == null ? null : MapVersion(d.CurrentPublishedVersion),
            CurrentDraftVersion = d.CurrentDraftVersion == null ? null : MapVersion(d.CurrentDraftVersion),
            Versions = d.Versions.OrderByDescending(v => v.VersionNumber).Select(MapVersion).ToList(),
            Reviews = d.Reviews.OrderByDescending(r => r.ReviewedAt).Select(MapReview).ToList(),
            EvidenceItems = d.EvidenceItems.OrderByDescending(e => e.CreatedAt).Select(MapEvidence).ToList(),
        };
    }

    private static ComplianceDocumentListItemDto MapListItem(ComplianceDocument d) => new()
    {
        Id = d.Id,
        Title = d.Title,
        Category = d.Category,
        DocumentType = d.DocumentType,
        Status = d.Status,
        OwnerName = d.Owner?.FullName,
        ApproverName = d.Approver?.FullName,
        EffectiveDate = d.EffectiveDate,
        LastReviewedDate = d.LastReviewedDate,
        NextReviewDate = d.NextReviewDate,
        ReviewCadence = d.ReviewCadence,
        Tags = d.Tags,
        CurrentPublishedVersionNumber = d.CurrentPublishedVersion?.VersionNumber,
        CurrentDraftVersionNumber = d.CurrentDraftVersion?.VersionNumber,
        UpdatedAt = d.UpdatedAt,
    };

    private static ComplianceDocumentVersionDto MapVersion(ComplianceDocumentVersion v) => new()
    {
        Id = v.Id,
        VersionNumber = v.VersionNumber,
        Status = v.Status,
        HtmlContent = v.HtmlContent,
        PlainText = v.PlainText,
        ChangeSummary = v.ChangeSummary,
        CreatedByName = v.CreatedBy.FullName,
        ApprovedByName = v.ApprovedBy?.FullName,
        CreatedAt = v.CreatedAt,
        ApprovedAt = v.ApprovedAt,
        EffectiveDate = v.EffectiveDate,
    };

    private static ComplianceDocumentReviewDto MapReview(ComplianceDocumentReview r) => new()
    {
        Id = r.Id,
        VersionId = r.VersionId,
        Status = r.Status,
        Notes = r.Notes,
        ReviewedByName = r.ReviewedBy.FullName,
        ReviewedAt = r.ReviewedAt,
        NextReviewDate = r.NextReviewDate,
    };

    private static ComplianceEvidenceDto MapEvidence(ComplianceEvidence e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        EvidenceType = e.EvidenceType,
        Description = e.Description,
        Url = e.Url,
        CreatedByName = e.CreatedBy.FullName,
        CreatedAt = e.CreatedAt,
    };

    private static ComplianceAttestationCampaignDto MapCampaign(ComplianceAttestationCampaign c)
    {
        var recipients = c.Recipients.OrderBy(r => r.User.FullName).Select(MapRecipient).ToList();
        return new ComplianceAttestationCampaignDto
        {
            Id = c.Id,
            DocumentId = c.DocumentId,
            VersionId = c.VersionId,
            DocumentTitle = c.Document.Title,
            VersionNumber = c.Version.VersionNumber,
            Name = c.Name,
            Statement = c.Statement,
            DueDate = c.DueDate,
            Status = c.Status,
            CreatedByName = c.CreatedBy.FullName,
            CreatedAt = c.CreatedAt,
            RecipientCount = recipients.Count,
            PendingCount = recipients.Count(r => r.Status == "Pending"),
            AttestedCount = recipients.Count(r => r.Status == "Attested"),
            DeclinedCount = recipients.Count(r => r.Status == "Declined"),
            Recipients = recipients,
        };
    }

    private static ComplianceAttestationRecipientDto MapRecipient(ComplianceAttestationRecipient r) => new()
    {
        Id = r.Id,
        UserId = r.UserId,
        UserName = r.User.FullName,
        Email = r.User.Email ?? string.Empty,
        Status = r.Status,
        AttestedAt = r.AttestedAt,
        Comment = r.Comment,
    };

    private static ComplianceAuditLogDto MapAuditLog(ComplianceAuditLog l) => new()
    {
        Id = l.Id,
        DocumentId = l.DocumentId,
        VersionId = l.VersionId,
        Action = l.Action,
        FieldName = l.FieldName,
        OldValue = l.OldValue,
        NewValue = l.NewValue,
        Comment = l.Comment,
        UserName = l.User.FullName,
        CreatedAt = l.CreatedAt,
    };

    private void AddAudit(Guid documentId, Guid? versionId, string action, string? fieldName, string? oldValue, string? newValue, string? comment, Guid userId)
    {
        Db.Set<ComplianceAuditLog>().Add(new ComplianceAuditLog
        {
            DocumentId = documentId,
            VersionId = versionId,
            Action = action,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            Comment = comment,
            UserId = userId,
        });
    }

    private static string Clean(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string[] CleanTags(IEnumerable<string>? tags) =>
        tags?.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];

    private static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var withBreaks = Regex.Replace(html, "</(p|div|li|h[1-6]|tr)>", " ", RegexOptions.IgnoreCase);
        var withoutTags = Regex.Replace(withBreaks, "<.*?>", " ");
        return Regex.Replace(WebUtility.HtmlDecode(withoutTags), "\\s+", " ").Trim();
    }

    private static DateOnly CalculateNextReviewDate(DateOnly reviewedDate, string cadence) =>
        cadence.Trim().ToLowerInvariant() switch
        {
            "quarterly" => reviewedDate.AddMonths(3),
            "semiannual" or "semi-annual" => reviewedDate.AddMonths(6),
            "biennial" => reviewedDate.AddYears(2),
            "manual" => reviewedDate,
            _ => reviewedDate.AddYears(1),
        };

    private static IReadOnlyList<ComplianceDiffPartDto> BuildDiff(string fromText, string toText)
    {
        var left = Tokenize(fromText);
        var right = Tokenize(toText);
        var dp = new int[left.Length + 1, right.Length + 1];

        for (var i = left.Length - 1; i >= 0; i--)
        for (var j = right.Length - 1; j >= 0; j--)
            dp[i, j] = left[i] == right[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);

        var parts = new List<ComplianceDiffPartDto>();
        var x = 0;
        var y = 0;
        while (x < left.Length && y < right.Length)
        {
            if (left[x] == right[y])
            {
                PushPart(parts, left[x], "Same");
                x++;
                y++;
            }
            else if (dp[x + 1, y] >= dp[x, y + 1])
            {
                PushPart(parts, left[x], "Removed");
                x++;
            }
            else
            {
                PushPart(parts, right[y], "Added");
                y++;
            }
        }

        while (x < left.Length) PushPart(parts, left[x++], "Removed");
        while (y < right.Length) PushPart(parts, right[y++], "Added");
        return parts;
    }

    private static string[] Tokenize(string value) =>
        Regex.Matches(value, @"\S+\s*").Select(m => m.Value).ToArray();

    private static void PushPart(List<ComplianceDiffPartDto> parts, string text, string kind)
    {
        var last = parts.LastOrDefault();
        if (last?.Kind == kind)
        {
            last.Text += text;
            return;
        }

        parts.Add(new ComplianceDiffPartDto { Text = text, Kind = kind });
    }
}
