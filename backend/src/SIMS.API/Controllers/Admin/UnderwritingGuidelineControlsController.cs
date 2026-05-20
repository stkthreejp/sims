using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using System.Security.Claims;

namespace SIMS.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/underwriting-guidelines")]
[Authorize(Policy = AppPermissions.AdminUnderwritingControlsManage)]
public class UnderwritingGuidelineControlsController : ControllerBase
{
    private readonly IUnderwritingGuidelineControlService _service;

    public UnderwritingGuidelineControlsController(IUnderwritingGuidelineControlService service) => _service = service;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments(CancellationToken ct)
        => Ok(await _service.GetDocumentsAsync(ct));

    [HttpPost("documents")]
    public async Task<IActionResult> CreateDocument([FromBody] CreateUnderwritingGuidelineDocumentRequest request, CancellationToken ct)
    {
        var result = await _service.CreateDocumentAsync(request, UserId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("documents/{documentId:guid}/controls")]
    public async Task<IActionResult> GetControls(Guid documentId, CancellationToken ct)
        => Ok(await _service.GetControlsAsync(documentId, ct));

    [HttpPost("documents/{documentId:guid}/proposed-controls")]
    public async Task<IActionResult> AddProposedControls(Guid documentId, [FromBody] AddProposedUnderwritingControlsRequest request, CancellationToken ct)
    {
        var result = await _service.AddProposedControlsAsync(documentId, request, UserId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("controls/{controlId:guid}")]
    public async Task<IActionResult> UpdateControl(Guid controlId, [FromBody] UpdateUnderwritingGuidelineControlRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateControlAsync(controlId, request, UserId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("controls/{controlId:guid}/approve")]
    public async Task<IActionResult> ApproveControl(Guid controlId, [FromBody] UnderwritingGuidelineDecisionRequest request, CancellationToken ct)
    {
        var result = await _service.ApproveControlAsync(controlId, UserId, request.Notes, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("controls/{controlId:guid}/reject")]
    public async Task<IActionResult> RejectControl(Guid controlId, [FromBody] UnderwritingGuidelineDecisionRequest request, CancellationToken ct)
    {
        var result = await _service.RejectControlAsync(controlId, UserId, request.Notes, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("controls/{controlId:guid}/publish")]
    [Authorize(Policy = AppPermissions.AdminUnderwritingControlsPublish)]
    public async Task<IActionResult> PublishControl(Guid controlId, [FromBody] UnderwritingGuidelineDecisionRequest request, CancellationToken ct)
    {
        var result = await _service.PublishControlAsync(controlId, UserId, request.Notes, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("controls/{controlId:guid}/retire")]
    [Authorize(Policy = AppPermissions.AdminUnderwritingControlsPublish)]
    public async Task<IActionResult> RetireControl(Guid controlId, [FromBody] UnderwritingGuidelineDecisionRequest request, CancellationToken ct)
    {
        var result = await _service.RetireControlAsync(controlId, UserId, request.Notes, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("audit-log")]
    public async Task<IActionResult> GetAuditLog([FromQuery] Guid? documentId, [FromQuery] Guid? controlId, CancellationToken ct)
        => Ok(await _service.GetAuditLogAsync(documentId, controlId, ct));
}

