namespace SIMS.Domain.Entities;

public class InboundEmail : BaseEntity
{
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? BodyText { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public Guid? LinkedSubmissionId { get; set; }
    public bool IsProcessed { get; set; }

    // Graph message ID to prevent re-ingestion
    public string? GraphMessageId { get; set; }

    public Submission? LinkedSubmission { get; set; }
    public ICollection<EmailAttachment> Attachments { get; set; } = new List<EmailAttachment>();
}
