using IMS.Domain.Enums;

namespace IMS.Domain.Entities;

public class Attachment : BaseEntity
{
    // ── Entity reference (only one will be set) ───────────────────────────────
    public Guid? QuoteId { get; set; }           // Policy (bound quote)
    public Guid? SubmissionId { get; set; }
    public Guid? CarrierId { get; set; }
    public Guid? AgentId { get; set; }

    public DocumentEntityType EntityType { get; set; }
    public DocumentType DocumentType { get; set; }

    // ── File metadata ─────────────────────────────────────────────────────────
    public string FileName { get; set; } = string.Empty;
    public string BlobPath { get; set; } = string.Empty;     // Azure blob name
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Description { get; set; }
    public Guid UploadedById { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────
    public Quote? Quote { get; set; }
    public Submission? Submission { get; set; }
    public Carrier? Carrier { get; set; }
    public Agent? Agent { get; set; }
    public User UploadedBy { get; set; } = null!;
}
