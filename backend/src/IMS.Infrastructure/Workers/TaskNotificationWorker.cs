using IMS.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IMS.Infrastructure.Workers;

public class TaskNotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskNotificationWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    public TaskNotificationWorker(IServiceScopeFactory scopeFactory, ILogger<TaskNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Task notification worker started. Polling every {Interval} minutes.", Interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope   = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<ITaskNotificationService>();

                await svc.SendAssignmentNotificationsAsync(stoppingToken);
                await svc.SendReminderNotificationsAsync(stoppingToken);
                await svc.SendOverdueNotificationsAsync(stoppingToken);
                await svc.SendMorningDigestAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in task notification worker.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
