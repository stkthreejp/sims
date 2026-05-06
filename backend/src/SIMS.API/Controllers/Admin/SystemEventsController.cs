using SIMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SIMS.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/system-events")]
[Authorize(Policy = AppPermissions.AdminSystemManage)]
public class SystemEventsController : ControllerBase
{
    private readonly ISystemEventService _svc;
    public SystemEventsController(ISystemEventService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _svc.GetAllAsync());
}
