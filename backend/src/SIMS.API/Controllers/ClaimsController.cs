using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Claims;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Domain.Enums;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/claims")]
[Authorize(Policy = AppPermissions.ClaimsView)]
public class ClaimsController : ControllerBase
{
    private readonly IClaimsService _service;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);

    public ClaimsController(IClaimsService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] Guid? policyId = null,
        [FromQuery] Guid? insuredId = null,
        [FromQuery] ClaimStatus? status = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken ct = default)
        => Ok(await _service.GetClaimsAsync(policyId, insuredId, status, fromDate, toDate, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetClaimAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.ClaimsManage)]
    public async Task<IActionResult> Create([FromBody] UpsertClaimRequest request, CancellationToken ct)
    {
        var result = await _service.CreateClaimAsync(request, CurrentUserId, ct);
        if (result.IsSuccess) return Ok(result.Value);
        return result.ErrorCode is "POLICY_NOT_FOUND" ? NotFound(new { result.ErrorCode, result.ErrorMessage })
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPermissions.ClaimsManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertClaimRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateClaimAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("import")]
    [Authorize(Policy = AppPermissions.ClaimsManage)]
    public async Task<IActionResult> Import([FromBody] ImportClaimsRequest request, CancellationToken ct)
    {
        var result = await _service.ImportClaimsAsync(request, CurrentUserId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("import-batches")]
    public async Task<IActionResult> GetImportBatches(CancellationToken ct)
        => Ok(await _service.GetImportBatchesAsync(ct));

    [HttpGet("loss-run")]
    public async Task<IActionResult> GetLossRun(
        [FromQuery] Guid? insuredId = null,
        [FromQuery] Guid? policyId = null,
        [FromQuery] DateOnly? asOfDate = null,
        CancellationToken ct = default)
    {
        var date = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await _service.GetLossRunAsync(insuredId, policyId, date, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
