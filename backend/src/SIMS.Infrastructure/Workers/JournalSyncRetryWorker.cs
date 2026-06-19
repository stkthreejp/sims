using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMS.Application.Interfaces.Services;
using SIMS.Infrastructure.Data;

namespace SIMS.Infrastructure.Workers;

/// <summary>
/// Polls for PendingJournalSync rows due for retry and re-exports the rollup via its driver.
/// Backoff schedule: 30s, 2m, 8m, 30m, 2h, 8h (max 6 attempts).
/// </summary>
public class JournalSyncRetryWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProcessingTimeout = TimeSpan.FromMinutes(15);
    private const string ProcessingStatus = "Processing";
    private static readonly TimeSpan[] BackoffSchedule =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(8),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(8),
    ];
    private const int MaxAttempts = 6;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JournalSyncRetryWorker> _logger;

    public JournalSyncRetryWorker(IServiceScopeFactory scopeFactory, ILogger<JournalSyncRetryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Journal sync retry worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await RunAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in journal sync retry worker.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RunAsync(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var processingCutoff = now.Subtract(ProcessingTimeout);

        var pendingIds = await db.PendingJournalSyncs
            .Where(p => p.TenantId == 1
                && (((p.Status == "Pending" || p.Status == "Retrying")
                        && (p.NextRetryAt == null || p.NextRetryAt <= now))
                    || (p.Status == ProcessingStatus && p.UpdatedAt <= processingCutoff))
                && p.AttemptCount < MaxAttempts)
            .OrderBy(p => p.NextRetryAt ?? p.CreatedAt)
            .ThenBy(p => p.Id)
            .Select(p => p.Id)
            .ToListAsync(ct);

        if (pendingIds.Count == 0) return;

        var rollupService = sp.GetRequiredService<IRollupService>();

        foreach (var syncId in pendingIds)
        {
            var claimed = await TryClaimAsync(db, syncId, now, processingCutoff, ct);
            if (!claimed)
                continue;

            var sync = await db.PendingJournalSyncs
                .Include(p => p.Rollup)
                .SingleAsync(p => p.Id == syncId, ct);

            try
            {
                var rollup = await rollupService.ResyncAsync(sync.RollupId, Guid.Empty, ct);
                if (string.Equals(rollup.Status, "Failed", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(rollup.ErrorMessage ?? "Journal sync failed.");
                if (!string.Equals(rollup.Status, "Exported", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(rollup.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Journal sync did not complete. Rollup status: {rollup.Status}.");

                sync.Status = "Done";
                sync.LastError = null;
                sync.NextRetryAt = null;
                _logger.LogInformation("Journal sync succeeded for rollup {RollupId} on attempt {Attempt}",
                    sync.RollupId, sync.AttemptCount);
            }
            catch (Exception ex)
            {
                sync.LastError = ex.Message;

                if (sync.AttemptCount >= MaxAttempts)
                {
                    sync.Status = "Failed";
                    _logger.LogError(ex, "Journal sync permanently failed for rollup {RollupId} after {MaxAttempts} attempts",
                        sync.RollupId, MaxAttempts);
                }
                else
                {
                    sync.Status = "Retrying";
                    var backoff = BackoffSchedule[Math.Min(sync.AttemptCount - 1, BackoffSchedule.Length - 1)];
                    sync.NextRetryAt = DateTime.UtcNow.Add(backoff);
                    _logger.LogWarning(ex, "Journal sync attempt {Attempt} failed for rollup {RollupId}. Next retry at {NextRetry}",
                        sync.AttemptCount, sync.RollupId, sync.NextRetryAt);
                }
            }

            sync.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task<bool> TryClaimAsync(ApplicationDbContext db, long syncId, DateTime now, DateTime processingCutoff, CancellationToken ct)
    {
        var claimed = await db.PendingJournalSyncs
            .Where(p => p.Id == syncId
                && p.TenantId == 1
                && (((p.Status == "Pending" || p.Status == "Retrying")
                        && (p.NextRetryAt == null || p.NextRetryAt <= now))
                    || (p.Status == ProcessingStatus && p.UpdatedAt <= processingCutoff))
                && p.AttemptCount < MaxAttempts)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Status, ProcessingStatus)
                .SetProperty(p => p.AttemptCount, p => p.AttemptCount + 1)
                .SetProperty(p => p.UpdatedAt, now), ct);

        return claimed == 1;
    }
}
