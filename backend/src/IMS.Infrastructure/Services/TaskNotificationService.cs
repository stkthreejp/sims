using Azure.Identity;
using IMS.Application.Interfaces.Services;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;

namespace IMS.Infrastructure.Services;

public class TaskNotificationService : ITaskNotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<TaskNotificationService> _logger;
    private readonly GraphServiceClient _graphClient;
    private readonly string _mailboxAddress;
    private readonly string _frontendBaseUrl;

    private static readonly TimeSpan WorkerInterval = TimeSpan.FromMinutes(15);

    public TaskNotificationService(
        ApplicationDbContext db,
        IConfiguration config,
        ILogger<TaskNotificationService> logger)
    {
        _db = db;
        _logger = logger;

        var tenantId     = config["MicrosoftAuth:TenantId"]     ?? throw new InvalidOperationException("MicrosoftAuth:TenantId not configured.");
        var clientId     = config["MicrosoftAuth:ClientId"]     ?? throw new InvalidOperationException("MicrosoftAuth:ClientId not configured.");
        var clientSecret = config["GraphApi:ClientSecret"]      ?? throw new InvalidOperationException("GraphApi:ClientSecret not configured.");
        _mailboxAddress  = config["GraphApi:MailboxAddress"]    ?? throw new InvalidOperationException("GraphApi:MailboxAddress not configured.");
        _frontendBaseUrl = config["AppSettings:FrontendBaseUrl"] ?? "http://localhost:5173";

        _graphClient = new GraphServiceClient(
            new ClientSecretCredential(tenantId, clientId, clientSecret));
    }

    // ── Assignment ────────────────────────────────────────────────────────────

    public async Task SendAssignmentNotificationsAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - WorkerInterval;

        var tasks = await _db.TaskInstances
            .Include(t => t.TaskType)
            .Where(t => t.CreatedAt >= cutoff
                     && t.AssignedUserId.HasValue
                     && !t.AuditEntries.Any(a => a.Action == TaskAuditAction.Assigned))
            .ToListAsync(ct);

        if (tasks.Count == 0) return;

        var userIds = tasks.Select(t => t.AssignedUserId!.Value).Distinct().ToList();
        var users   = await _db.Users.Where(u => userIds.Contains(u.Id))
                               .ToDictionaryAsync(u => u.Id, u => u, ct);

        var auditEntries = new List<TaskAuditEntry>();
        var now = DateTime.UtcNow;

        foreach (var task in tasks)
        {
            if (!users.TryGetValue(task.AssignedUserId!.Value, out var user) || string.IsNullOrEmpty(user.Email))
                continue;

            var deepLink = BuildDeepLink(task);
            var subject  = $"[SIMS] New task assigned: {task.TaskType.Name}";
            var body     = $"""
                <p>Hi {user.FirstName},</p>
                <p>You have been assigned a new task: <strong>{task.TaskType.Name}</strong></p>
                <p><strong>Due:</strong> {task.DueDate:MMM d, yyyy}</p>
                {(string.IsNullOrEmpty(deepLink) ? "" : $"<p><a href=\"{deepLink}\">View task →</a></p>")}
                """;

            await TrySendEmailAsync(user.Email, user.FullName, subject, body, ct);

            auditEntries.Add(new TaskAuditEntry
            {
                TaskInstanceId = task.Id,
                Action         = TaskAuditAction.Assigned,
                Notes          = $"Assignment email sent to {user.Email}",
                Timestamp      = now,
            });
        }

        if (auditEntries.Count > 0)
        {
            _db.TaskAuditEntries.AddRange(auditEntries);
            await _db.SaveChangesAsync(ct);
        }
    }

    // ── 24-hour reminder ──────────────────────────────────────────────────────

    public async Task SendReminderNotificationsAsync(CancellationToken ct = default)
    {
        var now      = DateTime.UtcNow;
        var windowLo = now.AddHours(23);
        var windowHi = now.AddHours(25);

        var tasks = await _db.TaskInstances
            .Include(t => t.TaskType)
            .Where(t => (t.Status == TaskInstanceStatus.Open || t.Status == TaskInstanceStatus.InProgress)
                     && t.AssignedUserId.HasValue
                     && t.DueDate >= windowLo
                     && t.DueDate <= windowHi
                     && !t.AuditEntries.Any(a => a.Action == TaskAuditAction.ReminderSent))
            .ToListAsync(ct);

        if (tasks.Count == 0) return;

        var userIds = tasks.Select(t => t.AssignedUserId!.Value).Distinct().ToList();
        var users   = await _db.Users.Where(u => userIds.Contains(u.Id))
                               .ToDictionaryAsync(u => u.Id, u => u, ct);

        var auditEntries = new List<TaskAuditEntry>();

        foreach (var task in tasks)
        {
            if (!users.TryGetValue(task.AssignedUserId!.Value, out var user) || string.IsNullOrEmpty(user.Email))
                continue;

            var deepLink = BuildDeepLink(task);
            var subject  = $"[SIMS] Reminder: \"{task.TaskType.Name}\" due tomorrow";
            var body     = $"""
                <p>Hi {user.FirstName},</p>
                <p>This is a reminder that the following task is due in approximately 24 hours:</p>
                <p><strong>{task.TaskType.Name}</strong><br/>Due: {task.DueDate:MMM d, yyyy h:mm tt} UTC</p>
                {(string.IsNullOrEmpty(deepLink) ? "" : $"<p><a href=\"{deepLink}\">View task →</a></p>")}
                """;

            await TrySendEmailAsync(user.Email, user.FullName, subject, body, ct);

            auditEntries.Add(new TaskAuditEntry
            {
                TaskInstanceId = task.Id,
                Action         = TaskAuditAction.ReminderSent,
                Notes          = $"24h reminder sent to {user.Email}",
                Timestamp      = now,
            });
        }

        if (auditEntries.Count > 0)
        {
            _db.TaskAuditEntries.AddRange(auditEntries);
            await _db.SaveChangesAsync(ct);
        }
    }

    // ── Overdue ───────────────────────────────────────────────────────────────

    public async Task SendOverdueNotificationsAsync(CancellationToken ct = default)
    {
        var now          = DateTime.UtcNow;
        var notifyWindow = now.AddHours(-24);

        var tasks = await _db.TaskInstances
            .Include(t => t.TaskType)
            .Include(t => t.AuditEntries)
            .Where(t => (t.Status == TaskInstanceStatus.Open || t.Status == TaskInstanceStatus.InProgress)
                     && t.AssignedUserId.HasValue
                     && t.DueDate < now
                     && !t.AuditEntries.Any(a => a.Action == TaskAuditAction.OverdueNotified
                                              && a.Timestamp >= notifyWindow))
            .ToListAsync(ct);

        if (tasks.Count == 0) return;

        var userIds = tasks.Select(t => t.AssignedUserId!.Value).Distinct().ToList();
        var users   = await _db.Users.Where(u => userIds.Contains(u.Id))
                               .ToDictionaryAsync(u => u.Id, u => u, ct);

        var auditEntries = new List<TaskAuditEntry>();

        foreach (var task in tasks)
        {
            if (!users.TryGetValue(task.AssignedUserId!.Value, out var user) || string.IsNullOrEmpty(user.Email))
                continue;

            var overdueDays = (int)(now - task.DueDate).TotalDays;
            var deepLink    = BuildDeepLink(task);
            var subject     = $"[SIMS] Overdue: \"{task.TaskType.Name}\"";
            var body        = $"""
                <p>Hi {user.FirstName},</p>
                <p>The following task is <strong>overdue by {overdueDays} day{(overdueDays == 1 ? "" : "s")}</strong>:</p>
                <p><strong>{task.TaskType.Name}</strong><br/>Was due: {task.DueDate:MMM d, yyyy}</p>
                {(string.IsNullOrEmpty(deepLink) ? "" : $"<p><a href=\"{deepLink}\">View task →</a></p>")}
                """;

            await TrySendEmailAsync(user.Email, user.FullName, subject, body, ct);

            auditEntries.Add(new TaskAuditEntry
            {
                TaskInstanceId = task.Id,
                Action         = TaskAuditAction.OverdueNotified,
                Notes          = $"Overdue alert sent to {user.Email}",
                Timestamp      = now,
            });
        }

        if (auditEntries.Count > 0)
        {
            _db.TaskAuditEntries.AddRange(auditEntries);
            await _db.SaveChangesAsync(ct);
        }
    }

    // ── Morning digest ────────────────────────────────────────────────────────

    public async Task SendMorningDigestAsync(CancellationToken ct = default)
    {
        // Guard: only run in the 7 AM hour
        var now = DateTime.UtcNow;
        if (now.Hour != 7) return;

        var todayUtc = now.Date;

        // Find all open tasks with an assignee
        var allOpen = await _db.TaskInstances
            .Include(t => t.TaskType)
            .Include(t => t.AuditEntries)
            .Where(t => (t.Status == TaskInstanceStatus.Open || t.Status == TaskInstanceStatus.InProgress)
                     && t.AssignedUserId.HasValue)
            .ToListAsync(ct);

        if (allOpen.Count == 0) return;

        // Group by assigned user
        var byUser = allOpen
            .GroupBy(t => t.AssignedUserId!.Value)
            .ToList();

        var userIds = byUser.Select(g => g.Key).ToList();
        var users   = await _db.Users.Where(u => userIds.Contains(u.Id))
                               .ToDictionaryAsync(u => u.Id, u => u, ct);

        var auditEntries = new List<TaskAuditEntry>();

        foreach (var group in byUser)
        {
            var userTasks = group.ToList();

            // Skip if digest already sent today for this user (any task in the group has a DigestSent today)
            if (userTasks.Any(t => t.AuditEntries.Any(a => a.Action == TaskAuditAction.DigestSent
                                                         && a.Timestamp >= todayUtc)))
                continue;

            if (!users.TryGetValue(group.Key, out var user) || string.IsNullOrEmpty(user.Email))
                continue;

            var overdue = userTasks.Where(t => t.DueDate < now).OrderBy(t => t.DueDate).ToList();
            var dueToday = userTasks.Where(t => t.DueDate.Date == todayUtc && t.DueDate >= now).OrderBy(t => t.DueDate).ToList();
            var upcoming = userTasks.Where(t => t.DueDate.Date > todayUtc).OrderBy(t => t.DueDate).ToList();

            var subject = $"[SIMS] Your task digest — {now:MMM d, yyyy}";
            var body    = BuildDigestBody(user.FirstName, overdue, dueToday, upcoming);

            await TrySendEmailAsync(user.Email, user.FullName, subject, body, ct);

            foreach (var task in userTasks)
            {
                auditEntries.Add(new TaskAuditEntry
                {
                    TaskInstanceId = task.Id,
                    Action         = TaskAuditAction.DigestSent,
                    Notes          = $"Morning digest sent to {user.Email}",
                    Timestamp      = now,
                });
            }
        }

        if (auditEntries.Count > 0)
        {
            _db.TaskAuditEntries.AddRange(auditEntries);
            await _db.SaveChangesAsync(ct);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string BuildDeepLink(TaskInstance task)
    {
        if (!string.IsNullOrEmpty(task.ReferenceUrl))
            return task.ReferenceUrl;

        return task.EntityType switch
        {
            TaskEntityType.Submission => $"{_frontendBaseUrl}/submissions/{task.EntityId}",
            TaskEntityType.Policy     => $"{_frontendBaseUrl}/policies/{task.EntityId}",
            TaskEntityType.Account    => $"{_frontendBaseUrl}/insureds/{task.EntityId}",
            _                         => string.Empty,
        };
    }

    private static string BuildDigestBody(
        string firstName,
        List<TaskInstance> overdue,
        List<TaskInstance> dueToday,
        List<TaskInstance> upcoming)
    {
        static string TaskRow(TaskInstance t) =>
            $"<tr><td>{t.TaskType.Name}</td><td>{t.Priority}</td><td>{t.DueDate:MMM d}</td></tr>";

        static string Section(string heading, List<TaskInstance> tasks, string rowColor) =>
            tasks.Count == 0 ? "" : $"""
                <h3 style="color:{rowColor}">{heading} ({tasks.Count})</h3>
                <table border="1" cellpadding="4" cellspacing="0" style="border-collapse:collapse;width:100%">
                  <thead><tr><th>Task</th><th>Priority</th><th>Due</th></tr></thead>
                  <tbody>{string.Join("", tasks.Select(TaskRow))}</tbody>
                </table>
                """;

        return $"""
            <p>Good morning, {firstName}!</p>
            <p>Here is your task summary for today:</p>
            {Section("Overdue", overdue, "#c0392b")}
            {Section("Due Today", dueToday, "#e67e22")}
            {Section("Upcoming", upcoming, "#2c3e50")}
            <p style="color:#888;font-size:12px">You are receiving this because tasks are assigned to you in SIMS.</p>
            """;
    }

    private async Task TrySendEmailAsync(
        string toEmail, string toName, string subject, string htmlBody, CancellationToken ct)
    {
        try
        {
            var request = new SendMailPostRequestBody
            {
                Message = new Message
                {
                    Subject = subject,
                    Body    = new ItemBody { ContentType = BodyType.Html, Content = htmlBody },
                    ToRecipients =
                    [
                        new Recipient { EmailAddress = new EmailAddress { Address = toEmail, Name = toName } }
                    ],
                },
                SaveToSentItems = false,
            };

            await _graphClient.Users[_mailboxAddress].SendMail.PostAsync(request, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send email to {Email} (subject: {Subject})", toEmail, subject);
        }
    }
}
