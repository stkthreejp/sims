using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;

namespace SIMS.API.Controllers;

[ApiController]
[Route("api/v1/fmcsa/analytics")]
[Authorize(Policy = AppPermissions.UnderwritingManage)]
public class FmcsaAnalyticsController : ControllerBase
{
    private readonly IFmcsaSafetyAnalyticsService _analytics;

    public FmcsaAnalyticsController(IFmcsaSafetyAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    [HttpPost("refresh-imported")]
    public async Task<IActionResult> RefreshImported([FromQuery] string? snapshotMonth, CancellationToken ct)
    {
        var result = await _analytics.RefreshImportedCarrierAnalyticsAsync(snapshotMonth, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
