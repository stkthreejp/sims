using SIMS.Application.Common;
using SIMS.Application.DTOs.Tasks;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace SIMS.Application.Services;

public class WorkflowTemplateService : IWorkflowTemplateService
{
    private readonly IServiceProvider _sp;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public WorkflowTemplateService(IServiceProvider sp) => _sp = sp;

    public async Task<IEnumerable<WorkflowTemplateListItemDto>> GetAllAsync()
    {
        var templates = await Db.Set<WorkflowTemplate>()
            .Include(t => t.TriggerEvent)
            .Include(t => t.Steps)
            .OrderBy(t => t.Name)
            .ToListAsync();

        return templates.Select(MapToListItem);
    }

    public async Task<Result<WorkflowTemplateDto>> GetByIdAsync(Guid id)
    {
        var template = await Db.Set<WorkflowTemplate>()
            .Include(t => t.TriggerEvent)
            .Include(t => t.Steps)
                .ThenInclude(s => s.TaskType)
            .FirstOrDefaultAsync(t => t.Id == id);

        return template == null
            ? Result<WorkflowTemplateDto>.Failure("NOT_FOUND", "Workflow template not found.")
            : Result<WorkflowTemplateDto>.Success(MapToDto(template));
    }

    public async Task<Result<WorkflowTemplateDto>> CreateAsync(WorkflowTemplateCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<WorkflowTemplateDto>.Failure("VALIDATION", "Name is required.");

        var eventExists = await Db.Set<SystemEvent>().AnyAsync(e => e.Id == dto.TriggerEventId);
        if (!eventExists)
            return Result<WorkflowTemplateDto>.Failure("NOT_FOUND", "Trigger event not found.");

        var template = new WorkflowTemplate
        {
            Name            = dto.Name.Trim(),
            Description     = dto.Description?.Trim(),
            IsActive        = dto.IsActive,
            TriggerEventId  = dto.TriggerEventId,
            EntityType      = dto.EntityType,
        };

        Db.Set<WorkflowTemplate>().Add(template);
        await Db.SaveChangesAsync();

        return await GetByIdAsync(template.Id);
    }

    public async Task<Result<WorkflowTemplateDto>> UpdateAsync(Guid id, WorkflowTemplateUpdateDto dto)
    {
        var template = await Db.Set<WorkflowTemplate>().FirstOrDefaultAsync(t => t.Id == id);
        if (template == null) return Result<WorkflowTemplateDto>.Failure("NOT_FOUND", "Workflow template not found.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<WorkflowTemplateDto>.Failure("VALIDATION", "Name is required.");

        var eventExists = await Db.Set<SystemEvent>().AnyAsync(e => e.Id == dto.TriggerEventId);
        if (!eventExists)
            return Result<WorkflowTemplateDto>.Failure("NOT_FOUND", "Trigger event not found.");

        template.Name           = dto.Name.Trim();
        template.Description    = dto.Description?.Trim();
        template.IsActive       = dto.IsActive;
        template.TriggerEventId = dto.TriggerEventId;
        template.EntityType     = dto.EntityType;
        template.UpdatedAt      = DateTime.UtcNow;

        await Db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var template = await Db.Set<WorkflowTemplate>().FirstOrDefaultAsync(t => t.Id == id);
        if (template == null) return Result.Failure("NOT_FOUND", "Workflow template not found.");

        var hasInstances = await Db.Set<TaskInstance>()
            .AnyAsync(ti => ti.WorkflowStep != null && ti.WorkflowStep.WorkflowTemplateId == id);
        if (hasInstances)
            return Result.Failure("HAS_INSTANCES", "Cannot delete a template that has generated task instances.");

        template.IsDeleted  = true;
        template.DeletedAt  = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<WorkflowTemplateDto>> SetStepsAsync(Guid id, List<WorkflowStepUpsertDto> steps)
    {
        var template = await Db.Set<WorkflowTemplate>()
            .Include(t => t.Steps)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (template == null) return Result<WorkflowTemplateDto>.Failure("NOT_FOUND", "Workflow template not found.");

        // Validate all referenced task types exist
        var taskTypeIds = steps.Select(s => s.TaskTypeId).Distinct().ToList();
        var validTaskTypes = await Db.Set<TaskType>().Where(t => taskTypeIds.Contains(t.Id)).Select(t => t.Id).ToListAsync();
        if (validTaskTypes.Count != taskTypeIds.Count)
            return Result<WorkflowTemplateDto>.Failure("INVALID_TASK_TYPE", "One or more task type IDs are invalid.");

        // Remove old steps that are not in the new list (by Id)
        var incomingIds = steps.Where(s => s.Id.HasValue).Select(s => s.Id!.Value).ToHashSet();
        var toRemove = template.Steps.Where(s => !incomingIds.Contains(s.Id)).ToList();
        Db.Set<WorkflowStep>().RemoveRange(toRemove);

        var now = DateTime.UtcNow;

        foreach (var dto in steps)
        {
            if (dto.Id.HasValue)
            {
                var existing = template.Steps.FirstOrDefault(s => s.Id == dto.Id.Value);
                if (existing != null)
                {
                    existing.StepOrder        = dto.StepOrder;
                    existing.TaskTypeId       = dto.TaskTypeId;
                    existing.DependsOnStepId  = dto.DependsOnStepId;
                    existing.TriggerCondition = dto.TriggerCondition?.Trim();
                    existing.UpdatedAt        = now;
                }
            }
            else
            {
                Db.Set<WorkflowStep>().Add(new WorkflowStep
                {
                    WorkflowTemplateId = id,
                    StepOrder          = dto.StepOrder,
                    TaskTypeId         = dto.TaskTypeId,
                    DependsOnStepId    = dto.DependsOnStepId,
                    TriggerCondition   = dto.TriggerCondition?.Trim(),
                });
            }
        }

        await Db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    private static WorkflowTemplateListItemDto MapToListItem(WorkflowTemplate t) => new()
    {
        Id               = t.Id,
        Name             = t.Name,
        Description      = t.Description,
        IsActive         = t.IsActive,
        TriggerEventId   = t.TriggerEventId,
        TriggerEventName = t.TriggerEvent.EventName,
        EntityType       = t.EntityType,
        StepCount        = t.Steps.Count(s => !s.IsDeleted),
        CreatedAt        = t.CreatedAt,
    };

    private static WorkflowTemplateDto MapToDto(WorkflowTemplate t) => new()
    {
        Id               = t.Id,
        Name             = t.Name,
        Description      = t.Description,
        IsActive         = t.IsActive,
        TriggerEventId   = t.TriggerEventId,
        TriggerEventName = t.TriggerEvent.EventName,
        EntityType       = t.EntityType,
        StepCount        = t.Steps.Count(s => !s.IsDeleted),
        CreatedAt        = t.CreatedAt,
        Steps            = t.Steps
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.StepOrder)
            .Select(s => new WorkflowStepDto
            {
                Id               = s.Id,
                StepOrder        = s.StepOrder,
                TaskTypeId       = s.TaskTypeId,
                TaskTypeName     = s.TaskType.Name,
                DependsOnStepId  = s.DependsOnStepId,
                TriggerCondition = s.TriggerCondition,
            }).ToList(),
    };
}
