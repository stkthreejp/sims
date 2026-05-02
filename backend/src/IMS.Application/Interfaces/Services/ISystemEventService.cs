using IMS.Application.DTOs.Tasks;

namespace IMS.Application.Interfaces.Services;

public interface ISystemEventService
{
    Task<IEnumerable<SystemEventDto>> GetAllAsync();
}
