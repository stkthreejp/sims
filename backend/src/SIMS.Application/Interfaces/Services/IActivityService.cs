using SIMS.Application.DTOs.Accounting;

namespace SIMS.Application.Interfaces.Services;

public interface IActivityService
{
    Task<IReadOnlyList<ActivityEventDto>> GetActivityAsync(ActivityFilterRequest filter, bool isAdmin, CancellationToken ct = default);
    Task<ActivityEventDto?> GetEventAsync(Guid transactionId, bool isAdmin, CancellationToken ct = default);
}
