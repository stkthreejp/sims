using SIMS.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SIMS.Infrastructure.Workers;

public class EmailIngestionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailIngestionWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public EmailIngestionWorker(IServiceScopeFactory scopeFactory, ILogger<EmailIngestionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email ingestion worker started. Polling every {Interval} minutes.", Interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IEmailIngestionService>();
                await service.IngestNewEmailsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in email ingestion worker.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
