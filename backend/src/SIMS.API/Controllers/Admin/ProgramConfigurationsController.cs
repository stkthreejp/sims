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

    [HttpGet("orphan-audit")]
    public async Task<IActionResult> OrphanAudit(CancellationToken ct = default)
        => Ok(await _service.GetOrphanAuditAsync(ct));

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

    [HttpPost("{programId:guid}/carriers")]
    public async Task<IActionResult> AddCarrier(Guid programId, [FromBody] UpsertProgramCarrierRequest request, CancellationToken ct)
    {
        var result = await _service.AddCarrierAsync(programId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{programId:guid}/carriers/{programCarrierId:guid}")]
    public async Task<IActionResult> UpdateCarrier(Guid programId, Guid programCarrierId, [FromBody] UpsertProgramCarrierRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateCarrierAsync(programId, programCarrierId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{programId:guid}/carriers/{programCarrierId:guid}/lines-of-business")]
    public async Task<IActionResult> AddLineOfBusiness(Guid programId, Guid programCarrierId, [FromBody] UpsertProgramCarrierLineOfBusinessRequest request, CancellationToken ct)
    {
        var result = await _service.AddLineOfBusinessAsync(programId, programCarrierId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{programId:guid}/carriers/{programCarrierId:guid}/lines-of-business/{programCarrierLobId:guid}")]
    public async Task<IActionResult> UpdateLineOfBusiness(Guid programId, Guid programCarrierId, Guid programCarrierLobId, [FromBody] UpsertProgramCarrierLineOfBusinessRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateLineOfBusinessAsync(programId, programCarrierId, programCarrierLobId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{programId:guid}/carriers/{programCarrierId:guid}/lines-of-business/{programCarrierLobId:guid}/states")]
    public async Task<IActionResult> AddState(Guid programId, Guid programCarrierId, Guid programCarrierLobId, [FromBody] UpsertProgramCarrierLobStateRequest request, CancellationToken ct)
    {
        var result = await _service.AddStateAsync(programId, programCarrierId, programCarrierLobId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{programId:guid}/carriers/{programCarrierId:guid}/lines-of-business/{programCarrierLobId:guid}/states/{stateId:guid}")]
    public async Task<IActionResult> UpdateState(Guid programId, Guid programCarrierId, Guid programCarrierLobId, Guid stateId, [FromBody] UpsertProgramCarrierLobStateRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateStateAsync(programId, programCarrierId, programCarrierLobId, stateId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{programId:guid}/carriers/{programCarrierId:guid}/lines-of-business/{programCarrierLobId:guid}/states/copy")]
    public async Task<IActionResult> CopyState(Guid programId, Guid programCarrierId, Guid programCarrierLobId, [FromBody] CopyProgramCarrierLobStateRequest request, CancellationToken ct)
    {
        var result = await _service.CopyStateAsync(programId, programCarrierId, programCarrierLobId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
