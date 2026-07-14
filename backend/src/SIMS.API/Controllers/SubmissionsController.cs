using SIMS.Application.Common;
using SIMS.Application.DTOs.Submissions;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SIMS.API.Controllers;

public class SetLinesOfBusinessRequest
{
    public List<string> LinesOfBusiness { get; set; } = [];
}

[ApiController]
[Route("api/v1/submissions")]
[Authorize]
public class SubmissionsController : ControllerBase
{
    private readonly ISubmissionService _submissionService;
    private readonly IUnderwritingClearanceService _clearance;
    private readonly IUnderwritingReferralService _referrals;
    private readonly IIntakeProcessingService _intake;

    public SubmissionsController(
        ISubmissionService submissionService,
        IUnderwritingClearanceService clearance,
        IUnderwritingReferralService referrals,
        IIntakeProcessingService intake)
    {
        _submissionService = submissionService;
        _clearance = clearance;
        _referrals = referrals;
        _intake = intake;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private UserAccessScope CurrentAccess => User.ToBusinessDataAccessScope();

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] QueryParameters query)
        => Ok(await _submissionService.GetAllAsync(query, CurrentAccess));

    [HttpGet("by-insured/{insuredId:guid}")]
    public async Task<IActionResult> GetByInsured(Guid insuredId)
        => Ok(await _submissionService.GetByInsuredAsync(insuredId, CurrentAccess));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _submissionService.GetByIdAsync(id, CurrentAccess);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { result.ErrorMessage });
    }

    [HttpGet("{id:guid}/intake")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> GetIntake(Guid id, CancellationToken ct)
        => Ok(await _intake.GetLatestForSubmissionAsync(id, ct));

    [HttpPost("{id:guid}/reintake")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> Reintake(Guid id, CancellationToken ct)
    {
        var result = await _intake.RequeueAsync(id, ct);
        return result.IsSuccess
            ? Ok(new { jobId = result.Value })
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpGet("{id:guid}/clearance")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> GetClearance(Guid id, CancellationToken ct)
    {
        var result = await _clearance.GetLatestSubmissionAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/clearance/evaluate")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> EvaluateClearance(Guid id, CancellationToken ct)
        => Ok(await _clearance.EvaluateSubmissionAsync(id, CurrentUserId, ct));

    [HttpPost("{id:guid}/clearance/override")]
    [Authorize(Policy = AppPermissions.UnderwritingClearanceOverride)]
    public async Task<IActionResult> OverrideClearance(Guid id, [FromBody] UnderwritingClearanceOverrideDto dto, CancellationToken ct)
        => Ok(await _clearance.OverrideSubmissionAsync(id, CurrentUserId, dto.Reason, ct));

    [HttpGet("{id:guid}/underwriting-referrals")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> GetUnderwritingReferrals(Guid id, CancellationToken ct)
        => Ok(await _referrals.GetSubmissionSummaryAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> Create([FromBody] SubmissionCreateDto dto)
    {
        var result = await _submissionService.CreateAsync(dto, CurrentUserId);
        if (result.ToHttpErrorResult(this) is { } err) return err;
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SubmissionUpdateDto dto)
    {
        var result = await _submissionService.UpdateAsync(id, dto, CurrentAccess);
        return result.ToHttpResult(this);
    }

    [HttpPatch("{id:guid}/lines-of-business")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> SetLinesOfBusiness(Guid id, [FromBody] SetLinesOfBusinessRequest request)
    {
        var result = await _submissionService.SetLinesOfBusinessAsync(id, request.LinesOfBusiness, CurrentAccess);
        return result.ToHttpResult(this);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppPermissions.UnderwritingManage)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _submissionService.DeleteAsync(id, CurrentAccess);
        if (result.ToHttpErrorResult(this) is { } err) return err;
        return NoContent();
    }
}
