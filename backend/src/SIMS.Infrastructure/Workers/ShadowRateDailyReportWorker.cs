using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMS.Application.Interfaces.Services;

namespace SIMS.Infrastructure.Workers;

public class ShadowRateDailyReportWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ShadowRateDailyReportWorker> _logger;

    public ShadowRateDailyReportWorker(IServiceScopeFactory scopeFactory, ILogger<ShadowRateDailyReportWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Shadow rate daily report worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNext2300Utc();
            _logger.LogInformation("Shadow rate report scheduled in {Minutes} minutes.", (int)delay.TotalMinutes);
            await Task.Delay(delay, stoppingToken);

            try
            {
                await RunDailyReportAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error running shadow rate daily report.");
            }
        }
    }

    private async Task RunDailyReportAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IShadowRatingService>();

        var results = await svc.GetResultsAsync(1, ct);

        if (results.Count == 0)
        {
            _logger.LogInformation("Shadow rate daily report: no results for today.");
            return;
        }

        var outliers = results.Where(r => r.IsOutlier).ToList();

        _logger.LogInformation(
            "Shadow rate daily report: {Total} results today. Outliers (>0.5% delta): {OutlierCount}.",
            results.Count, outliers.Count);

        foreach (var o in outliers)
        {
            _logger.LogWarning(
                "Shadow outlier — Quote {QuoteNumber} | Insured: {Insured} | Shadow: {Shadow:C} | Actual: {Actual:C} | Delta: {DeltaPct:F2}%",
                o.QuoteNumber, o.InsuredName, o.ShadowPremium, o.ActualPremium, o.DeltaPct);
        }
    }

    private static TimeSpan TimeUntilNext2300Utc()
    {
        var now = DateTime.UtcNow;
        var next2300 = now.Date.AddHours(23);
        if (now >= next2300)
            next2300 = next2300.AddDays(1);
        return next2300 - now;
    }
}
