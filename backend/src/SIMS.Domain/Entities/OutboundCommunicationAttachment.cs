namespace SIMS.Domain.Entities;

public class OutboundCommunicationAttachment : BaseEntity
{
    public Guid OutboundCommunicationId { get; set; }
    public OutboundCommunication OutboundCommunication { get; set; } = null!;

    public Guid AttachmentId { get; set; }
    public Attachment Attachment { get; set; } = null!;
}
