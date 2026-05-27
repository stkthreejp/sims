using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Intermediaries;
using SIMS.Application.Interfaces.Services;

namespace SIMS.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/intermediaries")]
[Authorize(Policy = AppPermissions.AdminSystemManage)]
public class IntermediariesController : ControllerBase
{
    private readonly IIntermediaryService _service;

    public IntermediariesController(IIntermediaryService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default) =>
        Ok(await _service.GetAsync(includeInactive, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateIntermediaryRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        if (!result.IsSuccess)
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIntermediaryRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/brokerage-setups")]
    public async Task<IActionResult> CreateBrokerageSetup(Guid id, [FromBody] UpsertIntermediaryBrokerageSetupRequest request, CancellationToken ct)
    {
        var result = await _service.CreateBrokerageSetupAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{id:guid}/brokerage-setups/{setupId:guid}")]
    public async Task<IActionResult> UpdateBrokerageSetup(Guid id, Guid setupId, [FromBody] UpsertIntermediaryBrokerageSetupRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateBrokerageSetupAsync(id, setupId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}/brokerage-setups/{setupId:guid}")]
    public async Task<IActionResult> DeleteBrokerageSetup(Guid id, Guid setupId, CancellationToken ct)
    {
        var result = await _service.DeleteBrokerageSetupAsync(id, setupId, ct);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
