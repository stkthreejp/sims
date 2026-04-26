namespace IMS.Application.DTOs.Submissions;

public class SubmissionSupplementalDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public List<string> CommoditiesHauled { get; set; } = [];
    public List<string> TerminalLocations { get; set; } = [];
    public bool SafetyProgramInPlace { get; set; }
    public List<string> FilingsRequired { get; set; } = [];
    public bool OwnerOperator { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SubmissionSupplementalUpsertDto
{
    public List<string> CommoditiesHauled { get; set; } = [];
    public List<string> TerminalLocations { get; set; } = [];
    public bool SafetyProgramInPlace { get; set; }
    public List<string> FilingsRequired { get; set; } = [];
    public bool OwnerOperator { get; set; }
}
