using SIMS.Application.Common;
using SIMS.Application.DTOs.Notes;
using SIMS.Application.DTOs.Attachments;
using SIMS.Application.DTOs.Policies;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Policies;
using SIMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/policies")]
[Authorize]
public class PoliciesController : ControllerBase
{
    private readonly IPolicyService _policies;
    private readonly INoteService _notes;
    private readonly IAttachmentService _attachments;

    public PoliciesController(
        IPolicyService policies,
        INoteService notes,
        IAttachmentService attachments)
    {
        _policies = policies;
        _notes = notes;
        _attachments = attachments;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private UserAccessScope CurrentAccess => User.ToBusinessDataAccessScope();

    // --- Policy CRUD ---

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] QueryParameters query)
        => Ok(await _policies.GetAllAsync(query, CurrentAccess));

    [HttpGet("cancellation-reasons")]
    [Authorize(Policy = AppPermissions.PoliciesCancel)]
    public IActionResult GetCancellationReasons()
        => Ok(CancellationReasonLibrary.All.Select(r => new
        {
            r.Code,
            r.Category,
            r.Label,
            r.DefaultNoticeRequirementDays,
            r.NoticeRequirementLabel,
            r.LanguageTemplate,
            r.RequiredInputTokens,
            r.RequiresSpecialHandling,
        }));

    [HttpGet("by-insured/{insuredId:guid}")]
    public async Task<IActionResult> GetByInsured(Guid insuredId)
        => Ok(await _policies.GetByInsuredAsync(insuredId, CurrentAccess));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _policies.GetByIdAsync(id, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorMessage });
    }

    [HttpGet("{id:guid}/transactions/{txnId:guid}/artifacts")]
    public async Task<IActionResult> GetTransactionArtifacts(Guid id, Guid txnId)
    {
        var result = await _policies.GetTransactionArtifactsAsync(id, txnId, CurrentAccess);
        return result.IsSuccess
            ? Ok(result.Value)
            : result.ErrorCode is "NOT_FOUND" or "TRANSACTION_NOT_FOUND"
                ? NotFound(new { result.ErrorCode, result.ErrorMessage })
                : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("{id:guid}/issuance-packet")]
    public async Task<IActionResult> GetIssuancePacket(Guid id)
    {
        var result = await _policies.GetIssuancePacketAsync(id, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/issuance-packet/preview")]
    [Authorize(Policy = AppPermissions.PoliciesIssue)]
    public async Task<IActionResult> GenerateIssuancePacketPreview(Guid id)
    {
        var result = await _policies.GenerateIssuancePacketPreviewAsync(id, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/issue")]
    [Authorize(Policy = AppPermissions.PoliciesIssue)]
    public async Task<IActionResult> Issue(Guid id, [FromBody] IssuePolicyDto dto)
    {
        var result = await _policies.IssueAsync(id, dto, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/void-test-bind")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> VoidTestBind(Guid id, [FromBody] VoidTestBindDto dto)
    {
        var result = await _policies.VoidTestBindAsync(id, dto, CurrentAccess, User.IsInRole("Admin"));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    // --- Endorsements ---

    [HttpPost("{id:guid}/endorsements")]
    [Authorize(Policy = AppPermissions.PoliciesEndorse)]
    public async Task<IActionResult> AddEndorsement(Guid id, [FromBody] CreateEndorsementDto dto)
    {
        var result = await _policies.AddEndorsementAsync(id, dto, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/endorsements/{txnId:guid}/issue")]
    [Authorize(Policy = AppPermissions.PoliciesEndorse)]
    public async Task<IActionResult> IssueEndorsement(Guid id, Guid txnId, [FromBody] IssueEndorsementDto dto)
    {
        var result = await _policies.IssueEndorsementAsync(id, txnId, dto, CurrentAccess, User.PermissionNames());
        return result.IsSuccess
            ? Ok(result.Value)
            : result.ErrorCode == "AUTHORITY_APPROVAL_REQUIRED"
                ? StatusCode(403, new { result.ErrorCode, result.ErrorMessage })
                : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    // --- Renewal ---

    [HttpPost("{id:guid}/renew")]
    [Authorize(Policy = AppPermissions.PoliciesRenew)]
    public async Task<IActionResult> CreateRenewalQuote(Guid id)
    {
        var result = await _policies.CreateRenewalQuoteAsync(id, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    // --- Cancellation guidance ---

    [HttpGet("{id:guid}/cancellation-guidance")]
    [Authorize(Policy = AppPermissions.PoliciesCancel)]
    public async Task<IActionResult> GetCancellationGuidance(Guid id)
    {
        var result = await _policies.GetCancellationGuidanceAsync(id, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("{id:guid}/non-renewal-guidance")]
    [Authorize(Policy = AppPermissions.PoliciesCancel)]
    public async Task<IActionResult> GetNonRenewalGuidance(Guid id)
    {
        var result = await _policies.GetNonRenewalGuidanceAsync(id, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    // --- Cancellation ---

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = AppPermissions.PoliciesCancel)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelPolicyDto dto)
    {
        var result = await _policies.CancelAsync(id, dto, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/cancellation-notice")]
    [Authorize(Policy = AppPermissions.PoliciesCancel)]
    public async Task<IActionResult> IssueCancellationNotice(Guid id, [FromBody] IssueCancellationNoticeDto dto)
    {
        var result = await _policies.IssueCancellationNoticeAsync(id, dto, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/cancellations/{txnId:guid}/complete")]
    [Authorize(Policy = AppPermissions.PoliciesCancel)]
    public async Task<IActionResult> CompleteCancellation(Guid id, Guid txnId, [FromBody] CompleteCancellationDto dto)
    {
        var result = await _policies.CompleteCancellationAsync(id, txnId, dto, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/reinstate")]
    [Authorize(Policy = AppPermissions.PoliciesCancel)]
    public async Task<IActionResult> Reinstate(Guid id, [FromBody] ReinstatePolicyDto dto)
    {
        var result = await _policies.ReinstateAsync(id, dto, CurrentAccess, User.PermissionNames());
        return result.IsSuccess
            ? Ok(result.Value)
            : result.ErrorCode == "AUTHORITY_APPROVAL_REQUIRED"
                ? StatusCode(StatusCodes.Status403Forbidden, new { result.ErrorCode, result.ErrorMessage })
                : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/rewrite")]
    [Authorize(Policy = AppPermissions.PoliciesEndorse)]
    public async Task<IActionResult> StartRewrite(Guid id, [FromBody] StartRewritePolicyDto dto)
    {
        var result = await _policies.StartRewriteAsync(id, dto, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/rewrites/{txnId:guid}/complete")]
    [Authorize(Policy = AppPermissions.PoliciesEndorse)]
    public async Task<IActionResult> CompleteRewrite(Guid id, Guid txnId, [FromBody] CompleteRewritePolicyDto dto)
    {
        var result = await _policies.CompleteRewriteAsync(id, txnId, dto, CurrentAccess, User.PermissionNames());
        return result.IsSuccess
            ? Ok(result.Value)
            : result.ErrorCode == "AUTHORITY_APPROVAL_REQUIRED"
                ? StatusCode(StatusCodes.Status403Forbidden, new { result.ErrorCode, result.ErrorMessage })
                : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    // --- Non-renewal ---

    [HttpPost("{id:guid}/non-renew")]
    [Authorize(Policy = AppPermissions.PoliciesCancel)]
    public async Task<IActionResult> NonRenew(Guid id, [FromBody] NonRenewPolicyDto dto)
    {
        var result = await _policies.NonRenewAsync(id, dto, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/non-renewals/{txnId:guid}/complete")]
    [Authorize(Policy = AppPermissions.PoliciesCancel)]
    public async Task<IActionResult> CompleteNonRenewal(Guid id, Guid txnId, [FromBody] CompleteNonRenewalDto dto)
    {
        var result = await _policies.CompleteNonRenewalAsync(id, txnId, dto, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    // --- Notes (delegate to NoteService using the bound quote ID) ---

    [HttpGet("{id:guid}/notes")]
    public async Task<IActionResult> GetNotes(Guid id)
    {
        var policy = await _policies.GetByIdAsync(id, CurrentAccess);
        if (!policy.IsSuccess) return NotFound();
        return Ok(await _notes.GetByQuoteAsync(policy.Value!.BoundQuoteId, CurrentAccess));
    }

    [HttpPost("{id:guid}/notes")]
    public async Task<IActionResult> CreateNote(Guid id, [FromBody] NoteCreateDto dto)
    {
        var policy = await _policies.GetByIdAsync(id, CurrentAccess);
        if (!policy.IsSuccess) return NotFound();
        var result = await _notes.CreateAsync(policy.Value!.BoundQuoteId, dto, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{id:guid}/notes/{noteId:guid}")]
    public async Task<IActionResult> UpdateNote(Guid id, Guid noteId, [FromBody] NoteUpdateDto dto)
    {
        var policy = await _policies.GetByIdAsync(id, CurrentAccess);
        if (!policy.IsSuccess) return NotFound();
        var result = await _notes.UpdateAsync(policy.Value!.BoundQuoteId, noteId, dto, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}/notes/{noteId:guid}")]
    public async Task<IActionResult> DeleteNote(Guid id, Guid noteId)
    {
        var policy = await _policies.GetByIdAsync(id, CurrentAccess);
        if (!policy.IsSuccess) return NotFound();
        var result = await _notes.DeleteAsync(policy.Value!.BoundQuoteId, noteId, CurrentAccess);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPatch("{id:guid}/notes/{noteId:guid}/pin")]
    public async Task<IActionResult> TogglePinNote(Guid id, Guid noteId)
    {
        var policy = await _policies.GetByIdAsync(id, CurrentAccess);
        if (!policy.IsSuccess) return NotFound();
        var result = await _notes.TogglePinAsync(policy.Value!.BoundQuoteId, noteId, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    // --- Attachments ---

    [HttpGet("{id:guid}/attachments")]
    public async Task<IActionResult> GetAttachments(Guid id)
        => Ok(await _attachments.GetByEntityAsync(DocumentEntityType.Policy, id, CurrentUserId));

    [HttpPost("{id:guid}/attachments")]
    public async Task<IActionResult> UploadAttachment(
        Guid id, IFormFile file, [FromForm] DocumentType documentType, [FromForm] string? description)
    {
        var result = await _attachments.UploadAsync(DocumentEntityType.Policy, id, file, documentType, description, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId)
    {
        var result = await _attachments.GetDownloadUrlAsync(attachmentId, CurrentUserId);
        if (!result.IsSuccess) return result.ErrorCode == "ATTACHMENT_ACCESS_DENIED" ? Forbid() : NotFound();
        return Redirect(result.Value!);
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId)
    {
        var result = await _attachments.DeleteAsync(attachmentId, CurrentUserId);
        return result.IsSuccess
            ? NoContent()
            : result.ErrorCode == "ATTACHMENT_ACCESS_DENIED"
                ? Forbid()
                : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
