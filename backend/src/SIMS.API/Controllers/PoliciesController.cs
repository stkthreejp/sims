using SIMS.Application.Common;
using SIMS.Application.DTOs.Notes;
using SIMS.Application.DTOs.Attachments;
using SIMS.Application.DTOs.Policies;
using SIMS.Application.Interfaces.Services;
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

    // --- Policy CRUD ---

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] QueryParameters query)
        => Ok(await _policies.GetAllAsync(query));

    [HttpGet("by-insured/{insuredId:guid}")]
    public async Task<IActionResult> GetByInsured(Guid insuredId)
        => Ok(await _policies.GetByInsuredAsync(insuredId));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _policies.GetByIdAsync(id);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorMessage });
    }

    // --- Endorsements ---

    [HttpPost("{id:guid}/endorsements")]
    public async Task<IActionResult> AddEndorsement(Guid id, [FromBody] CreateEndorsementDto dto)
    {
        var result = await _policies.AddEndorsementAsync(id, dto, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("{id:guid}/endorsements/{txnId:guid}/issue")]
    public async Task<IActionResult> IssueEndorsement(Guid id, Guid txnId, [FromBody] IssueEndorsementDto dto)
    {
        var result = await _policies.IssueEndorsementAsync(id, txnId, dto, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    // --- Renewal ---

    [HttpPost("{id:guid}/renew")]
    public async Task<IActionResult> CreateRenewalQuote(Guid id)
    {
        var result = await _policies.CreateRenewalQuoteAsync(id, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    // --- Non-renewal ---

    [HttpPost("{id:guid}/non-renew")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> NonRenew(Guid id, [FromBody] NonRenewPolicyDto dto)
    {
        var result = await _policies.NonRenewAsync(id, dto, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    // --- Notes (delegate to NoteService using the bound quote ID) ---

    [HttpGet("{id:guid}/notes")]
    public async Task<IActionResult> GetNotes(Guid id)
    {
        var policy = await _policies.GetByIdAsync(id);
        if (!policy.IsSuccess) return NotFound();
        return Ok(await _notes.GetByQuoteAsync(policy.Value!.BoundQuoteId));
    }

    [HttpPost("{id:guid}/notes")]
    public async Task<IActionResult> CreateNote(Guid id, [FromBody] NoteCreateDto dto)
    {
        var policy = await _policies.GetByIdAsync(id);
        if (!policy.IsSuccess) return NotFound();
        var result = await _notes.CreateAsync(policy.Value!.BoundQuoteId, dto, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPut("{id:guid}/notes/{noteId:guid}")]
    public async Task<IActionResult> UpdateNote(Guid id, Guid noteId, [FromBody] NoteUpdateDto dto)
    {
        var policy = await _policies.GetByIdAsync(id);
        if (!policy.IsSuccess) return NotFound();
        var result = await _notes.UpdateAsync(policy.Value!.BoundQuoteId, noteId, dto, CurrentUserId);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpDelete("{id:guid}/notes/{noteId:guid}")]
    public async Task<IActionResult> DeleteNote(Guid id, Guid noteId)
    {
        var policy = await _policies.GetByIdAsync(id);
        if (!policy.IsSuccess) return NotFound();
        var result = await _notes.DeleteAsync(policy.Value!.BoundQuoteId, noteId, CurrentUserId);
        return result.IsSuccess ? NoContent() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPatch("{id:guid}/notes/{noteId:guid}/pin")]
    public async Task<IActionResult> TogglePinNote(Guid id, Guid noteId)
    {
        var policy = await _policies.GetByIdAsync(id);
        if (!policy.IsSuccess) return NotFound();
        var result = await _notes.TogglePinAsync(policy.Value!.BoundQuoteId, noteId, CurrentUserId);
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
