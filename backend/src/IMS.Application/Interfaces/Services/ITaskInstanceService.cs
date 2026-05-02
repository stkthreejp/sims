using IMS.Application.Common;
using IMS.Application.DTOs.Tasks;
using IMS.Domain.Enums;

namespace IMS.Application.Interfaces.Services;

public interface ITaskInstanceService
{
    Task<IEnumerable<TaskInstanceListItemDto>> GetQueueAsync(Guid userId);
    Task<IEnumerable<TaskInstanceListItemDto>> GetByEntityAsync(TaskEntityType type, Guid entityId);
    Task<Result<TaskInstanceDto>> GetByIdAsync(Guid id);
    Task<Result<TaskInstanceDto>> UpdateStatusAsync(Guid id, TaskInstanceStatus newStatus, Guid actorUserId, string? notes);
    Task<Result<TaskInstanceDto>> ReassignAsync(Guid id, Guid newUserId, Guid actorUserId);
    Task CancelByEntityAsync(TaskEntityType type, Guid entityId);
    Task<Result<IEnumerable<TaskAuditEntryDto>>> GetAuditAsync(Guid id);
}
