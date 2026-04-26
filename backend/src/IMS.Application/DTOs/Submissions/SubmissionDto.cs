using IMS.Domain.Enums;

namespace IMS.Application.DTOs.Submissions;

public class SubmissionDto
{
    public Guid Id { get; set; }
    public string SubmissionNumber { get; set; } = string.Empty;
    public Guid InsuredId { get; set; }
    public string InsuredName { get; set; } = string.Empty;
    public Guid? AgentId { get; set; }
    public string? AgentName { get; set; }
    public string? AgencyName { get; set; }
    public Guid UnderwriterId { get; set; }
    public string UnderwriterName { get; set; } = string.Empty;
    public Guid? AssistantUWId { get; set; }
    public string? AssistantUWName { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public SubmissionStatus Status { get; set; }
    public string? DescriptionOfOperations { get; set; }
    public int QuoteCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SubmissionListItemDto
{
    public Guid Id { get; set; }
    public string SubmissionNumber { get; set; } = string.Empty;
    public Guid InsuredId { get; set; }
    public string InsuredName { get; set; } = string.Empty;
    public string? AgentName { get; set; }
    public string UnderwriterName { get; set; } = string.Empty;
    public DateOnly? EffectiveDate { get; set; }
    public SubmissionStatus Status { get; set; }
    public int QuoteCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SubmissionCreateDto
{
    public Guid InsuredId { get; set; }
    public Guid? AgentId { get; set; }
    public Guid UnderwriterId { get; set; }
    public Guid? AssistantUWId { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? DescriptionOfOperations { get; set; }
}

public class SubmissionUpdateDto : SubmissionCreateDto
{
    public SubmissionStatus Status { get; set; }
}
