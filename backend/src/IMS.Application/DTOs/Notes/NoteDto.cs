namespace IMS.Application.DTOs.Notes;

public class NoteDto
{
    public Guid Id { get; set; }
    public Guid QuoteId { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public Guid CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class NoteCreateDto
{
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
}

public class NoteUpdateDto
{
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
}
