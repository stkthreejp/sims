using IMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IMS.API.Controllers;

[ApiController]
[Route("api/v1/inbound-emails")]
[Authorize]
public class InboundEmailsController : ControllerBase
{
    private readonly IInboundEmailService _inboundEmailService;

    public InboundEmailsController(IInboundEmailService inboundEmailService) =>
        _inboundEmailService = inboundEmailService;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetUnprocessed()
        => Ok(await _inboundEmailService.GetUnprocessedAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _inboundEmailService.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorMessage });
    }

    [HttpPost("{id:guid}/create-submission")]
    public async Task<IActionResult> CreateSubmission(Guid id)
    {
        var result = await _inboundEmailService.CreateSubmissionFromEmailAsync(id, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        return Ok(result.Value);
    }
}
