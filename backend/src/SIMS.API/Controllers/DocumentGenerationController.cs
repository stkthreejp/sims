using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/document-generation")]
[Authorize]
public class DocumentGenerationController : ControllerBase
{
    private readonly IDocumentGenerationService _service;

    public DocumentGenerationController(IDocumentGenerationService service) => _service = service;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Generates a PDF from a template filled with entity data.
    /// Returns a signed blob URL for the generated PDF.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Generate([FromBody] GenerateDocumentRequest request)
    {
        var result = await _service.GenerateAsync(request.TemplateId, request.EntityType, request.EntityId, request.DocumentType, CurrentUserId);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}

public record GenerateDocumentRequest(Guid TemplateId, TemplateEntityType EntityType, Guid EntityId, DocumentType? DocumentType);
