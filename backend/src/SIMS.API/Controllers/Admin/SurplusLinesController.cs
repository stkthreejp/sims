using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.SurplusLines;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;

namespace SIMS.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/surplus-lines")]
[Authorize(Policy = AppPermissions.AdminSystemManage)]
public class SurplusLinesController : ControllerBase
{
    private readonly ISurplusLinesSetupAdminService _service;

    public SurplusLinesController(ISurplusLinesSetupAdminService service) => _service = service;

    [HttpGet("setups")]
    public async Task<IActionResult> Get([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _service.GetAsync(includeInactive, ct));

    [HttpGet("setups/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _service.GetAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("setups")]
    public async Task<IActionResult> Create([FromBody] UpsertSurplusLinesStateSetupRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("setups/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertSurplusLinesStateSetupRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("setups/{id:guid}/copy")]
    public async Task<IActionResult> Copy(Guid id, [FromBody] CopySurplusLinesStateSetupRequest request, CancellationToken ct)
    {
        var result = await _service.CopyAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
