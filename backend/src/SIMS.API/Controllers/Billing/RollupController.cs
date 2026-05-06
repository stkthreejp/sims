using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using System.Security.Claims;

namespace SIMS.API.Controllers.Billing;

[ApiController]
[Route("api/v1/billing/rollups")]
[Authorize(Policy = AppPermissions.AccountingManage)]
public class RollupController : ControllerBase
{
    private readonly IRollupService _svc;
    public RollupController(IRollupService svc) => _svc = svc;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetRollups(CancellationToken ct)
        => Ok(await _svc.GetRollupsAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetRollup(long id, CancellationToken ct)
    {
        var r = await _svc.GetRollupAsync(id, ct);
        return r != null ? Ok(r) : NotFound();
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.AccountingAdmin)]
    public async Task<IActionResult> TriggerRollup([FromBody] TriggerRollupRequest req, CancellationToken ct)
    {
        try
        {
            var r = await _svc.RollupPeriodAsync(req.PeriodYear, req.PeriodMonth, req.DriverType, UserId, ct);
            return Ok(r);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { ErrorCode = "ROLLUP_FAILED", ErrorMessage = ex.Message });
        }
    }

    [HttpPost("{id:long}/resync")]
    [Authorize(Policy = AppPermissions.AccountingAdmin)]
    public async Task<IActionResult> Resync(long id, CancellationToken ct)
    {
        try
        {
            return Ok(await _svc.ResyncAsync(id, UserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { ErrorCode = "RESYNC_FAILED", ErrorMessage = ex.Message });
        }
    }

    [HttpGet("{id:long}/download-url")]
    public async Task<IActionResult> GetDownloadUrl(long id, CancellationToken ct)
    {
        try
        {
            var url = await _svc.GetDownloadUrlAsync(id, ct);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { ErrorCode = "NO_EXPORT", ErrorMessage = ex.Message });
        }
    }
}
