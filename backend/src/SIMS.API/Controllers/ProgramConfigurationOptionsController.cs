using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.Interfaces.Services;

namespace SIMS.API.Controllers;

/// <summary>
/// Read-only program-configuration lookup for quote/setup pickers. The admin
/// surface (Admin/ProgramConfigurationsController) requires
/// AdminUnderwritingControlsManage even for reads, which silently 403s for
/// underwriters and degrades their pickers to unscoped fallbacks.
/// </summary>
[ApiController]
[Route("api/v1/program-configurations")]
[Authorize]
public class ProgramConfigurationOptionsController : ControllerBase
{
    private readonly IProgramConfigurationService _service;

    public ProgramConfigurationOptionsController(IProgramConfigurationService service) => _service = service;

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _service.GetAsync(includeInactive, ct));
}
