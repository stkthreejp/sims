namespace SIMS.Domain.Entities;

public class ComplianceEvidenceAttachment : BaseEntity
{
    public Guid EvidenceId { get; set; }
    public ComplianceEvidence Evidence { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string BlobPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Description { get; set; }
    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
}
