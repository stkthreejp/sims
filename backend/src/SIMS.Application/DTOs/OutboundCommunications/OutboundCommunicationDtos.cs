using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.OutboundCommunications;

public class OutboundCommunicationListItemDto
{
    public Guid Id { get; set; }
    public OutboundCommunicationEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public Guid? PolicyTransactionId { get; set; }
    public OutboundCommunicationPurpose Purpose { get; set; }
    public string ToAddress { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public OutboundCommunicationStatus Status { get; set; }
    public string? GraphMessageId { get; set; }
    public string? GraphMessageWebLink { get; set; }
    public DateTime? SentAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public int AttachmentCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OutboundCommunicationDto
{
    public Guid Id { get; set; }
    public OutboundCommunicationEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public Guid? PolicyTransactionId { get; set; }
    public OutboundCommunicationPurpose Purpose { get; set; }
    public Guid? TemplateId { get; set; }
    public string ToAddress { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string? CcAddresses { get; set; }
    public string? BccAddresses { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public OutboundCommunicationSenderType SenderType { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public OutboundCommunicationStatus Status { get; set; }
    public string? FailureReason { get; set; }
    public string? GraphMessageId { get; set; }
    public string? GraphMessageWebLink { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? SentByName { get; set; }
    public DateTime? SentAt { get; set; }
    public List<OutboundCommunicationAttachmentDto> Attachments { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class OutboundCommunicationAttachmentDto
{
    public Guid AttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
}

public class OutboundCommunicationCreateDto
{
    public OutboundCommunicationEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public Guid? PolicyTransactionId { get; set; }
    public OutboundCommunicationPurpose Purpose { get; set; } = OutboundCommunicationPurpose.Other;
    public Guid? TemplateId { get; set; }
    public string ToAddress { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string? CcAddresses { get; set; }
    public string? BccAddresses { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public OutboundCommunicationSenderType SenderType { get; set; } = OutboundCommunicationSenderType.CurrentUser;
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public List<Guid> AttachmentIds { get; set; } = [];
}

public class OutboundCommunicationUpdateDto
{
    public Guid? PolicyTransactionId { get; set; }
    public OutboundCommunicationPurpose Purpose { get; set; } = OutboundCommunicationPurpose.Other;
    public string ToAddress { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string? CcAddresses { get; set; }
    public string? BccAddresses { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public OutboundCommunicationSenderType SenderType { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public List<Guid> AttachmentIds { get; set; } = [];
}

public class OutboundCommunicationStatusUpdateDto
{
    public OutboundCommunicationStatus Status { get; set; }
    public string? FailureReason { get; set; }
    public string? GraphMessageId { get; set; }
}
