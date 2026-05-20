using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Interfaces.Services;

namespace SIMS.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/ai-guideline-control-proposals")]
[Authorize(Policy = AppPermissions.UnderwritingManage)]
public class AiGuidelineControlProposalsController : ControllerBase
{
    private readonly IAiGuidelineControlProposalService _service;

    public AiGuidelineControlProposalsController(IAiGuidelineControlProposalService service)
    {
        _service = service;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("from-text")]
    public async Task<IActionResult> ProposeFromText([FromBody] AiGuidelineControlProposalRequest request, CancellationToken ct)
    {
        var result = await _service.ProposeFromTextAsync(request, UserId, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
