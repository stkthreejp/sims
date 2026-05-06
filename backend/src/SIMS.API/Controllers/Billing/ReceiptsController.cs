using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using System.Security.Claims;

namespace SIMS.API.Controllers.Billing;

[ApiController]
[Route("api/v1/billing/receipts")]
[Authorize(Policy = AppPermissions.AccountingManage)]
public class ReceiptsController : ControllerBase
{
    private readonly IReceiptsService _svc;
    public ReceiptsController(IReceiptsService svc) => _svc = svc;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetReceipts(CancellationToken ct)
        => Ok(await _svc.GetReceiptsAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetReceipt(long id, CancellationToken ct)
    {
        var r = await _svc.GetReceiptAsync(id, ct);
        return r.IsSuccess ? Ok(r.Value) : NotFound(new { r.ErrorMessage });
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.AccountingAdmin)]
    public async Task<IActionResult> CreateReceipt([FromBody] CreateReceiptRequest req, CancellationToken ct)
    {
        var r = await _svc.CreateAsync(req, UserId, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return CreatedAtAction(nameof(GetReceipt), new { id = r.Value!.Id }, r.Value);
    }
}
