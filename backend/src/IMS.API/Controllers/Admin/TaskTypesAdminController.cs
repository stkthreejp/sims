using IMS.Application.DTOs.Tasks;
using IMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMS.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/task-types")]
[Authorize(Roles = "Admin")]
public class TaskTypesAdminController : ControllerBase
{
    private readonly ITaskTypeService _svc;
    public TaskTypesAdminController(ITaskTypeService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
        => Ok(await _svc.GetAllAsync(activeOnly));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var r = await _svc.GetByIdAsync(id);
        return r.IsSuccess ? Ok(r.Value) : NotFound(new { r.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TaskTypeCreateDto dto)
    {
        var r = await _svc.CreateAsync(dto);
        if (!r.IsSuccess) return BadRequest(new { r.ErrorCode, r.ErrorMessage });
        return CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TaskTypeUpdateDto dto)
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
