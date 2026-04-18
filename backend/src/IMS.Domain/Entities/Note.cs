namespace IMS.Domain.Entities;

public class Note : BaseEntity
{
    public Guid QuoteId { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsPinned { get; set; } = false;
    public Guid CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }

    public Quote Quote { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public User? UpdatedBy { get; set; }
}
