using IMS.Application.DTOs.Tasks;
using IMS.Application.Interfaces.Services;
using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IMS.Application.Services;

public class SystemEventService : ISystemEventService
{
    private readonly IServiceProvider _sp;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public SystemEventService(IServiceProvider sp) => _sp = sp;

    public async Task<IEnumerable<SystemEventDto>> GetAllAsync()
    {
        var events = await Db.Set<SystemEvent>()
            .Where(e => !e.IsDeleted)
            .OrderBy(e => e.EventName)
            .ToListAsync();

        return events.Select(e => new SystemEventDto
        {
            Id          = e.Id,
            EventName   = e.EventName,
            Description = e.Description,
        });
    }
}
