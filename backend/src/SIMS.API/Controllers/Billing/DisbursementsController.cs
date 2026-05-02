using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using System.Security.Claims;

namespace SIMS.API.Controllers.Billing;

[ApiController]
[Route("api/v1/billing/disbursements")]
[Authorize(Roles = "Admin,Underwriter")]
public class DisbursementsController : ControllerBase
{
    private readonly IDisbursementService _svc;
    public DisbursementsController(IDisbursementService svc) => _svc = svc;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("aging")]
    public async Task<IActionResult> GetAging(CancellationToken ct)
        => Ok(await _svc.GetAgingAsync(ct));

    [HttpGet("open-payables")]
    public async Task<IActionResult> GetOpenPayables(CancellationToken ct)
        => Ok(await _svc.GetOpenPayablesAsync(ct));

    [HttpGet]
    public async Task<IActionResult> GetDisbursements(CancellationToken ct)
        => Ok(await _svc.GetDisbursementsAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetDisbursement(long id, CancellationToken ct)
    {
        var r = await _svc.GetDisbursementAsync(id, ct);
        if (!r.IsSuccess) return NotFound(new { r.ErrorCode, r.ErrorMessage });
        return Ok(r.Value);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateDisbursementRequest req, CancellationToken ct)
    {
        var r = await _svc.CreateDisbursementAsync(req, UserId, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return Ok(r.Value);
    }

    [HttpPost("{id:long}/post")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Post(long id, CancellationToken ct)
    {
        var r = await _svc.PostDisbursementAsync(id, UserId, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return Ok(r.Value);
    }

    [HttpPost("{id:long}/void")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Void(
        long id, [FromBody] VoidDisbursementRequest req, CancellationToken ct)
    {
        var r = await _svc.VoidDisbursementAsync(id, req.Reason, UserId, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return Ok(r.Value);
    }
}
