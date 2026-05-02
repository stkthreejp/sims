using SIMS.Application.DTOs.Tasks;
using SIMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SIMS.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/escalation-rules")]
[Authorize(Roles = "Admin")]
public class EscalationRulesController : ControllerBase
{
    private readonly IEscalationRuleService _svc;
    public EscalationRulesController(IEscalationRuleService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _svc.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var r = await _svc.GetByIdAsync(id);
        return r.IsSuccess ? Ok(r.Value) : NotFound(new { r.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EscalationRuleCreateDto dto)
    {
        var r = await _svc.CreateAsync(dto);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] EscalationRuleUpdateDto dto)
    {
        var r = await _svc.UpdateAsync(id, dto);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { r.ErrorCode, r.ErrorMessage });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var r = await _svc.DeleteAsync(id);
        return r.IsSuccess ? NoContent() : BadRequest(new { r.ErrorCode, r.ErrorMessage });
    }
}
