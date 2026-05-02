using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using System.Security.Claims;

namespace SIMS.API.Controllers.Billing;

[ApiController]
[Route("api/v1/billing/invoices")]
[Authorize(Roles = "Admin,Underwriter")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoicingService _svc;
    public InvoicesController(IInvoicingService svc) => _svc = svc;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetInvoices(CancellationToken ct)
        => Ok(await _svc.GetInvoicesAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetInvoice(long id, CancellationToken ct)
    {
        var r = await _svc.GetInvoiceAsync(id, ct);
        return r.IsSuccess ? Ok(r.Value) : NotFound(new { r.ErrorMessage });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest req, CancellationToken ct)
    {
        var r = await _svc.BindAsync(req, UserId, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return CreatedAtAction(nameof(GetInvoice), new { id = r.Value!.Id }, r.Value);
    }
}
