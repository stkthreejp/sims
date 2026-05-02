using IMS.Application.Common;
using IMS.Application.DTOs.Tasks;

namespace IMS.Application.Interfaces.Services;

public interface IHolidayCalendarService
{
    Task<IEnumerable<HolidayCalendarDto>> GetAllAsync();
    Task<Result<HolidayCalendarDto>> CreateAsync(HolidayCalendarCreateDto dto);
    Task<Result> DeleteAsync(Guid id);
}
