using SIMS.Application.Common;
using SIMS.Application.DTOs.Tasks;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace SIMS.Application.Services;

public class TaskTypeService : ITaskTypeService
{
    private readonly IServiceProvider _sp;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public TaskTypeService(IServiceProvider sp) => _sp = sp;

    public async Task<IEnumerable<TaskTypeListItemDto>> GetAllAsync(bool activeOnly = false)
    {
        IQueryable<TaskType> q = Db.Set<TaskType>()
            .Include(t => t.ChildTaskTypes);

        if (activeOnly) q = q.Where(t => t.IsActive);

        var types = await q.OrderBy(t => t.Name).ToListAsync();

        return types.Select(t => new TaskTypeListItemDto
        {
            Id = t.Id,
            Name = t.Name,
            DefaultPriority = t.DefaultPriority,
            IsActive = t.IsActive,
            ChildCount = t.ChildTaskTypes.Count(c => !c.IsDeleted),
        });
    }

    public async Task<Result<TaskTypeDto>> GetByIdAsync(Guid id)
    {
        var taskType = await Db.Set<TaskType>()
            .Include(t => t.ParentTaskType)
            .FirstOrDefaultAsync(t => t.Id == id);

        return taskType == null
            ? Result<TaskTypeDto>.Failure("NOT_FOUND", "Task type not found.")
            : Result<TaskTypeDto>.Success(MapToDto(taskType));
    }

    public async Task<Result<TaskTypeDto>> CreateAsync(TaskTypeCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<TaskTypeDto>.Failure("VALIDATION", "Name is required.");

        var nameTaken = await Db.Set<TaskType>()
            .AnyAsync(t => t.Name == dto.Name.Trim());
        if (nameTaken)
            return Result<TaskTypeDto>.Failure("DUPLICATE", "A task type with this name already exists.");

        if (dto.ParentTaskTypeId.HasValue)
        {
            var parentExists = await Db.Set<TaskType>()
                .AnyAsync(t => t.Id == dto.ParentTaskTypeId.Value);
            if (!parentExists)
                return Result<TaskTypeDto>.Failure("NOT_FOUND", "Parent task type not found.");
        }

        var taskType = new TaskType
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            DefaultPriority = dto.DefaultPriority,
            AssignedRoleTemplate = dto.AssignedRoleTemplate?.Trim(),
            DueDateFormula = dto.DueDateFormula?.Trim(),
            IsActive = dto.IsActive,
            ParentTaskTypeId = dto.ParentTaskTypeId,
        };

        Db.Set<TaskType>().Add(taskType);
        await Db.SaveChangesAsync();

        var created = await Db.Set<TaskType>()
            .Include(t => t.ParentTaskType)
            .FirstAsync(t => t.Id == taskType.Id);

        return Result<TaskTypeDto>.Success(MapToDto(created));
    }

    public async Task<Result<TaskTypeDto>> UpdateAsync(Guid id, TaskTypeUpdateDto dto)
    {
        var taskType = await Db.Set<TaskType>()
            .Include(t => t.ParentTaskType)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (taskType == null) return Result<TaskTypeDto>.Failure("NOT_FOUND", "Task type not found.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<TaskTypeDto>.Failure("VALIDATION", "Name is required.");

        var nameTaken = await Db.Set<TaskType>()
            .AnyAsync(t => t.Name == dto.Name.Trim() && t.Id != id);
        if (nameTaken)
            return Result<TaskTypeDto>.Failure("DUPLICATE", "A task type with this name already exists.");

        if (dto.ParentTaskTypeId.HasValue)
        {
            if (dto.ParentTaskTypeId.Value == id)
                return Result<TaskTypeDto>.Failure("VALIDATION", "A task type cannot be its own parent.");

            var parentExists = await Db.Set<TaskType>()
                .AnyAsync(t => t.Id == dto.ParentTaskTypeId.Value);
            if (!parentExists)
                return Result<TaskTypeDto>.Failure("NOT_FOUND", "Parent task type not found.");
        }

        taskType.Name = dto.Name.Trim();
        taskType.Description = dto.Description?.Trim();
        taskType.DefaultPriority = dto.DefaultPriority;
        taskType.AssignedRoleTemplate = dto.AssignedRoleTemplate?.Trim();
        taskType.DueDateFormula = dto.DueDateFormula?.Trim();
        taskType.IsActive = dto.IsActive;
        taskType.ParentTaskTypeId = dto.ParentTaskTypeId;
        taskType.UpdatedAt = DateTime.UtcNow;

        await Db.SaveChangesAsync();
        return Result<TaskTypeDto>.Success(MapToDto(taskType));
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var taskType = await Db.Set<TaskType>()
            .Include(t => t.ChildTaskTypes)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (taskType == null) return Result.Failure("NOT_FOUND", "Task type not found.");

        if (taskType.ChildTaskTypes.Any(c => !c.IsDeleted))
            return Result.Failure("HAS_CHILDREN", "Cannot delete a task type that has active child task types.");

        taskType.IsDeleted = true;
        taskType.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    private static TaskTypeDto MapToDto(TaskType t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Description = t.Description,
        DefaultPriority = t.DefaultPriority,
        AssignedRoleTemplate = t.AssignedRoleTemplate,
        DueDateFormula = t.DueDateFormula,
        IsActive = t.IsActive,
        ParentTaskTypeId = t.ParentTaskTypeId,
        ParentTaskTypeName = t.ParentTaskType?.Name,
    };
}
