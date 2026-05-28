using SIMS.Application.DTOs.Notes;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/quotes/{quoteId:guid}/notes")]
[Authorize]
public class NotesController : ControllerBase
{
    private readonly INoteService _noteService;

    public NotesController(INoteService noteService) => _noteService = noteService;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private UserAccessScope CurrentAccess => User.ToBusinessDataAccessScope();

    [HttpGet]
    [Authorize(Policy = AppPermissions.PoliciesView)]
    public async Task<IActionResult> GetAll(Guid quoteId)
        => Ok(await _noteService.GetByQuoteAsync(quoteId, CurrentAccess));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AppPermissions.PoliciesView)]
    public async Task<IActionResult> GetById(Guid quoteId, Guid id)
    {
        var result = await _noteService.GetByIdAsync(quoteId, id, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.NotesCreate)]
    public async Task<IActionResult> Create(Guid quoteId, [FromBody] NoteCreateDto dto)
    {
        var result = await _noteService.CreateAsync(quoteId, dto, CurrentAccess);
        if (!result.IsSuccess) return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        return CreatedAtAction(nameof(GetById), new { quoteId, id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPermissions.NotesEdit)]
    public async Task<IActionResult> Update(Guid quoteId, Guid id, [FromBody] NoteUpdateDto dto)
    {
        var result = await _noteService.UpdateAsync(quoteId, id, dto, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppPermissions.NotesDelete)]
    public async Task<IActionResult> Delete(Guid quoteId, Guid id)
    {
        var result = await _noteService.DeleteAsync(quoteId, id, CurrentAccess);
        return result.IsSuccess ? NoContent() : NotFound();
    }

    [HttpPatch("{id:guid}/pin")]
    [Authorize(Policy = AppPermissions.NotesEdit)]
    public async Task<IActionResult> TogglePin(Guid quoteId, Guid id)
    {
        var result = await _noteService.TogglePinAsync(quoteId, id, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }
}
