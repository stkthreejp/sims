using IMS.Domain.Enums;

namespace IMS.Domain.Entities;

public class Submission : BaseEntity
{
    public string SubmissionNumber { get; set; } = string.Empty;
    public Guid InsuredId { get; set; }
    public Guid? AgentId { get; set; }
    public Guid UnderwriterId { get; set; }
    public Guid? AssistantUWId { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public SubmissionStatus Status { get; set; } = SubmissionStatus.New;
    public Guid CreatedById { get; set; }

    // Navigation
    public Insured Insured { get; set; } = null!;
    public Agent? Agent { get; set; }
    public User Underwriter { get; set; } = null!;
    public User? AssistantUW { get; set; }
    public User CreatedBy { get; set; } = null!;
    public ICollection<Quote> Quotes { get; set; } = new List<Quote>();
}
