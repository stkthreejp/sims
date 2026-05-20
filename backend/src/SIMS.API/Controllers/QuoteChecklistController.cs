using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Domain.Enums;
using System.Security.Claims;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/quotes/{quoteId:guid}/checklist")]
[Authorize]
public class QuoteChecklistController : ControllerBase
{
    private readonly IQuoteChecklistService _checklist;

    public QuoteChecklistController(IQuoteChecklistService checklist) => _checklist = checklist;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string CurrentUserName => User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue("name")
        ?? "Unknown";

    [HttpGet]
    public async Task<IActionResult> GetForQuote(Guid quoteId, [FromQuery] UnderwritingControlStage[]? stages)
    {
        var result = await _checklist.GetForQuoteAsync(quoteId, stages);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpPatch("{itemId:guid}/toggle")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> Toggle(Guid quoteId, Guid itemId, [FromBody] QuoteChecklistToggleDto dto)
    {
        var result = await _checklist.ToggleAsync(itemId, dto.IsCompleted, CurrentUserId, CurrentUserName);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
