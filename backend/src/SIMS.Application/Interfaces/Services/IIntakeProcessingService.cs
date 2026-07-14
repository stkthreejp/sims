using SIMS.Application.Common;
using SIMS.Application.DTOs.Intake;

namespace SIMS.Application.Interfaces.Services;

/// <summary>Drains the intake-job queue and backs the intake status / re-run endpoints.</summary>
public interface IIntakeProcessingService
{
    /// <summary>
    /// Claims and processes the oldest queued <c>IntakeJob</c>, if any.
    /// Returns true if a job was processed (so the worker can keep draining), false if the queue is empty.
    /// </summary>
    Task<bool> ProcessNextAsync(CancellationToken ct = default);

    /// <summary>Latest intake job for a submission, or null if none has been queued.</summary>
    Task<IntakeJobDto?> GetLatestForSubmissionAsync(Guid submissionId, CancellationToken ct = default);

    /// <summary>Queues a fresh intake job for a submission (manual re-run). Fails if intake is disabled.</summary>
    Task<Result<Guid>> RequeueAsync(Guid submissionId, CancellationToken ct = default);
}
