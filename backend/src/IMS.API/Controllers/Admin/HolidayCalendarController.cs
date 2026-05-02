using IMS.Application.DTOs.Tasks;
using IMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMS.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/holiday-calendar")]
[Authorize(Roles = "Admin")]
public class HolidayCalendarController : ControllerBase
{
    private readonly IHolidayCalendarService _svc;
    public HolidayCalendarController(IHolidayCalendarService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _svc.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] HolidayCalendarCreateDto dto)
    {
        var r = await _svc.CreateAsync(dto);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { r.ErrorCode, r.ErrorMessage });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var r = await _svc.DeleteAsync(id);
        return r.IsSuccess ? NoContent() : BadRequest(new { r.ErrorCode, r.ErrorMessage });
    }
}
