namespace SIMS.Application.DTOs.Compliance;

public class ComplianceDocumentSummaryDto
{
    public int TotalDocuments { get; set; }
    public int ActiveDocuments { get; set; }
    public int DraftDocuments { get; set; }
    public int DueSoon { get; set; }
    public int Overdue { get; set; }
    public int PendingAttestations { get; set; }
    public int ActiveAttestationCampaigns { get; set; }
}

public class ComplianceDocumentListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public string? ApproverName { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? LastReviewedDate { get; set; }
    public DateOnly? NextReviewDate { get; set; }
    public string ReviewCadence { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
    public int? CurrentPublishedVersionNumber { get; set; }
    public int? CurrentDraftVersionNumber { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ComplianceDocumentDetailDto : ComplianceDocumentListItemDto
{
    public Guid? OwnerId { get; set; }
    public Guid? ApproverId { get; set; }
    public ComplianceDocumentVersionDto? CurrentPublishedVersion { get; set; }
    public ComplianceDocumentVersionDto? CurrentDraftVersion { get; set; }
    public IReadOnlyList<ComplianceDocumentVersionDto> Versions { get; set; } = [];
    public IReadOnlyList<ComplianceDocumentReviewDto> Reviews { get; set; } = [];
    public IReadOnlyList<ComplianceEvidenceDto> EvidenceItems { get; set; } = [];
}

public class ComplianceDocumentVersionDto
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public string PlainText { get; set; } = string.Empty;
    public string? ChangeSummary { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? ApprovedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateOnly? EffectiveDate { get; set; }
}

public class ComplianceDocumentReviewDto
{
    public Guid Id { get; set; }
    public Guid? VersionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string ReviewedByName { get; set; } = string.Empty;
    public DateTime ReviewedAt { get; set; }
    public DateOnly? NextReviewDate { get; set; }
}

public class ComplianceEvidenceDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string EvidenceType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Url { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ComplianceAttestationCampaignDto
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid VersionId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int RecipientCount { get; set; }
    public int PendingCount { get; set; }
    public int AttestedCount { get; set; }
    public int DeclinedCount { get; set; }
    public IReadOnlyList<ComplianceAttestationRecipientDto> Recipients { get; set; } = [];
}

public class ComplianceAttestationRecipientDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? AttestedAt { get; set; }
    public string? Comment { get; set; }
}

public class ComplianceAuditLogDto
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid? VersionId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Comment { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ComplianceDocumentCreateDto
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "IT";
    public string DocumentType { get; set; } = "Policy";
    public Guid? OwnerId { get; set; }
    public Guid? ApproverId { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? NextReviewDate { get; set; }
    public string ReviewCadence { get; set; } = "Annual";
    public string[] Tags { get; set; } = [];
    public string HtmlContent { get; set; } = "<p></p>";
}

public class ComplianceDocumentUpdateDto
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }
    public Guid? ApproverId { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? NextReviewDate { get; set; }
    public string ReviewCadence { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
}

public class ComplianceDraftSaveDto
{
    public string HtmlContent { get; set; } = string.Empty;
    public string? ChangeSummary { get; set; }
}

public class CompliancePublishDto
{
    public string? Notes { get; set; }
    public DateOnly? EffectiveDate { get; set; }
}

public class ComplianceReviewCreateDto
{
    public string Status { get; set; } = "Completed";
    public string? Notes { get; set; }
    public DateOnly? NextReviewDate { get; set; }
}

public class ComplianceEvidenceCreateDto
{
    public string Title { get; set; } = string.Empty;
    public string EvidenceType { get; set; } = "Note";
    public string? Description { get; set; }
    public string? Url { get; set; }
}

public class ComplianceAttestationCampaignCreateDto
{
    public Guid VersionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Statement { get; set; } = "I acknowledge that I have reviewed and understand this document version.";
    public DateOnly DueDate { get; set; }
    public Guid[] UserIds { get; set; } = [];
}

public class ComplianceAttestationSubmitDto
{
    public string Status { get; set; } = "Attested";
    public string? Comment { get; set; }
}

public class ComplianceVersionCompareDto
{
    public Guid? FromVersionId { get; set; }
    public Guid? ToVersionId { get; set; }
    public string FromTitle { get; set; } = string.Empty;
    public string ToTitle { get; set; } = string.Empty;
    public IReadOnlyList<ComplianceDiffPartDto> Parts { get; set; } = [];
}

public class ComplianceDiffPartDto
{
    public string Text { get; set; } = string.Empty;
    public string Kind { get; set; } = "Same";
}
