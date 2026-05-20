using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Domain.Enums;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/underwriting/referrals")]
[Authorize(Policy = AppPermissions.UnderwritingManage)]
public class UnderwritingReferralsController : ControllerBase
{
    private readonly IUnderwritingReferralService _referrals;

    public UnderwritingReferralsController(IUnderwritingReferralService referrals)
    {
        _referrals = referrals;
    }

    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, [FromBody] UnderwritingReferralDecisionDto dto, CancellationToken ct)
        => Decide(id, UnderwritingReferralStatus.Approved, dto, ct);

    [HttpPost("{id:guid}/decline")]
    public Task<IActionResult> Decline(Guid id, [FromBody] UnderwritingReferralDecisionDto dto, CancellationToken ct)
        => Decide(id, UnderwritingReferralStatus.Declined, dto, ct);

    [HttpPost("{id:guid}/waive")]
    public Task<IActionResult> Waive(Guid id, [FromBody] UnderwritingReferralDecisionDto dto, CancellationToken ct)
        => Decide(id, UnderwritingReferralStatus.Waived, dto, ct);

    private async Task<IActionResult> Decide(Guid id, UnderwritingReferralStatus status, UnderwritingReferralDecisionDto dto, CancellationToken ct)
    {
        try
        {
            var referral = await _referrals.DecideAsync(id, status, CurrentUserId(), dto.Notes, ct);
            return Ok(new
            {
                referral.Id,
                referral.SubmissionId,
                referral.QuoteId,
                referral.ReferralType,
                referral.Status,
                referral.Required,
                referral.Reason,
                referral.RequestedById,
                referral.RequestedAt,
                referral.DecisionById,
                referral.DecisionAt,
                referral.DecisionNotes,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { ErrorMessage = ex.Message });
        }
    }

    private Guid CurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
