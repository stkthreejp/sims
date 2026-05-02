using IMS.Application.Interfaces.Services;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IMS.Application.Services;

public class WorkflowEngineService : IWorkflowEngineService
{
    private readonly IServiceProvider _sp;
    private readonly IDueDateFormulaService _formulaService;
    private readonly ILogger<WorkflowEngineService> _logger;

    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public WorkflowEngineService(
        IServiceProvider sp,
        IDueDateFormulaService formulaService,
        ILogger<WorkflowEngineService> logger)
    {
        _sp = sp;
        _formulaService = formulaService;
        _logger = logger;
    }

    public async Task FireEventAsync(
        string eventName,
        TaskEntityType entityType,
        Guid entityId,
        Dictionary<string, object> context)
    {
        // 1. Resolve the SystemEvent (unique index on EventName)
        var systemEvent = await Db.Set<SystemEvent>()
            .FirstOrDefaultAsync(e => e.EventName == eventName);

        if (systemEvent == null)
        {
            _logger.LogDebug(
                "WorkflowEngine: no SystemEvent registered for '{EventName}' — skipping.",
                eventName);
            return;
        }

        // 2. Find all active WorkflowTemplates for this event + entity type
        var templates = await Db.Set<WorkflowTemplate>()
            .Where(t => t.TriggerEventId == systemEvent.Id
                     && t.EntityType == entityType
                     && t.IsActive)
            .Include(t => t.Steps)
                .ThenInclude(s => s.TaskType)
            .ToListAsync();

        if (templates.Count == 0)
        {
            _logger.LogDebug(
                "WorkflowEngine: no active templates for event '{EventName}' / {EntityType}.",
                eventName, entityType);
            return;
        }

        // 3. Build DateTime context for the formula evaluator
        var dateContext = BuildDateContext(context);

        var now = DateTime.UtcNow;
        var newInstances = new List<TaskInstance>();
        var auditEntries  = new List<TaskAuditEntry>();

        foreach (var template in templates)
        {
            // 4. Process only root steps (no dependency) — ordered by StepOrder
            var rootSteps = template.Steps
                .Where(s => s.DependsOnStepId == null)
                .OrderBy(s => s.StepOrder)
                .ToList();

            foreach (var step in rootSteps)
            {
                // 5. Evaluate optional TriggerCondition
                if (!EvaluateCondition(step.TriggerCondition, context))
                {
                    _logger.LogDebug(
                        "WorkflowEngine: step {StepId} condition '{Condition}' not met — skipping.",
                        step.Id, step.TriggerCondition);
                    continue;
                }

                // 6. Resolve AssignedUserId from RoleAssignmentExpression on the TaskType
                var assignedUserId = ResolveAssignedUser(step.TaskType.AssignedRoleTemplate, context);

                // 7. Evaluate DueDate formula (falls back to 7 calendar days if unset)
                var dueDate = await ResolveDueDateAsync(step.TaskType.DueDateFormula, dateContext, now);

                // 8. Create TaskInstance
                var instance = new TaskInstance
                {
                    TaskTypeId            = step.TaskTypeId,
                    WorkflowStepId        = step.Id,
                    EntityType            = entityType,
                    EntityId              = entityId,
                    AssignedUserId        = assignedUserId,
                    AssignedRoleExpression = step.TaskType.AssignedRoleTemplate,
                    Status                = TaskInstanceStatus.Open,
                    Priority              = step.TaskType.DefaultPriority,
                    DueDate               = dueDate,
                    CreatedAt             = now,
                    UpdatedAt             = now,
                };

                newInstances.Add(instance);

                // 9. Prepare immutable audit entry (attached after SaveChanges gives us the Id)
                auditEntries.Add(new TaskAuditEntry
                {
                    Action    = TaskAuditAction.Created,
                    Timestamp = now,
                    Notes     = $"Created by WorkflowEngine for event '{eventName}'",
                    // TaskInstanceId filled in below after insert
                });

                _logger.LogInformation(
                    "WorkflowEngine: creating TaskInstance for step '{StepId}' (template '{TemplateName}', entity {EntityId}).",
                    step.Id, template.Name, entityId);
            }
        }

        if (newInstances.Count == 0)
            return;

        // 10. Bulk-insert instances, then attach audit entries
        Db.Set<TaskInstance>().AddRange(newInstances);
        await Db.SaveChangesAsync();

        for (var i = 0; i < newInstances.Count; i++)
        {
            auditEntries[i].TaskInstanceId = newInstances[i].Id;
        }

        Db.Set<TaskAuditEntry>().AddRange(auditEntries);
        await Db.SaveChangesAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts DateTime-typed values from the context bag for the formula engine.
    /// DateOnly values are converted to DateTime (midnight UTC).
    /// </summary>
    private static Dictionary<string, DateTime> BuildDateContext(Dictionary<string, object> context)
    {
        var result = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in context)
        {
            switch (value)
            {
                case DateTime dt:
                    result[key] = dt;
                    break;
                case DateOnly d:
                    result[key] = d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                    break;
            }
        }
        return result;
    }

    /// <summary>
    /// Resolves the assigned user from the context bag using the role expression.
    /// The expression is expected to be a context key whose value is a Guid
    /// (e.g., "UnderwriterId", "AssistantUWId").
    /// </summary>
    private static Guid? ResolveAssignedUser(string? expression, Dictionary<string, object> context)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;

        if (context.TryGetValue(expression, out var val))
        {
            return val switch
            {
                Guid g    => g == Guid.Empty ? null : g,
                string s  => Guid.TryParse(s, out var parsed) ? parsed : null,
                _         => null
            };
        }

        // Fallback: expression might itself be a literal Guid
        return Guid.TryParse(expression, out var literal) ? literal : null;
    }

    /// <summary>
    /// Evaluates a TriggerCondition of the form "Key=Value" against the context.
    /// A null/empty condition is always true.
    /// </summary>
    private static bool EvaluateCondition(string? condition, Dictionary<string, object> context)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return true;

        var parts = condition.Split('=', 2);
        if (parts.Length != 2)
            return true; // unrecognised format — don't block

        var key   = parts[0].Trim();
        var value = parts[1].Trim();

        return context.TryGetValue(key, out var ctxVal)
            && string.Equals(ctxVal?.ToString(), value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Evaluates the DueDateFormula via IDueDateFormulaService.
    /// Falls back to now + 7 calendar days if the formula is absent or fails.
    /// </summary>
    private async Task<DateTime> ResolveDueDateAsync(
        string? formula,
        Dictionary<string, DateTime> dateContext,
        DateTime fallbackBase)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return fallbackBase.AddDays(7);

        var result = await _formulaService.EvaluateAsync(formula, dateContext);
        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "WorkflowEngine: formula '{Formula}' failed ({Code}: {Msg}) — using +7d fallback.",
                formula, result.ErrorCode, result.ErrorMessage);
            return fallbackBase.AddDays(7);
        }

        return result.Value;
    }
}
