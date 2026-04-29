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
    public async Task<IActionResult> CreateSubmission(Guid id, [FromBody] CreateSubmissionFromEmailRequest? request = null)
    {
        var result = await _inboundEmailService.CreateSubmissionFromEmailAsync(id, CurrentUserId, request?.InsuredId, request?.AttachmentIds, request?.LineOfBusiness);
        if (!result.IsSuccess) return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/re-extract")]
    public async Task<IActionResult> ReExtract(Guid id)
    {
        var result = await _inboundEmailService.ReExtractAsync(id, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        return Ok(new { extractionStatus = result.Value });
    }
}

public class CreateSubmissionFromEmailRequest
{
    public Guid? InsuredId { get; set; }
    /// <summary>If provided, only these attachment IDs are copied and extracted. Omit to include all.</summary>
    public List<Guid>? AttachmentIds { get; set; }
    /// <summary>Line of business selected by the user — used to guide Gemini extraction prompt.</summary>
    public string? LineOfBusiness { get; set; }
}
