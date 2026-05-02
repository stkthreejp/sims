using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/document-generation")]
[Authorize]
public class DocumentGenerationController : ControllerBase
{
    private readonly IDocumentGenerationService _service;

    public DocumentGenerationController(IDocumentGenerationService service) => _service = service;

    /// <summary>
    /// Generates a PDF from a template filled with entity data.
    /// Returns a signed blob URL for the generated PDF.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Generate([FromBody] GenerateDocumentRequest request)
    {
        var result = await _service.GenerateAsync(request.TemplateId, request.EntityType, request.EntityId);
        return result.IsSuccess
            ? Ok(new { url = result.Value })
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}

public record GenerateDocumentRequest(Guid TemplateId, TemplateEntityType EntityType, Guid EntityId);
