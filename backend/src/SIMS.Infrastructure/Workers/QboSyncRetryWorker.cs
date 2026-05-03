using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMS.Application.Interfaces.Services;
using SIMS.Infrastructure.Data;

namespace SIMS.Infrastructure.Workers;

/// <summary>
/// Polls for PendingQboSync rows due for retry and attempts to re-export the rollup via QBO.
/// Backoff schedule: 30s, 2m, 8m, 30m, 2h, 8h (max 6 attempts).
/// </summary>
public class QboSyncRetryWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
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
    private readonly ILogger<QboSyncRetryWorker> _logger;

    public QboSyncRetryWorker(IServiceScopeFactory scopeFactory, ILogger<QboSyncRetryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("QBO sync retry worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await RunAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in QBO sync retry worker.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RunAsync(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        var pending = await db.PendingQboSyncs
            .Include(p => p.Rollup)
            .Where(p => p.TenantId == 1
                && (p.Status == "Pending" || p.Status == "Retrying")
                && (p.NextRetryAt == null || p.NextRetryAt <= now)
                && p.AttemptCount < MaxAttempts)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        var rollupService = sp.GetRequiredService<IRollupService>();

        foreach (var sync in pending)
        {
            sync.AttemptCount++;
            sync.Status = "Retrying";
            sync.UpdatedAt = now;
            await db.SaveChangesAsync(ct);

            try
            {
                await rollupService.ResyncAsync(sync.RollupId, Guid.Empty, ct);
                sync.Status = "Done";
                sync.LastError = null;
                _logger.LogInformation("QBO sync succeeded for rollup {RollupId} on attempt {Attempt}",
                    sync.RollupId, sync.AttemptCount);
            }
            catch (Exception ex)
            {
                sync.LastError = ex.Message;

                if (sync.AttemptCount >= MaxAttempts)
                {
                    sync.Status = "Failed";
                    _logger.LogError(ex, "QBO sync permanently failed for rollup {RollupId} after {MaxAttempts} attempts",
                        sync.RollupId, MaxAttempts);
                }
                else
                {
                    sync.Status = "Retrying";
                    var backoff = BackoffSchedule[Math.Min(sync.AttemptCount - 1, BackoffSchedule.Length - 1)];
                    sync.NextRetryAt = DateTime.UtcNow.Add(backoff);
                    _logger.LogWarning(ex, "QBO sync attempt {Attempt} failed for rollup {RollupId}. Next retry at {NextRetry}",
                        sync.AttemptCount, sync.RollupId, sync.NextRetryAt);
                }
            }

            sync.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
}
