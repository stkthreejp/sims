using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Legal;
using SIMS.Application.Interfaces.Services;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/legal-requirements/legiscan")]
[Authorize]
public class LegiScanController : ControllerBase
{
    private readonly ILegiScanService _service;

    public LegiScanController(ILegiScanService service)
    {
        _service = service;
    }

    private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
        ? userId
        : null;

    private string CurrentUserName => User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue("name")
        ?? "Unknown";

    [HttpGet("status")]
    public async Task<ActionResult<LegiScanStatusDto>> GetStatus(CancellationToken ct)
    {
        return Ok(await _service.GetStatusAsync(ct));
    }

    [HttpGet("bills")]
    public async Task<ActionResult<IReadOnlyList<LegiScanTrackedBillDto>>> GetBills(CancellationToken ct)
    {
        return Ok(await _service.GetTrackedBillsAsync(ct));
    }

    [HttpPost("monitor")]
    public async Task<ActionResult<IReadOnlyList<LegiScanTrackedBillDto>>> AddToMonitor(
        [FromBody] LegiScanMonitorRequest request,
        CancellationToken ct)
    {
        var result = await _service.AddToMonitorAsync(request.BillIds, request.Stance, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("monitor/{billId:int}")]
    public async Task<IActionResult> RemoveFromMonitor(int billId, CancellationToken ct)
    {
        var result = await _service.RemoveFromMonitorAsync(billId, ct);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("sync")]
    public async Task<ActionResult<LegiScanSyncResultDto>> Sync(CancellationToken ct)
    {
        var result = await _service.SyncMonitorAsync(CurrentUserId, CurrentUserName, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
