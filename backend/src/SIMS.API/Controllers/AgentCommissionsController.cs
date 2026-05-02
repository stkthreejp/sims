using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs;
using SIMS.Application.Interfaces.Services;
using System.Security.Claims;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/agents/{agentId:guid}/commissions")]
[Authorize(Roles = "Admin")]
public class AgentCommissionsController : ControllerBase
{
    private readonly IAgentCommissionService _svc;
    public AgentCommissionsController(IAgentCommissionService svc) => _svc = svc;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid agentId, CancellationToken ct)
        => Ok(await _svc.GetAllAsync(agentId, ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid agentId, [FromBody] CreateAgentCommissionRequest req, CancellationToken ct)
    {
        var r = await _svc.CreateAsync(agentId, req, UserId, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return Ok(r.Value);
    }

    [HttpPost("{id:long}/disable")]
    public async Task<IActionResult> Disable(
        long id, [FromBody] DisableAgentCommissionRequest req, CancellationToken ct)
    {
        var r = await _svc.DisableAsync(id, req.DisabledDate, ct);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return Ok(r.Value);
    }
}
