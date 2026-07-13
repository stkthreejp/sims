using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

/// <summary>
/// One automated-intake processing job for a submission. Enqueued when a submission is
/// created from an inbound email (behind the intake feature flag) and drained by the
/// intake worker, which renders + analyzes the combined PDF, files the split documents,
/// runs the completeness check, and writes the account summary.
/// </summary>
public class IntakeJob : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public IntakeJobStatus Status { get; set; } = IntakeJobStatus.Queued;
    public string? Stage { get; set; }          // e.g. "Rendering","Analyzing","Splitting","Completeness","Summary"
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResultJson { get; set; }     // SubmissionAnalysis + completeness checklist snapshot

    public Submission Submission { get; set; } = null!;
}
