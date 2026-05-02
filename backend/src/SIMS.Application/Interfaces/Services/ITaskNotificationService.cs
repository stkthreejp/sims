namespace SIMS.Application.Interfaces.Services;

public interface ITaskNotificationService
{
    Task SendAssignmentNotificationsAsync(CancellationToken ct = default);
    Task SendReminderNotificationsAsync(CancellationToken ct = default);
    Task SendOverdueNotificationsAsync(CancellationToken ct = default);
    Task SendMorningDigestAsync(CancellationToken ct = default);
}
