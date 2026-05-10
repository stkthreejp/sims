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
    private readonly IFmcsaInspectionEnrichmentService _inspectionEnrichment;

    public FmcsaAnalyticsController(IFmcsaSafetyAnalyticsService analytics, IFmcsaInspectionEnrichmentService inspectionEnrichment)
    {
        _analytics = analytics;
        _inspectionEnrichment = inspectionEnrichment;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var result = await _analytics.GetStatusAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("refresh-imported")]
    public async Task<IActionResult> RefreshImported([FromQuery] string? snapshotMonth, CancellationToken ct)
    {
        var result = await _analytics.RefreshImportedCarrierAnalyticsAsync(snapshotMonth, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("refresh-official-sms")]
    public async Task<IActionResult> RefreshOfficialSms([FromQuery] string? snapshotMonth, [FromQuery] int? maxRowsPerDataset, CancellationToken ct)
    {
        var result = await _analytics.RefreshOfficialSmsPeerAnalyticsAsync(snapshotMonth, maxRowsPerDataset, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("enrich-inspection-details")]
    public async Task<IActionResult> EnrichInspectionDetails([FromQuery] int? maxInspections, CancellationToken ct)
    {
        var result = await _inspectionEnrichment.EnrichRecentInspectionsAsync(maxInspections ?? 50, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }
}
