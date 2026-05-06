using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs;
using SIMS.Application.Interfaces.Services;
using System.Security.Claims;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/carriers/{carrierId:guid}/commissions")]
[Authorize(Policy = AppPermissions.AccountingAdmin)]
public class CarrierCommissionsController : ControllerBase
{
    private readonly ICarrierCommissionService _svc;
    public CarrierCommissionsController(ICarrierCommissionService svc) => _svc = svc;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid carrierId, CancellationToken ct)
        => Ok(await _svc.GetAllAsync(carrierId, ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid carrierId, [FromBody] CreateCarrierCommissionRequest req, CancellationToken ct)
    {
        var r = await _svc.CreateAsync(carrierId, req, UserId, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return Ok(r.Value);
    }

    [HttpPost("{id:long}/disable")]
    public async Task<IActionResult> Disable(
        long id, [FromBody] DisableCarrierCommissionRequest req, CancellationToken ct)
    {
        var r = await _svc.DisableAsync(id, req.DisabledDate, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return Ok(r.Value);
    }
}
