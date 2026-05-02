using SIMS.Application.Common;
using SIMS.Application.DTOs.Tasks;

namespace SIMS.Application.Interfaces.Services;

public interface IHolidayCalendarService
{
    Task<IEnumerable<HolidayCalendarDto>> GetAllAsync();
    Task<Result<HolidayCalendarDto>> CreateAsync(HolidayCalendarCreateDto dto);
    Task<Result> DeleteAsync(Guid id);
}
