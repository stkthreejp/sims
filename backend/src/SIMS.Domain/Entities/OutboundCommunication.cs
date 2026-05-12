using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class OutboundCommunication : BaseEntity
{
    public OutboundCommunicationEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public Guid? TemplateId { get; set; }
    public DocumentTemplate? Template { get; set; }

    public string ToAddress { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string? CcAddresses { get; set; }
    public string? BccAddresses { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public OutboundCommunicationSenderType SenderType { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public OutboundCommunicationStatus Status { get; set; } = OutboundCommunicationStatus.Draft;
    public string? FailureReason { get; set; }
    public string? GraphMessageId { get; set; }

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public Guid? SentById { get; set; }
    public User? SentBy { get; set; }
    public DateTime? SentAt { get; set; }

    public ICollection<OutboundCommunicationAttachment> Attachments { get; set; } = new List<OutboundCommunicationAttachment>();
}
