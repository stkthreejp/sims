using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SIMS.API.Controllers;

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
    [Authorize(Policy = AppPermissions.PoliciesView)]
    public Task<IActionResult> GetSubmission(Guid entityId) => GetAll(DocumentEntityType.Submission, entityId);

    [HttpGet("api/v1/quotes/{entityId:guid}/attachments")]
    [Authorize(Policy = AppPermissions.PoliciesView)]
    public Task<IActionResult> GetQuote(Guid entityId) => GetAll(DocumentEntityType.Policy, entityId);

    [HttpGet("api/v1/carriers/{entityId:guid}/attachments")]
    [Authorize(Policy = AppPermissions.PoliciesView)]
    public Task<IActionResult> GetCarrier(Guid entityId) => GetAll(DocumentEntityType.Carrier, entityId);

    [HttpGet("api/v1/agents/{entityId:guid}/attachments")]
    [Authorize(Policy = AppPermissions.PoliciesView)]
    public Task<IActionResult> GetAgent(Guid entityId) => GetAll(DocumentEntityType.Agent, entityId);

    [HttpGet("api/v1/insureds/{entityId:guid}/attachments")]
    [Authorize(Policy = AppPermissions.PoliciesView)]
    public Task<IActionResult> GetInsured(Guid entityId) => GetAll(DocumentEntityType.Insured, entityId);

    private async Task<IActionResult> GetAll(DocumentEntityType entityType, Guid entityId)
        => Ok(await _attachmentService.GetByEntityAsync(entityType, entityId, CurrentUserId));

    // ── Upload ────────────────────────────────────────────────────────────────

    [HttpPost("api/v1/submissions/{entityId:guid}/attachments")]
    [Authorize(Policy = AppPermissions.AttachmentsUpload)]
    [RequestSizeLimit(52_428_800)]
    public Task<IActionResult> UploadSubmission(Guid entityId, IFormFile file, [FromForm] DocumentType documentType, [FromForm] string? description)
        => Upload(DocumentEntityType.Submission, entityId, file, documentType, description, null);

    [HttpPost("api/v1/quotes/{entityId:guid}/attachments")]
    [Authorize(Policy = AppPermissions.AttachmentsUpload)]
    [RequestSizeLimit(52_428_800)]
    public Task<IActionResult> UploadQuote(Guid entityId, IFormFile file, [FromForm] DocumentType documentType, [FromForm] string? description, [FromForm] Guid? policyTransactionId)
        => Upload(DocumentEntityType.Policy, entityId, file, documentType, description, policyTransactionId);

    [HttpPost("api/v1/carriers/{entityId:guid}/attachments")]
    [Authorize(Policy = AppPermissions.AttachmentsUpload)]
    [RequestSizeLimit(52_428_800)]
    public Task<IActionResult> UploadCarrier(Guid entityId, IFormFile file, [FromForm] DocumentType documentType, [FromForm] string? description)
        => Upload(DocumentEntityType.Carrier, entityId, file, documentType, description, null);

    [HttpPost("api/v1/agents/{entityId:guid}/attachments")]
    [Authorize(Policy = AppPermissions.AttachmentsUpload)]
    [RequestSizeLimit(52_428_800)]
    public Task<IActionResult> UploadAgent(Guid entityId, IFormFile file, [FromForm] DocumentType documentType, [FromForm] string? description)
        => Upload(DocumentEntityType.Agent, entityId, file, documentType, description, null);

    [HttpPost("api/v1/insureds/{entityId:guid}/attachments")]
    [Authorize(Policy = AppPermissions.AttachmentsUpload)]
    [RequestSizeLimit(52_428_800)]
    public Task<IActionResult> UploadInsured(Guid entityId, IFormFile file, [FromForm] DocumentType documentType, [FromForm] string? description)
        => Upload(DocumentEntityType.Insured, entityId, file, documentType, description, null);

    private async Task<IActionResult> Upload(DocumentEntityType entityType, Guid entityId, IFormFile file, DocumentType documentType, string? description, Guid? policyTransactionId)
    {
        var result = await _attachmentService.UploadAsync(entityType, entityId, file, documentType, description, CurrentUserId, policyTransactionId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    // ── Download (signed URL) ─────────────────────────────────────────────────

    [HttpGet("api/v1/attachments/{id:guid}/download-url")]
    [Authorize(Policy = AppPermissions.PoliciesView)]
    public async Task<IActionResult> GetDownloadUrl(Guid id)
    {
        var result = await _attachmentService.GetDownloadUrlAsync(id, CurrentUserId);
        return result.IsSuccess ? Ok(new { url = result.Value }) : ToAttachmentError(result.ErrorCode, result.ErrorMessage);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [HttpDelete("api/v1/attachments/{id:guid}")]
    [Authorize(Policy = AppPermissions.AttachmentsDelete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _attachmentService.DeleteAsync(id, CurrentUserId);
        return result.IsSuccess ? NoContent() : ToAttachmentError(result.ErrorCode, result.ErrorMessage);
    }

    private IActionResult ToAttachmentError(string? errorCode, string? errorMessage)
        => errorCode == "ATTACHMENT_ACCESS_DENIED"
            ? Forbid()
            : NotFound(new { ErrorCode = errorCode, ErrorMessage = errorMessage });
}
