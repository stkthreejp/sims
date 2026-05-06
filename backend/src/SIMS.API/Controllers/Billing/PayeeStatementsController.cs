using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using System.Security.Claims;

namespace SIMS.API.Controllers.Billing;

[ApiController]
[Route("api/v1/billing/payee-statements")]
[Authorize(Policy = AppPermissions.AccountingManage)]
public class PayeeStatementsController : ControllerBase
{
    private readonly IPayeeStatementService _svc;
    public PayeeStatementsController(IPayeeStatementService svc) => _svc = svc;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _svc.GetAllAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var r = await _svc.GetAsync(id, ct);
        return r.IsSuccess ? Ok(r.Value) : NotFound(new { r.ErrorMessage });
    }

    [HttpPost("import")]
    [Authorize(Policy = AppPermissions.AccountingAdmin)]
    public async Task<IActionResult> Import([FromForm] ImportPayeeStatementRequest req, IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { errorMessage = "No file uploaded." });

        using var stream = file.OpenReadStream();
        var r = await _svc.ImportAsync(req, stream, file.FileName, UserId, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return CreatedAtAction(nameof(Get), new { id = r.Value!.Id }, r.Value);
    }

    [HttpPut("{statementId:long}/lines/{lineId:long}/match")]
    [Authorize(Policy = AppPermissions.AccountingAdmin)]
    public async Task<IActionResult> SetLineMatch(
        long statementId, long lineId, [FromBody] SetLineMatchRequest req, CancellationToken ct)
    {
        var r = await _svc.SetLineMatchAsync(statementId, lineId, req.InvoiceLineId, ct);
        if (!r.IsSuccess) return r.ErrorCode == "NOT_FOUND"
            ? NotFound(new { r.ErrorMessage })
            : BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return Ok(r.Value);
    }

    [HttpPost("{id:long}/post")]
    [Authorize(Policy = AppPermissions.AccountingAdmin)]
    public async Task<IActionResult> PostReconciliation(long id, CancellationToken ct)
    {
        var r = await _svc.PostReconciliationAsync(id, UserId, ct);
        if (!r.IsSuccess) return r.ErrorCode == "NOT_FOUND"
            ? NotFound(new { r.ErrorMessage })
            : BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return Ok(r.Value);
    }
}
