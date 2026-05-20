using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Domain.Enums;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/underwriting/control-enforcement")]
[Authorize(Policy = AppPermissions.UnderwritingManage)]
public class UnderwritingControlEnforcementController : ControllerBase
{
    private readonly IUnderwritingControlEnforcementService _enforcement;

    public UnderwritingControlEnforcementController(IUnderwritingControlEnforcementService enforcement)
    {
        _enforcement = enforcement;
    }

    [HttpGet("{targetType}/{targetId:guid}")]
    public async Task<IActionResult> GetForTarget(UnderwritingControlTargetType targetType, Guid targetId, CancellationToken ct)
        => Ok(await _enforcement.GetForTargetAsync(targetType, targetId, ct));

    [HttpPost("quotes/{quoteId:guid}/evaluate/{stage}")]
    public async Task<IActionResult> EvaluateQuote(Guid quoteId, UnderwritingControlStage stage, CancellationToken ct)
        => Ok(await _enforcement.EvaluateQuoteAsync(quoteId, stage, CurrentUserId(), ct));

    [HttpPost("policies/{policyId:guid}/evaluate/{stage}")]
    public async Task<IActionResult> EvaluatePolicy(Guid policyId, UnderwritingControlStage stage, CancellationToken ct)
        => Ok(await _enforcement.EvaluatePolicyAsync(policyId, stage, CurrentUserId(), ct));

    [HttpPost("results/{resultId:guid}/override")]
    [Authorize(Policy = AppPermissions.UnderwritingClearanceOverride)]
    public async Task<IActionResult> Override(Guid resultId, [FromBody] UnderwritingControlOverrideRequest request, CancellationToken ct)
    {
        var result = await _enforcement.OverrideAsync(resultId, CurrentUserId(), request.Reason, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    private Guid CurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
