using IMS.Application.Interfaces.Services;
using IMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IMS.API.Controllers;

[ApiController]
[Authorize]
public class AttachmentsController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;
    public AttachmentsController(IAttachmentService attachmentService) => _attachmentService = attachmentService;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── GET all attachments for an entity ─────────────────────────────────────
    // e.g. GET /api/v1/submissions/{id}/attachments
    //      GET /api/v1/quotes/{id}/attachments
    //      GET /api/v1/carriers/{id}/attachments
    //      GET /api/v1/agents/{id}/attachments

    [HttpGet("api/v1/submissions/{entityId:guid}/attachments")]
    public Task<IActionResult> GetSubmission(Guid entityId) => GetAll(DocumentEntityType.Submission, entityId);

    [HttpGet("api/v1/quotes/{entityId:guid}/attachments")]
    public Task<IActionResult> GetPolicy(Guid entityId) => GetAll(DocumentEntityType.Policy, entityId);

    [HttpGet("api/v1/carriers/{entityId:guid}/attachments")]
    public Task<IActionResult> GetCarrier(Guid entityId) => GetAll(DocumentEntityType.Carrier, entityId);

    [HttpGet("api/v1/agents/{entityId:guid}/attachments")]
    public Task<IActionResult> GetAgent(Guid entityId) => GetAll(DocumentEntityType.Agent, entityId);

    private async Task<IActionResult> GetAll(DocumentEntityType entityType, Guid entityId)
        => Ok(await _attachmentService.GetByEntityAsync(entityType, entityId));

    // ── Upload ────────────────────────────────────────────────────────────────

    [HttpPost("api/v1/submissions/{entityId:guid}/attachments")]
    [RequestSizeLimit(52_428_800)]
    public Task<IActionResult> UploadSubmission(Guid entityId, IFormFile file, [FromForm] DocumentType documentType, [FromForm] string? description)
        => Upload(DocumentEntityType.Submission, entityId, file, documentType, description);

    [HttpPost("api/v1/quotes/{entityId:guid}/attachments")]
    [RequestSizeLimit(52_428_800)]
    public Task<IActionResult> UploadPolicy(Guid entityId, IFormFile file, [FromForm] DocumentType documentType, [FromForm] string? description)
        => Upload(DocumentEntityType.Policy, entityId, file, documentType, description);

    [HttpPost("api/v1/carriers/{entityId:guid}/attachments")]
    [RequestSizeLimit(52_428_800)]
    public Task<IActionResult> UploadCarrier(Guid entityId, IFormFile file, [FromForm] DocumentType documentType, [FromForm] string? description)
        => Upload(DocumentEntityType.Carrier, entityId, file, documentType, description);

    [HttpPost("api/v1/agents/{entityId:guid}/attachments")]
    [RequestSizeLimit(52_428_800)]
    public Task<IActionResult> UploadAgent(Guid entityId, IFormFile file, [FromForm] DocumentType documentType, [FromForm] string? description)
        => Upload(DocumentEntityType.Agent, entityId, file, documentType, description);

    private async Task<IActionResult> Upload(DocumentEntityType entityType, Guid entityId, IFormFile file, DocumentType documentType, string? description)
    {
        var result = await _attachmentService.UploadAsync(entityType, entityId, file, documentType, description, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    // ── Download (signed URL) ─────────────────────────────────────────────────

    [HttpGet("api/v1/attachments/{id:guid}/download-url")]
    public async Task<IActionResult> GetDownloadUrl(Guid id)
    {
        var result = await _attachmentService.GetDownloadUrlAsync(id);
        return result.IsSuccess ? Ok(new { url = result.Value }) : NotFound(new { result.ErrorMessage });
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [HttpDelete("api/v1/attachments/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _attachmentService.DeleteAsync(id, CurrentUserId);
        return result.IsSuccess ? NoContent() : NotFound(new { result.ErrorMessage });
    }
}
