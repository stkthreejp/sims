namespace SIMS.Application.Interfaces.Services;

/// <summary>Drains the intake-job queue. The intake worker calls this on each tick.</summary>
public interface IIntakeProcessingService
{
    /// <summary>
    /// Claims and processes the oldest queued <c>IntakeJob</c>, if any.
    /// Returns true if a job was processed (so the worker can keep draining), false if the queue is empty.
    /// </summary>
    Task<bool> ProcessNextAsync(CancellationToken ct = default);
}
