using SIMS.Application.Common;
using SIMS.Application.DTOs.Tasks;

namespace SIMS.Application.Interfaces.Services;

public interface ITaskTypeService
{
    Task<IEnumerable<TaskTypeListItemDto>> GetAllAsync(bool activeOnly = false);
    Task<Result<TaskTypeDto>> GetByIdAsync(Guid id);
    Task<Result<TaskTypeDto>> CreateAsync(TaskTypeCreateDto dto);
    Task<Result<TaskTypeDto>> UpdateAsync(Guid id, TaskTypeUpdateDto dto);
    Task<Result> DeleteAsync(Guid id);
}
