using IMS.Application.Common;
using IMS.Application.DTOs.Tasks;
using IMS.Application.Interfaces.Services;
using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IMS.Application.Services;

public class EscalationRuleService : IEscalationRuleService
{
    private readonly IServiceProvider _sp;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public EscalationRuleService(IServiceProvider sp) => _sp = sp;

    public async Task<IEnumerable<EscalationRuleDto>> GetAllAsync()
    {
        var rules = await Db.Set<EscalationRule>()
            .Include(r => r.TaskType)
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.HoursOverdue)
            .ToListAsync();

        return rules.Select(Map);
    }

    public async Task<Result<EscalationRuleDto>> GetByIdAsync(Guid id)
    {
        var rule = await Db.Set<EscalationRule>()
            .Include(r => r.TaskType)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

        return rule == null
            ? Result<EscalationRuleDto>.Failure("NOT_FOUND", "Escalation rule not found.")
            : Result<EscalationRuleDto>.Success(Map(rule));
    }

    public async Task<Result<EscalationRuleDto>> CreateAsync(EscalationRuleCreateDto dto)
    {
        if (dto.HoursOverdue <= 0)
            return Result<EscalationRuleDto>.Failure("VALIDATION", "HoursOverdue must be greater than 0.");
        if (string.IsNullOrWhiteSpace(dto.NotifyRoleName))
            return Result<EscalationRuleDto>.Failure("VALIDATION", "NotifyRoleName is required.");

        if (dto.TaskTypeId.HasValue)
        {
            var exists = await Db.Set<TaskType>().AnyAsync(t => t.Id == dto.TaskTypeId.Value);
            if (!exists) return Result<EscalationRuleDto>.Failure("NOT_FOUND", "Task type not found.");
        }

        var rule = new EscalationRule
        {
            TaskTypeId       = dto.TaskTypeId,
            HoursOverdue     = dto.HoursOverdue,
            NotifyRoleName   = dto.NotifyRoleName.Trim(),
            IncreasePriority = dto.IncreasePriority,
            IsActive         = dto.IsActive,
        };

        Db.Set<EscalationRule>().Add(rule);
        await Db.SaveChangesAsync();
        return await GetByIdAsync(rule.Id);
    }

    public async Task<Result<EscalationRuleDto>> UpdateAsync(Guid id, EscalationRuleUpdateDto dto)
    {
        var rule = await Db.Set<EscalationRule>().FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        if (rule == null) return Result<EscalationRuleDto>.Failure("NOT_FOUND", "Escalation rule not found.");

        if (dto.HoursOverdue <= 0)
            return Result<EscalationRuleDto>.Failure("VALIDATION", "HoursOverdue must be greater than 0.");
        if (string.IsNullOrWhiteSpace(dto.NotifyRoleName))
            return Result<EscalationRuleDto>.Failure("VALIDATION", "NotifyRoleName is required.");

        if (dto.TaskTypeId.HasValue)
        {
            var exists = await Db.Set<TaskType>().AnyAsync(t => t.Id == dto.TaskTypeId.Value);
            if (!exists) return Result<EscalationRuleDto>.Failure("NOT_FOUND", "Task type not found.");
        }

        rule.TaskTypeId       = dto.TaskTypeId;
        rule.HoursOverdue     = dto.HoursOverdue;
        rule.NotifyRoleName   = dto.NotifyRoleName.Trim();
        rule.IncreasePriority = dto.IncreasePriority;
        rule.IsActive         = dto.IsActive;
        rule.UpdatedAt        = DateTime.UtcNow;

        await Db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var rule = await Db.Set<EscalationRule>().FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        if (rule == null) return Result.Failure("NOT_FOUND", "Escalation rule not found.");
        rule.IsDeleted = true;
        rule.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    private static EscalationRuleDto Map(EscalationRule r) => new()
    {
        Id               = r.Id,
        TaskTypeId       = r.TaskTypeId,
        TaskTypeName     = r.TaskType?.Name,
        HoursOverdue     = r.HoursOverdue,
        NotifyRoleName   = r.NotifyRoleName,
        IncreasePriority = r.IncreasePriority,
        IsActive         = r.IsActive,
        CreatedAt        = r.CreatedAt,
    };
}
