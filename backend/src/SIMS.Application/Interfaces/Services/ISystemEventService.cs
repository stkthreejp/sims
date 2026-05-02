using SIMS.Application.DTOs.Tasks;

namespace SIMS.Application.Interfaces.Services;

public interface ISystemEventService
{
    Task<IEnumerable<SystemEventDto>> GetAllAsync();
}
