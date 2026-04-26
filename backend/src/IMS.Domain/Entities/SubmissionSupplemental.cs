namespace IMS.Domain.Entities;

public class SubmissionSupplemental : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public string? CommoditiesHauled { get; set; }        // stored as JSON array string
    public string? TerminalLocations { get; set; }        // stored as JSON array string
    public bool SafetyProgramInPlace { get; set; }
    public string? FilingsRequired { get; set; }          // stored as JSON array string
    public bool OwnerOperator { get; set; }

    public Submission Submission { get; set; } = null!;
}
