using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using System.Security.Claims;

namespace SIMS.API.Controllers.Billing;

[ApiController]
[Route("api/v1/billing/void")]
[Authorize(Policy = AppPermissions.AccountingManage)]
public class VoidController : ControllerBase
{
    private readonly IVoidService _svc;
    public VoidController(IVoidService svc) => _svc = svc;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole("Admin");

    [HttpPost("receipts/{id:long}")]
    public async Task<IActionResult> VoidReceipt(long id, [FromBody] VoidRequest req, CancellationToken ct)
    {
        var r = await _svc.VoidReceiptAsync(id, req.Reason, UserId, IsAdmin, ct);
        return r.Success ? Ok(r) : BadRequest(new { r.ErrorCode, r.ErrorMessage });
    }

    [HttpPost("cash-applications/{id:long}")]
    public async Task<IActionResult> VoidCashApplication(long id, [FromBody] VoidRequest req, CancellationToken ct)
    {
        var r = await _svc.VoidCashApplicationAsync(id, req.Reason, UserId, IsAdmin, ct);
        return r.Success ? Ok(r) : BadRequest(new { r.ErrorCode, r.ErrorMessage });
    }

    [HttpPost("invoices/{id:long}")]
    public async Task<IActionResult> VoidInvoice(long id, [FromBody] VoidRequest req, CancellationToken ct)
    {
        var r = await _svc.VoidInvoiceAsync(id, req.Reason, UserId, IsAdmin, ct);
        return r.Success ? Ok(r) : BadRequest(new { r.ErrorCode, r.ErrorMessage });
    }

    [HttpPost("disbursements/{id:long}")]
    public async Task<IActionResult> VoidDisbursement(long id, [FromBody] VoidRequest req, CancellationToken ct)
    {
        var r = await _svc.VoidDisbursementAsync(id, req.Reason, UserId, IsAdmin, ct);
        return r.Success ? Ok(r) : BadRequest(new { r.ErrorCode, r.ErrorMessage });
    }
}
