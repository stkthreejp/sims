using System.ComponentModel.DataAnnotations;
using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Submissions;

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
    /// <summary>Detected/manually-set lines of business for this submission (e.g. ["CommercialAuto","GeneralLiability"]).</summary>
    public List<string> LinesOfBusiness { get; set; } = [];
    public Guid? RenewingPolicyId { get; set; }
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
    public string? AgencyName { get; set; }
    public string UnderwriterName { get; set; } = string.Empty;
    public DateOnly? EffectiveDate { get; set; }
    public SubmissionStatus Status { get; set; }
    public List<string> LinesOfBusiness { get; set; } = [];
    public int QuoteCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SubmissionCreateDto
{
    [Required]
    public Guid InsuredId { get; set; }

    public Guid? AgentId { get; set; }

    [Required]
    public Guid UnderwriterId { get; set; }

    public Guid? AssistantUWId { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }

    [MaxLength(2000)]
    public string? DescriptionOfOperations { get; set; }

    public List<string> LinesOfBusiness { get; set; } = [];
    public Guid? RenewingPolicyId { get; set; }
}

public class SubmissionUpdateDto : SubmissionCreateDto
{
    public SubmissionStatus Status { get; set; }
}
