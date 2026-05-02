using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using System.Security.Claims;

namespace SIMS.API.Controllers.Billing;

[ApiController]
[Route("api/v1/billing/cash-distribution")]
[Authorize(Roles = "Admin,Underwriter")]
public class CashDistributionController : ControllerBase
{
    private readonly ICashDistributionService _svc;
    public CashDistributionController(ICashDistributionService svc) => _svc = svc;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(CancellationToken ct)
        => Ok(await _svc.GetPendingAsync(ct));

    [HttpGet("batches")]
    public async Task<IActionResult> GetBatches(CancellationToken ct)
        => Ok(await _svc.GetBatchesAsync(ct));

    [HttpGet("batches/{id:long}")]
    public async Task<IActionResult> GetBatch(long id, CancellationToken ct)
    {
        var r = await _svc.GetBatchAsync(id, ct);
        if (!r.IsSuccess) return NotFound(new { r.ErrorCode, r.ErrorMessage });
        return Ok(r.Value);
    }

    [HttpPost("batches")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateBatch([FromBody] CreateBatchRequest req, CancellationToken ct)
    {
        var r = await _svc.CreateBatchAsync(req, UserId, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return Ok(r.Value);
    }

    [HttpPost("batches/{id:long}/mark-executed")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MarkExecuted(
        long id, [FromBody] MarkExecutedRequest req, CancellationToken ct)
    {
        var r = await _svc.MarkExecutedAsync(id, req, UserId, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return Ok(r.Value);
    }

    [HttpGet("batches/{id:long}/pdf-url")]
    public async Task<IActionResult> GetPdfUrl(long id, CancellationToken ct)
    {
        var r = await _svc.GetBatchPdfDownloadUrlAsync(id, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return Ok(new { url = r.Value });
    }
}
