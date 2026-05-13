using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.Interfaces.Services;
using System.Security.Claims;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/quotes/{quoteId:guid}/proposal")]
[Authorize(Policy = AppPermissions.PoliciesView)]
public class ProposalGenerationController : ControllerBase
{
    private readonly IProposalGenerationService _service;

    public ProposalGenerationController(IProposalGenerationService service) => _service = service;
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("inland-marine/html")]
    public async Task<IActionResult> GetInlandMarineHtml(Guid quoteId)
    {
        var result = await _service.GenerateInlandMarineHtmlAsync(quoteId);
        return result.IsSuccess
            ? Content(result.Value!, "text/html; charset=utf-8")
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("inland-marine/html")]
    public async Task<IActionResult> SaveInlandMarineHtml(Guid quoteId)
    {
        var result = await _service.SaveInlandMarineHtmlAsync(quoteId, CurrentUserId);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("inland-marine/pdf")]
    public async Task<IActionResult> SaveInlandMarinePdf(Guid quoteId)
    {
        var result = await _service.SaveInlandMarinePdfAsync(quoteId, CurrentUserId);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("inland-marine/send-draft")]
    public async Task<IActionResult> CreateInlandMarineSendDraft(Guid quoteId)
    {
        var result = await _service.CreateInlandMarineSendDraftAsync(quoteId, CurrentUserId);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
