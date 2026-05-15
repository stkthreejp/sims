namespace SIMS.Domain.Entities;

public class ComplianceAuditLog : BaseEntity
{
    public Guid DocumentId { get; set; }
    public ComplianceDocument Document { get; set; } = null!;
    public Guid? VersionId { get; set; }
    public ComplianceDocumentVersion? Version { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Comment { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
