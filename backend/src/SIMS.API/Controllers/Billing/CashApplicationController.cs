using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using System.Security.Claims;

namespace SIMS.API.Controllers.Billing;

[ApiController]
[Route("api/v1/billing/cash-application")]
[Authorize(Roles = "Admin,Underwriter")]
public class CashApplicationController : ControllerBase
{
    private readonly ICashApplicationService _svc;
    public CashApplicationController(ICashApplicationService svc) => _svc = svc;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("open-invoices")]
    public async Task<IActionResult> GetOpenInvoices(CancellationToken ct)
        => Ok(await _svc.GetOpenInvoicesAsync(ct));

    [HttpPost("apply")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Apply([FromBody] ApplyCashRequest req, CancellationToken ct)
    {
        var r = await _svc.ApplyAsync(req, UserId, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return Ok(r.Value);
    }
}
