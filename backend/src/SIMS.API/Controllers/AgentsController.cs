using SIMS.Application.DTOs.Agents;
using SIMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/agents")]
[Authorize]
public class AgentsController : ControllerBase
{
    private readonly IAgentService _agentService;

    public AgentsController(IAgentService agentService) => _agentService = agentService;

    // ─── Core ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
        => Ok(await _agentService.GetAllAsync(activeOnly));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _agentService.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorMessage });
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    public async Task<IActionResult> Create([FromBody] AgentCreateDto dto)
    {
        var result = await _agentService.CreateAsync(dto);
        if (!result.IsSuccess) return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] AgentUpdateDto dto)
    {
        var result = await _agentService.UpdateAsync(id, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _agentService.DeleteAsync(id);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    // ─── Locations ────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/locations")]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    public async Task<IActionResult> AddLocation(Guid id, [FromBody] AgentLocationInputDto dto)
    {
        var result = await _agentService.AddLocationAsync(id, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{id:guid}/locations/{locationId:guid}")]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    public async Task<IActionResult> UpdateLocation(Guid id, Guid locationId, [FromBody] AgentLocationInputDto dto)
    {
        var result = await _agentService.UpdateLocationAsync(id, locationId, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}/locations/{locationId:guid}")]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    public async Task<IActionResult> DeleteLocation(Guid id, Guid locationId)
    {
        var result = await _agentService.DeleteLocationAsync(id, locationId);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    // ─── Contacts ─────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/locations/{locationId:guid}/contacts")]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    public async Task<IActionResult> AddContact(Guid id, Guid locationId, [FromBody] AgentContactInputDto dto)
    {
        var result = await _agentService.AddContactAsync(id, locationId, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{id:guid}/locations/{locationId:guid}/contacts/{contactId:guid}")]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    public async Task<IActionResult> UpdateContact(Guid id, Guid locationId, Guid contactId, [FromBody] AgentContactInputDto dto)
    {
        var result = await _agentService.UpdateContactAsync(id, locationId, contactId, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}/locations/{locationId:guid}/contacts/{contactId:guid}")]
    [Authorize(Policy = AppPermissions.AdminSystemManage)]
    public async Task<IActionResult> DeleteContact(Guid id, Guid locationId, Guid contactId)
    {
        var result = await _agentService.DeleteContactAsync(id, locationId, contactId);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
