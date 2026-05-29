using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMS.Application.Configuration;
using SIMS.Application.Interfaces.Services;
using SIMS.Infrastructure.Data;

namespace SIMS.Infrastructure.Workers;

public class FmcsaScheduledJobsWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FmcsaScheduledJobsWorker> _logger;
    private readonly FmcsaJobSettings _settings;
    private DateOnly? _lastDailyRun;
    private string? _lastMonthlySmsRun;

    public FmcsaScheduledJobsWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<FmcsaJobSettings> settings,
        ILogger<FmcsaScheduledJobsWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("FMCSA scheduled jobs are disabled.");
            return;
        }

        _logger.LogInformation("FMCSA scheduled jobs worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDueJobsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error while checking FMCSA scheduled jobs.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RunDueJobsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (IsDailyDue(now))
        {
            await RunDailyJobsAsync(DateOnly.FromDateTime(now), ct);
        }

        if (IsMonthlySmsDue(now))
        {
            await RunMonthlySmsImportAsync(now.ToString("yyyy-MM"), ct);
        }
    }

    private async Task RunDailyJobsAsync(DateOnly runDate, CancellationToken ct)
    {
        if (_lastDailyRun == runDate)
            return;

        using var scope = _scopeFactory.CreateScope();
        var analytics = scope.ServiceProvider.GetRequiredService<IFmcsaSafetyAnalyticsService>();
        var allSucceeded = true;

        if (_settings.RunImportedCarrierAnalytics && !await AnalyticsBatchStartedTodayAsync(scope, "SIMS imported FMCSA carriers", ct))
        {
            _logger.LogInformation("Running scheduled FMCSA imported carrier analytics refresh.");
            var result = await analytics.RefreshImportedCarrierAnalyticsAsync(null, ct);
            if (!result.IsSuccess)
            {
                allSucceeded = false;
                _logger.LogWarning("Scheduled FMCSA imported carrier analytics refresh failed: {Code} {Message}", result.ErrorCode, result.ErrorMessage);
            }
        }

        if (_settings.RunInspectionEnrichment)
        {
            _logger.LogInformation("Running scheduled FMCSA inspection detail enrichment.");
            var enrichment = scope.ServiceProvider.GetRequiredService<IFmcsaInspectionEnrichmentService>();
            var result = await enrichment.EnrichRecentInspectionsAsync(_settings.InspectionEnrichmentMaxRows, ct);
            if (!result.IsSuccess)
            {
                allSucceeded = false;
                _logger.LogWarning("Scheduled FMCSA inspection enrichment failed: {Code} {Message}", result.ErrorCode, result.ErrorMessage);
            }
        }

        if (allSucceeded)
            _lastDailyRun = runDate;
    }

    private async Task RunMonthlySmsImportAsync(string snapshotMonth, CancellationToken ct)
    {
        if (_lastMonthlySmsRun == snapshotMonth)
            return;

        using var scope = _scopeFactory.CreateScope();
        if (await AnalyticsBatchStartedThisMonthAsync(scope, "FMCSA official SMS pass-property population", snapshotMonth, ct))
        {
            _lastMonthlySmsRun = snapshotMonth;
            return;
        }

        _logger.LogInformation("Running scheduled FMCSA official SMS peer import for {SnapshotMonth}.", snapshotMonth);
        var analytics = scope.ServiceProvider.GetRequiredService<IFmcsaSafetyAnalyticsService>();
        var result = await analytics.RefreshOfficialSmsPeerAnalyticsAsync(snapshotMonth, null, ct);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Scheduled FMCSA official SMS peer import failed: {Code} {Message}", result.ErrorCode, result.ErrorMessage);
            return;
        }

        _lastMonthlySmsRun = snapshotMonth;
    }

    private bool IsDailyDue(DateTime now)
    {
        if (!_settings.RunImportedCarrierAnalytics && !_settings.RunInspectionEnrichment)
            return false;

        var runDate = DateOnly.FromDateTime(now);
        return _lastDailyRun != runDate && now.TimeOfDay >= ParseTime(_settings.DailyRunTimeUtc);
    }

    private bool IsMonthlySmsDue(DateTime now)
    {
        if (!_settings.RunOfficialSmsPeerImport)
            return false;

        var day = Math.Clamp(_settings.MonthlySmsImportDay, 1, 28);
        var snapshotMonth = now.ToString("yyyy-MM");
        return _lastMonthlySmsRun != snapshotMonth &&
               now.Day >= day &&
               now.TimeOfDay >= ParseTime(_settings.MonthlySmsImportTimeUtc);
    }

    private static async Task<bool> AnalyticsBatchStartedTodayAsync(IServiceScope scope, string sourceName, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetService<SafetyAnalyticsDbContext>();
        if (db == null)
            return false;

        var today = DateTime.UtcNow.Date;
        return await db.FmcsaAnalyticsImportBatches
            .AsNoTracking()
            .AnyAsync(b => b.SourceName == sourceName && b.StartedAt >= today, ct);
    }

    private static async Task<bool> AnalyticsBatchStartedThisMonthAsync(IServiceScope scope, string sourceName, string snapshotMonth, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetService<SafetyAnalyticsDbContext>();
        if (db == null)
            return false;

        return await db.FmcsaAnalyticsImportBatches
            .AsNoTracking()
            .AnyAsync(b => b.SourceName == sourceName && b.SnapshotMonth == snapshotMonth, ct);
    }

    private static TimeSpan ParseTime(string value)
        => TimeSpan.TryParse(value, out var parsed) ? parsed : TimeSpan.FromHours(6);
}
