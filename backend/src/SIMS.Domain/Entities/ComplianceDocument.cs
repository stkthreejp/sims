namespace SIMS.Domain.Entities;

public class ComplianceDocument : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }
    public Guid? ApproverId { get; set; }
    public User? Approver { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? LastReviewedDate { get; set; }
    public DateOnly? NextReviewDate { get; set; }
    public string ReviewCadence { get; set; } = "Annual";
    public string[] Tags { get; set; } = [];
    public Guid? CurrentPublishedVersionId { get; set; }
    public ComplianceDocumentVersion? CurrentPublishedVersion { get; set; }
    public Guid? CurrentDraftVersionId { get; set; }
    public ComplianceDocumentVersion? CurrentDraftVersion { get; set; }
    public ICollection<ComplianceDocumentVersion> Versions { get; set; } = new List<ComplianceDocumentVersion>();
    public ICollection<ComplianceDocumentReview> Reviews { get; set; } = new List<ComplianceDocumentReview>();
    public ICollection<ComplianceEvidence> EvidenceItems { get; set; } = new List<ComplianceEvidence>();
}
