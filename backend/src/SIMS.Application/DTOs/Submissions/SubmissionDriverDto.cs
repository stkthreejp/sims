namespace SIMS.Application.DTOs.Submissions;

public class SubmissionDriverDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public int DriverNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? LicenseNumber { get; set; }
    public string? LicenseState { get; set; }
    public DateOnly? DateHired { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SubmissionDriverCreateDto
{
    public int DriverNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? LicenseNumber { get; set; }
    public string? LicenseState { get; set; }
    public DateOnly? DateHired { get; set; }
}

public class SubmissionDriverUpdateDto : SubmissionDriverCreateDto { }
