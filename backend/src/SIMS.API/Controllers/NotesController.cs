using SIMS.Application.DTOs.Notes;
using SIMS.Application.Interfaces.Services;
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

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid quoteId)
        => Ok(await _noteService.GetByQuoteAsync(quoteId));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid quoteId, Guid id)
    {
        var result = await _noteService.GetByIdAsync(quoteId, id);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid quoteId, [FromBody] NoteCreateDto dto)
    {
        var result = await _noteService.CreateAsync(quoteId, dto, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        return CreatedAtAction(nameof(GetById), new { quoteId, id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid quoteId, Guid id, [FromBody] NoteUpdateDto dto)
    {
        var result = await _noteService.UpdateAsync(quoteId, id, dto, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid quoteId, Guid id)
    {
        var result = await _noteService.DeleteAsync(quoteId, id, CurrentUserId);
        return result.IsSuccess ? NoContent() : NotFound();
    }

    [HttpPatch("{id:guid}/pin")]
    public async Task<IActionResult> TogglePin(Guid quoteId, Guid id)
    {
        var result = await _noteService.TogglePinAsync(quoteId, id, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }
}
