using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;

namespace SIMS.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/program-configurations")]
[Authorize(Policy = AppPermissions.AdminUnderwritingControlsManage)]
public class ProgramConfigurationsController : ControllerBase
{
    private readonly IProgramConfigurationService _service;

    public ProgramConfigurationsController(IProgramConfigurationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _service.GetAsync(includeInactive, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProgramConfigurationRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProgramConfigurationRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
