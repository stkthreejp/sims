using SIMS.Application.Common;
using SIMS.Application.DTOs.Insureds;
using SIMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/insureds")]
[Authorize]
public class InsuredsController : ControllerBase
{
    private readonly IInsuredService _insuredService;

    public InsuredsController(IInsuredService insuredService) => _insuredService = insuredService;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] QueryParameters query)
        => Ok(await _insuredService.GetAllAsync(query));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _insuredService.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] InsuredCreateDto dto)
    {
        var result = await _insuredService.CreateAsync(dto, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] InsuredUpdateDto dto)
    {
        var result = await _insuredService.UpdateAsync(id, dto);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _insuredService.DeleteAsync(id);
        return result.IsSuccess ? NoContent() : NotFound(new { result.ErrorMessage });
    }
}
