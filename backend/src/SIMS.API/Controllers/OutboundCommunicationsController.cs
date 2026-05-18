using System.Security.Claims;
using SIMS.Application.DTOs.OutboundCommunications;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/outbound-communications")]
[Authorize(Policy = AppPermissions.UnderwritingManage)]
public class OutboundCommunicationsController : ControllerBase
{
    private readonly IOutboundCommunicationService _service;

    public OutboundCommunicationsController(IOutboundCommunicationService service) => _service = service;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetForEntity(
        [FromQuery] OutboundCommunicationEntityType entityType,
        [FromQuery] Guid entityId,
        [FromQuery] Guid? policyTransactionId)
        => Ok(await _service.GetForEntityAsync(entityType, entityId, policyTransactionId));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> CreateDraft([FromBody] OutboundCommunicationCreateDto dto)
    {
        var result = await _service.CreateDraftAsync(dto, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] OutboundCommunicationUpdateDto dto)
    {
        var result = await _service.UpdateDraftAsync(id, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] OutboundCommunicationStatusUpdateDto dto)
    {
        var result = await _service.UpdateStatusAsync(id, dto, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/send")]
    public async Task<IActionResult> Send(Guid id)
    {
        var result = await _service.SendAsync(id, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
