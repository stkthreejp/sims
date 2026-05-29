using Azure.Identity;
using DomainUser = SIMS.Domain.Entities.User;
using SIMS.Application.Configuration;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;

namespace SIMS.Infrastructure.Workers;

public class TaskEscalationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskEscalationWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    public TaskEscalationWorker(IServiceScopeFactory scopeFactory, ILogger<TaskEscalationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Task escalation worker started. Polling every {Interval} minutes.", Interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await RunAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in task escalation worker.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunAsync(IServiceProvider sp, CancellationToken ct)
    {
        var db          = sp.GetRequiredService<ApplicationDbContext>();
        var userManager = sp.GetRequiredService<UserManager<DomainUser>>();
        var config      = sp.GetRequiredService<IConfiguration>();

        var tenantId     = MicrosoftAuthConfiguration.GetTenantId(config);
        var clientId     = MicrosoftAuthConfiguration.GetClientId(config);
        var clientSecret = config["GraphApi:ClientSecret"]   ?? throw new InvalidOperationException("GraphApi:ClientSecret not configured.");
        var mailbox      = config["GraphApi:MailboxAddress"] ?? throw new InvalidOperationException("GraphApi:MailboxAddress not configured.");
        var frontendBase = config["AppSettings:FrontendBaseUrl"] ?? "http://localhost:5173";

        var graph = new GraphServiceClient(new ClientSecretCredential(tenantId, clientId, clientSecret));

        // Load all active escalation rules
        var rules = await db.EscalationRules
            .Where(r => r.IsActive && !r.IsDeleted)
            .ToListAsync(ct);

        if (rules.Count == 0) return;

        // Assign tier index: 0-based position within each group sorted by HoursOverdue.
        // null TaskTypeId = global (applies to any task); non-null = specific task type.
        var tieredRules = rules
            .GroupBy(r => r.TaskTypeId)
            .SelectMany(g => g.OrderBy(r => r.HoursOverdue).Select((r, i) => (Rule: r, TierIndex: i)))
            .ToList();

        var now          = DateTime.UtcNow;
        var auditEntries = new List<TaskAuditEntry>();
        var roleUserCache = new Dictionary<string, IList<DomainUser>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (rule, tierIndex) in tieredRules)
        {
            var overdueThreshold = now.AddHours(-rule.HoursOverdue);

            var matchingTasks = await db.TaskInstances
                .Include(t => t.TaskType)
                .Where(t => (t.Status == TaskInstanceStatus.Open || t.Status == TaskInstanceStatus.InProgress)
                         && t.DueDate < overdueThreshold
                         && t.EscalationLevel == tierIndex
                         && (rule.TaskTypeId == null || t.TaskTypeId == rule.TaskTypeId))
                .ToListAsync(ct);

            if (matchingTasks.Count == 0) continue;

            // Resolve notified users once per unique role
            if (!roleUserCache.TryGetValue(rule.NotifyRoleName, out var notifyUsers))
            {
                notifyUsers = await userManager.GetUsersInRoleAsync(rule.NotifyRoleName);
                roleUserCache[rule.NotifyRoleName] = notifyUsers;
            }

            if (notifyUsers == null) continue;

            foreach (var task in matchingTasks)
            {
                if (rule.IncreasePriority && task.Priority < TaskPriority.High)
                    task.Priority = task.Priority + 1;

                task.EscalationLevel++;
                task.EscalatedAt = now;
                task.UpdatedAt   = now;

                auditEntries.Add(new TaskAuditEntry
                {
                    TaskInstanceId = task.Id,
                    Action         = TaskAuditAction.Escalated,
                    NewValue       = task.EscalationLevel.ToString(),
                    Notes          = $"Rule '{rule.NotifyRoleName}' tier {tierIndex} ({rule.HoursOverdue}h overdue).",
                    Timestamp      = now,
                });

                _logger.LogInformation(
                    "Escalated task {TaskId} to level {Level} (rule {RuleId}, tier {Tier}).",
                    task.Id, task.EscalationLevel, rule.Id, tierIndex);
            }

            // Send one email per notified user listing all affected tasks
            if (notifyUsers.Count > 0)
            {
                var taskRows = string.Join("", matchingTasks.Select(t =>
                    $"<tr><td>{t.TaskType.Name}</td><td>{t.DueDate:MMM d, yyyy}</td><td>{t.Priority}</td></tr>"));

                var emailBody = $"""
                    <p>{matchingTasks.Count} task{(matchingTasks.Count == 1 ? " has" : "s have")} been escalated
                    to <strong>{rule.NotifyRoleName}</strong>
                    (tier {tierIndex + 1} — {rule.HoursOverdue}h overdue):</p>
                    <table border="1" cellpadding="4" cellspacing="0" style="border-collapse:collapse;width:100%">
                      <thead><tr><th>Task</th><th>Due Date</th><th>Priority</th></tr></thead>
                      <tbody>{taskRows}</tbody>
                    </table>
                    <p>Please review and take action.</p>
                    """;

                var subject = $"[SIMS] Escalation: {matchingTasks.Count} task{(matchingTasks.Count == 1 ? "" : "s")} overdue {rule.HoursOverdue}h+";

                foreach (var user in notifyUsers)
                {
                    if (string.IsNullOrEmpty(user.Email)) continue;
                    await TrySendEmailAsync(graph, mailbox, user.Email, user.FullName, subject, emailBody, ct);
                }
            }
        }

        if (auditEntries.Count > 0)
        {
            db.TaskAuditEntries.AddRange(auditEntries);
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task TrySendEmailAsync(
        GraphServiceClient graph, string mailbox,
        string toEmail, string toName, string subject, string htmlBody,
        CancellationToken ct)
    {
        try
        {
            await graph.Users[mailbox].SendMail.PostAsync(new SendMailPostRequestBody
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
            }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send escalation email to {Email}", toEmail);
        }
    }
}
