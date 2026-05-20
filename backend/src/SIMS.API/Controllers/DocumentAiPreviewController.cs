using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.Interfaces.Services;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/submissions/{submissionId:guid}/attachments/{attachmentId:guid}/ai-preview")]
[Authorize(Policy = AppPermissions.UnderwritingManage)]
public class DocumentAiPreviewController : ControllerBase
{
    private readonly IDocumentAiPreviewService _previewService;

    public DocumentAiPreviewController(IDocumentAiPreviewService previewService)
    {
        _previewService = previewService;
    }

    [HttpPost]
    public async Task<IActionResult> PreviewSubmissionAttachment(Guid submissionId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var result = await _previewService.PreviewSubmissionAttachmentAsync(submissionId, attachmentId, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.ErrorCode switch
        {
            "SUBMISSION_ATTACHMENT_NOT_FOUND" => NotFound(new { result.ErrorCode, result.ErrorMessage }),
            _ => BadRequest(new { result.ErrorCode, result.ErrorMessage })
        };
    }
}
