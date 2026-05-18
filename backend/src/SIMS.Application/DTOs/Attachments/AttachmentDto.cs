using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Attachments;

public class AttachmentDto
{
    public Guid Id { get; set; }
    public DocumentEntityType EntityType { get; set; }
    public DocumentType DocumentType { get; set; }
    public Guid? PolicyTransactionId { get; set; }
    public Guid? PolicyVersionId { get; set; }
    public int? PolicyVersionNumber { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Description { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
