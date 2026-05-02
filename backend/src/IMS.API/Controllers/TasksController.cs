using IMS.Application.DTOs.Tasks;
using IMS.Application.Interfaces.Services;
using IMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IMS.API.Controllers;

[ApiController]
[Route("api/v1/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskInstanceService _tasks;

    public TasksController(ITaskInstanceService tasks) => _tasks = tasks;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>My task queue — open tasks assigned to me (including OOO delegations).</summary>
    [HttpGet("my-queue")]
    public async Task<IActionResult> GetMyQueue()
        => Ok(await _tasks.GetQueueAsync(CurrentUserId));

    /// <summary>All tasks on a specific entity (submission, policy, account).</summary>
    [HttpGet("{entityType}/{entityId:guid}")]
    public async Task<IActionResult> GetByEntity(TaskEntityType entityType, Guid entityId)
        => Ok(await _tasks.GetByEntityAsync(entityType, entityId));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _tasks.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorMessage });
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTaskStatusDto dto)
    {
        var result = await _tasks.UpdateStatusAsync(id, dto.NewStatus, CurrentUserId, dto.Notes);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPatch("{id:guid}/reassign")]
    public async Task<IActionResult> Reassign(Guid id, [FromBody] ReassignTaskDto dto)
    {
        var result = await _tasks.ReassignAsync(id, dto.NewUserId, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("{id:guid}/audit")]
    public async Task<IActionResult> GetAudit(Guid id)
    {
        var result = await _tasks.GetAuditAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorMessage });
    }
}
