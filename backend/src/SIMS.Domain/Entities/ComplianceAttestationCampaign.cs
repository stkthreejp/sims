namespace SIMS.Domain.Entities;

public class ComplianceAttestationCampaign : BaseEntity
{
    public Guid DocumentId { get; set; }
    public ComplianceDocument Document { get; set; } = null!;
    public Guid VersionId { get; set; }
    public ComplianceDocumentVersion Version { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Statement { get; set; } = "I acknowledge that I have reviewed and understand this document version.";
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = "Active";
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public ICollection<ComplianceAttestationRecipient> Recipients { get; set; } = new List<ComplianceAttestationRecipient>();
}
