using IMS.Application.Common;
using IMS.Application.DTOs.Tasks;
using IMS.Application.Interfaces.Services;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IMS.Application.Services;

public class TaskInstanceService : ITaskInstanceService
{
    private readonly IServiceProvider _sp;
    private readonly IWorkflowEngineService _workflowEngine;

    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public TaskInstanceService(IServiceProvider sp, IWorkflowEngineService workflowEngine)
    {
        _sp = sp;
        _workflowEngine = workflowEngine;
    }

    public async Task<IEnumerable<TaskInstanceListItemDto>> GetQueueAsync(Guid userId)
    {
        var now = DateTime.UtcNow;

        // Active delegations pointing to this user
        var delegatedFromIds = await Db.Set<UserDelegation>()
            .Where(d => d.DelegateToUserId == userId
                     && d.IsActive
                     && d.StartDate <= now
                     && d.EndDate >= now
                     && !d.IsDeleted)
            .Select(d => d.UserId)
            .ToListAsync();

        var assigneeIds = new HashSet<Guid>(delegatedFromIds) { userId };

        var tasks = await Db.Set<TaskInstance>()
            .Include(t => t.TaskType)
            .Include(t => t.WorkflowStep)
            .Where(t => t.AssignedUserId.HasValue
                     && assigneeIds.Contains(t.AssignedUserId.Value)
                     && (t.Status == TaskInstanceStatus.Open || t.Status == TaskInstanceStatus.InProgress))
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.DueDate)
            .ToListAsync();

        return tasks.Select(t => MapToListItem(t, now));
    }

    public async Task<IEnumerable<TaskInstanceListItemDto>> GetByEntityAsync(TaskEntityType type, Guid entityId)
    {
        var now = DateTime.UtcNow;
        var tasks = await Db.Set<TaskInstance>()
            .Include(t => t.TaskType)
            .Where(t => t.EntityType == type && t.EntityId == entityId)
            .OrderBy(t => t.Status)
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => t.DueDate)
            .ToListAsync();

        return tasks.Select(t => MapToListItem(t, now));
    }

    public async Task<Result<TaskInstanceDto>> GetByIdAsync(Guid id)
    {
        var task = await Db.Set<TaskInstance>()
            .Include(t => t.TaskType)
            .Include(t => t.AuditEntries)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return Result<TaskInstanceDto>.Failure("NOT_FOUND", "Task not found.");

        return Result<TaskInstanceDto>.Success(await MapToDtoAsync(task));
    }

    public async Task<Result<TaskInstanceDto>> UpdateStatusAsync(
        Guid id, TaskInstanceStatus newStatus, Guid actorUserId, string? notes)
    {
        var task = await Db.Set<TaskInstance>()
            .Include(t => t.TaskType)
            .Include(t => t.AuditEntries)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return Result<TaskInstanceDto>.Failure("NOT_FOUND", "Task not found.");

        if (task.Status == TaskInstanceStatus.Cancelled)
            return Result<TaskInstanceDto>.Failure("CANCELLED", "Cannot update a cancelled task.");

        if (task.Status == TaskInstanceStatus.Closed)
            return Result<TaskInstanceDto>.Failure("ALREADY_CLOSED", "Task is already closed.");

        var oldStatus = task.Status.ToString();
        var now = DateTime.UtcNow;

        task.Status = newStatus;
        task.UpdatedAt = now;

        var action = newStatus == TaskInstanceStatus.Closed ? TaskAuditAction.Completed : TaskAuditAction.StatusChanged;

        if (newStatus == TaskInstanceStatus.Closed)
        {
            task.CompletedAt = now;
            task.CompletedByUserId = actorUserId;
        }

        Db.Set<TaskAuditEntry>().Add(new TaskAuditEntry
        {
            TaskInstanceId = task.Id,
            UserId = actorUserId,
            Action = action,
            OldValue = oldStatus,
            NewValue = newStatus.ToString(),
            Notes = notes,
            Timestamp = now,
        });

        await Db.SaveChangesAsync();

        // Trigger dependent steps if the task was just closed
        if (newStatus == TaskInstanceStatus.Closed && task.WorkflowStepId.HasValue)
        {
            var context = await BuildEntityContextAsync(task.EntityType, task.EntityId);
            await _workflowEngine.FireStepCompletedAsync(
                task.WorkflowStepId.Value, task.EntityType, task.EntityId, context);
        }

        return Result<TaskInstanceDto>.Success(await MapToDtoAsync(task));
    }

    public async Task<Result<TaskInstanceDto>> ReassignAsync(Guid id, Guid newUserId, Guid actorUserId)
    {
        var task = await Db.Set<TaskInstance>()
            .Include(t => t.TaskType)
            .Include(t => t.AuditEntries)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return Result<TaskInstanceDto>.Failure("NOT_FOUND", "Task not found.");

        if (task.Status is TaskInstanceStatus.Closed or TaskInstanceStatus.Cancelled)
            return Result<TaskInstanceDto>.Failure("TERMINAL_STATUS", "Cannot reassign a closed or cancelled task.");

        // Check for an active delegation on the target user and redirect if present
        var now = DateTime.UtcNow;
        var delegation = await Db.Set<UserDelegation>()
            .FirstOrDefaultAsync(d => d.UserId == newUserId
                                   && d.IsActive
                                   && d.StartDate <= now
                                   && d.EndDate >= now
                                   && !d.IsDeleted);

        var resolvedUserId = delegation?.DelegateToUserId ?? newUserId;

        var oldUserId = task.AssignedUserId?.ToString() ?? "(unassigned)";
        task.AssignedUserId = resolvedUserId;
        task.UpdatedAt = now;

        Db.Set<TaskAuditEntry>().Add(new TaskAuditEntry
        {
            TaskInstanceId = task.Id,
            UserId = actorUserId,
            Action = TaskAuditAction.Reassigned,
            OldValue = oldUserId,
            NewValue = resolvedUserId.ToString(),
            Notes = delegation != null
                ? $"Redirected to delegate {resolvedUserId} (OOO delegation active for {newUserId})"
                : null,
            Timestamp = now,
        });

        await Db.SaveChangesAsync();
        return Result<TaskInstanceDto>.Success(await MapToDtoAsync(task));
    }

    public async Task CancelByEntityAsync(TaskEntityType type, Guid entityId)
    {
        var openTasks = await Db.Set<TaskInstance>()
            .Where(t => t.EntityType == type
                     && t.EntityId == entityId
                     && (t.Status == TaskInstanceStatus.Open || t.Status == TaskInstanceStatus.InProgress))
            .ToListAsync();

        if (openTasks.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var auditEntries = new List<TaskAuditEntry>(openTasks.Count);

        foreach (var task in openTasks)
        {
            task.Status = TaskInstanceStatus.Cancelled;
            task.UpdatedAt = now;
            auditEntries.Add(new TaskAuditEntry
            {
                TaskInstanceId = task.Id,
                Action = TaskAuditAction.Cancelled,
                OldValue = TaskInstanceStatus.Open.ToString(),
                NewValue = TaskInstanceStatus.Cancelled.ToString(),
                Notes = $"Auto-cancelled: entity {type}/{entityId} reached terminal state.",
                Timestamp = now,
            });
        }

        Db.Set<TaskAuditEntry>().AddRange(auditEntries);
        await Db.SaveChangesAsync();
    }

    // ── Context builder ───────────────────────────────────────────────────────

    private async Task<Dictionary<string, object>> BuildEntityContextAsync(TaskEntityType type, Guid entityId)
    {
        var ctx = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (type == TaskEntityType.Submission)
        {
            var s = await Db.Set<Submission>().FirstOrDefaultAsync(x => x.Id == entityId);
            if (s != null)
            {
                ctx["UnderwriterId"] = s.UnderwriterId;
                ctx["Status"] = s.Status.ToString();
                if (s.AssistantUWId.HasValue) ctx["AssistantUWId"] = s.AssistantUWId.Value;
                if (s.AgentId.HasValue)       ctx["AgentId"]       = s.AgentId.Value;
                if (s.EffectiveDate.HasValue)  ctx["EffectiveDate"] = s.EffectiveDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                if (s.ExpirationDate.HasValue) ctx["ExpirationDate"] = s.ExpirationDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            }
        }

        return ctx;
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static TaskInstanceListItemDto MapToListItem(TaskInstance t, DateTime now)
    {
        return new TaskInstanceListItemDto
        {
            Id              = t.Id,
            TaskTypeName    = t.TaskType.Name,
            EntityType      = t.EntityType,
            EntityId        = t.EntityId,
            AssignedUserId  = t.AssignedUserId,
            Status          = t.Status,
            Priority        = t.Priority,
            DueDate         = t.DueDate,
            IsOverdue       = t.Status != TaskInstanceStatus.Closed && t.Status != TaskInstanceStatus.Cancelled && t.DueDate < now,
            EscalationLevel = t.EscalationLevel,
            CreatedAt       = t.CreatedAt,
        };
    }

    private async Task<TaskInstanceDto> MapToDtoAsync(TaskInstance t)
    {
        var now = DateTime.UtcNow;

        // Load user display names for audit entries in one query
        var userIds = t.AuditEntries
            .Where(a => a.UserId.HasValue)
            .Select(a => a.UserId!.Value)
            .Distinct()
            .ToList();

        if (t.AssignedUserId.HasValue) userIds.Add(t.AssignedUserId.Value);
        if (t.CompletedByUserId.HasValue) userIds.Add(t.CompletedByUserId.Value);

        var users = userIds.Count > 0
            ? await Db.Set<User>()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName)
            : new Dictionary<Guid, string>();

        return new TaskInstanceDto
        {
            Id                     = t.Id,
            TaskTypeId             = t.TaskTypeId,
            TaskTypeName           = t.TaskType.Name,
            WorkflowStepId         = t.WorkflowStepId,
            EntityType             = t.EntityType,
            EntityId               = t.EntityId,
            AssignedUserId         = t.AssignedUserId,
            AssignedUserName       = t.AssignedUserId.HasValue && users.TryGetValue(t.AssignedUserId.Value, out var aName) ? aName : null,
            AssignedRoleExpression = t.AssignedRoleExpression,
            Status                 = t.Status,
            Priority               = t.Priority,
            DueDate                = t.DueDate,
            IsOverdue              = t.Status != TaskInstanceStatus.Closed && t.Status != TaskInstanceStatus.Cancelled && t.DueDate < now,
            EscalationLevel        = t.EscalationLevel,
            CompletedAt            = t.CompletedAt,
            CompletedByUserId      = t.CompletedByUserId,
            CompletedByUserName    = t.CompletedByUserId.HasValue && users.TryGetValue(t.CompletedByUserId.Value, out var cName) ? cName : null,
            ReferenceUrl           = t.ReferenceUrl,
            CreatedAt              = t.CreatedAt,
            AuditEntries = t.AuditEntries
                .OrderByDescending(a => a.Timestamp)
                .Select(a => new TaskAuditEntryDto
                {
                    Id        = a.Id,
                    UserId    = a.UserId,
                    UserName  = a.UserId.HasValue && users.TryGetValue(a.UserId.Value, out var uName) ? uName : null,
                    Action    = a.Action,
                    OldValue  = a.OldValue,
                    NewValue  = a.NewValue,
                    Notes     = a.Notes,
                    Timestamp = a.Timestamp,
                })
                .ToList(),
        };
    }
}
