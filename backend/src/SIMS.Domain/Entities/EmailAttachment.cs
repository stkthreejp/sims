using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class EmailAttachment : BaseEntity
{
    public Guid InboundEmailId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public EmailAttachmentDocumentType DocumentType { get; set; } = EmailAttachmentDocumentType.Unknown;

    public InboundEmail InboundEmail { get; set; } = null!;
}
