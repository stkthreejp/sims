using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Compliance;
using SIMS.Application.Interfaces.Services;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/compliance-documents")]
[Authorize]
public class ComplianceDocumentsController : ControllerBase
{
    private readonly IComplianceDocumentService _service;

    public ComplianceDocumentsController(IComplianceDocumentService service) => _service = service;

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var result = await _service.GetSummaryAsync(ct);
        return Ok(result);
    }

    [HttpGet("attestations")]
    public async Task<IActionResult> GetAttestationCampaigns([FromQuery] Guid? documentId = null, CancellationToken ct = default)
    {
        var result = await _service.GetAttestationCampaignsAsync(documentId, ct);
        return Ok(result);
    }

    [HttpGet("attestations/{campaignId:guid}")]
    public async Task<IActionResult> GetAttestationCampaign(Guid campaignId, CancellationToken ct)
    {
        var result = await _service.GetAttestationCampaignAsync(campaignId, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorMessage });
    }

    [HttpPost("attestations/{campaignId:guid}/submit")]
    public async Task<IActionResult> SubmitAttestation(Guid campaignId, [FromBody] ComplianceAttestationSubmitDto dto, CancellationToken ct)
    {
        var result = await _service.SubmitAttestationAsync(campaignId, dto, CurrentUserId(), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetDocumentsAsync(status, category, search, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDocument(Guid id, CancellationToken ct)
    {
        var result = await _service.GetDocumentAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorMessage });
    }

    [HttpGet("{id:guid}/audit-log")]
    public async Task<IActionResult> GetAuditLog(Guid id, CancellationToken ct)
    {
        var result = await _service.GetAuditLogAsync(id, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDocument([FromBody] ComplianceDocumentCreateDto dto, CancellationToken ct)
    {
        var result = await _service.CreateDocumentAsync(dto, CurrentUserId(), ct);
        if (!result.IsSuccess) return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        return CreatedAtAction(nameof(GetDocument), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateDocument(Guid id, [FromBody] ComplianceDocumentUpdateDto dto, CancellationToken ct)
    {
        var result = await _service.UpdateDocumentAsync(id, dto, CurrentUserId(), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{id:guid}/draft")]
    public async Task<IActionResult> SaveDraft(Guid id, [FromBody] ComplianceDraftSaveDto dto, CancellationToken ct)
    {
        var result = await _service.SaveDraftAsync(id, dto, CurrentUserId(), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/submit-review")]
    public async Task<IActionResult> SubmitForReview(Guid id, [FromBody] ComplianceWorkflowActionDto dto, CancellationToken ct)
    {
        var result = await _service.SubmitForReviewAsync(id, dto, CurrentUserId(), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/require-changes")]
    public async Task<IActionResult> RequireChanges(Guid id, [FromBody] ComplianceWorkflowActionDto dto, CancellationToken ct)
    {
        var result = await _service.RequireChangesAsync(id, dto, CurrentUserId(), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> PublishDraft(Guid id, [FromBody] CompliancePublishDto dto, CancellationToken ct)
    {
        var result = await _service.PublishDraftAsync(id, dto, CurrentUserId(), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/reviews")]
    public async Task<IActionResult> AddReview(Guid id, [FromBody] ComplianceReviewCreateDto dto, CancellationToken ct)
    {
        var result = await _service.AddReviewAsync(id, dto, CurrentUserId(), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/evidence")]
    public async Task<IActionResult> AddEvidence(Guid id, [FromBody] ComplianceEvidenceCreateDto dto, CancellationToken ct)
    {
        var result = await _service.AddEvidenceAsync(id, dto, CurrentUserId(), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/attestations")]
    public async Task<IActionResult> CreateAttestationCampaign(Guid id, [FromBody] ComplianceAttestationCampaignCreateDto dto, CancellationToken ct)
    {
        var result = await _service.CreateAttestationCampaignAsync(id, dto, CurrentUserId(), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("{id:guid}/compare")]
    public async Task<IActionResult> CompareVersions(
        Guid id,
        [FromQuery] Guid? fromVersionId = null,
        [FromQuery] Guid? toVersionId = null,
        CancellationToken ct = default)
    {
        var result = await _service.CompareVersionsAsync(id, fromVersionId, toVersionId, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
