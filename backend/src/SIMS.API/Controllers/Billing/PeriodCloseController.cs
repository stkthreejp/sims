using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using System.Security.Claims;

namespace SIMS.API.Controllers.Billing;

[ApiController]
[Route("api/v1/billing/periods")]
[Authorize(Policy = AppPermissions.AccountingManage)]
public class PeriodCloseController : ControllerBase
{
    private readonly IPeriodCloseService _svc;
    public PeriodCloseController(IPeriodCloseService svc) => _svc = svc;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetPeriods(CancellationToken ct)
        => Ok(await _svc.GetPeriodsAsync(ct));

    [HttpPost("{year:int}/{month:int}")]
    public async Task<IActionResult> GetOrCreatePeriod(int year, int month, CancellationToken ct)
        => Ok(await _svc.GetOrCreatePeriodAsync(year, month, ct));

    [HttpPost("{id:long}/evaluate")]
    public async Task<IActionResult> EvaluateChecklist(long id, CancellationToken ct)
        => Ok(await _svc.EvaluateChecklistAsync(id, ct));

    [HttpPost("{id:long}/close")]
    [Authorize(Policy = AppPermissions.AccountingAdmin)]
    public async Task<IActionResult> ClosePeriod(long id, [FromBody] ClosePeriodRequest req, CancellationToken ct)
    {
        var r = await _svc.ClosePeriodAsync(id, req.Notes, UserId, ct);
        return r.Success ? Ok(r) : BadRequest(new { r.ErrorCode, r.ErrorMessage });
    }

    [HttpPost("{id:long}/reopen")]
    [Authorize(Policy = AppPermissions.AccountingAdmin)]
    public async Task<IActionResult> ReopenPeriod(long id, [FromBody] ReopenPeriodRequest req, CancellationToken ct)
    {
        var r = await _svc.ReopenPeriodAsync(id, req.Reason, UserId, ct);
        return r.Success ? Ok(r) : BadRequest(new { r.ErrorCode, r.ErrorMessage });
    }
}
