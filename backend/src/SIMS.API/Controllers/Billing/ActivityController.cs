using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using System.Security.Claims;

namespace SIMS.API.Controllers.Billing;

[ApiController]
[Route("api/v1/billing/activity")]
[Authorize(Roles = "Admin,Underwriter")]
public class ActivityController : ControllerBase
{
    private readonly IActivityService _svc;
    public ActivityController(IActivityService svc) => _svc = svc;

    private bool IsAdmin => User.IsInRole("Admin");

    [HttpGet]
    public async Task<IActionResult> GetActivity(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] string? sourceType,
        [FromQuery] string? postingStatus,
        CancellationToken ct)
    {
        var filter = new ActivityFilterRequest(fromDate, toDate, sourceType, postingStatus);
        return Ok(await _svc.GetActivityAsync(filter, IsAdmin, ct));
    }

    [HttpGet("{transactionId:guid}")]
    public async Task<IActionResult> GetEvent(Guid transactionId, CancellationToken ct)
    {
        var evt = await _svc.GetEventAsync(transactionId, IsAdmin, ct);
        return evt != null ? Ok(evt) : NotFound();
    }
}
