namespace SIMS.Application.DTOs.Intake;

/// <summary>Read model for a submission's intake job (status endpoint / UI chip).</summary>
public class IntakeJobDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public string Status { get; set; } = "";   // IntakeJobStatus name
    public string? Stage { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}
