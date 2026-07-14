using SIMS.Application.Configuration;
using SIMS.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SIMS.Infrastructure.Workers;

/// <summary>
/// Drains the intake-job queue on a fixed interval. Honors the Intake:Enabled kill-switch
/// (checked at startup, mirroring FmcsaScheduledJobsWorker). Each tick drains all queued
/// jobs, then sleeps.
/// </summary>
public class IntakeWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IntakeWorker> _logger;
    private readonly IntakeSettings _settings;

    public IntakeWorker(IServiceScopeFactory scopeFactory, IOptions<IntakeSettings> settings, ILogger<IntakeWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Intake worker is disabled (Intake:Enabled = false).");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _settings.PollingIntervalMinutes));
        _logger.LogInformation("Intake worker started. Polling every {Minutes} min.", interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IIntakeProcessingService>();
                while (await svc.ProcessNextAsync(stoppingToken)) { }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in intake worker.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
