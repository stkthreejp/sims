using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.PolicyNumbers;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Enums;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/policy-numbers")]
[Authorize(Policy = AppPermissions.UnderwritingManage)]
public class PolicyNumbersController : ControllerBase
{
    private readonly IPolicyNumberAdminService _service;

    public PolicyNumbersController(IPolicyNumberAdminService service)
    {
        _service = service;
    }

    [HttpGet("sequences")]
    public async Task<IActionResult> GetSequences([FromQuery] bool includeInactive = false)
        => Ok(await _service.GetSequencesAsync(includeInactive));

    [HttpPost("sequences")]
    public async Task<IActionResult> CreateSequence([FromBody] PolicyNumberSequenceUpsertDto dto)
    {
        var result = await _service.CreateSequenceAsync(dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("sequences/{id:guid}")]
    public async Task<IActionResult> UpdateSequence(Guid id, [FromBody] PolicyNumberSequenceUpsertDto dto)
    {
        var result = await _service.UpdateSequenceAsync(id, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("sequences/{id:guid}")]
    public async Task<IActionResult> DeleteSequence(Guid id)
    {
        var result = await _service.DeleteSequenceAsync(id);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("assignments")]
    public async Task<IActionResult> GetAssignments([FromQuery] bool includeInactive = false)
        => Ok(await _service.GetAssignmentsAsync(includeInactive));

    [HttpPost("assignments")]
    public async Task<IActionResult> CreateAssignment([FromBody] PolicyNumberAssignmentUpsertDto dto)
    {
        var result = await _service.CreateAssignmentAsync(dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("assignments/{id:guid}")]
    public async Task<IActionResult> UpdateAssignment(Guid id, [FromBody] PolicyNumberAssignmentUpsertDto dto)
    {
        var result = await _service.UpdateAssignmentAsync(id, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("assignments/{id:guid}")]
    public async Task<IActionResult> DeleteAssignment(Guid id)
    {
        var result = await _service.DeleteAssignmentAsync(id);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("preview")]
    public IActionResult Preview([FromBody] PolicyNumberPreviewRequestDto dto)
        => Ok(_service.Preview(dto));
}
