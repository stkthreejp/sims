using SIMS.Application.Common;
using SIMS.Application.DTOs.Tasks;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace SIMS.Application.Services;

public class HolidayCalendarService : IHolidayCalendarService
{
    private readonly IServiceProvider _sp;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public HolidayCalendarService(IServiceProvider sp) => _sp = sp;

    public async Task<IEnumerable<HolidayCalendarDto>> GetAllAsync()
    {
        var holidays = await Db.Set<HolidayCalendar>()
            .Where(h => !h.IsDeleted)
            .OrderBy(h => h.Date)
            .ToListAsync();

        return holidays.Select(Map);
    }

    public async Task<Result<HolidayCalendarDto>> CreateAsync(HolidayCalendarCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<HolidayCalendarDto>.Failure("VALIDATION", "Name is required.");

        var duplicate = await Db.Set<HolidayCalendar>()
            .AnyAsync(h => h.Date == dto.Date && !h.IsDeleted);
        if (duplicate)
            return Result<HolidayCalendarDto>.Failure("DUPLICATE", "A holiday already exists for this date.");

        var holiday = new HolidayCalendar { Date = dto.Date, Name = dto.Name.Trim() };
        Db.Set<HolidayCalendar>().Add(holiday);
        await Db.SaveChangesAsync();
        return Result<HolidayCalendarDto>.Success(Map(holiday));
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var holiday = await Db.Set<HolidayCalendar>().FirstOrDefaultAsync(h => h.Id == id);
        if (holiday == null) return Result.Failure("NOT_FOUND", "Holiday not found.");
        holiday.IsDeleted = true;
        holiday.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    private static HolidayCalendarDto Map(HolidayCalendar h) => new()
    {
        Id   = h.Id,
        Date = h.Date,
        Name = h.Name,
    };
}
