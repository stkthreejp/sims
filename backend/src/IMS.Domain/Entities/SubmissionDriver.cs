namespace IMS.Domain.Entities;

public class SubmissionDriver : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public int DriverNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? LicenseNumber { get; set; }
    public string? LicenseState { get; set; }
    public DateOnly? DateHired { get; set; }

    public Submission Submission { get; set; } = null!;
}
