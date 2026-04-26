using IMS.Domain.Enums;

namespace IMS.Application.DTOs.InboundEmails;

public class InboundEmailListItemDto
{
    public Guid Id { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public bool IsProcessed { get; set; }
    public Guid? LinkedSubmissionId { get; set; }
    public int AttachmentCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class InboundEmailDto
{
    public Guid Id { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? BodyText { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public bool IsProcessed { get; set; }
    public Guid? LinkedSubmissionId { get; set; }
    public List<EmailAttachmentDto> Attachments { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class EmailAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public EmailAttachmentDocumentType DocumentType { get; set; }
}
