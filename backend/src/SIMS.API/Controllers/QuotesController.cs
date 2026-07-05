using SIMS.Application.Common;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/quotes")]
[Authorize]
public class QuotesController : ControllerBase
{
    private readonly IQuoteService _quoteService;
    private readonly IRatingEngineService _ratingEngine;
    private readonly IShadowRatingService _shadowRating;
    private readonly IFmcsaSafetyService _fmcsaSafety;
    private readonly IAutoSafetyReportService _autoSafetyReport;
    private readonly IQuotePolicyFormSelectionService _quotePolicyForms;
    private readonly IAuthorityApprovalService _authorityApproval;

    public QuotesController(IQuoteService quoteService, IRatingEngineService ratingEngine,
        IShadowRatingService shadowRating, IFmcsaSafetyService fmcsaSafety, IAutoSafetyReportService autoSafetyReport,
        IQuotePolicyFormSelectionService quotePolicyForms, IAuthorityApprovalService authorityApproval)
    {
        _quoteService = quoteService;
        _ratingEngine = ratingEngine;
        _shadowRating = shadowRating;
        _fmcsaSafety = fmcsaSafety;
        _autoSafetyReport = autoSafetyReport;
        _quotePolicyForms = quotePolicyForms;
        _authorityApproval = authorityApproval;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private UserAccessScope CurrentAccess => User.ToBusinessDataAccessScope();

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] QueryParameters query)
        => Ok(await _quoteService.GetAllAsync(query, CurrentAccess));

    [HttpGet("by-submission/{submissionId:guid}")]
    public async Task<IActionResult> GetBySubmission(Guid submissionId)
        => Ok(await _quoteService.GetBySubmissionAsync(submissionId, CurrentAccess));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _quoteService.GetByIdAsync(id, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorMessage });
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.PoliciesCreate)]
    public async Task<IActionResult> Create([FromBody] QuoteCreateDto dto)
    {
        var result = await _quoteService.CreateAsync(dto, CurrentUserId, CurrentAccess);
        if (result.ToHttpErrorResult(this) is { } err) return err;
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPermissions.PoliciesEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] QuoteUpdateDto dto)
    {
        var result = await _quoteService.UpdateAsync(id, dto, CurrentAccess);
        return result.ToHttpResult(this);
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = AppPermissions.PoliciesEdit)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] QuoteStatusUpdateDto dto)
    {
        var result = await _quoteService.SetStatusAsync(id, dto.Status, CurrentAccess);
        return result.IsSuccess
            ? Ok(result.Value)
            : (result.ErrorCode == "NOT_FOUND"
                ? NotFound(new { result.ErrorCode, result.ErrorMessage })
                : BadRequest(new { result.ErrorCode, result.ErrorMessage }));
    }

    [HttpPost("{id:guid}/rate")]
    [Authorize(Policy = AppPermissions.PoliciesEdit)]
    public async Task<IActionResult> Rate(Guid id, [FromBody] RateQuoteRequest request)
    {
        var quote = await _quoteService.GetByIdAsync(id, CurrentAccess);
        if (!quote.IsSuccess) return NotFound();

        var result = await _ratingEngine.RateAsync(id, request, CurrentUserId);
        return result.ToHttpResult(this);
    }

    [HttpGet("{id:guid}/rating-snapshot")]
    public async Task<IActionResult> GetRatingSnapshot(Guid id)
    {
        var quote = await _quoteService.GetByIdAsync(id, CurrentAccess);
        if (!quote.IsSuccess) return NotFound();

        var result = await _ratingEngine.GetLatestSnapshotAsync(id);
        return result.ToHttpResult(this);
    }

    [HttpGet("{id:guid}/invoice-preview")]
    public async Task<IActionResult> GetInvoicePreview(Guid id)
    {
        var result = await _quoteService.GetInvoicePreviewAsync(id, CurrentAccess);
        return result.ToHttpResult(this);
    }

    [HttpGet("{id:guid}/auto-safety")]
    public async Task<IActionResult> GetAutoSafety(Guid id)
    {
        var quote = await _quoteService.GetByIdAsync(id, CurrentAccess);
        if (!quote.IsSuccess) return NotFound();

        var result = await _fmcsaSafety.GetQuoteAutoSafetyAsync(id);
        return result.ToHttpResult(this);
    }

    [HttpGet("{id:guid}/auto-safety/details")]
    public async Task<IActionResult> GetAutoSafetyDetails(Guid id, [FromQuery] string kind, [FromQuery] string? basic)
    {
        var quote = await _quoteService.GetByIdAsync(id, CurrentAccess);
        if (!quote.IsSuccess) return NotFound();

        var result = await _fmcsaSafety.GetQuoteAutoSafetyDetailsAsync(id, kind, basic);
        return result.ToHttpResult(this);
    }

    [HttpPost("{id:guid}/auto-safety/refresh")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> RefreshAutoSafety(Guid id)
    {
        var quote = await _quoteService.GetByIdAsync(id, CurrentAccess);
        if (!quote.IsSuccess) return NotFound();

        var result = await _fmcsaSafety.RefreshQuoteAutoSafetyAsync(id);
        return result.ToHttpResult(this);
    }

    [HttpPost("{id:guid}/auto-safety/report")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> GenerateAutoSafetyReport(Guid id)
    {
        var quote = await _quoteService.GetByIdAsync(id, CurrentAccess);
        if (!quote.IsSuccess) return NotFound();

        var result = await _autoSafetyReport.GenerateQuoteReportAsync(id, CurrentUserId);
        return result.ToHttpResult(this);
    }

    [HttpPost("{id:guid}/shadow-rate")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> ShadowRate(Guid id, [FromBody] RateQuoteRequest request)
    {
        // Look up the quote's LOB to check the per-LOB shadow flag
        var quote = await _quoteService.GetByIdAsync(id, CurrentAccess);
        if (!quote.IsSuccess) return NotFound();
        if (!await _shadowRating.IsShadowModeEnabledForLobAsync(quote.Value!.LineOfBusiness))
            return Conflict(new { ErrorCode = "SHADOW_MODE_DISABLED", ErrorMessage = "Shadow mode is not enabled for this line of business." });
        var result = await _shadowRating.ShadowRateAsync(id, request, CurrentUserId);
        return result.ToHttpResult(this);
    }

    [HttpPost("{id:guid}/bind")]
    [Authorize(Policy = AppPermissions.PoliciesBind)]
    public async Task<IActionResult> Bind(Guid id, [FromBody] QuoteBindDto dto)
    {
        var result = await _quoteService.BindAsync(id, dto, CurrentAccess);
        return result.ToHttpResult(this);
    }

    [HttpPost("{id:guid}/commission-override")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> CommissionOverride(Guid id, [FromBody] CommissionOverrideRequest req, CancellationToken ct)
    {
        var authority = await _authorityApproval.EvaluateAsync(
            new AuthorityApprovalEvaluationRequest(
                AuthorityApprovalTargetType.Quote,
                id,
                "quote.commission-override",
                "Commission override",
                AppPermissions.UnderwritingAuthorityApprove,
                "CommissionOverride",
                "Commission override requires underwriting authority approval.",
                null,
                null),
            User.PermissionNames(),
            CurrentUserId,
            ct);
        if (!authority.Allowed)
            return StatusCode(403, new { ErrorCode = "AUTHORITY_APPROVAL_REQUIRED", ErrorMessage = authority.Message, authority.ApprovalRequestId });

        var result = await _quoteService.ApplyCommissionOverrideAsync(id, req, CurrentAccess);
        return result.ToHttpResult(this);
    }

    [HttpGet("{id:guid}/policy-forms")]
    public async Task<IActionResult> GetPolicyForms(Guid id)
    {
        var quote = await _quoteService.GetByIdAsync(id, CurrentAccess);
        if (!quote.IsSuccess) return NotFound();

        var result = await _quotePolicyForms.GetOrSeedAsync(id);
        return result.ToHttpResult(this);
    }

    [HttpPut("{id:guid}/policy-forms")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> SavePolicyForms(Guid id, [FromBody] IReadOnlyList<QuotePolicyFormSelectionUpsertDto> forms)
    {
        var quote = await _quoteService.GetByIdAsync(id, CurrentAccess);
        if (!quote.IsSuccess) return NotFound();

        var result = await _quotePolicyForms.SaveAsync(id, forms);
        return result.ToHttpResult(this);
    }

    [HttpPost("{id:guid}/policy-forms/reset")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> ResetPolicyForms(Guid id)
    {
        var quote = await _quoteService.GetByIdAsync(id, CurrentAccess);
        if (!quote.IsSuccess) return NotFound();

        var result = await _quotePolicyForms.ResetFromPackageAsync(id);
        return result.ToHttpResult(this);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppPermissions.PoliciesDelete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _quoteService.DeleteAsync(id, CurrentAccess);
        if (result.ToHttpErrorResult(this) is { } err) return err;
        return NoContent();
    }
}
