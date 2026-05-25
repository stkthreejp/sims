using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Bordereaux;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Domain.Enums;

namespace SIMS.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/bordereaux-profiles")]
[Authorize(Policy = AppPermissions.AccountingAdmin)]
public class BordereauxProfilesController : ControllerBase
{
    private readonly IBordereauxService _service;

    public BordereauxProfilesController(IBordereauxService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] bool includeInactive = false,
        [FromQuery] Guid? programId = null,
        [FromQuery] Guid? carrierId = null,
        [FromQuery] BordereauxReportType? reportType = null,
        [FromQuery] BordereauxOutputFormat? outputFormat = null,
        CancellationToken ct = default)
        => Ok(await _service.GetProfilesAsync(includeInactive, programId, carrierId, reportType, outputFormat, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetProfileAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertBordereauxProfileRequest request, CancellationToken ct)
    {
        var result = await _service.CreateProfileAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertBordereauxProfileRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateProfileAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
